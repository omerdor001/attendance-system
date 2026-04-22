using System.Net;
using System.Text;
using System.Text.Json;
using AttendanceSystem.Core.Domain;
using AttendanceSystem.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.Tests.Integration;

public class OllamaAnalysisServiceTests
{
    private const string BaseAddress = "http://localhost:11434/";

    private static readonly User TestUser = new()
    {
        Id = 1,
        Username = "testuser",
        Role = "Employee",
        ExpectedShiftStartTime = new TimeOnly(8, 0),
        ExpectedShiftEndTime = new TimeOnly(17, 0)
    };

    // UTC timestamps for events that are on time in Zurich (UTC+1 in January)
    // 8:00 UTC = 9:00 Zurich → 1h late (triggers late_arrival)
    private static readonly DateTime LateClockInUtc = new(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc);
    // 7:00 UTC = 8:00 Zurich → on time, but no clock-out (triggers forgotten_clock_out)
    private static readonly DateTime OnTimeClockInUtc = new(2024, 1, 15, 7, 0, 0, DateTimeKind.Utc);

    private static OllamaAnalysisService CreateService(HttpStatusCode status, string body, string model = "llama3.2")
    {
        var client = new HttpClient(new FakeHttpHandler(status, body)) { BaseAddress = new Uri(BaseAddress) };
        var options = Options.Create(new OllamaOptions { BaseUrl = BaseAddress, Model = model });
        return new OllamaAnalysisService(client, options, NullLogger<OllamaAnalysisService>.Instance);
    }

    private static OllamaAnalysisService CreateService(CapturingHttpHandler handler, string model = "llama3.2")
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var options = Options.Create(new OllamaOptions { BaseUrl = BaseAddress, Model = model });
        return new OllamaAnalysisService(client, options, NullLogger<OllamaAnalysisService>.Instance);
    }

    private static string BuildOllamaResponse(string innerJson)
        => JsonSerializer.Serialize(new { response = innerJson });

    [Fact]
    public async Task AnalyzeUserPatternsAsync_EmptyEvents_ReturnsEmptyWithoutHttpCall()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "");
        var sut = CreateService(handler);

        var result = await sut.AnalyzeUserPatternsAsync([], TestUser);

        Assert.Empty(result);
        Assert.Null(handler.CapturedRequest);
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_ValidResponse_ReturnsOllamaAnomalies()
    {
        var innerJson = """
            {
                "anomalies": [
                    {
                        "type": "late_arrival",
                        "date": "2024-01-15",
                        "description": "Clocked in at 09:00, expected 08:00",
                        "confidence": 1.0,
                        "suggestedCorrection": null
                    }
                ]
            }
            """;
        var sut = CreateService(HttpStatusCode.OK, BuildOllamaResponse(innerJson));

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Single(result);
        Assert.Equal("late_arrival", result[0].Type);
        Assert.Equal("2024-01-15", result[0].Date);
        Assert.Equal(1.0, result[0].Confidence);
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_EmptyAnomaliesFromOllama_FallsBackToRuleBased()
    {
        var innerJson = """{"anomalies": []}""";
        var sut = CreateService(HttpStatusCode.OK, BuildOllamaResponse(innerJson));

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = OnTimeClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Single(result);
        Assert.Equal("forgotten_clock_out", result[0].Type);
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_NullAnomaliesFromOllama_FallsBackToRuleBased()
    {
        var innerJson = """{"anomalies": null}""";
        var sut = CreateService(HttpStatusCode.OK, BuildOllamaResponse(innerJson));

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Contains(result, a => a.Type == "late_arrival");
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_HttpError_FallsBackToRuleBased()
    {
        var sut = CreateService(HttpStatusCode.InternalServerError, "");

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Contains(result, a => a.Type == "late_arrival");
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_NetworkFailure_FallsBackToRuleBased()
    {
        var client = new HttpClient(new ThrowingHttpHandler()) { BaseAddress = new Uri(BaseAddress) };
        var options = Options.Create(new OllamaOptions { BaseUrl = BaseAddress, Model = "llama3.2" });
        var sut = new OllamaAnalysisService(client, options, NullLogger<OllamaAnalysisService>.Instance);

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Contains(result, a => a.Type == "late_arrival");
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_MalformedOuterJson_FallsBackToRuleBased()
    {
        var sut = CreateService(HttpStatusCode.OK, "not json {{{{");

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Contains(result, a => a.Type == "late_arrival");
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_MalformedInnerJson_FallsBackToRuleBased()
    {
        var sut = CreateService(HttpStatusCode.OK, BuildOllamaResponse("not valid inner json {{{{"));

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        var result = await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.Contains(result, a => a.Type == "late_arrival");
    }

    [Fact]
    public async Task AnalyzeUserPatternsAsync_SendsCorrectRequestBody()
    {
        var innerJson = """{"anomalies": []}""";
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, BuildOllamaResponse(innerJson));
        var sut = CreateService(handler, model: "llama3.2");

        var events = new List<AttendanceEvent>
        {
            new() { UserId = 1, EventType = "ClockIn", Timestamp = LateClockInUtc }
        };

        await sut.AnalyzeUserPatternsAsync(events, TestUser);

        Assert.NotNull(handler.CapturedBody);
        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        var root = doc.RootElement;
        Assert.Equal("llama3.2", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("json", root.GetProperty("format").GetString());
        Assert.Contains("testuser", root.GetProperty("prompt").GetString());
    }

    private sealed class FakeHttpHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    internal sealed class CapturingHttpHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            CapturedBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated network failure");
    }
}
