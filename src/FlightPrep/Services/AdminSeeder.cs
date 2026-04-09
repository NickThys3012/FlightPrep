using FlightPrep.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace FlightPrep.Services;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        foreach (var role in new[] { "Admin", "Pilot" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var username = config["SEED_ADMIN_USERNAME"];
        var password = config["SEED_ADMIN_PASSWORD"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return;
        }

        if (await userManager.FindByNameAsync(username) is null)
        {
            var admin = new ApplicationUser { UserName = username, Email = username, IsApproved = true, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}
