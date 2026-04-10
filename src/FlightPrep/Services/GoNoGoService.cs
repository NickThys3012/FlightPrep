using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

public class GoNoGoService(IDbContextFactory<AppDbContext> dbFactory) : IGoNoGoService
{
    public async Task<GoNoGoSettings> GetSettingsAsync(string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GoNoGoSettings.AsNoTracking().FirstOrDefaultAsync(g => g.UserId == userId)
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
            // Update each threshold individually — SetValues would try to overwrite the
            // tracked PK (existing.Id) with s.Id which EF Core forbids for key properties.
            existing.WindYellowKt  = s.WindYellowKt;
            existing.WindRedKt     = s.WindRedKt;
            existing.VisYellowKm   = s.VisYellowKm;
            existing.VisRedKm      = s.VisRedKm;
            existing.CapeYellowJkg = s.CapeYellowJkg;
            existing.CapeRedJkg    = s.CapeRedJkg;
            // existing.Id and existing.UserId are intentionally unchanged
        }
        else
        {
            db.GoNoGoSettings.Add(s);
        }
        await db.SaveChangesAsync();
    }

    public string Compute(FlightPreparation fp, GoNoGoSettings s) => Compute(fp.SurfaceWindSpeedKt, fp.ZichtbaarheidKm, fp.CapeJkg, s);

    /// <summary>
    ///     Compute overload that accepts raw weather values — used by the list page
    ///     with <see cref="FlightPreparationSummary" /> rows.
    /// </summary>
    public string Compute(double? windKt, double? visKm, double? capeJkg, GoNoGoSettings s)
    {
        var hasData = windKt.HasValue || visKm.HasValue || capeJkg.HasValue;
        if (!hasData)
        {
            return "unknown";
        }

        var red =
            windKt >= s.WindRedKt ||
            (visKm.HasValue && visKm < s.VisRedKm) ||
            capeJkg >= s.CapeRedJkg;

        var yellow =
            windKt >= s.WindYellowKt ||
            (visKm.HasValue && visKm < s.VisYellowKm) ||
            capeJkg >= s.CapeYellowJkg;

        return red ? "red" : yellow ? "yellow" : "green";
    }
}
