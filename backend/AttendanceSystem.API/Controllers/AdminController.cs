using System.Security.Claims;
using AttendanceSystem.API.DTOs.Admin;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    private int AdminUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? userId)
    {
        var reports = await adminService.GetReportsAsync(from, to, userId);
        return Ok(new { employees = reports });
    }

    [HttpGet("pending-approvals")]
    public async Task<IActionResult> PendingApprovals()
    {
        var entries = await adminService.GetPendingApprovalsAsync();
        return Ok(new
        {
            pendingCount = entries.Count,
            entries = entries.Select(e => new
            {
                e.Id, e.EmployeeName, e.EventType,
                requestedTimestamp = e.RequestedTimestamp,
                reason = e.Reason,
                submittedAt = e.SubmittedAt
            })
        });
    }

    [HttpPost("approve-retrospective/{eventId:int}")]
    public async Task<IActionResult> Approve(int eventId)
    {
        try
        {
            var result = await adminService.ApproveAsync(eventId, AdminUserId);
            return Ok(new
            {
                eventId = result.EventId,
                approvalStatus = result.ApprovalStatus,
                approvedAt = result.ApprovedAt
            });
        }
        catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("reject-retrospective/{eventId:int}")]
    public async Task<IActionResult> Reject(int eventId, [FromBody] RejectRequest req)
    {
        try
        {
            var result = await adminService.RejectAsync(eventId, req.RejectionReason);
            return Ok(new { eventId = result.EventId, approvalStatus = result.ApprovalStatus });
        }
        catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
