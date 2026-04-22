using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Core.Services;

public class AuthService(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<RegisterResult> RegisterAsync(string username, string password, string role,
        TimeOnly shiftStart, TimeOnly shiftEnd) {
        if (await userRepository.ExistsByUsernameAsync(username)) {
            logger.LogError("Attempt to register with existing username: {Username}", username);
            throw new BusinessException("Username already exists");
        }
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        var user = await userRepository.AddAsync(new User
        {
            Username = username,
            PasswordHash = hash,
            Role = role,
            ExpectedShiftStartTime = shiftStart,
            ExpectedShiftEndTime = shiftEnd,
            CreatedAt = DateTime.UtcNow
        });
        logger.LogInformation("User {Username} registered with role {Role}", username, role);
        return new RegisterResult(user.Id, user.Username, user.Role);
    }

    public async Task<LoginResult> LoginAsync(string username, string password) {
        var user = await userRepository.GetByUsernameAsync(username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) {
            logger.LogError("Invalid login attempt for username: {Username}", username);
            throw new BusinessException("Invalid credentials");
        }   
        var (token, expiresAt) = jwtTokenService.GenerateToken(user);
        return new LoginResult(token, expiresAt, user.Id, user.Username, user.Role);
    }

    public Task<User?> GetUserByIdAsync(int userId) => userRepository.GetByIdAsync(userId);

    public Task<List<User>> GetAllUsersAsync() => userRepository.GetAllAsync();
}
