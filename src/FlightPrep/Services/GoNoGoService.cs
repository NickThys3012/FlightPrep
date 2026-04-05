using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

public class GoNoGoService(IDbContextFactory<AppDbContext> dbFactory) : IGoNoGoService
{
    public async Task<GoNoGoSettings> GetSettingsAsync(string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GoNoGoSettings.FirstOrDefaultAsync(g => g.UserId == userId)
               ?? new GoNoGoSettings { UserId = userId };
    }

    public async Task SaveSettingsAsync(GoNoGoSettings s, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        s.UserId = userId;
        var existing = await db.GoNoGoSettings
            .FirstOrDefaultAsync(g => g.UserId == userId);
        if (existing != null)
        {
            var originalId     = existing.Id;
            var originalUserId = existing.UserId;
            db.Entry(existing).CurrentValues.SetValues(s);
            existing.Id     = originalId;     // restore PK — SetValues would have overwritten it with s.Id
            existing.UserId = originalUserId; // restore FK — preserve ownership
        }
        else
        {
            db.GoNoGoSettings.Add(s);
        }
        await db.SaveChangesAsync();
    }

    public string Compute(FlightPreparation fp, GoNoGoSettings s)
    {
        return Compute(fp.SurfaceWindSpeedKt, fp.ZichtbaarheidKm, fp.CapeJkg, s);
    }

    /// <summary>
    /// Compute overload that accepts raw weather values — used by the list page
    /// with <see cref="FlightPreparationSummary"/> rows.
    /// </summary>
    public string Compute(double? windKt, double? visKm, double? capeJkg, GoNoGoSettings s)
    {
        bool hasData = windKt.HasValue || visKm.HasValue || capeJkg.HasValue;
        if (!hasData) return "unknown";

        bool red =
            (windKt >= s.WindRedKt) ||
            (visKm.HasValue && visKm < s.VisRedKm) ||
            (capeJkg >= s.CapeRedJkg);

        bool yellow =
            (windKt >= s.WindYellowKt) ||
            (visKm.HasValue && visKm < s.VisYellowKm) ||
            (capeJkg >= s.CapeYellowJkg);

        return red ? "red" : yellow ? "yellow" : "green";
    }
}
