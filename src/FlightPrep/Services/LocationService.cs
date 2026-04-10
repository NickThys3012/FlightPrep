using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

/// <summary>
///     Encapsulates all EF Core read/write operations for <see cref="Location" /> entities.
///     Pages must not inject <see cref="IDbContextFactory{AppDbContext}" /> directly;
///     all persistence flows through this service.
/// </summary>
public class LocationService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<LocationService> logger) : ILocationService
{
    /// <inheritdoc />
    public async Task<List<Location>> GetAllAsync(string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Locations.AsQueryable();
        if (!isAdmin)
        {
            if (userId == null)
            {
                return [];
            }

            query = query.Where(l => l.OwnerId == userId);
        }

        return await query.OrderBy(l => l.Name).ToListAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Location editLoc, string? userId, bool isAdmin)
    {
        ArgumentNullException.ThrowIfNull(editLoc);

        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Locations.FindAsync(editLoc.Id);
        if (l is null || (!isAdmin && l.OwnerId != userId))
        {
            logger.LogDebug("UpdateAsync: location {Id} not found or access denied for user {UserId}.", editLoc.Id, userId);
            return;
        }

        var originalOwnerId = l.OwnerId;
        db.Entry(l).CurrentValues.SetValues(editLoc);
        l.OwnerId = originalOwnerId;
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task AddAsync(Location newLoc, string? userId)
    {
        ArgumentNullException.ThrowIfNull(newLoc);
        if (userId is null) return;   // [Authorize] prevents this in practice, but guard explicitly

        newLoc.OwnerId = userId;
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Locations.Add(newLoc);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Locations.FindAsync(id);
        if (l is null || (!isAdmin && l.OwnerId != userId))
        {
            logger.LogDebug("DeleteAsync: location {Id} not found or access denied for user {UserId}.", id, userId);
            return;
        }

        db.Locations.Remove(l);
        await db.SaveChangesAsync();
    }
}
