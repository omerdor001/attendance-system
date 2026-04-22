using AttendanceSystem.Core.Domain;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Core.Services;

public class AttendanceService(
    IAttendanceRepository repository,
    IWorldTimeApiService timeService,
    IAuthService authService,
    IAttendanceAnalysisService analysisService,
    ILogger<AttendanceService> logger) : IAttendanceService
{

    public async Task<ClockResult> ClockInAsync(int userId) {
        var zurichTime = await timeService.GetCurrentZurichTimeAsync();
        var utcNow = zurichTime.UtcDateTime;
        var recentEvents = await GetRecentApprovedEvents(userId, utcNow);
        var lastEvent = recentEvents.LastOrDefault();
        if (lastEvent?.EventType == "ClockIn") {
            logger.LogError("User {UserId} attempted to clock in at {Timestamp} but is already clocked in", userId, utcNow);
            throw new ConflictException("Already clocked in — please clock out first");
        }
        var evt = await repository.AddAsync(new AttendanceEvent
        {
            UserId = userId,
            EventType = "ClockIn",
            Timestamp = utcNow,
            IsRetrospective = false
        });
        logger.LogInformation("User {UserId} clocked in at {Timestamp}", userId, utcNow);
        return new ClockResult(evt.Id, evt.Timestamp, zurichTime);
    }

    public async Task<ClockResult> ClockOutAsync(int userId) {
        var zurichTime = await timeService.GetCurrentZurichTimeAsync();
        var utcNow = zurichTime.UtcDateTime;
        var recentEvents = await GetRecentApprovedEvents(userId, utcNow);
        var lastEvent = recentEvents.LastOrDefault();
        if (lastEvent == null || lastEvent.EventType != "ClockIn") {
            logger.LogError("User {UserId} attempted to clock out at {Timestamp} but has no active clock-in", userId, utcNow);
            throw new ConflictException("No active clock-in found — please clock in first");
        }
        var evt = await repository.AddAsync(new AttendanceEvent
        {
            UserId = userId,
            EventType = "ClockOut",
            Timestamp = utcNow,
            IsRetrospective = false
        });
        var workedHours = AttendanceCalculator.CalculateWorkedHours(recentEvents.Append(evt).ToList());
        logger.LogInformation("User {UserId} clocked out at {Timestamp}", userId, utcNow);
        return new ClockResult(evt.Id, evt.Timestamp, zurichTime, workedHours);
    }

    public async Task<RetrospectiveResult> ClockInRetrospectiveAsync(int userId, DateTime timestamp, string reason) {
        var utcNow = await ValidateRetrospectiveTimestamp(timestamp, reason);
        var dayEvents = await repository.GetUserEventsForDayAsync(userId, timestamp.Date);
        var approvedEvents = dayEvents.Where(IsApproved).OrderBy(e => e.Timestamp).ToList();
        var lastApproved = approvedEvents.LastOrDefault();
        if (lastApproved?.EventType == "ClockIn") {
            logger.LogError("User {UserId} attempted to add a retrospective clock-in at {Timestamp} but already has an unpaired clock-in", userId, timestamp);
            throw new ConflictException("You already have an unpaired clock-in — cannot add another clock-in");
        }
        if (approvedEvents.Any(e => e.Timestamp.Minute == timestamp.Minute &&
                                    e.Timestamp.Hour == timestamp.Hour &&
                                    e.EventType == "ClockIn")) {
            logger.LogError("User {UserId} attempted to add a retrospective clock-in at {Timestamp} but an event already exists at that time", userId, timestamp);
            throw new ConflictException("Entry overlaps with existing attendance data for that time period");
        }
        var evt = await repository.AddAsync(new AttendanceEvent
        {
            UserId = userId,
            EventType = "ClockIn",
            Timestamp = timestamp,
            IsRetrospective = true,
            RetrospectiveReason = reason,
            ApprovalStatus = "Pending",
            SubmittedAt = utcNow
        });
        logger.LogInformation("User {UserId} submitted a retrospective clock-in for {Timestamp} with reason: {Reason}", userId, timestamp, reason);
        return new RetrospectiveResult(evt.Id, evt.Timestamp, "Pending",
            "Your request has been submitted and is awaiting admin approval");
    }

    public async Task<RetrospectiveResult> ClockOutRetrospectiveAsync(int userId, DateTime timestamp, string reason) {
        var utcNow = await ValidateRetrospectiveTimestamp(timestamp, reason);
        var dayEvents = await repository.GetUserEventsForDayAsync(userId, timestamp.Date);
        var approvedEvents = dayEvents.Where(IsApproved).OrderBy(e => e.Timestamp).ToList();
        var lastClockIn = approvedEvents.LastOrDefault(e => e.EventType == "ClockIn");
        if (lastClockIn == null) {
            logger.LogError("User {UserId} attempted to add a retrospective clock-out at {Timestamp} but has no unpaired clock-in", userId, timestamp);
            throw new ConflictException("No unpaired clock-in found — cannot add a clock-out without a matching clock-in");
        }
        var lastEvent = approvedEvents.LastOrDefault();
        if (lastEvent?.EventType == "ClockOut" || (lastClockIn != null && timestamp <= lastClockIn.Timestamp)) {
            logger.LogError("User {UserId} attempted to add a retrospective clock-out at {Timestamp} but it is before the last clock-in at {LastClockIn}", userId, timestamp, lastClockIn?.Timestamp);
            throw new ConflictException("Clock-out time is before the last clock-in time");
        }
        var evt = await repository.AddAsync(new AttendanceEvent
        {
            UserId = userId,
            EventType = "ClockOut",
            Timestamp = timestamp,
            IsRetrospective = true,
            RetrospectiveReason = reason,
            ApprovalStatus = "Pending",
            SubmittedAt = utcNow
        });
        logger.LogInformation("User {UserId} submitted a retrospective clock-out for {Timestamp} with reason: {Reason}", userId, timestamp, reason);
        return new RetrospectiveResult(evt.Id, evt.Timestamp, "Pending",
            "Your request has been submitted and is awaiting admin approval");
    }

    public async Task<HeartbeatResult> GetHeartbeatAsync(int userId) {
        var user = await authService.GetUserByIdAsync(userId);
        if(user==null) {
            logger.LogError("User {UserId} not found when attempting to get heartbeat", userId);
            throw new NotFoundException("User not found");
        }
        var zurichTime = await timeService.GetCurrentZurichTimeAsync();
        var utcNow = zurichTime.UtcDateTime;
        var zurichLocal = zurichTime.DateTime;
        var recentEvents = await GetRecentApprovedEvents(userId, utcNow);
        var isClockedIn = recentEvents.LastOrDefault()?.EventType == "ClockIn";
        var todayEvents = await GetTodayZurichEvents(userId, zurichTime);
        var workedHours = AttendanceCalculator.CalculateWorkedHours(todayEvents);
        var alerts = new List<AlertItem>();
        var currentTime = TimeOnly.FromDateTime(zurichLocal);
        var shiftEnd = user.ExpectedShiftEndTime;
        var shiftStart = user.ExpectedShiftStartTime;
        if (isClockedIn && currentTime > shiftEnd.AddHours(1)) { 
            alerts.Add(new AlertItem("overtime_alert",
                $"It's {currentTime:HH:mm}, your shift ended at {shiftEnd:HH:mm}. Don't forget to clock out!",
                "warning"));
        }
        if (!isClockedIn && currentTime > shiftStart.AddMinutes(15)) {
            alerts.Add(new AlertItem("late_arrival_alert",
                $"It's {currentTime:HH:mm}, your shift starts at {shiftStart:HH:mm}. You haven't clocked in yet!",
                "warning"));
        }
        var allPending = await repository.GetAllPendingAsync();
        var pendingCount = allPending.Count;
        logger.LogInformation("User {UserId} heartbeat at {Timestamp}: ClockedIn={IsClockedIn}, WorkedHoursToday={WorkedHours}, Alerts={AlertCount}, PendingApprovals={PendingCount}",
            userId, zurichTime, isClockedIn, workedHours, alerts.Count, pendingCount);
        return new HeartbeatResult(zurichTime, isClockedIn, workedHours, alerts, pendingCount);
    }

    public async Task<HistoryResult> GetHistoryAsync(int userId, DateTime from, DateTime to) {
        var user = await authService.GetUserByIdAsync(userId);
        if (user == null) {
            logger.LogError("User {UserId} not found when attempting to get history", userId);
            throw new NotFoundException("User not found");
        }
        var events = await repository.GetEventsAsync(from, to, userId);
        var entries = events.Select(e => new HistoryEntry(
            e.Id, e.EventType, e.Timestamp, e.IsRetrospective,
            e.RetrospectiveReason, e.ApprovalStatus, e.ApprovedAt)).ToList();
        var approvedEvents = events.Where(IsApproved).OrderBy(e => e.Timestamp).ToList();
        var totalHours = AttendanceCalculator.CalculateWorkedHours(approvedEvents);
        var allAnomalies = await analysisService.AnalyzeUserPatternsAsync(approvedEvents, user);
        var zurichToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZurichTz).Date.ToString("yyyy-MM-dd");
        var todayAnomalies = allAnomalies.Where(a => a.Date == zurichToday).ToList();
        logger.LogInformation("User {UserId} retrieved history from {From} to {To}: TotalHours={TotalHours}, Anomalies={AnomalyCount}",
            userId, from, to, totalHours, todayAnomalies.Count);
        return new HistoryResult(entries, totalHours, todayAnomalies);
    }

    private async Task<List<AttendanceEvent>> GetRecentApprovedEvents(int userId, DateTime utcNow) {
        var events = await repository.GetEventsAsync(utcNow.AddHours(-30), utcNow, userId);
        return events.Where(IsApproved).OrderBy(e => e.Timestamp).ToList();
    }

    private async Task<List<AttendanceEvent>> GetTodayZurichEvents(int userId, DateTimeOffset zurichTime) {
        var local = zurichTime.DateTime;
        var dayStart = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0);
        var utcStart = new DateTimeOffset(dayStart, zurichTime.Offset).UtcDateTime;
        var utcEnd = utcStart.AddDays(1);
        var events = await repository.GetEventsAsync(utcStart, utcEnd, userId);
        return events.Where(IsApproved).OrderBy(e => e.Timestamp).ToList();
    }

    private static bool IsApproved(AttendanceEvent e) =>
        !e.IsRetrospective || e.ApprovalStatus == "Approved";

    private async Task<DateTime> ValidateRetrospectiveTimestamp(DateTime timestamp, string reason) {
        if (reason.Length < 10) {
            logger.LogError("Retrospective entry validation failed: reason too short ({ReasonLength} characters)", reason.Length);
            throw new Exceptions.ValidationException("Reason must be at least 10 characters");
        }
        var zurichNow = await timeService.GetCurrentZurichTimeAsync();
        var utcNow = zurichNow.UtcDateTime;
        if (timestamp > utcNow) {
            logger.LogError("Retrospective entry validation failed: timestamp {Timestamp} is in the future", timestamp);
            throw new Exceptions.ValidationException("Cannot add entries for future dates");
        }
        if (timestamp < utcNow.AddDays(-30)) {
            logger.LogError("Retrospective entry validation failed: timestamp {Timestamp} is older than 30 days", timestamp);
            throw new Exceptions.ValidationException("Cannot add entries older than 30 days");
        }
        return utcNow;
    }

    private static readonly TimeZoneInfo ZurichTz =
       TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
}
