namespace AttendanceSystem.API.DTOs.Auth;

public record RegisterRequest(
    string Username,
    string Password,
    string Role,
    string ExpectedShiftStartTime,
    string ExpectedShiftEndTime);
