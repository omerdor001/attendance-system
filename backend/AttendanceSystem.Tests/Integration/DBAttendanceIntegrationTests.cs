using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using AttendanceSystem.Core.Services;
using AttendanceSystem.Infrastructure.Data;
using AttendanceSystem.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AttendanceSystem.Tests.Integration;

public class DBIntegrationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AttendanceRepository _repository;
    private readonly Mock<IWorldTimeApiService> _time = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<IAttendanceAnalysisService> _analysis = new();
    private readonly AttendanceService _sut;

    private static readonly DateTimeOffset ZurichNow =
        new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.FromHours(1));

    private readonly User _testUser;

    public DBIntegrationTests()
    {
        _testUser = new User
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hash",
            Role = "Employee",
            ExpectedShiftStartTime = new TimeOnly(8, 0),
            ExpectedShiftEndTime = new TimeOnly(17, 0),
            CreatedAt = DateTime.UtcNow
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        _repository = new AttendanceRepository(_db);

        _time.Setup(t => t.GetCurrentZurichTimeAsync()).ReturnsAsync(ZurichNow);
        _auth.Setup(a => a.GetUserByIdAsync(1)).ReturnsAsync(_testUser);
        _analysis.Setup(a => a.AnalyzeUserPatternsAsync(It.IsAny<List<AttendanceEvent>>(), It.IsAny<User>()))
                 .ReturnsAsync([]);

        _sut = new AttendanceService(_repository, _time.Object, _auth.Object, _analysis.Object,
            NullLogger<AttendanceService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // --- ClockIn and ClockOut ---

    [Fact]
    public async Task ClockIn_PersistsEventToDatabase()
    {
        await _sut.ClockInAsync(1);

        var events = await _db.AttendanceEvents.ToListAsync();
        events.Should().HaveCount(1);
        events[0].EventType.Should().Be("ClockIn");
        events[0].UserId.Should().Be(1);
        events[0].IsRetrospective.Should().BeFalse();
    }

    [Fact]
    public async Task ClockIn_AfterCompletedClockInOut_Succeeds()
    {
        await _sut.ClockInAsync(1);
        _time.Setup(t => t.GetCurrentZurichTimeAsync()).ReturnsAsync(ZurichNow.AddHours(8));
        await _sut.ClockOutAsync(1);

        _time.Setup(t => t.GetCurrentZurichTimeAsync()).ReturnsAsync(ZurichNow.AddHours(9));
        var result = await _sut.ClockInAsync(1);

        result.Message.Should().Be("Clocked in successfully");
        _db.AttendanceEvents.Count().Should().Be(3);
    }

    [Fact]
    public async Task ClockInThenClockOut_CalculatesWorkedHoursCorrectly()
    {
        await _sut.ClockInAsync(1);
        _time.Setup(t => t.GetCurrentZurichTimeAsync()).ReturnsAsync(ZurichNow.AddHours(8));

        var result = await _sut.ClockOutAsync(1);

        result.Message.Should().Be("Clocked out successfully");
        result.WorkedHoursToday.Should().BeApproximately(8.0, 0.01);
    }

    // --- Retrospectives ---

    [Fact]
    public async Task ClockInRetrospective_PersistsAsPendingWithReason()
    {
        var past = ZurichNow.UtcDateTime.AddHours(-3);

        var result = await _sut.ClockInRetrospectiveAsync(1, past, "Forgot to clock in this morning");

        result.ApprovalStatus.Should().Be("Pending");

        var evt = await _db.AttendanceEvents.FirstAsync();
        evt.IsRetrospective.Should().BeTrue();
        evt.ApprovalStatus.Should().Be("Pending");
        evt.RetrospectiveReason.Should().Be("Forgot to clock in this morning");
        evt.SubmittedAt.Should().NotBeNull();
    }

    // --- History ---

    [Fact]
    public async Task GetHistoryAsync_ReturnsAllEventsInRange()
    {
        _db.AttendanceEvents.AddRange(
            new AttendanceEvent { UserId = 1, EventType = "ClockIn",  Timestamp = ZurichNow.UtcDateTime.AddDays(-1) },
            new AttendanceEvent { UserId = 1, EventType = "ClockOut", Timestamp = ZurichNow.UtcDateTime.AddDays(-1).AddHours(8) }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetHistoryAsync(1, ZurichNow.UtcDateTime.AddDays(-2), ZurichNow.UtcDateTime);

        result.Entries.Should().HaveCount(2);
        result.TotalWorkedHours.Should().BeApproximately(8.0, 0.01);
    }

    [Fact]
    public async Task GetHistoryAsync_ExcludesEventsOutsideRange()
    {
        _db.AttendanceEvents.Add(new AttendanceEvent
        {
            UserId = 1,
            EventType = "ClockIn",
            Timestamp = ZurichNow.UtcDateTime.AddDays(-10)
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetHistoryAsync(1, ZurichNow.UtcDateTime.AddDays(-2), ZurichNow.UtcDateTime);

        result.Entries.Should().BeEmpty();
        result.TotalWorkedHours.Should().Be(0);
    }

    [Fact]
    public async Task GetHistoryAsync_PendingRetrospectiveEventsExcludedFromHoursCalculation()
    {
        _db.AttendanceEvents.AddRange(
            new AttendanceEvent { UserId = 1, EventType = "ClockIn",  Timestamp = ZurichNow.UtcDateTime.AddHours(-5) },
            new AttendanceEvent { UserId = 1, EventType = "ClockOut", Timestamp = ZurichNow.UtcDateTime.AddHours(-4) },
            new AttendanceEvent { UserId = 1, EventType = "ClockIn",  Timestamp = ZurichNow.UtcDateTime.AddHours(-3), IsRetrospective = true, ApprovalStatus = "Pending" }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetHistoryAsync(1, ZurichNow.UtcDateTime.AddHours(-6), ZurichNow.UtcDateTime);

        result.Entries.Should().HaveCount(3);
        result.TotalWorkedHours.Should().BeApproximately(1.0, 0.01);
    }
}
