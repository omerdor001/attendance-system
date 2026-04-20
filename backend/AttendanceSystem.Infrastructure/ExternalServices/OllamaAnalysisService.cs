using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.Infrastructure.ExternalServices;

public class OllamaAnalysisService(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaAnalysisService> logger) : IAttendanceAnalysisService
{
    private static readonly TimeZoneInfo ZurichTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<List<AnomalyItem>> AnalyzeUserPatternsAsync(List<AttendanceEvent> events, User user)
    {
        if (events.Count == 0) return [];

        try
        {
            var prompt = BuildPrompt(events, user);
            var requestBody = JsonSerializer.Serialize(
                new OllamaRequest(options.Value.Model, prompt, false, "json"), JsonOpts);

            var response = await httpClient.PostAsync(
                "api/generate", new StringContent(requestBody, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var responseText = doc.RootElement.GetProperty("response").GetString()
                ?? throw new InvalidOperationException("Empty response from Ollama");

            var result = JsonSerializer.Deserialize<OllamaAnomaliesResult>(responseText, JsonOpts);
            if (result?.Anomalies is null or { Count: 0 })
                return AttendanceCalculator.DetectAnomalies(events, user);

            logger.LogInformation("Ollama detected {Count} anomalies for user {UserId}",
                result.Anomalies.Count, user.Id);

            return result.Anomalies
                .Select(a => new AnomalyItem(a.Type, a.Date, a.Description, a.Confidence, a.SuggestedCorrection))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama analysis failed for user {UserId}, using rule-based fallback", user.Id);
            return AttendanceCalculator.DetectAnomalies(events, user);
        }
    }

    private static string BuildPrompt(List<AttendanceEvent> events, User user)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Analyze attendance data for employee \"{user.Username}\".");
        sb.AppendLine($"Expected shift: {user.ExpectedShiftStartTime:HH:mm} - {user.ExpectedShiftEndTime:HH:mm} (Europe/Zurich).");
        sb.AppendLine();
        sb.AppendLine("Attendance events:");

        foreach (var e in events.OrderBy(e => e.Timestamp))
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(e.Timestamp, ZurichTz);
            sb.AppendLine($"- {e.EventType} at {local:yyyy-MM-dd HH:mm} (Zurich)");
        }

        sb.AppendLine();
        sb.AppendLine("Return ONLY a JSON object with this exact structure:");
        sb.AppendLine("{\"anomalies\":[{\"type\":\"string\",\"date\":\"YYYY-MM-DD\",\"description\":\"string\",\"confidence\":0.0,\"suggestedCorrection\":\"string or null\"}]}");
        sb.AppendLine();
        sb.AppendLine("Anomaly types to detect:");
        sb.AppendLine("- forgotten_clock_out: clock-in with no matching clock-out that day");
        sb.AppendLine("- late_arrival: clocked in more than 15 minutes after expected start time");
        sb.AppendLine("- early_arrival: clocked in more than 15 minutes before expected start time");
        sb.AppendLine("- early_leave: clocked out more than 60 minutes before expected end time");
        sb.AppendLine("- long_shift: total shift duration exceeds 12 hours");
        sb.AppendLine("Use confidence 1.0 for deterministic rules, 0.7-0.9 for inferred anomalies.");

        return sb.ToString();
    }

    private record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format")] string Format);

    private record OllamaAnomaliesResult(List<OllamaAnomaly>? Anomalies);

    private record OllamaAnomaly(
        string Type,
        string Date,
        string Description,
        double Confidence,
        string? SuggestedCorrection);
}
