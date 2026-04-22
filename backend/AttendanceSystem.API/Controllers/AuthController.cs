using AttendanceSystem.API.DTOs.Auth;
using AttendanceSystem.Core.Exceptions;
using AttendanceSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!TimeOnly.TryParse(req.ExpectedShiftStartTime, out var start)) 
        {
            logger.LogError("Invalid shift start time format: {ShiftStartTime}", req.ExpectedShiftStartTime);
            return BadRequest(new { error = "Invalid shift start time format" });
        }  
        if (!TimeOnly.TryParse(req.ExpectedShiftEndTime, out var end)){
            logger.LogError("Invalid shift end time format: {ShiftEndTime}", req.ExpectedShiftEndTime);
            return BadRequest(new { error = "Invalid shift end time format" });
        }
        if (end <= start) {
            logger.LogError("Shift end time must be after start time: {ShiftStartTime} - {ShiftEndTime}", start, end);
            return BadRequest(new { error = "Shift end time must be after start time" });
        }
        if (req.Role != "Admin") {
            logger.LogError("Role must be Admin: {Role}", req.Role);
            return BadRequest(new { error = "Role must be Admin" });
        }
        try {
            var result = await authService.RegisterAsync(req.Username, req.Password, req.Role, start, end);
            logger.LogInformation("User registered successfully: {Username} (ID: {UserId})", result.Username, result.Id);
            return StatusCode(201, new { result.Id, result.Username, result.Role });
        }
        catch (BusinessException ex) {
           logger.LogError(ex, "Failed to register user: {Username}", req.Username);
           return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try {
            var result = await authService.LoginAsync(req.Username, req.Password);
            logger.LogInformation("User logged in successfully: {Username} (ID: {UserId})", result.Username, result.UserId);
        return Ok(new {
                result.Token,
                result.ExpiresAt,
                User = new { result.UserId, result.Username, result.Role }
            });
        }
        catch (BusinessException){
            logger.LogError("Failed login attempt for username: {Username}", req.Username);
            return Unauthorized(new { error = "Invalid username or password" });
        }
    }
}
