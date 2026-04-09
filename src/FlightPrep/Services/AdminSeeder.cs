using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        // Seed global default Go/No-Go thresholds (UserId == null = system-wide default)
        // so fresh installs have persisted settings rather than silent in-memory defaults.
        var dbFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.GoNoGoSettings.AnyAsync(g => g.UserId == null))
        {
            db.GoNoGoSettings.Add(new GoNoGoSettings
            {
                UserId        = null,
                WindYellowKt  = 10,
                WindRedKt     = 15,
                VisYellowKm   = 5,
                VisRedKm      = 3,
                CapeYellowJkg = 300,
                CapeRedJkg    = 500,
            });
            await db.SaveChangesAsync();
        }
    }
}
