using Microsoft.AspNetCore.Identity;

namespace AvailabilityCollector.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    public string? Position { get; set; } // Primary position
    public EmploymentType? EmploymentType { get; set; }
    public string? SecondaryPositions { get; set; } // Comma-separated
    public bool IsActive { get; set; } = true;
    public bool EnableNotifications { get; set; } = true; // Default: enabled
}
