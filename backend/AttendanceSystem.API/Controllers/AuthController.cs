using AttendanceSystem.API.DTOs.Auth;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!TimeOnly.TryParse(req.ExpectedShiftStartTime, out var start))
            return BadRequest(new { error = "Invalid shift start time format" });
        if (!TimeOnly.TryParse(req.ExpectedShiftEndTime, out var end))
            return BadRequest(new { error = "Invalid shift end time format" });
        if (end <= start)
            return BadRequest(new { error = "Shift end time must be after start time" });
        if (req.Role != "Employee" && req.Role != "Admin")
            return BadRequest(new { error = "Role must be 'Employee' or 'Admin'" });

        try
        {
            var result = await authService.RegisterAsync(req.Username, req.Password, req.Role, start, end);
            return StatusCode(201, new { result.Id, result.Username, result.Role });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var result = await authService.LoginAsync(req.Username, req.Password);
            return Ok(new
            {
                result.Token,
                result.ExpiresAt,
                User = new { result.UserId, result.Username, result.Role }
            });
        }
        catch (BusinessException)
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }
    }
}
