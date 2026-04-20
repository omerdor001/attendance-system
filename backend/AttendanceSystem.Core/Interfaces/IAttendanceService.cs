namespace AttendanceSystem.Core.Interfaces;

public record ClockResult(int EventId, DateTime Timestamp, DateTimeOffset ZurichTime, string Message, double? WorkedHoursToday = null);
public record RetrospectiveResult(int EventId, DateTime Timestamp, string ApprovalStatus, string Message);
public record HeartbeatResult(
    DateTimeOffset CurrentZurichTime,
    bool IsClockedIn,
    double WorkedHoursToday,
    List<AlertItem> RealtimeAlerts,
    int PendingApprovalCount);
public record AlertItem(string Type, string Message, string Severity);
public record HistoryResult(List<HistoryEntry> Entries, double TotalWorkedHours, List<AnomalyItem> Anomalies);
public record HistoryEntry(int Id, string EventType, DateTime Timestamp, bool IsRetrospective,
    string? RetrospectiveReason, string? ApprovalStatus, DateTime? ApprovedAt);
public record AnomalyItem(string Type, string Date, string Description, double Confidence, string? SuggestedCorrection = null);

public interface IAttendanceService
{
    Task<ClockResult> ClockInAsync(int userId);
    Task<ClockResult> ClockOutAsync(int userId);
    Task<RetrospectiveResult> ClockInRetrospectiveAsync(int userId, DateTime timestamp, string reason);
    Task<RetrospectiveResult> ClockOutRetrospectiveAsync(int userId, DateTime timestamp, string reason);
    Task<HeartbeatResult> GetHeartbeatAsync(int userId);
    Task<HistoryResult> GetHistoryAsync(int userId, DateTime from, DateTime to);
}
