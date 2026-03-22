using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

public class GoNoGoService(IDbContextFactory<AppDbContext> dbFactory)
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
        bool hasData = fp.SurfaceWindSpeedKt.HasValue || fp.ZichtbaarheidKm.HasValue || fp.CapeJkg.HasValue;
        if (!hasData) return "unknown";

        bool red =
            (fp.SurfaceWindSpeedKt >= s.WindRedKt) ||
            (fp.ZichtbaarheidKm.HasValue && fp.ZichtbaarheidKm < s.VisRedKm) ||
            (fp.CapeJkg >= s.CapeRedJkg);

        bool yellow =
            (fp.SurfaceWindSpeedKt >= s.WindYellowKt) ||
            (fp.ZichtbaarheidKm.HasValue && fp.ZichtbaarheidKm < s.VisYellowKm) ||
            (fp.CapeJkg >= s.CapeYellowJkg);

        return red ? "red" : yellow ? "yellow" : "green";
    }
}
