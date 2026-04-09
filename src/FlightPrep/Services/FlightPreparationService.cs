using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Services;

/// <summary>
///     Encapsulates all EF Core read/write operations for <see cref="FlightPreparation" />.
///     Pages must not inject <see cref="IDbContextFactory{AppDbContext}" /> directly;
///     all persistence flows through this service.
/// </summary>
public class FlightPreparationService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<FlightPreparationService> logger) : IFlightPreparationService
{
    // ── Reference data ────────────────────────────────────────────────────────

    /// <summary>Returns all balloons ordered by registration.</summary>
    public async Task<List<Balloon>> GetBalloonsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Balloons.OrderBy(b => b.Registration).ToListAsync();
    }

    /// <summary>Returns all pilots ordered by name.</summary>
    public async Task<List<Pilot>> GetPilotsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Pilots.OrderBy(p => p.Name).ToListAsync();
    }

    /// <summary>Returns all locations ordered by name.</summary>
    public async Task<List<Location>> GetLocationsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Locations.OrderBy(l => l.Name).ToListAsync();
    }

    // ── Flight preparation queries ────────────────────────────────────────────

    /// <summary>
    ///     Returns a lightweight summary list for the FlightList page.
    ///     Includes flights owned by the user and flights shared with the user.
    ///     No heavy navigation collections are loaded.
    /// </summary>
    public async Task<List<FlightPreparationSummary>> GetSummariesAsync(string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.FlightPreparations.AsQueryable();
        if (!isAdmin)
        {
            if (userId == null)
            {
                return [];
            }

            query = query.Where(f =>
                f.CreatedByUserId == userId ||
                f.Shares.Any(s => s.SharedWithUserId == userId));
        }

        // Load all matching flights with their shares so we can determine IsShared and SharedByName.
        // We perform a left join to AspNetUsers to get the owner's UserName for shared preps.
        var flights = await query
            .Include(f => f.Shares)
            .Select(f => new
            {
                f.Id,
                f.Datum,
                f.Tijdstip,
                f.IsFlown,
                BalloonRegistration = f.Balloon != null ? f.Balloon.Registration : null,
                PilotName = f.Pilot != null ? f.Pilot.Name : null,
                LocationName = f.Location != null ? f.Location.Name : null,
                f.SurfaceWindSpeedKt,
                f.ZichtbaarheidKm,
                f.CapeJkg,
                f.CreatedByUserId,
                IsShared = isAdmin ? false : f.CreatedByUserId != userId,
                OwnerUserName = f.CreatedByUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == f.CreatedByUserId).Select(u => u.UserName).FirstOrDefault()
            })
            .ToListAsync();

        return flights
            .Select(f => new FlightPreparationSummary(
                f.Id,
                f.Datum,
                f.Tijdstip,
                f.IsFlown,
                f.BalloonRegistration,
                f.PilotName,
                f.LocationName,
                f.SurfaceWindSpeedKt,
                f.ZichtbaarheidKm,
                f.CapeJkg,
                f.CreatedByUserId)
            {
                IsShared = f.IsShared,
                SharedByName = f.IsShared ? (f.OwnerUserName ?? f.CreatedByUserId) : null
            })
            .ToList();
    }

    /// <summary>
    ///     Returns a single page of flight summaries filtered by <paramref name="statusFilter" />
    ///     (alle / gevlogen / niet-gevlogen / gedeeld) with a total count for pagination controls.
    ///     Filtering and pagination are applied at the database level to avoid loading all rows.
    /// </summary>
    public async Task<(List<FlightPreparationSummary> Items, int Total)> GetSummariesPagedAsync(
        string? userId, bool isAdmin, string statusFilter, int page, int pageSize, bool sortDescending = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.FlightPreparations.AsQueryable();

        if (!isAdmin)
        {
            if (userId == null) return ([], 0);
            query = query.Where(f =>
                f.CreatedByUserId == userId ||
                f.Shares.Any(s => s.SharedWithUserId == userId));
        }

        // Apply status filter at the database level
        query = statusFilter switch
        {
            "gevlogen"      => query.Where(f => f.IsFlown),
            "niet-gevlogen" => query.Where(f => !f.IsFlown),
            "gedeeld"       => query.Where(f => f.CreatedByUserId != userId && f.Shares.Any(s => s.SharedWithUserId == userId)),
            _               => query
        };

        var total = await query.CountAsync();

        var ordered = sortDescending
            ? query.OrderByDescending(f => f.Datum).ThenByDescending(f => f.Tijdstip)
            : query.OrderBy(f => f.Datum).ThenBy(f => f.Tijdstip);

        var flights = await ordered
            .Include(f => f.Shares)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.Id,
                f.Datum,
                f.Tijdstip,
                f.IsFlown,
                BalloonRegistration = f.Balloon != null ? f.Balloon.Registration : null,
                PilotName = f.Pilot != null ? f.Pilot.Name : null,
                LocationName = f.Location != null ? f.Location.Name : null,
                f.SurfaceWindSpeedKt,
                f.ZichtbaarheidKm,
                f.CapeJkg,
                f.CreatedByUserId,
                IsShared = isAdmin ? false : f.CreatedByUserId != userId,
                OwnerUserName = f.CreatedByUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == f.CreatedByUserId).Select(u => u.UserName).FirstOrDefault()
            })
            .ToListAsync();

        var items = flights
            .Select(f => new FlightPreparationSummary(
                f.Id,
                f.Datum,
                f.Tijdstip,
                f.IsFlown,
                f.BalloonRegistration,
                f.PilotName,
                f.LocationName,
                f.SurfaceWindSpeedKt,
                f.ZichtbaarheidKm,
                f.CapeJkg,
                f.CreatedByUserId)
            {
                IsShared = f.IsShared,
                SharedByName = f.IsShared ? (f.OwnerUserName ?? f.CreatedByUserId) : null
            })
            .ToList();

        return (items, total);
    }

    /// <summary>
    ///     Returns the full <see cref="FlightPreparation" /> with all navigation properties
    ///     eagerly loaded, or <c>null</c> if not found.
    /// </summary>
    public async Task<FlightPreparation?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FlightPreparations
            .Include(f => f.Balloon)
            .Include(f => f.Pilot)
            .Include(f => f.Location)
            .Include(f => f.Passengers.OrderBy(p => p.Order))
            .Include(f => f.Images.OrderBy(i => i.Order))
            .Include(f => f.WindLevels.OrderBy(w => w.Order))
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Upserts a flight preparation.
    ///     Handles the replace-passengers / replace-images / replace-wind-levels pattern.
    ///     Navigation properties on <paramref name="fp" /> are preserved across the call.
    /// </summary>
    /// <returns>The persisted flight preparation id.</returns>
    public async Task<int> SaveAsync(FlightPreparation fp)
    {
        ArgumentNullException.ThrowIfNull(fp);

        // Hoist nav props outside try so they are accessible in the final block.
        Balloon? balloon = null;
        Pilot? pilot = null;
        Location? location = null;
        List<Passenger> passengers = [];
        List<FlightImage> images = [];
        List<WindLevel> windLevels = [];

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();

            // Capture navigation props and detach them so EF does not try to upsert them.
            balloon = fp.Balloon;
            fp.Balloon = null;
            pilot = fp.Pilot;
            fp.Pilot = null;
            location = fp.Location;
            fp.Location = null;
            passengers = fp.Passengers.ToList();
            fp.Passengers.Clear();
            images = fp.Images.ToList();
            fp.Images.Clear();
            windLevels = fp.WindLevels.ToList();
            fp.WindLevels.Clear();

            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                if (fp.Id == 0)
                {
                    // ── CREATE ─────────────────────────────────────────────────
                    fp.CreatedAt = fp.UpdatedAt = DateTime.UtcNow;
                    db.FlightPreparations.Add(fp);
                    await db.SaveChangesAsync(); // assigns fp.Id

                    for (var i = 0; i < passengers.Count; i++)
                    {
                        passengers[i].FlightPreparationId = fp.Id;
                        passengers[i].Order = i;
                        passengers[i].Id = 0;
                    }

                    db.Passengers.AddRange(passengers);

                    for (var i = 0; i < images.Count; i++)
                    {
                        images[i].FlightPreparationId = fp.Id;
                        images[i].Id = 0;
                        images[i].Order = i;
                    }

                    db.FlightImages.AddRange(images);

                    for (var i = 0; i < windLevels.Count; i++)
                    {
                        windLevels[i].FlightPreparationId = fp.Id;
                        windLevels[i].Id = 0;
                        windLevels[i].Order = i;
                    }

                    db.WindLevels.AddRange(windLevels);

                    await db.SaveChangesAsync();
                }
                else
                {
                    // ── UPDATE ─────────────────────────────────────────────────
                    fp.UpdatedAt = DateTime.UtcNow;

                    db.Passengers.RemoveRange(
                        await db.Passengers.Where(p => p.FlightPreparationId == fp.Id).ToListAsync());
                    db.FlightImages.RemoveRange(
                        await db.FlightImages.Where(i => i.FlightPreparationId == fp.Id).ToListAsync());
                    db.WindLevels.RemoveRange(
                        await db.WindLevels.Where(w => w.FlightPreparationId == fp.Id).ToListAsync());

                    db.FlightPreparations.Update(fp);

                    for (var i = 0; i < passengers.Count; i++)
                    {
                        passengers[i].FlightPreparationId = fp.Id;
                        passengers[i].Order = i;
                        passengers[i].Id = 0;
                    }

                    db.Passengers.AddRange(passengers);

                    for (var i = 0; i < images.Count; i++)
                    {
                        images[i].FlightPreparationId = fp.Id;
                        images[i].Id = 0;
                        images[i].Order = i;
                    }

                    db.FlightImages.AddRange(images);

                    for (var i = 0; i < windLevels.Count; i++)
                    {
                        windLevels[i].FlightPreparationId = fp.Id;
                        windLevels[i].Id = 0;
                        windLevels[i].Order = i;
                    }

                    db.WindLevels.AddRange(windLevels);

                    await db.SaveChangesAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return fp.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveAsync failed for FlightPreparation Id={Id}", fp.Id);
            throw;
        }
        finally
        {
            // Restore all navigation props whether save succeeded or failed,
            // so the caller's entity is never left in a half-null state.
            fp.Balloon = balloon;
            fp.Pilot = pilot;
            fp.Location = location;
            fp.Passengers = passengers;
            fp.Images = images;
            fp.WindLevels = windLevels;
        }
    }

    /// <summary>Hard-deletes a flight preparation and all cascade-related entities.</summary>
    public async Task DeleteAsync(int id, string userId, bool isAdmin = false)
    {
        ArgumentNullException.ThrowIfNull(userId);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var fp = await db.FlightPreparations.FindAsync(id);
            if (fp == null) return;
            if (fp.CreatedByUserId != userId && !isAdmin)
            {
                logger.LogWarning("DeleteAsync blocked: user {UserId} is not the owner of flight {FlightId}", userId, id);
                return;
            }
            db.FlightPreparations.Remove(fp);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteAsync failed for FlightPreparation Id={Id}", id);
            throw;
        }
    }

    // ── Patch operations ──────────────────────────────────────────────────────

    /// <summary>
    ///     Patches only the <c>TrajectorySimulationJson</c> column — used by auto-save
    ///     after simulation without triggering a full round-trip.
    /// </summary>
    public async Task PatchTrajectoryJsonAsync(int id, string? json)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var stub = new FlightPreparation { Id = id, TrajectorySimulationJson = json };
            db.Attach(stub);
            db.Entry(stub).Property(x => x.TrajectorySimulationJson).IsModified = true;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatchTrajectoryJsonAsync failed for FlightPreparation Id={Id}", id);
            throw;
        }
    }

    /// <summary>
    ///     Patches the <c>KmlTrack</c> column after a KML upload.
    /// </summary>
    public async Task PatchKmlTrackAsync(int id, string kml)
    {
        ArgumentNullException.ThrowIfNull(kml);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var stub = new FlightPreparation { Id = id, KmlTrack = kml };
            db.Attach(stub);
            db.Entry(stub).Property(x => x.KmlTrack).IsModified = true;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatchKmlTrackAsync failed for FlightPreparation Id={Id}", id);
            throw;
        }
    }

    /// <summary>
    ///     Marks a flight as flown and persists the post-flight report fields.
    /// </summary>
    public async Task PatchFlownAsync(
        int id,
        bool isFlown,
        string? landingNotes,
        int? durationMinutes,
        string? remarks,
        double? fuelConsumptionL,
        string? landingLocationText,
        bool? visibleDefects,
        string? visibleDefectsNotes)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var fp = await db.FlightPreparations.FindAsync(id);
            if (fp == null)
            {
                logger.LogWarning("PatchFlownAsync: FlightPreparation Id={Id} not found", id);
                return;
            }

            fp.IsFlown = isFlown;
            fp.ActualLandingNotes = landingNotes;
            fp.ActualFlightDurationMinutes = durationMinutes;
            fp.ActualRemarks = remarks;
            fp.FuelConsumptionL = fuelConsumptionL;
            fp.LandingLocationText = landingLocationText;
            fp.VisibleDefects = visibleDefects;
            fp.VisibleDefectsNotes = visibleDefectsNotes;
            fp.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatchFlownAsync failed for FlightPreparation Id={Id}", id);
            throw;
        }
    }

    // ── Sharing ───────────────────────────────────────────────────────────────

    /// <summary>
    ///     Returns all shares for a flight, but only if <paramref name="ownerId" /> matches
    ///     the flight's <c>CreatedByUserId</c>.
    /// </summary>
    public async Task<List<ApplicationUserSummary>> GetSharesAsync(int flightId, string ownerId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var isOwner = await db.FlightPreparations
            .AnyAsync(f => f.Id == flightId && f.CreatedByUserId == ownerId);
        if (!isOwner)
        {
            return [];
        }

        return await db.FlightPreparationShares
            .Where(s => s.FlightPreparationId == flightId)
            .Join(db.Users,
                s => s.SharedWithUserId,
                u => u.Id,
                (s, u) => new ApplicationUserSummary(u.Id, u.UserName ?? u.Email ?? u.Id, null))
            .ToListAsync();
    }

    /// <summary>
    ///     Returns all application users except the owner and users already shared with.
    ///     Only callable by the flight owner.
    /// </summary>
    public async Task<List<ApplicationUserSummary>> GetShareableUsersAsync(int flightId, string ownerId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var isOwner = await db.FlightPreparations
            .AnyAsync(f => f.Id == flightId && f.CreatedByUserId == ownerId);
        if (!isOwner)
        {
            return [];
        }

        var alreadySharedIds = await db.FlightPreparationShares
            .Where(s => s.FlightPreparationId == flightId)
            .Select(s => s.SharedWithUserId)
            .ToListAsync();

        return await db.Users
            .Where(u => u.Id != ownerId && !alreadySharedIds.Contains(u.Id))
            .Select(u => new ApplicationUserSummary(u.Id, u.UserName!, null))
            .ToListAsync();
    }

    /// <summary>
    ///     Shares a flight with <paramref name="targetUserId" />.
    ///     No-op if already shared. Only the owner may share.
    /// </summary>
    public async Task ShareAsync(int flightId, string ownerId, string targetUserId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(targetUserId);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var isOwner = await db.FlightPreparations
                .AnyAsync(f => f.Id == flightId && f.CreatedByUserId == ownerId);
            if (!isOwner)
            {
                logger.LogWarning("ShareAsync: user {UserId} is not the owner of flight {FlightId}", ownerId, flightId);
                return;
            }

            var alreadyShared = await db.FlightPreparationShares
                .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == targetUserId);
            if (alreadyShared)
            {
                return;
            }

            db.FlightPreparationShares.Add(new FlightPreparationShare
            {
                FlightPreparationId = flightId,
                SharedWithUserId = targetUserId,
                SharedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShareAsync failed for FlightPreparation Id={FlightId}, TargetUser={TargetUserId}", flightId, targetUserId);
            throw;
        }
    }

    /// <summary>
    ///     Revokes a share for <paramref name="targetUserId" />.
    ///     Only the owner may revoke.
    /// </summary>
    public async Task RevokeShareAsync(int flightId, string ownerId, string targetUserId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(targetUserId);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var isOwner = await db.FlightPreparations
                .AnyAsync(f => f.Id == flightId && f.CreatedByUserId == ownerId);
            if (!isOwner)
            {
                logger.LogWarning("RevokeShareAsync: user {UserId} is not the owner of flight {FlightId}", ownerId, flightId);
                return;
            }

            var share = await db.FlightPreparationShares
                .FirstOrDefaultAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == targetUserId);
            if (share != null)
            {
                db.FlightPreparationShares.Remove(share);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RevokeShareAsync failed for FlightPreparation Id={FlightId}, TargetUser={TargetUserId}", flightId, targetUserId);
            throw;
        }
    }

    /// <summary>
    ///     Returns <c>true</c> if a share row exists for the given flight and user.
    /// </summary>
    public async Task<bool> IsSharedWithAsync(int flightId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FlightPreparationShares
            .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == userId);
    }

    // ── Additional queries ────────────────────────────────────────────────────

    /// <summary>
    ///     Returns aggregate flight counts for the dashboard.
    /// </summary>
    public async Task<(int Total, int ThisYear, int Flown)> GetFlightCountsAsync(string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var currentYear = DateTime.UtcNow.Year;

        IQueryable<FlightPreparation> query = db.FlightPreparations;
        if (!isAdmin && userId != null)
            query = query.Where(f => f.CreatedByUserId == userId);
        else if (!isAdmin)
            return (0, 0, 0);

        var total = await query.CountAsync();
        var thisYear = await query.CountAsync(f => f.Datum.Year == currentYear);
        var flown = await query.CountAsync(f => f.IsFlown);
        return (total, thisYear, flown);
    }

    /// <summary>
    ///     Returns the <paramref name="count" /> most recent flights with Balloon, Pilot, and Location
    ///     navigation properties loaded. Used by the Home dashboard.
    /// </summary>
    public async Task<List<FlightPreparation>> GetRecentAsync(int count, string? userId, bool isAdmin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.FlightPreparations
            .Include(f => f.Balloon)
            .Include(f => f.Pilot)
            .Include(f => f.Location)
            .AsQueryable();

        if (!isAdmin && userId != null)
        {
            query = query.Where(f =>
                f.CreatedByUserId == userId ||
                f.Shares.Any(s => s.SharedWithUserId == userId));
        }
        else if (!isAdmin)
        {
            return [];
        }

        return await query
            .OrderByDescending(f => f.Datum)
            .ThenByDescending(f => f.Tijdstip)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    ///     Returns all flights with Balloon, Pilot, and Location navigation properties loaded.
    ///     Includes flights owned by the user and flights shared with the user.
    ///     Used by the Logboek page for statistics and charts.
    ///     Heavy collections (Passengers, Images, WindLevels) are NOT loaded.
    /// </summary>
    public async Task<List<FlightPreparation>> GetAllWithNavAsync(string? userId, bool isAdmin)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.FlightPreparations
            .Include(f => f.Balloon)
            .Include(f => f.Pilot)
            .Include(f => f.Location)
            .AsQueryable();
        if (!isAdmin)
        {
            if (userId == null)
            {
                return [];
            }

            query = query.Where(f =>
                f.CreatedByUserId == userId ||
                f.Shares.Any(s => s.SharedWithUserId == userId));
        }

        return await query
            .OrderBy(f => f.Datum)
            .ToListAsync();
    }
}
