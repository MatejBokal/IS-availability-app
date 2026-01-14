using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Data;

namespace AvailabilityCollector.Controllers.Api;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
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
}
