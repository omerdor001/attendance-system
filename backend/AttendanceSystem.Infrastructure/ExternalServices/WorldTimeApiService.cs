using System.Text.Json;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.ExternalServices;

public class WorldTimeApiService(
    HttpClient httpClient,
    ILogger<WorldTimeApiService> logger) : IWorldTimeApiService
{

    public async Task<DateTimeOffset> GetCurrentZurichTimeAsync() {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try {
            logger.LogInformation("Calling TimeAPI for Europe/Zurich time");
            var response = await httpClient.GetAsync("api/time/current/zone?timeZone=Europe/Zurich");
            stopwatch.Stop();
            logger.LogInformation("TimeAPI responded: {Status} in {Ms}ms",
                response.StatusCode, stopwatch.ElapsedMilliseconds);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("timeZone", out var tz) || tz.GetString() != "Europe/Zurich") { 
                logger.LogError("TimeAPI returned unexpected timezone: {Timezone}", tz.GetString());
                throw new ExternalServiceException("TimeAPI returned unexpected timezone");
            }
            if (!root.TryGetProperty("dateTime", out var dt) || dt.ValueKind == JsonValueKind.Null || dt.GetString() is null) {
                logger.LogError("TimeAPI response missing dateTime property");
                throw new ExternalServiceException("TimeAPI response missing dateTime");
            }
            var datetimeStr = dt.GetString()!;
            var localDt = DateTime.Parse(datetimeStr, System.Globalization.CultureInfo.InvariantCulture);
            var offset = ZurichTz.GetUtcOffset(localDt);
            return new DateTimeOffset(localDt, offset);
        }
        catch (Exception ex) when (ex is not ExternalServiceException) {
            stopwatch.Stop();
            logger.LogWarning(ex, "TimeAPI call failed after {Ms}ms, falling back to system clock: {Message}",
                stopwatch.ElapsedMilliseconds, ex.Message);
            var fallbackUtc = DateTime.UtcNow;
            var fallbackLocal = TimeZoneInfo.ConvertTimeFromUtc(fallbackUtc, ZurichTz);
            return new DateTimeOffset(fallbackLocal, ZurichTz.GetUtcOffset(fallbackLocal));
        }
    }

    public async Task<DateTime> GetCurrentUtcTimeAsync() {
        var zurich = await GetCurrentZurichTimeAsync();
        return zurich.UtcDateTime;
    }

    private static readonly TimeZoneInfo ZurichTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
}
