using System.Security.Claims;
using AttendanceSystem.API.DTOs.Admin;
using AttendanceSystem.Infrastructure.ExternalServices;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("admin")]
public class AdminController(IAdminService adminService, ILogger<AdminController> logger) : ControllerBase
{
    private int AdminUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? userId)
    {
        var reports = await adminService.GetReportsAsync(from, to, userId);
        logger.LogInformation("Retrieved reports for user {UserId}", userId);
        return Ok(new { employees = reports });
    }

    [HttpGet("pending-approvals")]
    public async Task<IActionResult> PendingApprovals()
    {
        var entries = await adminService.GetPendingApprovalsAsync();
        logger.LogInformation("Retrieved {Count} pending approvals", entries.Count);
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
        try {
            var result = await adminService.ApproveAsync(eventId, AdminUserId);
            logger.LogInformation("Approved retrospective event {EventId} by admin {AdminUserId}", eventId, AdminUserId);
            return Ok(new
            {
                eventId = result.EventId,
                approvalStatus = result.ApprovalStatus,
                approvedAt = result.ApprovedAt
            });
        }
        catch (NotFoundException ex) {
            logger.LogError(ex, "Attempted to approve non-existent event {EventId}", eventId);
            return NotFound(new { error = ex.Message }); 
        }
        catch (ConflictException ex) {
            logger.LogError(ex, "Conflict occurred while approving event {EventId}", eventId);
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("reject-retrospective/{eventId:int}")]
    public async Task<IActionResult> Reject(int eventId, [FromBody] RejectRequest req)
    {
        try {
            var result = await adminService.RejectAsync(eventId, req.RejectionReason);
            logger.LogInformation("Rejected retrospective event {EventId} with reason: {Reason}", eventId, req.RejectionReason);    
            return Ok(new { eventId = result.EventId, approvalStatus = result.ApprovalStatus });
        }
        catch (NotFoundException ex) {
            logger.LogError(ex, "Attempted to reject non-existent event {EventId}", eventId);
            return NotFound(new { error = ex.Message }); 
        }
    }

    [HttpGet("export-employee-pdf/{userId:int}")]
    public async Task<IActionResult> ExportEmployeePdf(
        int userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try {
            var fromDate = from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var toDate = to ?? DateTime.UtcNow;
            var reports = await adminService.GetReportsAsync(fromDate, toDate, userId);
            var report = reports.FirstOrDefault();
            if (report == null) return NotFound(new { error = "Employee not found" });
            var pdf = PdfReportService.GenerateEmployeeReport(report, fromDate, toDate);
            var safeName = report.Username.Replace(".", "").Replace(" ", "_");
            var filename = $"Employee_{safeName}_{fromDate:yyyy-MM-dd}_to_{toDate:yyyy-MM-dd}.pdf";
            logger.LogInformation("Generating PDF for employee {UserId}", userId);
            return File(pdf, "application/pdf", filename);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to generate PDF for employee {UserId}", userId);
            return StatusCode(500, new { error = "Failed to generate PDF report" });
        }
    }

    [HttpGet("export-all-employees-pdf")]
    public async Task<IActionResult> ExportAllEmployeesPdf(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try {
            var fromDate = from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var toDate = to ?? DateTime.UtcNow;
            var reports = await adminService.GetReportsAsync(fromDate, toDate, null);

            var pdfBytes = PdfReportService.GenerateAllEmployeesReport(reports, fromDate, toDate);
            var filename = $"AllEmployees_Report_{fromDate:yyyy-MM-dd}_to_{toDate:yyyy-MM-dd}.pdf";
            logger.LogInformation("Generating all-employees PDF report for period {From} to {To}", fromDate, toDate);
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to generate all-employees PDF report");
            return StatusCode(500, new { error = "Failed to generate PDF report" });
        }
    }
}
