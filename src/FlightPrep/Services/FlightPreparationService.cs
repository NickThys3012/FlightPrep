using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlightPrep.Services;

/// <summary>
/// Encapsulates all EF Core read/write operations for <see cref="FlightPreparation"/>.
/// Pages must not inject <see cref="IDbContextFactory{AppDbContext}"/> directly;
/// all persistence flows through this service.
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
    /// Returns a lightweight summary list for the FlightList page.
    /// No heavy navigation collections are loaded.
    /// </summary>
    public async Task<List<FlightPreparationSummary>> GetSummariesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FlightPreparations
            .Select(f => new FlightPreparationSummary(
                f.Id,
                f.Datum,
                f.Tijdstip,
                f.IsFlown,
                f.Balloon != null ? f.Balloon.Registration : null,
                f.Pilot != null ? f.Pilot.Name : null,
                f.Location != null ? f.Location.Name : null,
                f.SurfaceWindSpeedKt,
                f.ZichtbaarheidKm,
                f.CapeJkg))
            .ToListAsync();
    }

    /// <summary>
    /// Returns the full <see cref="FlightPreparation"/> with all navigation properties
    /// eagerly loaded, or <c>null</c> if not found.
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
    /// Upserts a flight preparation.
    /// Handles the replace-passengers / replace-images / replace-wind-levels pattern.
    /// Navigation properties on <paramref name="fp"/> are preserved across the call.
    /// </summary>
    /// <returns>The persisted flight preparation id.</returns>
    public async Task<int> SaveAsync(FlightPreparation fp)
    {
        ArgumentNullException.ThrowIfNull(fp);

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();

            // Capture navigation props and detach them so EF does not try to upsert them.
            var balloon    = fp.Balloon;    fp.Balloon    = null;
            var pilot      = fp.Pilot;      fp.Pilot      = null;
            var location   = fp.Location;   fp.Location   = null;
            var passengers = fp.Passengers.ToList(); fp.Passengers.Clear();
            var images     = fp.Images.ToList();     fp.Images.Clear();
            var windLevels = fp.WindLevels.ToList(); fp.WindLevels.Clear();

            if (fp.Id == 0)
            {
                // ── CREATE ─────────────────────────────────────────────────
                fp.CreatedAt = fp.UpdatedAt = DateTime.UtcNow;
                db.FlightPreparations.Add(fp);
                await db.SaveChangesAsync();

                for (int i = 0; i < passengers.Count; i++)
                { passengers[i].FlightPreparationId = fp.Id; passengers[i].Order = i; passengers[i].Id = 0; }
                db.Passengers.AddRange(passengers);

                for (int i = 0; i < images.Count; i++)
                { images[i].FlightPreparationId = fp.Id; images[i].Id = 0; images[i].Order = i; }
                db.FlightImages.AddRange(images);

                for (int i = 0; i < windLevels.Count; i++)
                { windLevels[i].FlightPreparationId = fp.Id; windLevels[i].Id = 0; windLevels[i].Order = i; }
                db.WindLevels.AddRange(windLevels);

                await db.SaveChangesAsync();
            }
            else
            {
                // ── UPDATE ─────────────────────────────────────────────────
                fp.UpdatedAt = DateTime.UtcNow;

                // Remove existing related records before updating scalar props.
                db.Passengers.RemoveRange(
                    await db.Passengers.Where(p => p.FlightPreparationId == fp.Id).ToListAsync());

                db.FlightPreparations.Update(fp);
                await db.SaveChangesAsync();

                // Replace images and wind levels.
                db.FlightImages.RemoveRange(
                    await db.FlightImages.Where(i => i.FlightPreparationId == fp.Id).ToListAsync());

                for (int i = 0; i < passengers.Count; i++)
                { passengers[i].FlightPreparationId = fp.Id; passengers[i].Order = i; passengers[i].Id = 0; }
                db.Passengers.AddRange(passengers);

                for (int i = 0; i < images.Count; i++)
                { images[i].FlightPreparationId = fp.Id; images[i].Id = 0; images[i].Order = i; }
                db.FlightImages.AddRange(images);

                db.WindLevels.RemoveRange(
                    await db.WindLevels.Where(w => w.FlightPreparationId == fp.Id).ToListAsync());

                for (int i = 0; i < windLevels.Count; i++)
                { windLevels[i].FlightPreparationId = fp.Id; windLevels[i].Id = 0; windLevels[i].Order = i; }
                db.WindLevels.AddRange(windLevels);

                await db.SaveChangesAsync();
            }

            // Restore navigation props on the entity (callers may rely on them).
            fp.Balloon   = balloon;
            fp.Pilot     = pilot;
            fp.Location  = location;

            return fp.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveAsync failed for FlightPreparation Id={Id}", fp.Id);
            throw;
        }
    }

    /// <summary>Hard-deletes a flight preparation and all cascade-related entities.</summary>
    public async Task DeleteAsync(int id)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var fp = await db.FlightPreparations.FindAsync(id);
            if (fp != null)
            {
                db.FlightPreparations.Remove(fp);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteAsync failed for FlightPreparation Id={Id}", id);
            throw;
        }
    }

    // ── Patch operations ──────────────────────────────────────────────────────

    /// <summary>
    /// Patches only the <c>TrajectorySimulationJson</c> column — used by auto-save
    /// after simulation without triggering a full round-trip.
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
    /// Patches the <c>KmlTrack</c> column after a KML upload.
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
    /// Marks a flight as flown and persists the post-flight report fields.
    /// </summary>
    public async Task PatchFlownAsync(
        int id,
        bool isFlown,
        string? landingNotes,
        int? durationMinutes,
        string? remarks)
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

            fp.IsFlown                     = isFlown;
            fp.ActualLandingNotes          = landingNotes;
            fp.ActualFlightDurationMinutes = durationMinutes;
            fp.ActualRemarks               = remarks;
            fp.UpdatedAt                   = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PatchFlownAsync failed for FlightPreparation Id={Id}", id);
            throw;
        }
    }

    // ── Additional queries ────────────────────────────────────────────────────

    /// <summary>
    /// Returns aggregate flight counts for the dashboard.
    /// </summary>
    public async Task<(int Total, int ThisYear, int Flown)> GetFlightCountsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var currentYear = DateTime.UtcNow.Year;
        var total      = await db.FlightPreparations.CountAsync();
        var thisYear   = await db.FlightPreparations.CountAsync(f => f.Datum.Year == currentYear);
        var flown      = await db.FlightPreparations.CountAsync(f => f.IsFlown);
        return (total, thisYear, flown);
    }

    /// <summary>
    /// Returns the <paramref name="count"/> most recent flights with Balloon, Pilot, and Location
    /// navigation properties loaded. Used by the Home dashboard.
    /// </summary>
    public async Task<List<FlightPreparation>> GetRecentAsync(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FlightPreparations
            .Include(f => f.Balloon)
            .Include(f => f.Pilot)
            .Include(f => f.Location)
            .OrderByDescending(f => f.Datum)
            .ThenByDescending(f => f.Tijdstip)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Returns all flights with Balloon, Pilot, and Location navigation properties loaded.
    /// Used by the Logboek page for statistics and charts.
    /// Heavy collections (Passengers, Images, WindLevels) are NOT loaded.
    /// </summary>
    public async Task<List<FlightPreparation>> GetAllWithNavAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FlightPreparations
            .Include(f => f.Balloon)
            .Include(f => f.Pilot)
            .Include(f => f.Location)
            .OrderBy(f => f.Datum)
            .ToListAsync();
    }
}
