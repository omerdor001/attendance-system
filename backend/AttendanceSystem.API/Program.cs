using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Core.Services;
using AttendanceSystem.Infrastructure.Data;
using AttendanceSystem.Infrastructure.ExternalServices;
using AttendanceSystem.Infrastructure.Repositories;
using JwtTokenService = AttendanceSystem.Infrastructure.ExternalServices.JwtTokenService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new AttendanceSystem.API.UtcDateTimeConverter());
        opts.JsonSerializerOptions.Converters.Add(new AttendanceSystem.API.UtcNullableDateTimeConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Attendance System API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure(3)));

// Repositories
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Infrastructure services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Core Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Ollama analysis service
builder.Services.Configure<AttendanceSystem.Infrastructure.ExternalServices.OllamaOptions>(
    builder.Configuration.GetSection("Ollama"));
builder.Services.AddHttpClient<IAttendanceAnalysisService,
    AttendanceSystem.Infrastructure.ExternalServices.OllamaAnalysisService>(client => {
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/";
    var timeoutSeconds = int.TryParse(builder.Configuration["Ollama:TimeoutSeconds"], out var t) ? t : 30;
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

// API Time service
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
var circuitBreaker = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<IWorldTimeApiService, WorldTimeApiService>(client => {
    client.BaseAddress = new Uri(
        builder.Configuration["WorldTimeApi:BaseUrl"] ?? "https://timeapi.io/");
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreaker);

// JWT service
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Rate limiting: 10 req/min per user (partitioned by user ID), 60 req/min for admin endpoints
builder.Services.AddRateLimiter(options => {
    static string PartitionKey(HttpContext ctx) =>
        ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    options.AddPolicy("per-user", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(ctx), _ =>
            new SlidingWindowRateLimiterOptions {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("admin", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(ctx), _ =>
            new SlidingWindowRateLimiterOptions {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS — allow Vite dev server
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers().RequireRateLimiting("per-user");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .AllowAnonymous();

// Run migrations and seed on startup
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await AttendanceSystem.Infrastructure.Data.DatabaseSeeder.SeedAsync(db);
}

app.Run();

public partial class Program { }
