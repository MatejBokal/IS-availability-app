using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers.Api;

[ApiController]
[Route("api/settings")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // All settings endpoints require authentication
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public SettingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("min-time-range-hours")]
    public async Task<ActionResult<double>> GetMinTimeRangeHours()
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "MinTimeRangeHours");

        if (setting != null && double.TryParse(setting.Value, out var hours))
        {
            return Ok(hours);
        }

        return Ok(4.0); // Default: 4 hours
    }

    [HttpGet("allowed-time-window")]
    public async Task<ActionResult<AllowedTimeWindowResponse>> GetAllowedTimeWindow()
    {
        var startTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedStartTime");
        
        var endTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedEndTime");
        
        var startTime = "07:00"; // Default: 07:00
        var endTime = "23:00"; // Default: 23:00
        
        if (startTimeSetting != null && !string.IsNullOrEmpty(startTimeSetting.Value))
        {
            startTime = startTimeSetting.Value;
        }
        
        if (endTimeSetting != null && !string.IsNullOrEmpty(endTimeSetting.Value))
        {
            endTime = endTimeSetting.Value;
        }

        return Ok(new AllowedTimeWindowResponse
        {
            StartTime = startTime,
            EndTime = endTime
        });
    }

    [HttpGet("lock-after-initial-submission")]
    public async Task<ActionResult<bool>> GetLockAfterInitialSubmission()
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "LockAfterInitialSubmission");

        if (setting != null && bool.TryParse(setting.Value, out var enabled))
        {
            return Ok(enabled);
        }

        return Ok(false); // Default: false
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<bool>> GetNotificationPreference()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(user.EnableNotifications);
    }

    [HttpPost("notifications")]
    public async Task<ActionResult> SetNotificationPreference([FromBody] NotificationPreferenceRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        user.EnableNotifications = request.EnableNotifications;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "Notification preference updated" });
    }

    public class NotificationPreferenceRequest
    {
        public bool EnableNotifications { get; set; }
    }

    public class AllowedTimeWindowResponse
    {
        public string StartTime { get; set; } = default!;
        public string EndTime { get; set; } = default!;
    }
}
