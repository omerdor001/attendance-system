using AttendanceSystem.Core.Domain;

namespace AttendanceSystem.Core.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
