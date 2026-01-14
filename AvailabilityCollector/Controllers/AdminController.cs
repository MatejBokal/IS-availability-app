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

    [Route("razpolozljivosti/zgodovina")]
    public async Task<IActionResult> History()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var currentMonthKey = currentMonth.ToString("MM-yyyy");
        
        // Load all months first, then filter in memory (can't use ParseExact in EF query)
        var allMonths = await _context.AvailabilityMonths.ToListAsync();
        
        var pastAndCurrentMonths = allMonths
            .Where(m =>
            {
                try
                {
                    var monthDate = DateTime.ParseExact(m.MonthKey, "MM-yyyy", null);
                    // Include current month and past months only
                    return monthDate <= currentMonth;
                }
                catch
                {
                    return false; // Skip invalid month keys
                }
            })
            .OrderByDescending(m => m.MonthKey)
            .ToList();
        
        return View(pastAndCurrentMonths);
    }

    [Route("razpolozljivosti/prihajajoce")]
    public async Task<IActionResult> Prihajajoce()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);
        var monthAfterNext = currentMonth.AddMonths(2);
        
        // Load all months first, then filter in memory (can't use ParseExact in EF query)
        var allMonths = await _context.AvailabilityMonths.ToListAsync();
        
        // Find the last unlocked month
        var lastUnlockedMonth = allMonths
            .Where(m => m.IsUnlocked)
            .Select(m =>
            {
                try
                {
                    return new { Month = m, Date = DateTime.ParseExact(m.MonthKey, "MM-yyyy", null) };
                }
                catch
                {
                    return null;
                }
            })
            .Where(x => x != null)
            .OrderByDescending(x => x!.Date)
            .FirstOrDefault();
        
        // Calculate the next month to unlock (after the last unlocked month, or next month if none unlocked)
        DateTime nextMonthToUnlock;
        if (lastUnlockedMonth != null)
        {
            nextMonthToUnlock = lastUnlockedMonth.Date.AddMonths(1);
        }
        else
        {
            nextMonthToUnlock = nextMonth;
        }
        
        var upcomingMonths = allMonths
            .Where(m =>
            {
                try
                {
                    var monthDate = DateTime.ParseExact(m.MonthKey, "MM-yyyy", null);
                    return monthDate >= nextMonth;
                }
                catch
                {
                    return false; // Skip invalid month keys
                }
            })
            .OrderBy(m => m.MonthKey)
            .ToList();
        
        ViewBag.NextMonthKey = nextMonth.ToString("MM-yyyy");
        ViewBag.MonthAfterNextKey = monthAfterNext.ToString("MM-yyyy");
        ViewBag.NextMonthToUnlock = nextMonthToUnlock.ToString("MM-yyyy");
        ViewBag.HasNextMonthToUnlock = !allMonths.Any(m => m.MonthKey == nextMonthToUnlock.ToString("MM-yyyy") && m.IsUnlocked);
        
        return View(upcomingMonths);
    }

    [HttpGet]
    [Route("aktualno")]
    [Route("aktualno/{monthKey}")]
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

        // If month doesn't exist, create it (unlocked by default for viewing)
        if (month == null)
        {
            month = new AvailabilityMonth
            {
                MonthKey = monthKey,
                IsUnlocked = false, // Not unlocked yet, but we can still view it
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.AvailabilityMonths.Add(month);
            await _context.SaveChangesAsync();
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
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Get holidays for this month's year
        var monthParts = monthKey.Split('-');
        var year = int.Parse(monthParts[1]);
        var holidays = await _context.Holidays
            .Where(h => h.Year == year)
            .ToListAsync();

        // Get filter parameters
        var selectedPosition = Request.Query["position"].ToString();
        var selectedEmploymentType = Request.Query["employmentType"].ToString();

        ViewBag.MonthKey = monthKey;
        ViewBag.Month = month;
        ViewBag.Positions = positions;
        ViewBag.Holidays = holidays;
        ViewBag.SelectedPositionFilter = selectedPosition;
        ViewBag.SelectedEmploymentTypeFilter = selectedEmploymentType;
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
    public async Task<IActionResult> Matrica()
    {
        // Get or create the single shift matrix
        var matrix = await _context.ShiftMatrices
            .Include(m => m.PositionShifts)
                .ThenInclude(ps => ps.Position)
            .Include(m => m.PositionShifts)
                .ThenInclude(ps => ps.ShiftEntries)
            .FirstOrDefaultAsync();

        if (matrix == null)
        {
            matrix = new ShiftMatrix();
            _context.ShiftMatrices.Add(matrix);
            await _context.SaveChangesAsync();
        }

        // Get all positions for adding new ones
        var allPositions = await _context.Positions
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Get positions already in matrix
        var usedPositionIds = matrix.PositionShifts.Select(ps => ps.PositionId).ToHashSet();
        var availablePositions = allPositions.Where(p => !usedPositionIds.Contains(p.Id)).ToList();

        ViewBag.AvailablePositions = availablePositions;
        ViewBag.AllPositions = allPositions;

        return View(matrix);
    }

    [HttpPost]
    [Route("matrica/save")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveMatrica([FromBody] SaveMatricaRequest request)
    {
        var matrix = await _context.ShiftMatrices
            .Include(m => m.PositionShifts)
                .ThenInclude(ps => ps.ShiftEntries)
            .FirstOrDefaultAsync();

        if (matrix == null)
        {
            matrix = new ShiftMatrix();
            _context.ShiftMatrices.Add(matrix);
        }

        // Clear existing data
        _context.ShiftEntries.RemoveRange(matrix.PositionShifts.SelectMany(ps => ps.ShiftEntries));
        _context.PositionShifts.RemoveRange(matrix.PositionShifts);

        // Rebuild from request
        foreach (var posData in request.Positions)
        {
            var positionShift = new PositionShift
            {
                ShiftMatrixId = matrix.Id,
                PositionId = posData.PositionId,
                ShiftEntries = posData.ShiftEntries.Select(e => new ShiftEntry
                {
                    Days = e.Days,
                    ShiftTime = e.ShiftTime,
                    NumberOfPeople = e.NumberOfPeople
                }).ToList()
            };
            matrix.PositionShifts.Add(positionShift);
        }

        matrix.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Matrica je bila uspešno shranjena." });
    }

    [HttpPost]
    [Route("matrica/add-position")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddPositionToMatrica([FromBody] AddPositionToMatricaRequest request)
    {
        var matrix = await _context.ShiftMatrices
            .Include(m => m.PositionShifts)
            .FirstOrDefaultAsync();

        if (matrix == null)
        {
            matrix = new ShiftMatrix();
            _context.ShiftMatrices.Add(matrix);
            await _context.SaveChangesAsync();
        }

        // Check if position already exists
        if (matrix.PositionShifts.Any(ps => ps.PositionId == request.PositionId))
        {
            return Json(new { success = false, error = "Pozicija je že v matriki." });
        }

        var position = await _context.Positions.FindAsync(request.PositionId);
        if (position == null)
        {
            return Json(new { success = false, error = "Pozicija ni najdena." });
        }

        var positionShift = new PositionShift
        {
            ShiftMatrixId = matrix.Id,
            PositionId = request.PositionId,
            ShiftEntries = new List<ShiftEntry>()
        };

        matrix.PositionShifts.Add(positionShift);
        matrix.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Pozicija je bila dodana.", positionShiftId = positionShift.Id, positionName = position.Name });
    }

    [HttpPost]
    [Route("matrica/delete-position")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeletePositionFromMatrica([FromBody] DeletePositionFromMatricaRequest request)
    {
        var positionShift = await _context.PositionShifts
            .Include(ps => ps.ShiftEntries)
            .FirstOrDefaultAsync(ps => ps.Id == request.PositionShiftId);

        if (positionShift == null)
        {
            return Json(new { success = false, error = "Pozicija ni najdena." });
        }

        _context.ShiftEntries.RemoveRange(positionShift.ShiftEntries);
        _context.PositionShifts.Remove(positionShift);

        var matrix = await _context.ShiftMatrices.FindAsync(positionShift.ShiftMatrixId);
        if (matrix != null)
        {
            matrix.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Pozicija je bila izbrisana." });
    }

    [Route("nastavitve/splosno")]
    public async Task<IActionResult> Splosno()
    {
        var holidays = await _context.Holidays
            .OrderBy(h => h.Year)
            .ThenBy(h => h.Date)
            .ToListAsync();

        // Get minimum time range setting (default to 4 hours)
        var minTimeRangeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "MinTimeRangeHours");
        
        var minTimeRangeHours = 4.0; // Default
        if (minTimeRangeSetting != null && double.TryParse(minTimeRangeSetting.Value, out var parsed))
        {
            minTimeRangeHours = parsed;
        }

        // Get auto-lock day of month setting (default to 1 = lock on month start)
        var autoLockDaySetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AutoLockDayOfMonth");
        
        var autoLockDay = 1; // Default: lock on month start (day 1 of previous month)
        if (autoLockDaySetting != null && int.TryParse(autoLockDaySetting.Value, out var day))
        {
            autoLockDay = day;
        }

        // Get lock after initial submission setting (default to false)
        var lockAfterSubmissionSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "LockAfterInitialSubmission");
        
        var lockAfterSubmission = false; // Default: unlocked
        if (lockAfterSubmissionSetting != null && bool.TryParse(lockAfterSubmissionSetting.Value, out var lockSetting))
        {
            lockAfterSubmission = lockSetting;
        }

        // Get admin notifications setting (default to false)
        var adminNotificationsSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "EnableAdminNotifications");
        
        var enableAdminNotifications = false; // Default: disabled
        if (adminNotificationsSetting != null && bool.TryParse(adminNotificationsSetting.Value, out var adminNotifSetting))
        {
            enableAdminNotifications = adminNotifSetting;
        }

        ViewBag.MinTimeRangeHours = minTimeRangeHours;
        ViewBag.AutoLockDayOfMonth = autoLockDay;
        ViewBag.LockAfterInitialSubmission = lockAfterSubmission;
        ViewBag.EnableAdminNotifications = enableAdminNotifications;
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

        // Get auto-lock day of month setting (default to 1 = lock on month start)
        var autoLockDaySetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AutoLockDayOfMonth");
        
        var autoLockDay = 1; // Default: lock on month start (day 1 of previous month)
        if (autoLockDaySetting != null && int.TryParse(autoLockDaySetting.Value, out var day))
        {
            autoLockDay = day;
        }

        // Calculate lock date based on setting
        var monthParts = monthKey.Split('-');
        var year = int.Parse(monthParts[1]);
        var monthNum = int.Parse(monthParts[0]);
        var previousMonth = new DateTime(year, monthNum, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        
        // Use the specified day of the previous month, but ensure it's valid (handle months with fewer days)
        var daysInPreviousMonth = DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month);
        var lockDay = Math.Min(autoLockDay, daysInPreviousMonth);
        var lockDateTime = new DateTime(previousMonth.Year, previousMonth.Month, lockDay, 23, 59, 59, DateTimeKind.Utc);

        if (month == null)
        {
            month = new AvailabilityMonth
            {
                MonthKey = monthKey,
                IsUnlocked = true,
                LockDateTimeUtc = lockDateTime,
                CreatedAtUtc = DateTime.UtcNow
            };
            _context.AvailabilityMonths.Add(month);
        }
        else
        {
            month.IsUnlocked = true;
            month.LockDateTimeUtc = lockDateTime;
        }

        await _context.SaveChangesAsync();

        // Send notifications if enabled
        if (sendNotification)
        {
            // Check if admin notifications are enabled
            var adminNotificationsSetting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "EnableAdminNotifications");
            
            var enableAdminNotifications = false;
            if (adminNotificationsSetting != null && bool.TryParse(adminNotificationsSetting.Value, out var adminNotifSetting))
            {
                enableAdminNotifications = adminNotifSetting;
            }

            if (enableAdminNotifications)
            {
                // Get all admin users
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                // TODO: Send in-app notifications to admin users
                // Implementation for in-app notifications will come later
            }

            // Get all workers with notifications enabled
            var workerUsers = await _userManager.GetUsersInRoleAsync("Worker");
            var workersWithNotifications = workerUsers
                .Where(u => u.EnableNotifications && u.IsActive)
                .ToList();
            
            // TODO: Send in-app notifications to workers
            // Implementation for in-app notifications will come later
        }

        TempData["SuccessMessage"] = $"Mesec {monthKey} je bil uspešno odklenjen. Zaklenjen bo {lockDateTime:dd.MM.yyyy HH:mm} UTC.";
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
    [Route("delete-holiday")]
    [IgnoreAntiforgeryToken] // For AJAX call
    public async Task<IActionResult> DeleteHoliday([FromBody] int id)
    {
        var holiday = await _context.Holidays.FindAsync(id);
        if (holiday == null)
        {
            return Json(new { success = false, message = "Praznik ni najden." });
        }

        _context.Holidays.Remove(holiday);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Praznik je bil uspešno izbrisan." });
    }

    [HttpPost]
    [Route("save-settings")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveSettings([FromBody] SaveSettingsRequest request)
    {
        // Save MinTimeRangeHours
        var minTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "MinTimeRangeHours");
        
        if (minTimeSetting == null)
        {
            minTimeSetting = new AppSettings { Key = "MinTimeRangeHours" };
            _context.AppSettings.Add(minTimeSetting);
        }
        
        var minTimeRangeHours = 4.0; // Default: 4 hours
        if (request.MinTimeRangeHours.HasValue)
        {
            minTimeRangeHours = request.MinTimeRangeHours.Value;
        }
        minTimeSetting.Value = minTimeRangeHours.ToString();
        minTimeSetting.UpdatedAtUtc = DateTime.UtcNow;

        // Save AutoLockDayOfMonth
        var autoLockDaySetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AutoLockDayOfMonth");
        
        if (autoLockDaySetting == null)
        {
            autoLockDaySetting = new AppSettings { Key = "AutoLockDayOfMonth" };
            _context.AppSettings.Add(autoLockDaySetting);
        }
        
        var autoLockDay = 1; // Default: lock on month start (day 1 of previous month)
        if (request.AutoLockDayOfMonth.HasValue)
        {
            autoLockDay = Math.Max(1, Math.Min(31, request.AutoLockDayOfMonth.Value)); // Clamp between 1 and 31
        }
        autoLockDaySetting.Value = autoLockDay.ToString();
        autoLockDaySetting.UpdatedAtUtc = DateTime.UtcNow;

        // Save LockAfterInitialSubmission
        var lockAfterSubmissionSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "LockAfterInitialSubmission");
        
        if (lockAfterSubmissionSetting == null)
        {
            lockAfterSubmissionSetting = new AppSettings { Key = "LockAfterInitialSubmission" };
            _context.AppSettings.Add(lockAfterSubmissionSetting);
        }
        
        var lockAfterSubmission = request.LockAfterInitialSubmission ?? false;
        lockAfterSubmissionSetting.Value = lockAfterSubmission.ToString();
        lockAfterSubmissionSetting.UpdatedAtUtc = DateTime.UtcNow;

        // Save EnableAdminNotifications
        var adminNotificationsSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "EnableAdminNotifications");
        
        if (adminNotificationsSetting == null)
        {
            adminNotificationsSetting = new AppSettings { Key = "EnableAdminNotifications" };
            _context.AppSettings.Add(adminNotificationsSetting);
        }
        
        var enableAdminNotifications = request.EnableAdminNotifications ?? false;
        adminNotificationsSetting.Value = enableAdminNotifications.ToString();
        adminNotificationsSetting.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Nastavitve so bile uspešno shranjene." });
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
    [Route("add-position")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddPosition([FromBody] AddPositionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Json(new { success = false, error = "Ime pozicije je obvezno." });
        }

        // Check if position already exists
        var existingPosition = await _context.Positions
            .FirstOrDefaultAsync(p => p.Name.ToLower() == request.Name.Trim().ToLower());

        if (existingPosition != null)
        {
            // If it exists but is inactive, reactivate it
            if (!existingPosition.IsActive)
            {
                existingPosition.IsActive = true;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true, message = "Pozicija že obstaja.", positionId = existingPosition.Id });
        }

        // Create new position
        var position = new Position
        {
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Positions.Add(position);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Pozicija je bila uspešno dodana.", positionId = position.Id, positionName = position.Name });
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

    [HttpPost]
    [Route("migrate-positions")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MigratePositions()
    {
        // Get all unique positions from employees (both primary and secondary)
        var allUsers = await _userManager.Users.ToListAsync();
        var positionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in allUsers)
        {
            // Add primary position
            if (!string.IsNullOrWhiteSpace(user.Position))
            {
                positionNames.Add(user.Position.Trim());
            }

            // Add secondary positions (comma-separated)
            if (!string.IsNullOrWhiteSpace(user.SecondaryPositions))
            {
                var secondaryPositions = user.SecondaryPositions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p));

                foreach (var pos in secondaryPositions)
                {
                    positionNames.Add(pos);
                }
            }
        }

        // Get existing positions from database
        var existingPositions = await _context.Positions
            .Select(p => p.Name.ToLower())
            .ToListAsync();

        // Add new positions that don't exist
        int addedCount = 0;
        foreach (var positionName in positionNames)
        {
            if (!existingPositions.Contains(positionName.ToLower()))
            {
                var position = new Position
                {
                    Name = positionName,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _context.Positions.Add(position);
                addedCount++;
            }
        }

        await _context.SaveChangesAsync();

        return Json(new { 
            success = true, 
            message = $"Migracija končana. Dodanih {addedCount} novih pozicij iz {positionNames.Count} unikatnih pozicij zaposlenih." 
        });
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

public class AddPositionRequest
{
    public string Name { get; set; } = default!;
}

public class SaveMatricaRequest
{
    public List<PositionData> Positions { get; set; } = new();
}

public class PositionData
{
    public int PositionId { get; set; }
    public List<ShiftEntryData> ShiftEntries { get; set; } = new();
}

public class ShiftEntryData
{
    public string Days { get; set; } = default!;
    public string ShiftTime { get; set; } = default!;
    public int NumberOfPeople { get; set; }
}

public class AddPositionToMatricaRequest
{
    public int PositionId { get; set; }
}

    public class DeletePositionFromMatricaRequest
    {
        public int PositionShiftId { get; set; }
    }

    public class SaveSettingsRequest
    {
        public double? MinTimeRangeHours { get; set; }
        public int? AutoLockDayOfMonth { get; set; }
        public bool? LockAfterInitialSubmission { get; set; }
        public bool? EnableAdminNotifications { get; set; }
    }

// ViewModel for Availability table
public class AvailabilityTableViewModel
{
    public List<ApplicationUser> Employees { get; set; } = new();
    public List<AvailabilitySubmission> Submissions { get; set; } = new();
    public string MonthKey { get; set; } = default!;
}
