using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendanceSystem.Tests.Unit;

public class AttendanceServiceTests
{
    private readonly Mock<IAttendanceRepository> _repo = new();
    private readonly Mock<IWorldTimeApiService> _time = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<IAttendanceAnalysisService> _analysis = new();
    private readonly AttendanceService _sut;

    private static readonly DateTimeOffset ZurichNow =
        new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.FromHours(1));

    public AttendanceServiceTests()
    {
        _time.Setup(t => t.GetCurrentZurichTimeAsync()).ReturnsAsync(ZurichNow);
        _repo.Setup(r => r.AddAsync(It.IsAny<AttendanceEvent>()))
             .ReturnsAsync((AttendanceEvent e) => { e.Id = 1; return e; });
        _analysis.Setup(a => a.AnalyzeUserPatternsAsync(It.IsAny<List<AttendanceEvent>>(), It.IsAny<User>()))
                 .ReturnsAsync([]);
        _sut = new AttendanceService(_repo.Object, _time.Object, _auth.Object, _analysis.Object,
            NullLogger<AttendanceService>.Instance);
    }

    [Fact]
    public async Task ClockIn_WhenNotClockedIn_ReturnsClockResult()
    {
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>()))
             .ReturnsAsync([]);

        var result = await _sut.ClockInAsync(1);

        Assert.Equal(1, result.EventId);
        Assert.Equal("Clocked in successfully", result.Message);
    }

    [Fact]
    public async Task ClockIn_WhenAlreadyClockedIn_ThrowsConflict()
    {
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>()))
             .ReturnsAsync([new AttendanceEvent { EventType = "ClockIn", Timestamp = ZurichNow.UtcDateTime }]);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.ClockInAsync(1));
    }

    [Fact]
    public async Task ClockOut_WhenClockedIn_ReturnsResult()
    {
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>()))
             .ReturnsAsync([new AttendanceEvent { EventType = "ClockIn", Timestamp = ZurichNow.UtcDateTime.AddHours(-1) }]);

        var result = await _sut.ClockOutAsync(1);

        Assert.Equal("Clocked out successfully", result.Message);
        Assert.True(result.WorkedHoursToday > 0);
    }

    [Fact]
    public async Task ClockOut_WhenNotClockedIn_ThrowsConflict()
    {
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>()))
             .ReturnsAsync([]);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.ClockOutAsync(1));
    }

    [Fact]
    public async Task ClockInRetrospective_FutureDate_ThrowsValidation()
    {
        var future = ZurichNow.UtcDateTime.AddHours(1);

        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.ClockInRetrospectiveAsync(1, future, "Valid reason here"));
    }

    [Fact]
    public async Task ClockInRetrospective_ShortReason_ThrowsValidation()
    {
        var past = ZurichNow.UtcDateTime.AddHours(-2);
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>())).ReturnsAsync([]);

        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.ClockInRetrospectiveAsync(1, past, "short"));
    }

    [Fact]
    public async Task ClockInRetrospective_OlderThan30Days_ThrowsValidation()
    {
        var old = ZurichNow.UtcDateTime.AddDays(-31);

        await Assert.ThrowsAsync<Core.Exceptions.ValidationException>(
            () => _sut.ClockInRetrospectiveAsync(1, old, "Valid reason here long enough"));
    }

    [Fact]
    public async Task ClockInRetrospective_WhenUnpairedClockInExists_ThrowsConflict()
    {
        var past = ZurichNow.UtcDateTime.AddHours(-2);
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>()))
             .ReturnsAsync([new AttendanceEvent { EventType = "ClockIn", Timestamp = past.AddHours(-1) }]);

        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.ClockInRetrospectiveAsync(1, past, "Forgot to clock in earlier"));
    }

    [Fact]
    public async Task ClockInRetrospective_ValidRequest_ReturnsPending()
    {
        var past = ZurichNow.UtcDateTime.AddHours(-2);
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>())).ReturnsAsync([]);

        var result = await _sut.ClockInRetrospectiveAsync(1, past, "Forgot to clock in today");

        Assert.Equal("Pending", result.ApprovalStatus);
    }

    [Fact]
    public async Task Heartbeat_OvertimeAlert_WhenClockedInPastShiftEnd()
    {
        var overtimeNow = new DateTimeOffset(2024, 1, 15, 19, 0, 0, TimeSpan.FromHours(1));
        _time.Setup(t => t.GetCurrentZurichTimeAsync()).ReturnsAsync(overtimeNow);

        var user = new User
        {
            Id = 1, Username = "test", Role = "Employee",
            ExpectedShiftStartTime = new TimeOnly(8, 0),
            ExpectedShiftEndTime = new TimeOnly(17, 0)
        };
        _auth.Setup(a => a.GetUserByIdAsync(1)).ReturnsAsync(user);
        _repo.Setup(r => r.GetUserEventsForDayAsync(1, It.IsAny<DateTime>()))
             .ReturnsAsync([new AttendanceEvent { EventType = "ClockIn", Timestamp = overtimeNow.UtcDateTime.AddHours(-10) }]);
        _repo.Setup(r => r.GetAllPendingAsync()).ReturnsAsync([]);

        var result = await _sut.GetHeartbeatAsync(1);

        Assert.Contains(result.RealtimeAlerts, a => a.Type == "overtime_alert");
    }
}
