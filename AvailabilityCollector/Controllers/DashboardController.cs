using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AvailabilityCollector.Controllers;

[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        // View will show different content based on user role
        return View();
    }
}
