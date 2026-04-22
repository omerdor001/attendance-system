using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Core.Services;
using AttendanceSystem.Infrastructure.Data;
using AttendanceSystem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendanceSystem.Tests.Integration;

public class DBUsersIntegrationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UserRepository _userRepository;
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly AuthService _sut;

    private static readonly TimeOnly ShiftStart = new(8, 0);
    private static readonly TimeOnly ShiftEnd = new(17, 0);

    public DBUsersIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _userRepository = new UserRepository(_db);

        _jwt.Setup(j => j.GenerateToken(It.IsAny<User>()))
            .Returns(("fake-jwt-token", DateTime.UtcNow.AddHours(1)));

        _sut = new AuthService(_userRepository, _jwt.Object, NullLogger<AuthService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // --- Registration ---

    [Fact]
    public async Task Register_PersistsUserToDatabase()
    {
        await _sut.RegisterAsync("alice", "password123", "Employee", ShiftStart, ShiftEnd);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == "alice");
        Assert.NotNull(user);
        Assert.Equal("alice", user.Username);
        Assert.Equal("Employee", user.Role);
    }

    [Fact]
    public async Task Register_PasswordIsHashedInDatabase()
    {
        await _sut.RegisterAsync("alice", "password123", "Employee", ShiftStart, ShiftEnd);

        var user = await _db.Users.FirstAsync(u => u.Username == "alice");
        Assert.NotEqual("password123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("password123", user.PasswordHash));
    }

    [Fact]
    public async Task Register_StoresShiftTimesCorrectly()
    {
        await _sut.RegisterAsync("alice", "password123", "Employee", new TimeOnly(9, 30), new TimeOnly(18, 0));

        var user = await _db.Users.FirstAsync(u => u.Username == "alice");
        Assert.Equal(new TimeOnly(9, 30), user.ExpectedShiftStartTime);
        Assert.Equal(new TimeOnly(18, 0), user.ExpectedShiftEndTime);
    }

    [Fact]
    public async Task Register_ReturnsCorrectResult()
    {
        var result = await _sut.RegisterAsync("alice", "password123", "Admin", ShiftStart, ShiftEnd);

        Assert.Equal("alice", result.Username);
        Assert.Equal("Admin", result.Role);
        Assert.True(result.Id > 0);
    }


    // --- User existence checks ---

    [Fact]
    public async Task ExistsByUsername_ReturnsTrueAfterRegistration()
    {
        await _sut.RegisterAsync("alice", "password123", "Employee", ShiftStart, ShiftEnd);

        var exists = await _userRepository.ExistsByUsernameAsync("alice");

        Assert.True(exists);
    }


    // --- Retrieval ---

    [Fact]
    public async Task GetUserByIdAsync_ReturnsRegisteredUser()
    {
        var registered = await _sut.RegisterAsync("alice", "password123", "Employee", ShiftStart, ShiftEnd);

        var user = await _sut.GetUserByIdAsync(registered.Id);

        Assert.NotNull(user);
        Assert.Equal("alice", user.Username);
        Assert.Equal("Employee", user.Role);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllRegisteredUsers()
    {
        await _sut.RegisterAsync("alice", "pass1", "Employee", ShiftStart, ShiftEnd);
        await _sut.RegisterAsync("bob", "pass2", "Admin", ShiftStart, ShiftEnd);

        var users = await _sut.GetAllUsersAsync();

        Assert.Equal(2, users.Count);
        Assert.Contains(users, u => u.Username == "alice");
        Assert.Contains(users, u => u.Username == "bob");
    }

    // --- Login ---

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await _sut.RegisterAsync("alice", "password123", "Employee", ShiftStart, ShiftEnd);

        var result = await _sut.LoginAsync("alice", "password123");

        Assert.Equal("fake-jwt-token", result.Token);
        Assert.Equal("alice", result.Username);
        Assert.Equal("Employee", result.Role);
    }
}
