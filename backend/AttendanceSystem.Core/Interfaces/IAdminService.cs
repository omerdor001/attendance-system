namespace AttendanceSystem.Core.Interfaces;

public record PendingEntry(int Id, string EmployeeName, string EventType, DateTime RequestedTimestamp,
    string Reason, DateTime SubmittedAt);
public record ApprovalResult(int EventId, string ApprovalStatus, DateTime? ApprovedAt);
public record RejectionResult(int EventId, string ApprovalStatus);
public record EmployeeReport(int UserId, string Username, double TotalWorkedHours,
    List<HistoryEntry> Entries, List<AnomalyItem> Anomalies);

public interface IAdminService
{
    Task<List<PendingEntry>> GetPendingApprovalsAsync();
    Task<ApprovalResult> ApproveAsync(int eventId, int adminUserId);
    Task<RejectionResult> RejectAsync(int eventId, string rejectionReason);
    Task<List<EmployeeReport>> GetReportsAsync(DateTime? from, DateTime? to, int? userId);
}
