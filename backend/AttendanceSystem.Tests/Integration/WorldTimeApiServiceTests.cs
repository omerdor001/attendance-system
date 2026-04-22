using System.Net;
using System.Text;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace AttendanceSystem.Tests.Integration;

public class WorldTimeApiServiceTests
{
    private const string BaseAddress = "https://timeapi.io/";

    private static WorldTimeApiService CreateService(HttpStatusCode status, string body)
    {
        var client = new HttpClient(new FakeHttpHandler(status, body)) { BaseAddress = new Uri(BaseAddress) };
        return new WorldTimeApiService(client, NullLogger<WorldTimeApiService>.Instance);
    }

    private static WorldTimeApiService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        return new WorldTimeApiService(client, NullLogger<WorldTimeApiService>.Instance);
    }

    [Fact]
    public async Task GetCurrentZurichTimeAsync_ValidResponse_ReturnsParsedOffset()
    {
        var json = """
            {
                "timeZone": "Europe/Zurich",
                "dateTime": "2024-01-15T09:30:00.0000000",
                "utcOffset": "+01:00"
            }
            """;
        var sut = CreateService(HttpStatusCode.OK, json);

        var result = await sut.GetCurrentZurichTimeAsync();

        Assert.Equal(new DateTime(2024, 1, 15, 9, 30, 0), result.DateTime);
        Assert.Equal(TimeSpan.FromHours(1), result.Offset);
    }

    [Fact]
    public async Task GetCurrentZurichTimeAsync_WrongTimezone_ThrowsExternalServiceException()
    {
        var json = """{"timeZone": "America/New_York", "dateTime": "2024-01-15T09:00:00"}""";
        var sut = CreateService(HttpStatusCode.OK, json);

        await Assert.ThrowsAsync<ExternalServiceException>(() => sut.GetCurrentZurichTimeAsync());
    }

    [Fact]
    public async Task GetCurrentZurichTimeAsync_NullDateTime_ThrowsExternalServiceException()
    {
        var json = """{"timeZone": "Europe/Zurich", "dateTime": null}""";
        var sut = CreateService(HttpStatusCode.OK, json);

        await Assert.ThrowsAsync<ExternalServiceException>(() => sut.GetCurrentZurichTimeAsync());
    }

    [Fact]
    public async Task GetCurrentZurichTimeAsync_HttpError_FallsBackToSystemClock()
    {
        var sut = CreateService(HttpStatusCode.InternalServerError, "");
        var before = DateTime.UtcNow;

        var result = await sut.GetCurrentZurichTimeAsync();

        Assert.InRange(result.UtcDateTime, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GetCurrentZurichTimeAsync_NetworkFailure_FallsBackToSystemClock()
    {
        var sut = CreateService(new ThrowingHttpHandler());
        var before = DateTime.UtcNow;

        var result = await sut.GetCurrentZurichTimeAsync();

        Assert.InRange(result.UtcDateTime, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GetCurrentZurichTimeAsync_MalformedJson_FallsBackToSystemClock()
    {
        var sut = CreateService(HttpStatusCode.OK, "not valid json {{{{");
        var before = DateTime.UtcNow;

        var result = await sut.GetCurrentZurichTimeAsync();

        Assert.InRange(result.UtcDateTime, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GetCurrentUtcTimeAsync_ReturnsUtcEquivalentOfZurichTime()
    {
        // 14:00 Zurich in July (UTC+2) = 12:00 UTC
        var json = """
            {
                "timeZone": "Europe/Zurich",
                "dateTime": "2024-07-15T14:00:00.0000000",
                "utcOffset": "+02:00"
            }
            """;
        var sut = CreateService(HttpStatusCode.OK, json);

        var result = await sut.GetCurrentUtcTimeAsync();

        Assert.Equal(new DateTime(2024, 7, 15, 12, 0, 0), result);
    }

    private sealed class FakeHttpHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated network failure");
    }
}
