using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

/// <summary>
///     Encapsulates all EF Core read/write operations for <see cref="Pilot" /> entities.
///     Pages must not inject <see cref="IDbContextFactory{AppDbContext}" /> directly;
///     all persistence flows through this service.
/// </summary>
public class PilotService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<PilotService> logger) : IPilotService
{
    /// <inheritdoc />
    public async Task<List<Pilot>> GetAllAsync(string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Pilots.AsQueryable();
        if (!isAdmin)
        {
            if (userId == null)
            {
                return [];
            }

            query = query.Where(p => p.OwnerId == userId);
        }

        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Pilot editPilot, string? userId, bool isAdmin)
    {
        ArgumentNullException.ThrowIfNull(editPilot);

        await using var db = await dbFactory.CreateDbContextAsync();
        var p = await db.Pilots.FindAsync(editPilot.Id);
        if (p is null || (!isAdmin && p.OwnerId != userId))
        {
            logger.LogDebug("UpdateAsync: pilot {Id} not found or access denied for user {UserId}.", editPilot.Id, userId);
            return;
        }

        var originalOwnerId = p.OwnerId;
        db.Entry(p).CurrentValues.SetValues(editPilot);
        p.OwnerId = originalOwnerId;
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task AddAsync(Pilot newPilot, string? userId)
    {
        ArgumentNullException.ThrowIfNull(newPilot);

        newPilot.OwnerId = userId;
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Pilots.Add(newPilot);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var p = await db.Pilots.FindAsync(id);
        if (p is null || (!isAdmin && p.OwnerId != userId))
        {
            logger.LogDebug("DeleteAsync: pilot {Id} not found or access denied for user {UserId}.", id, userId);
            return;
        }

        db.Pilots.Remove(p);
        await db.SaveChangesAsync();
    }
}
