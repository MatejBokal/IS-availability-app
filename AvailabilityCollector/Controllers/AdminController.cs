using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using AvailabilityCollector.Data;
using AvailabilityCollector.Models;
using AvailabilityCollector.Services;

namespace AvailabilityCollector.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notificationService;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, NotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
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
    public async Task<IActionResult> Analiza(string monthKey)
    {
        // Validate monthKey format
        if (!System.Text.RegularExpressions.Regex.IsMatch(monthKey, @"^\d{2}-\d{4}$"))
        {
            return NotFound();
        }

        // Parse month
        var monthParts = monthKey.Split('-');
        var monthNum = int.Parse(monthParts[0]);
        var year = int.Parse(monthParts[1]);
        var monthStart = new DateOnly(year, monthNum, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var daysInMonth = monthEnd.Day;

        // Get shift matrix
        var shiftMatrix = await _context.ShiftMatrices
            .Include(m => m.PositionShifts)
                .ThenInclude(ps => ps.Position)
            .Include(m => m.PositionShifts)
                .ThenInclude(ps => ps.ShiftEntries)
            .FirstOrDefaultAsync();

        if (shiftMatrix == null)
        {
            ViewBag.MonthKey = monthKey;
            ViewBag.Error = "Matrica izmen ni nastavljena.";
            return View();
        }

        // Get month
        var month = await _context.AvailabilityMonths
            .FirstOrDefaultAsync(m => m.MonthKey == monthKey);

        // Get all availability submissions for this month
        var submissions = new List<AvailabilitySubmission>();
        if (month != null)
        {
            submissions = await _context.AvailabilitySubmissions
                .Include(s => s.Entries)
                .Where(s => s.AvailabilityMonthId == month.Id)
                .ToListAsync();
        }

        // Get all employees with Worker role
        var allWorkers = await _userManager.GetUsersInRoleAsync("Worker");
        var employees = allWorkers.Where(u => u.IsActive).ToList();

        // Get holidays
        var holidays = await _context.Holidays
            .Where(h => h.Year == year)
            .ToListAsync();
        var holidayDates = holidays.Select(h => h.Date).ToHashSet();

        // Create day name mapping (Slovenian to DayOfWeek)
        var dayNameMap = new Dictionary<string, DayOfWeek>
        {
            { "pon", DayOfWeek.Monday },
            { "tor", DayOfWeek.Tuesday },
            { "sre", DayOfWeek.Wednesday },
            { "cet", DayOfWeek.Thursday },
            { "pet", DayOfWeek.Friday },
            { "sob", DayOfWeek.Saturday },
            { "ned", DayOfWeek.Sunday }
        };

        // Build availability lookup by employee ID and date
        var availabilityByEmployeeAndDate = new Dictionary<string, Dictionary<DateOnly, AvailabilityEntry>>();
        foreach (var submission in submissions)
        {
            if (!availabilityByEmployeeAndDate.ContainsKey(submission.UserId))
            {
                availabilityByEmployeeAndDate[submission.UserId] = new Dictionary<DateOnly, AvailabilityEntry>();
            }
            foreach (var entry in submission.Entries)
            {
                availabilityByEmployeeAndDate[submission.UserId][entry.Date] = entry;
            }
        }

        // Analyze each day
        var uncoveredShifts = new List<UncoveredShiftViewModel>();
        var daysWithInsufficientCoverage = new HashSet<DateOnly>();
        var totalRequiredShifts = 0;
        var totalUncoveredShifts = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, monthNum, day);
            var dayOfWeek = date.DayOfWeek;
            var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
            var isHoliday = holidayDates.Contains(date);

            // Skip weekends and holidays for shift analysis (shifts typically don't run on weekends/holidays)
            // But we'll still check if shifts are defined for these days

            // Find shifts that apply to this day
            var dayNameSlovenian = dayNameMap.FirstOrDefault(kvp => kvp.Value == dayOfWeek).Key ?? "";
            var applicableShifts = shiftMatrix.PositionShifts
                .SelectMany(ps => ps.ShiftEntries
                    .Where(se => se.Days.Split(',').Select(d => d.Trim().ToLower()).Contains(dayNameSlovenian))
                    .Select(se => new { PositionShift = ps, ShiftEntry = se }))
                .ToList();

            totalRequiredShifts += applicableShifts.Count;

            var totalRequiredPeopleForDay = 0;
            var availablePeopleForDay = new HashSet<string>();

            // Analyze each shift
            foreach (var shift in applicableShifts)
            {
                var positionId = shift.PositionShift.PositionId;
                var shiftTimeParts = shift.ShiftEntry.ShiftTime.Split('-');
                var shiftStartTime = TimeOnly.Parse(shiftTimeParts[0].Trim());
                var shiftEndTime = TimeOnly.Parse(shiftTimeParts[1].Trim());
                var requiredPeople = shift.ShiftEntry.NumberOfPeople;

                totalRequiredPeopleForDay += requiredPeople;

                // Find employees who can work this shift
                var positionName = shift.PositionShift.Position.Name;
                var availableEmployees = new List<ApplicationUser>();
                foreach (var employee in employees)
                {
                    // Check if employee's position matches (primary or secondary)
                    var matchesPosition = false;
                    if (employee.Position != null && employee.Position == positionName)
                    {
                        matchesPosition = true;
                    }
                    if (!matchesPosition && !string.IsNullOrEmpty(employee.SecondaryPositions))
                    {
                        var secondaryPositions = employee.SecondaryPositions.Split(',').Select(p => p.Trim());
                        if (secondaryPositions.Contains(positionName))
                        {
                            matchesPosition = true;
                        }
                    }

                    if (!matchesPosition) continue;

                    // Check availability
                    if (availabilityByEmployeeAndDate.TryGetValue(employee.Id, out var employeeAvailability) &&
                        employeeAvailability.TryGetValue(date, out var availabilityEntry))
                    {
                        var canWork = false;
                        if (availabilityEntry.Type == AvailabilityType.FullDay)
                        {
                            canWork = true;
                        }
                        else if (availabilityEntry.Type == AvailabilityType.TimeRange &&
                                 availabilityEntry.StartTime.HasValue && availabilityEntry.EndTime.HasValue)
                        {
                            // Check if time ranges overlap
                            var availStart = availabilityEntry.StartTime.Value;
                            var availEnd = availabilityEntry.EndTime.Value;
                            // Overlap if: availStart < shiftEndTime && availEnd > shiftStartTime
                            if (availStart < shiftEndTime && availEnd > shiftStartTime)
                            {
                                canWork = true;
                            }
                        }

                        if (canWork)
                        {
                            availableEmployees.Add(employee);
                            availablePeopleForDay.Add(employee.Id);
                        }
                    }
                }

                var availableCount = availableEmployees.Count;
                if (availableCount < requiredPeople)
                {
                    totalUncoveredShifts++;
                    uncoveredShifts.Add(new UncoveredShiftViewModel
                    {
                        Date = date,
                        PositionName = shift.PositionShift.Position.Name,
                        ShiftTime = shift.ShiftEntry.ShiftTime,
                        RequiredPeople = requiredPeople,
                        AvailablePeople = availableCount,
                        Gap = requiredPeople - availableCount
                    });
                }
            }

            // Check if total required people > total available people for the day
            if (totalRequiredPeopleForDay > availablePeopleForDay.Count)
            {
                daysWithInsufficientCoverage.Add(date);
            }
        }

        // Analyze regular employees
        var regularEmployees = employees.Where(e => e.EmploymentType == EmploymentType.RednoZaposleni).ToList();
        var expectedDays = daysInMonth;
        // Subtract weekends
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, monthNum, day);
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                expectedDays--;
            }
        }
        // Subtract holidays
        expectedDays -= holidays.Count(h => h.Date >= monthStart && h.Date <= monthEnd);

        var regularEmployeesWithInsufficientAvailability = new List<RegularEmployeeAvailabilityViewModel>();
        foreach (var employee in regularEmployees)
        {
            var availableDays = 0;
            if (availabilityByEmployeeAndDate.TryGetValue(employee.Id, out var employeeAvailability))
            {
                foreach (var entry in employeeAvailability.Values)
                {
                    if (entry.Date >= monthStart && entry.Date <= monthEnd &&
                        (entry.Type == AvailabilityType.FullDay || entry.Type == AvailabilityType.TimeRange))
                    {
                        availableDays++;
                    }
                }
            }

            if (availableDays < expectedDays)
            {
                regularEmployeesWithInsufficientAvailability.Add(new RegularEmployeeAvailabilityViewModel
                {
                    EmployeeId = employee.Id,
                    EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
                    Position = employee.Position ?? "Ni dodeljeno",
                    ExpectedDays = expectedDays,
                    AvailableDays = availableDays,
                    Gap = expectedDays - availableDays
                });
            }
        }

        // Calculate coverage percentage
        var coveragePercentage = totalRequiredShifts > 0
            ? Math.Round((double)(totalRequiredShifts - totalUncoveredShifts) / totalRequiredShifts * 100, 1)
            : 100.0;

        var viewModel = new AnalyticsViewModel
        {
            MonthKey = monthKey,
            TotalRequiredShifts = totalRequiredShifts,
            TotalUncoveredShifts = totalUncoveredShifts,
            CoveragePercentage = coveragePercentage,
            DaysWithInsufficientCoverage = daysWithInsufficientCoverage.OrderBy(d => d).ToList(),
            UncoveredShifts = uncoveredShifts.OrderBy(s => s.Date).ThenBy(s => s.PositionName).ToList(),
            RegularEmployeesWithInsufficientAvailability = regularEmployeesWithInsufficientAvailability.OrderBy(e => e.EmployeeName).ToList()
        };

        ViewBag.MonthKey = monthKey;
        return View(viewModel);
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

        // Get allowed time window settings (default to 07:00 - 23:00)
        var allowedStartTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedStartTime");
        
        var allowedStartTime = "07:00"; // Default
        if (allowedStartTimeSetting != null && !string.IsNullOrEmpty(allowedStartTimeSetting.Value))
        {
            allowedStartTime = allowedStartTimeSetting.Value;
        }

        var allowedEndTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedEndTime");
        
        var allowedEndTime = "23:00"; // Default
        if (allowedEndTimeSetting != null && !string.IsNullOrEmpty(allowedEndTimeSetting.Value))
        {
            allowedEndTime = allowedEndTimeSetting.Value;
        }

        // Get days before lock for reminder setting (default to 5 days)
        var daysBeforeLockSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "DaysBeforeLockForReminder");
        
        var daysBeforeLockForReminder = 5; // Default: 5 days
        if (daysBeforeLockSetting != null && int.TryParse(daysBeforeLockSetting.Value, out var days))
        {
            daysBeforeLockForReminder = days;
        }

        ViewBag.MinTimeRangeHours = minTimeRangeHours;
        ViewBag.AutoLockDayOfMonth = autoLockDay;
        ViewBag.LockAfterInitialSubmission = lockAfterSubmission;
        ViewBag.EnableAdminNotifications = enableAdminNotifications;
        ViewBag.AllowedStartTime = allowedStartTime;
        ViewBag.AllowedEndTime = allowedEndTime;
        ViewBag.DaysBeforeLockForReminder = daysBeforeLockForReminder;
        return View(holidays);
    }

    [Route("obvescanje")]
    public async Task<IActionResult> Obvescanje()
    {
        var positions = await _context.Positions
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
        
        ViewBag.Positions = positions;
        return View();
    }

    [HttpPost]
    [Route("send-notification")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return Json(new { success = false, error = "Naslov in sporočilo sta obvezna." });
        }

        var userIds = new List<string>();

        switch (request.RecipientType)
        {
            case "all":
                var allWorkers = await _userManager.GetUsersInRoleAsync("Worker");
                userIds = allWorkers.Where(u => u.IsActive).Select(u => u.Id).ToList();
                break;

            case "admins":
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                userIds = admins.Select(u => u.Id).ToList();
                break;

            case "position":
                if (string.IsNullOrWhiteSpace(request.Position))
                {
                    return Json(new { success = false, error = "Pozicija je obvezna." });
                }
                var positionUsers = await _userManager.Users
                    .Where(u => u.IsActive && u.Position == request.Position)
                    .Select(u => u.Id)
                    .ToListAsync();
                userIds = positionUsers;
                break;

            case "employmentType":
                if (string.IsNullOrWhiteSpace(request.EmploymentType))
                {
                    return Json(new { success = false, error = "Vrsta zaposlitve je obvezna." });
                }
                if (!Enum.TryParse<EmploymentType>(request.EmploymentType, out var empType))
                {
                    return Json(new { success = false, error = "Neveljavna vrsta zaposlitve." });
                }
                var employmentTypeUsers = await _userManager.Users
                    .Where(u => u.IsActive && u.EmploymentType == empType)
                    .Select(u => u.Id)
                    .ToListAsync();
                userIds = employmentTypeUsers;
                break;

            default:
                return Json(new { success = false, error = "Neveljavna vrsta prejemnikov." });
        }

        if (!userIds.Any())
        {
            return Json(new { success = false, error = "Ni najdenih prejemnikov za izbrane kriterije." });
        }

        await _notificationService.CreateCustomNotificationAsync(
            request.Title,
            request.Body,
            userIds
        );

        return Json(new { success = true, message = $"Obvestilo je bilo poslano {userIds.Count} prejemnikom." });
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

            // Create notifications for workers and optionally admins
            await _notificationService.CreateMonthUnlockedNotificationsAsync(monthKey, enableAdminNotifications);
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

        // Save AllowedStartTime
        var allowedStartTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedStartTime");
        
        if (allowedStartTimeSetting == null)
        {
            allowedStartTimeSetting = new AppSettings { Key = "AllowedStartTime" };
            _context.AppSettings.Add(allowedStartTimeSetting);
        }
        
        var allowedStartTime = request.AllowedStartTime ?? "07:00"; // Default: 07:00
        // Validate time format
        if (TimeOnly.TryParse(allowedStartTime, out _))
        {
            allowedStartTimeSetting.Value = allowedStartTime;
            allowedStartTimeSetting.UpdatedAtUtc = DateTime.UtcNow;
        }

        // Save AllowedEndTime
        var allowedEndTimeSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "AllowedEndTime");
        
        if (allowedEndTimeSetting == null)
        {
            allowedEndTimeSetting = new AppSettings { Key = "AllowedEndTime" };
            _context.AppSettings.Add(allowedEndTimeSetting);
        }
        
        var allowedEndTime = request.AllowedEndTime ?? "23:00"; // Default: 23:00
        // Validate time format
        if (TimeOnly.TryParse(allowedEndTime, out _))
        {
            allowedEndTimeSetting.Value = allowedEndTime;
            allowedEndTimeSetting.UpdatedAtUtc = DateTime.UtcNow;
        }

        // Save DaysBeforeLockForReminder
        var daysBeforeLockSetting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "DaysBeforeLockForReminder");
        
        if (daysBeforeLockSetting == null)
        {
            daysBeforeLockSetting = new AppSettings { Key = "DaysBeforeLockForReminder" };
            _context.AppSettings.Add(daysBeforeLockSetting);
        }
        
        var daysBeforeLock = request.DaysBeforeLockForReminder ?? 5; // Default: 5 days
        daysBeforeLock = Math.Max(1, Math.Min(30, daysBeforeLock)); // Clamp between 1 and 30
        daysBeforeLockSetting.Value = daysBeforeLock.ToString();
        daysBeforeLockSetting.UpdatedAtUtc = DateTime.UtcNow;

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
        public string? AllowedStartTime { get; set; }
        public string? AllowedEndTime { get; set; }
        public int? DaysBeforeLockForReminder { get; set; }
    }

    public class SendNotificationRequest
    {
        public string Title { get; set; } = default!;
        public string Body { get; set; } = default!;
        public string RecipientType { get; set; } = default!; // "all", "admins", "position", "employmentType"
        public string? Position { get; set; }
        public string? EmploymentType { get; set; }
    }

// ViewModel for Availability table
public class AvailabilityTableViewModel
{
    public List<ApplicationUser> Employees { get; set; } = new();
    public List<AvailabilitySubmission> Submissions { get; set; } = new();
    public string MonthKey { get; set; } = default!;
}

// ViewModels for Analytics
public class AnalyticsViewModel
{
    public string MonthKey { get; set; } = default!;
    public int TotalRequiredShifts { get; set; }
    public int TotalUncoveredShifts { get; set; }
    public double CoveragePercentage { get; set; }
    public List<DateOnly> DaysWithInsufficientCoverage { get; set; } = new();
    public List<UncoveredShiftViewModel> UncoveredShifts { get; set; } = new();
    public List<RegularEmployeeAvailabilityViewModel> RegularEmployeesWithInsufficientAvailability { get; set; } = new();
}

public class UncoveredShiftViewModel
{
    public DateOnly Date { get; set; }
    public string PositionName { get; set; } = default!;
    public string ShiftTime { get; set; } = default!;
    public int RequiredPeople { get; set; }
    public int AvailablePeople { get; set; }
    public int Gap { get; set; }
}

public class RegularEmployeeAvailabilityViewModel
{
    public string EmployeeId { get; set; } = default!;
    public string EmployeeName { get; set; } = default!;
    public string Position { get; set; } = default!;
    public int ExpectedDays { get; set; }
    public int AvailableDays { get; set; }
    public int Gap { get; set; }
}
