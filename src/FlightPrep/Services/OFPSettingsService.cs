using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

public class OfpSettingsService(IDbContextFactory<AppDbContext> dbFactory) : IOFPSettingsService
{
    public async Task<OFPSettings> GetSettingsAsync(string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.OfpSettings.FirstOrDefaultAsync(o => o.UserId == userId)
               ?? await db.OfpSettings.FirstOrDefaultAsync(o => o.UserId == null)
               ?? new OFPSettings();
    }

    public async Task SaveSettingsAsync(OFPSettings s, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        s.UserId = userId;
        var existing = await db.OfpSettings
            .FirstOrDefaultAsync(o => o.UserId == userId);
        if (existing != null)
        {
            // Update per-property — SetValues would try to overwrite the tracked PK
            existing.PassengerEquipmentWeightKg = s.PassengerEquipmentWeightKg;
        }
        else
        {
            db.OfpSettings.Add(new OFPSettings
            {
                UserId                     = userId,
                PassengerEquipmentWeightKg = s.PassengerEquipmentWeightKg
            });
        }
        await db.SaveChangesAsync();
    }
}
