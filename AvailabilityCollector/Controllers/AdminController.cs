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

    [HttpPost]
    [Route("add-employee")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddEmployee([FromBody] AddEmployeeRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Json(new { success = false, error = "E-pošta in geslo sta obvezna." });
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Json(new { success = false, error = "Uporabnik s tem e-poštnim naslovom že obstaja." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Position = request.Position,
            EmploymentType = request.EmploymentType,
            SecondaryPositions = request.SecondaryPositions,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Json(new { success = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        // Add to Worker role by default
        await _userManager.AddToRoleAsync(user, "Worker");

        return Json(new { success = true, message = "Zaposleni je bil uspešno dodan." });
    }

    [HttpPost]
    [Route("update-employee")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateEmployee([FromBody] UpdateEmployeeRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { success = false, error = "Uporabnik ni najden." });
        }

        // Update email if changed (also update UserName since they're the same)
        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            var setEmailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!setEmailResult.Succeeded)
            {
                return Json(new { success = false, error = string.Join(", ", setEmailResult.Errors.Select(e => e.Description)) });
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, request.Email);
            if (!setUserNameResult.Succeeded)
            {
                return Json(new { success = false, error = string.Join(", ", setUserNameResult.Errors.Select(e => e.Description)) });
            }
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Position = request.Position;
        user.EmploymentType = request.EmploymentType;
        user.SecondaryPositions = request.SecondaryPositions;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Json(new { success = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return Json(new { success = true, message = "Zaposleni je bil uspešno posodobljen." });
    }

    [HttpPost]
    [Route("assign-admin-role")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AssignAdminRole([FromBody] RoleRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { success = false, error = "Uporabnik ni najden." });
        }

        if (!await _userManager.IsInRoleAsync(user, "Admin"))
        {
            await _userManager.AddToRoleAsync(user, "Admin");
        }

        return Json(new { success = true, message = "Admin vloga je bila uspešno dodeljena." });
    }

    [HttpPost]
    [Route("remove-admin-role")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RemoveAdminRole([FromBody] RoleRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { success = false, error = "Uporabnik ni najden." });
        }

        // Prevent removing admin role from yourself
        if (user.Id == _userManager.GetUserId(User))
        {
            return Json(new { success = false, error = "Ne morete odstraniti Admin vloge sami sebi." });
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            await _userManager.RemoveFromRoleAsync(user, "Admin");
        }

        return Json(new { success = true, message = "Admin vloga je bila uspešno odstranjena." });
    }

    [HttpPost]
    [Route("set-employee-active")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SetEmployeeActive([FromBody] EmployeeStatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { success = false, error = "Uporabnik ni najden." });
        }

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return Json(new { success = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return Json(new { success = true, message = "Zaposleni je bil aktiviran." });
    }

    [HttpPost]
    [Route("set-employee-inactive")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SetEmployeeInactive([FromBody] EmployeeStatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { success = false, error = "Uporabnik ni najden." });
        }

        // Prevent deactivating yourself
        if (user.Id == _userManager.GetUserId(User))
        {
            return Json(new { success = false, error = "Ne morete deaktivirati samega sebe." });
        }

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return Json(new { success = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return Json(new { success = true, message = "Zaposleni je bil deaktiviran." });
    }

    [HttpPost]
    [Route("delete-employee")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteEmployee([FromBody] EmployeeStatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return Json(new { success = false, error = "Uporabnik ni najden." });
        }

        // Prevent deleting yourself
        if (user.Id == _userManager.GetUserId(User))
        {
            return Json(new { success = false, error = "Ne morete izbrisati samega sebe." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Json(new { success = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return Json(new { success = true, message = "Zaposleni je bil uspešno izbrisan." });
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

public class AddEmployeeRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Position { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public string? SecondaryPositions { get; set; }
}

public class UpdateEmployeeRequest
{
    public string UserId { get; set; } = default!;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Position { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public string? SecondaryPositions { get; set; }
}

public class RoleRequest
{
    public string UserId { get; set; } = default!;
}

public class EmployeeStatusRequest
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
