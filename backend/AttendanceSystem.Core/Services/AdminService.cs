using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Core.Services;

public class AdminService(
    IAttendanceRepository repository,
    IAuthService authService,
    IAttendanceAnalysisService analysisService,
    ILogger<AdminService> logger) : IAdminService
{
    public async Task<List<PendingEntry>> GetPendingApprovalsAsync()
    {
        var pending = await repository.GetAllPendingAsync();
        var result = new List<PendingEntry>();

        foreach (var e in pending)
        {
            var user = await authService.GetUserByIdAsync(e.UserId);
            result.Add(new PendingEntry(
                e.Id,
                user?.Username ?? "Unknown",
                e.EventType,
                e.Timestamp,
                e.RetrospectiveReason ?? "",
                e.SubmittedAt ?? e.Timestamp));
        }
        return result;
    }

    public async Task<ApprovalResult> ApproveAsync(int eventId, int adminUserId)
    {
        var evt = await repository.GetByIdAsync(eventId)
            ?? throw new NotFoundException("Event not found");

        if (evt.ApprovalStatus != "Pending")
            throw new ConflictException("Approving this entry creates a logical conflict");

        var dayEvents = await repository.GetUserEventsForDayAsync(evt.UserId, evt.Timestamp.Date);
        var approvedOthers = dayEvents
            .Where(e => e.Id != eventId && (e.ApprovalStatus == "Approved" || !e.IsRetrospective))
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (evt.EventType == "ClockIn")
        {
            var lastApproved = approvedOthers.LastOrDefault();
            if (lastApproved?.EventType == "ClockIn")
                throw new ConflictException("Approving this entry creates a logical conflict");
        }
        else if (evt.EventType == "ClockOut")
        {
            var lastClockIn = approvedOthers.LastOrDefault(e => e.EventType == "ClockIn");
            if (lastClockIn == null || evt.Timestamp <= lastClockIn.Timestamp)
                throw new ConflictException("Approving this entry creates a logical conflict");
        }

        evt.ApprovalStatus = "Approved";
        evt.ApprovedByUserId = adminUserId;
        evt.ApprovedAt = DateTime.UtcNow;
        await repository.UpdateAsync(evt);

        logger.LogInformation("Admin {AdminId} approved event {EventId}", adminUserId, eventId);
        return new ApprovalResult(evt.Id, evt.ApprovalStatus, evt.ApprovedAt);
    }

    public async Task<RejectionResult> RejectAsync(int eventId, string rejectionReason)
    {
        var evt = await repository.GetByIdAsync(eventId)
            ?? throw new NotFoundException("Event not found");

        evt.ApprovalStatus = "Rejected";
        evt.RejectionReason = rejectionReason;
        await repository.UpdateAsync(evt);

        logger.LogInformation("Event {EventId} rejected", eventId);
        return new RejectionResult(evt.Id, evt.ApprovalStatus);
    }

    public async Task<List<EmployeeReport>> GetReportsAsync(DateTime? from, DateTime? to, int? userId)
    {
        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;
        var events = await repository.GetAllUsersEventsAsync(fromDate, toDate, userId);
        var users = await authService.GetAllUsersAsync();

        var reports = new List<EmployeeReport>();
        foreach (var user in users)
        {
            if (userId.HasValue && user.Id != userId.Value) continue;

            var userEvents = events.Where(e => e.UserId == user.Id)
                .Where(e => !e.IsRetrospective || e.ApprovalStatus == "Approved")
                .OrderBy(e => e.Timestamp)
                .ToList();

            var entries = userEvents.Select(e => new HistoryEntry(
                e.Id, e.EventType, e.Timestamp, e.IsRetrospective,
                e.RetrospectiveReason, e.ApprovalStatus, e.ApprovedAt)).ToList();

            var totalHours = AttendanceCalculator.CalculateWorkedHours(userEvents);
            var anomalies = await analysisService.AnalyzeUserPatternsAsync(userEvents, user);

            reports.Add(new EmployeeReport(user.Id, user.Username, totalHours, entries, anomalies));
        }
        return reports;
    }
}
