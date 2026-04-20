namespace AttendanceSystem.API.DTOs.Attendance;

public record RetrospectiveRequest(DateTime Timestamp, string Reason);
