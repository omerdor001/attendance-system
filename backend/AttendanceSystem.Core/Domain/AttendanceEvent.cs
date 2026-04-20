namespace AttendanceSystem.Core.Domain;

public class AttendanceEvent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string EventType { get; set; } = string.Empty;   // "ClockIn" | "ClockOut"
    public DateTime Timestamp { get; set; }                  // UTC
    public bool IsRetrospective { get; set; }
    public string? RetrospectiveReason { get; set; }
    public string? ApprovalStatus { get; set; }             // "Pending" | "Approved" | "Rejected"
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public User User { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
