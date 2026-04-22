using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendanceSystem.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly AuthService _sut;

    private static readonly TimeOnly ShiftStart = new(8, 0);
    private static readonly TimeOnly ShiftEnd = new(17, 0);

    private static readonly User ExistingUser = new()
    {
        Id = 1,
        Username = "alice",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password", workFactor: 4),
        Role = "Employee",
        ExpectedShiftStartTime = ShiftStart,
        ExpectedShiftEndTime = ShiftEnd,
        CreatedAt = DateTime.UtcNow
    };

    public AuthServiceTests()
    {
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                 .ReturnsAsync((User u) => { u.Id = 99; return u; });

        _jwt.Setup(j => j.GenerateToken(It.IsAny<User>()))
            .Returns(("token-value", DateTime.UtcNow.AddHours(8)));

        _sut = new AuthService(_userRepo.Object, _jwt.Object, NullLogger<AuthService>.Instance);
    }

    // ── Register ────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewUsername_ReturnsRegisterResult()
    {
        _userRepo.Setup(r => r.ExistsByUsernameAsync("bob")).ReturnsAsync(false);

        var result = await _sut.RegisterAsync("bob", "pass123", "Employee", ShiftStart, ShiftEnd);

        Assert.Equal(99, result.Id);
        Assert.Equal("bob", result.Username);
        Assert.Equal("Employee", result.Role);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsBusinessException()
    {
        _userRepo.Setup(r => r.ExistsByUsernameAsync("alice")).ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.RegisterAsync("alice", "pass123", "Employee", ShiftStart, ShiftEnd));
    }

    [Fact]
    public async Task Register_PersistsUserWithHashedPassword()
    {
        _userRepo.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>())).ReturnsAsync(false);
        User? saved = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                 .Callback<User>(u => saved = u)
                 .ReturnsAsync((User u) => { u.Id = 1; return u; });

        await _sut.RegisterAsync("carol", "plaintext", "Admin", ShiftStart, ShiftEnd);

        Assert.NotNull(saved);
        Assert.NotEqual("plaintext", saved!.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("plaintext", saved.PasswordHash));
    }

    [Fact]
    public async Task Register_SetsCorrectShiftTimes()
    {
        _userRepo.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>())).ReturnsAsync(false);
        User? saved = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>()))
                 .Callback<User>(u => saved = u)
                 .ReturnsAsync((User u) => { u.Id = 1; return u; });

        var start = new TimeOnly(9, 30);
        var end = new TimeOnly(18, 30);
        await _sut.RegisterAsync("dave", "pass", "Employee", start, end);

        Assert.Equal(start, saved!.ExpectedShiftStartTime);
        Assert.Equal(end, saved!.ExpectedShiftEndTime);
    }

    // ── Login ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsLoginResult()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync(ExistingUser);

        var result = await _sut.LoginAsync("alice", "correct-password");

        Assert.Equal("token-value", result.Token);
        Assert.Equal(1, result.UserId);
        Assert.Equal("alice", result.Username);
        Assert.Equal("Employee", result.Role);
    }

    [Fact]
    public async Task Login_UnknownUsername_ThrowsBusinessException()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("nobody")).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.LoginAsync("nobody", "any-password"));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsBusinessException()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync(ExistingUser);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.LoginAsync("alice", "wrong-password"));
    }

    [Fact]
    public async Task Login_ValidCredentials_TokenExpiresInFuture()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("alice")).ReturnsAsync(ExistingUser);

        var result = await _sut.LoginAsync("alice", "correct-password");

        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    // ── GetUserById ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_ExistingId_ReturnsUser()
    {
        _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ExistingUser);

        var user = await _sut.GetUserByIdAsync(1);

        Assert.NotNull(user);
        Assert.Equal("alice", user!.Username);
    }

    [Fact]
    public async Task GetUserById_NonExistentId_ReturnsNull()
    {
        _userRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

        var user = await _sut.GetUserByIdAsync(999);

        Assert.Null(user);
    }

    // ── GetAllUsers ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllUsers_ReturnsAllUsers()
    {
        var users = new List<User> { ExistingUser, new() { Id = 2, Username = "bob", Role = "Admin" } };
        _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users);

        var result = await _sut.GetAllUsersAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllUsers_NoUsers_ReturnsEmptyList()
    {
        _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        var result = await _sut.GetAllUsersAsync();

        Assert.Empty(result);
    }
}
