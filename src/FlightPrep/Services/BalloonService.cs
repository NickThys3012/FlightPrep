using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

/// <summary>
///     Encapsulates all EF Core read/write operations for <see cref="Balloon" /> entities.
///     Pages must not inject <see cref="IDbContextFactory{AppDbContext}" /> directly;
///     all persistence flows through this service.
/// </summary>
public class BalloonService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<BalloonService> logger) : IBalloonService
{
    /// <inheritdoc />
    public async Task<List<Balloon>> GetAllAsync(string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Balloons.AsQueryable();
        if (!isAdmin)
        {
            if (userId == null)
            {
                return [];
            }

            query = query.Where(b => b.OwnerId == userId);
        }

        return await query.AsNoTracking().OrderBy(b => b.Registration).ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Balloon editBalloon, string? userId, bool isAdmin)
    {
        ArgumentNullException.ThrowIfNull(editBalloon);

        await using var db = await dbFactory.CreateDbContextAsync();
        var b = await db.Balloons.FindAsync(editBalloon.Id);
        if (b is null || (!isAdmin && b.OwnerId != userId))
        {
            logger.LogDebug("UpdateAsync: balloon {Id} not found or access denied for user {UserId}.", editBalloon.Id, userId);
            return;
        }

        var originalOwnerId = b.OwnerId;
        db.Entry(b).CurrentValues.SetValues(editBalloon);
        b.OwnerId = originalOwnerId;
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task AddAsync(Balloon newBalloon, string? userId)
    {
        ArgumentNullException.ThrowIfNull(newBalloon);
        if (userId is null) return;   // [Authorize] prevents this in practice, but guard explicitly

        newBalloon.OwnerId = userId;
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Balloons.Add(newBalloon);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var b = await db.Balloons.FindAsync(id);
        if (b is null || (!isAdmin && b.OwnerId != userId))
        {
            logger.LogDebug("DeleteAsync: balloon {Id} not found or access denied for user {UserId}.", id, userId);
            return;
        }

        db.Balloons.Remove(b);
        await db.SaveChangesAsync();
    }
}
