using Microsoft.AspNetCore.Identity;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Data;

public static class SeedData
{
    /// <param name="isDevelopment">When true, resets passwords for seeded users to known dev values so you can always log in locally.</param>
    public static async Task EnsureSeededAsync(IServiceProvider services, bool isDevelopment = false)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = new[] { "Admin", "Worker" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new IdentityRole(r));
        }

        // Demo users (simple local/dev accounts)
        await EnsureUserAsync(userManager, "admin@demo.si", "Admin123!", "Admin", isDevelopment);
        await EnsureUserAsync(userManager, "worker@demo.si", "Worker123!", "Worker", isDevelopment);

        // Same test users as README / production (so local DB has "all the users" with known passwords)
        await EnsureUserAsync(userManager, "matej@bokal.si", "Matej123.", "Admin", isDevelopment);
        await EnsureUserAsync(userManager, "gabrijel@avsec.si", "Gabrijel123.", "Worker", isDevelopment);
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role, bool resetPasswordInDev)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var res = await userManager.CreateAsync(user, password);
            if (res.Succeeded)
                await userManager.AddToRoleAsync(user, role);
        }
        else if (resetPasswordInDev)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, token, password);
        }
    }
}
