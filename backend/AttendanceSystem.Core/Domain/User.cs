namespace AttendanceSystem.Core.Domain;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public TimeOnly ExpectedShiftStartTime { get; set; }
    public TimeOnly ExpectedShiftEndTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AttendanceEvent> AttendanceEvents { get; set; } = [];
}
