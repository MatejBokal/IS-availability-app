using Microsoft.AspNetCore.Identity;

namespace AvailabilityCollector.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = new[] { "Admin", "Worker" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new IdentityRole(r));
        }

        // Create an admin user if none exists
        var adminEmail = "admin@demo.si";
        var adminPass = "Admin123!ChangeMe";

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var res = await userManager.CreateAsync(admin, adminPass);
            if (res.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
