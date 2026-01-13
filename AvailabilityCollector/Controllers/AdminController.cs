using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Route("dashboard")]
    [Route("")]
    public IActionResult Dashboard()
    {
        return View();
    }

    [Route("razpolozljivosti")]
    public IActionResult Razpolozljivosti()
    {
        return View();
    }

    [Route("razpolozljivosti/history")]
    public async Task<IActionResult> History()
    {
        var months = await _context.AvailabilityMonths
            .OrderByDescending(m => m.MonthKey)
            .ToListAsync();
        
        return View(months);
    }

    [Route("razpolozljivosti/prihajajoce")]
    public async Task<IActionResult> Prihajajoce()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);
        var monthAfterNext = currentMonth.AddMonths(2);
        
        var upcomingMonths = await _context.AvailabilityMonths
            .Where(m => 
                DateTime.ParseExact(m.MonthKey, "MM-yyyy", null) >= nextMonth)
            .OrderBy(m => m.MonthKey)
            .ToListAsync();
        
        ViewBag.NextMonthKey = nextMonth.ToString("MM-yyyy");
        ViewBag.MonthAfterNextKey = monthAfterNext.ToString("MM-yyyy");
        
        return View(upcomingMonths);
    }

    [Route("availability")]
    [Route("availability/{monthKey}")]
    public async Task<IActionResult> Availability(string? monthKey)
    {
        // If no monthKey provided, show next month (aktualno)
        if (string.IsNullOrEmpty(monthKey))
        {
            var now = DateTime.UtcNow;
            var nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            monthKey = nextMonth.ToString("MM-yyyy");
        }

        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            return NotFound();
        }

        // Get all active users (employees)
        var employees = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        // Get all submissions for this month
        var submissions = await _context.AvailabilitySubmissions
            .Include(s => s.Entries)
            .Include(s => s.AvailabilityMonth)
            .Where(s => s.AvailabilityMonthId == month.Id)
            .ToListAsync();

        // Get all positions for filter
        var positions = await _context.Positions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.MonthKey = monthKey;
        ViewBag.Month = month;
        ViewBag.Positions = positions;
        ViewBag.EmploymentTypes = Enum.GetValues(typeof(EmploymentType))
            .Cast<EmploymentType>()
            .ToList();

        var viewModel = new AvailabilityTableViewModel
        {
            Employees = employees,
            Submissions = submissions,
            MonthKey = monthKey
        };

        return View(viewModel);
    }

    [Route("analiza/{monthKey}")]
    public IActionResult Analiza(string monthKey)
    {
        ViewBag.MonthKey = monthKey;
        return View();
    }

    [Route("zaposleni")]
    public async Task<IActionResult> Zaposleni()
    {
        var employees = await _userManager.Users
            .OrderBy(u => u.IsActive ? 0 : 1) // Active first
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        var positions = await _context.Positions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.Positions = positions;
        ViewBag.EmploymentTypes = Enum.GetValues(typeof(EmploymentType))
            .Cast<EmploymentType>()
            .ToList();

        return View(employees);
    }

    [Route("nastavitve")]
    public IActionResult Nastavitve()
    {
        return View();
    }

    [Route("nastavitve/matrica")]
    public IActionResult Matrica()
    {
        return View();
    }

    [Route("nastavitve/splosno")]
    public async Task<IActionResult> Splosno()
    {
        var holidays = await _context.Holidays
            .OrderBy(h => h.Year)
            .ThenBy(h => h.Date)
            .ToListAsync();

        return View(holidays);
    }

    [Route("obvescanje")]
    public IActionResult Obvescanje()
    {
        return View();
    }

    [HttpPost]
    [Route("unlock-month")]
    public async Task<IActionResult> UnlockMonth(string monthKey, bool sendNotification = false)
    {
        if (string.IsNullOrEmpty(monthKey) || !System.Text.RegularExpressions.Regex.IsMatch(monthKey, @"^\d{2}-\d{4}$"))
        {
            return BadRequest("Invalid monthKey format");
        }

        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        if (month == null)
        {
            month = new AvailabilityMonth
            {
                MonthKey = monthKey,
                IsUnlocked = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.AvailabilityMonths.Add(month);
        }
        else
        {
            month.IsUnlocked = true;
        }

        await _context.SaveChangesAsync();

        // TODO: Send notification if sendNotification is true
        if (sendNotification)
        {
            // Implementation for in-app notifications will come later
        }

        TempData["SuccessMessage"] = $"Mesec {monthKey} je bil uspešno odklenjen.";
        return RedirectToAction("Prihajajoce");
    }

    [HttpPost]
    [Route("generate-password")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GeneratePassword([FromBody] GeneratePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { error = "User not found" });
        }

        // Generate random password
        var password = GenerateRandomPassword();
        
        // Remove old password and set new one
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, password);
        
        if (result.Succeeded)
        {
            return Json(new { password = password });
        }
        
        return Json(new { error = string.Join(", ", result.Errors.Select(e => e.Description)) });
    }

    [HttpPost]
    [Route("add-holiday")]
    public async Task<IActionResult> AddHoliday(string name, DateOnly date)
    {
        var holiday = new Holiday
        {
            Name = name,
            Date = date,
            Year = date.Year,
            CreatedAtUtc = DateTime.UtcNow
        };
        
        _context.Holidays.Add(holiday);
        await _context.SaveChangesAsync();
        
        TempData["SuccessMessage"] = $"Praznik {name} je bil dodan.";
        return RedirectToAction("Splosno");
    }

    private string GenerateRandomPassword(int length = 12)
    {
        const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(validChars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class GeneratePasswordRequest
{
    public string UserId { get; set; } = default!;
}

// ViewModel for Availability table
public class AvailabilityTableViewModel
{
    public List<ApplicationUser> Employees { get; set; } = new();
    public List<AvailabilitySubmission> Submissions { get; set; } = new();
    public string MonthKey { get; set; } = default!;
}
