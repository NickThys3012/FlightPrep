using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

public class GoNoGoService(IDbContextFactory<AppDbContext> dbFactory) : IGoNoGoService
{
    public async Task<GoNoGoSettings> GetSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GoNoGoSettings.FindAsync(1)
               ?? new GoNoGoSettings();
    }

    public async Task SaveSettingsAsync(GoNoGoSettings s)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GoNoGoSettings.FindAsync(1);
        if (existing == null)
        {
            s.Id = 1;
            db.GoNoGoSettings.Add(s);
        }
        else
        {
            existing.WindYellowKt  = s.WindYellowKt;
            existing.WindRedKt     = s.WindRedKt;
            existing.VisYellowKm   = s.VisYellowKm;
            existing.VisRedKm      = s.VisRedKm;
            existing.CapeYellowJkg = s.CapeYellowJkg;
            existing.CapeRedJkg    = s.CapeRedJkg;
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
