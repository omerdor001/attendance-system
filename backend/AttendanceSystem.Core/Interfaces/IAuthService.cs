using AttendanceSystem.Core.Domain;

namespace AttendanceSystem.Core.Interfaces;

public record RegisterResult(int Id, string Username, string Role);
public record LoginResult(string Token, DateTime ExpiresAt, int UserId, string Username, string Role);

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(string username, string password, string role,
        TimeOnly shiftStart, TimeOnly shiftEnd);
    Task<LoginResult> LoginAsync(string username, string password);
    Task<User?> GetUserByIdAsync(int userId);
    Task<List<User>> GetAllUsersAsync();
}
