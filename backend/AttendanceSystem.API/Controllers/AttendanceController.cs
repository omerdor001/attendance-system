using System.Security.Claims;
using AttendanceSystem.API.DTOs.Attendance;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController(IAttendanceService attendanceService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn()
    {
        try
        {
            var result = await attendanceService.ClockInAsync(UserId);
            return Ok(new
            {
                eventId = result.EventId,
                zurichTime = result.ZurichTime,
                message = result.Message
            });
        }
        catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (ExternalServiceException ex) { return StatusCode(503, new { error = ex.Message }); }
    }

    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut()
    {
        try
        {
            var result = await attendanceService.ClockOutAsync(UserId);
            return Ok(new
            {
                eventId = result.EventId,
                zurichTime = result.ZurichTime,
                workedHoursToday = result.WorkedHoursToday,
                message = result.Message
            });
        }
        catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (ExternalServiceException ex) { return StatusCode(503, new { error = ex.Message }); }
    }

    [HttpPost("clock-in-retrospective")]
    public async Task<IActionResult> ClockInRetrospective([FromBody] RetrospectiveRequest req)
    {
        try
        {
            var result = await attendanceService.ClockInRetrospectiveAsync(UserId, req.Timestamp, req.Reason);
            return Ok(new
            {
                eventId = result.EventId,
                timestamp = result.Timestamp,
                approvalStatus = result.ApprovalStatus,
                message = result.Message
            });
        }
        catch (Core.Exceptions.ValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("clock-out-retrospective")]
    public async Task<IActionResult> ClockOutRetrospective([FromBody] RetrospectiveRequest req)
    {
        try
        {
            var result = await attendanceService.ClockOutRetrospectiveAsync(UserId, req.Timestamp, req.Reason);
            return Ok(new
            {
                eventId = result.EventId,
                timestamp = result.Timestamp,
                approvalStatus = result.ApprovalStatus,
                message = result.Message
            });
        }
        catch (Core.Exceptions.ValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpGet("heartbeat")]
    public async Task<IActionResult> Heartbeat()
    {
        try
        {
            var result = await attendanceService.GetHeartbeatAsync(UserId);
            return Ok(new
            {
                currentZurichTime = result.CurrentZurichTime,
                isClockedIn = result.IsClockedIn,
                workedHoursToday = result.WorkedHoursToday,
                realtimeAlerts = result.RealtimeAlerts.Select(a => new
                {
                    type = a.Type,
                    message = a.Message,
                    severity = a.Severity
                }),     //See if alerts needed 
                pendingApprovalCount = result.PendingApprovalCount
            });
        }
        catch (ExternalServiceException ex) { return StatusCode(503, new { error = ex.Message }); }
    }

    [HttpGet("my-history")]
    public async Task<IActionResult> MyHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;
        var result = await attendanceService.GetHistoryAsync(UserId, fromDate, toDate);
        return Ok(new
        {
            entries = result.Entries.Select(e => new
            {
                e.Id, e.EventType, e.Timestamp, e.IsRetrospective,
                e.RetrospectiveReason, e.ApprovalStatus, e.ApprovedAt
            }),
            totalWorkedHours = result.TotalWorkedHours,
            anomalies = result.Anomalies    
        });
    }
}
