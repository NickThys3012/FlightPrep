using FlightPrep.Data;
using FlightPrep.Models;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="FlightPreparationService"/> using the EF Core
/// in-memory provider.  Each test gets a unique named database so tests are fully
/// isolated even when running in parallel.
/// </summary>
public class FlightPreparationServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="IDbContextFactory{AppDbContext}"/> backed by a unique
    /// EF Core in-memory database so each test is fully isolated.
    /// </summary>
    private static (IDbContextFactory<AppDbContext> factory, IServiceProvider provider)
        CreateFactory()
    {
        var dbName = Guid.NewGuid().ToString("N");

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName));

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IDbContextFactory<AppDbContext>>(), provider);
    }

    private static FlightPreparationService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory, NullLogger<FlightPreparationService>.Instance);

    // ── Minimal seed helpers ──────────────────────────────────────────────────

    private static Balloon SeedBalloon() => new()
    {
        Registration = "OO-TST",
        Type         = "BB20N",
        Volume       = "2000M³"
    };

    private static Pilot SeedPilot() => new()
    {
        Name     = "Test Pilot",
        WeightKg = 80
    };

    private static Location SeedLocation() => new()
    {
        Name = "Test Field"
    };

    private static FlightPreparation SeedFlight(
        Balloon? balloon  = null,
        Pilot?   pilot    = null,
        Location? location = null) => new()
    {
        Datum    = DateOnly.FromDateTime(DateTime.Today),
        Tijdstip = TimeOnly.FromDateTime(DateTime.Now),
        Balloon  = balloon,
        Pilot    = pilot,
        Location = location
    };

    // ── GetSummariesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummariesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummariesAsync_WithFlights_ReturnsCorrectCount()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        await sut.SaveAsync(SeedFlight());
        await sut.SaveAsync(SeedFlight());
        await sut.SaveAsync(SeedFlight());

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert
        Assert.Equal(3, result.Count);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsFlightWithNavProps()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // Seed reference data directly so they get real IDs
        await using var db = await factory.CreateDbContextAsync();
        var balloon  = SeedBalloon();
        var pilot    = SeedPilot();
        var location = SeedLocation();
        db.Balloons.Add(balloon);
        db.Pilots.Add(pilot);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        // Build the flight using FK ids (not nav prop objects) so SaveAsync works correctly
        var fp = SeedFlight();
        fp.BalloonId  = balloon.Id;
        fp.PilotId    = pilot.Id;
        fp.LocationId = location.Id;
        fp.Passengers.Add(new Passenger { Name = "Alice", WeightKg = 65 });

        var id = await sut.SaveAsync(fp);

        // Act
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(id, loaded.Id);
        Assert.NotNull(loaded.Balloon);
        Assert.NotNull(loaded.Pilot);
        Assert.NotNull(loaded.Location);
        Assert.Single(loaded.Passengers);
        Assert.Equal("Alice", loaded.Passengers[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    // ── SaveAsync (create) ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NewFlight_AssignsId()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);
        var fp = SeedFlight();

        // Act
        var id = await sut.SaveAsync(fp);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task SaveAsync_WithPassengers_PersistsPassengers()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.Passengers.Add(new Passenger { Name = "Bob",   WeightKg = 70 });
        fp.Passengers.Add(new Passenger { Name = "Carol", WeightKg = 55 });

        var id = await sut.SaveAsync(fp);

        // Act
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Passengers.Count);
        Assert.Contains(loaded.Passengers, p => p.Name == "Bob");
        Assert.Contains(loaded.Passengers, p => p.Name == "Carol");
    }

    [Fact]
    public async Task SaveAsync_WithWindLevels_PersistsWindLevels()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 0,    SpeedKt = 8,  DirectionDeg = 270 });
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 2000, SpeedKt = 12, DirectionDeg = 280 });

        var id = await sut.SaveAsync(fp);

        // Act
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.WindLevels.Count);
    }

    // ── SaveAsync (update) ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ExistingFlight_UpdatesFields()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        var id = await sut.SaveAsync(fp);

        // Re-load and mutate
        var loaded = await sut.GetByIdAsync(id);
        Assert.NotNull(loaded);
        loaded.SurfaceWindSpeedKt = 12.5;

        // Act
        await sut.SaveAsync(loaded);
        var updated = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(12.5, updated.SurfaceWindSpeedKt);
    }

    [Fact]
    public async Task SaveAsync_ReplacesPassengers_OldOnesRemoved()
    {
        // Arrange — save with two passengers
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.Passengers.Add(new Passenger { Name = "Dan",   WeightKg = 80 });
        fp.Passengers.Add(new Passenger { Name = "Emily", WeightKg = 60 });
        var id = await sut.SaveAsync(fp);

        // Re-load and replace the passenger list
        var loaded = await sut.GetByIdAsync(id);
        Assert.NotNull(loaded);
        loaded.Passengers.Clear();
        loaded.Passengers.Add(new Passenger { Name = "Frank", WeightKg = 75 });

        // Act
        await sut.SaveAsync(loaded);
        var updated = await sut.GetByIdAsync(id);

        // Assert — only the new passenger should remain
        Assert.NotNull(updated);
        Assert.Single(updated.Passengers);
        Assert.Equal("Frank", updated.Passengers[0].Name);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingFlight_RemovesFromDatabase()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var id = await sut.SaveAsync(SeedFlight());
        Assert.NotNull(await sut.GetByIdAsync(id));

        // Act
        await sut.DeleteAsync(id);

        // Assert
        Assert.Null(await sut.GetByIdAsync(id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_DoesNotThrow()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // Act & Assert — must complete without exception
        var ex = await Record.ExceptionAsync(() => sut.DeleteAsync(99999));
        Assert.Null(ex);
    }

    // ── GetFlightCountsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetFlightCountsAsync_MixedFlights_ReturnsCorrectCounts()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // 2 flights this year, 1 flown
        var fp1 = SeedFlight(); fp1.IsFlown = true;
        var fp2 = SeedFlight(); fp2.IsFlown = false;
        // 1 flight from a past year, not flown
        var fp3 = SeedFlight(); fp3.Datum = new DateOnly(2020, 1, 1); fp3.IsFlown = false;

        await sut.SaveAsync(fp1);
        await sut.SaveAsync(fp2);
        await sut.SaveAsync(fp3);

        // Act
        var (total, thisYear, flown) = await sut.GetFlightCountsAsync();

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(2, thisYear);
        Assert.Equal(1, flown);
    }

    // ── GetRecentAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecentAsync_ReturnsLatestN()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var oldest = SeedFlight(); oldest.Datum = new DateOnly(2024, 1, 1);
        var middle = SeedFlight(); middle.Datum = new DateOnly(2024, 6, 1);
        var newest = SeedFlight(); newest.Datum = new DateOnly(2025, 1, 1);

        await sut.SaveAsync(oldest);
        await sut.SaveAsync(middle);
        await sut.SaveAsync(newest);

        // Act — ask for the 2 most recent
        var result = await sut.GetRecentAsync(2);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2025, 1, 1), result[0].Datum);
        Assert.Equal(new DateOnly(2024, 6, 1), result[1].Datum);
    }

    [Fact]
    public async Task GetRecentAsync_ZeroCount_Throws()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.GetRecentAsync(0));
    }

    // ── PatchTrajectoryJsonAsync ───────────────────────────────────────────────

    [Fact]
    public async Task PatchTrajectoryJsonAsync_UpdatesJsonField()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var id = await sut.SaveAsync(SeedFlight());
        const string json = """{"points":[]}""";

        // Act
        await sut.PatchTrajectoryJsonAsync(id, json);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(json, loaded.TrajectorySimulationJson);
    }

    [Fact]
    public async Task PatchTrajectoryJsonAsync_NullJson_ClearsField()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var id = await sut.SaveAsync(SeedFlight());
        await sut.PatchTrajectoryJsonAsync(id, """{"points":[1]}""");

        // Act — patch to null to clear it
        await sut.PatchTrajectoryJsonAsync(id, null);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded.TrajectorySimulationJson);
    }

    // ── PatchFlownAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task PatchFlownAsync_SetsFlownAndReport()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var id = await sut.SaveAsync(SeedFlight());

        // Act
        await sut.PatchFlownAsync(id, isFlown: true, landingNotes: "Smooth landing",
            durationMinutes: 45, remarks: "Great flight");

        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.True(loaded.IsFlown);
        Assert.Equal("Smooth landing", loaded.ActualLandingNotes);
        Assert.Equal(45, loaded.ActualFlightDurationMinutes);
        Assert.Equal("Great flight", loaded.ActualRemarks);
    }

    [Fact]
    public async Task PatchFlownAsync_NonExistingId_DoesNotThrow()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        // Act & Assert — must complete gracefully (logs warning, returns)
        var ex = await Record.ExceptionAsync(() =>
            sut.PatchFlownAsync(99999, true, null, null, null));
        Assert.Null(ex);
    }

    // ── GetAllWithNavAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllWithNavAsync_OrderedByDatum_ReturnsAscending()
    {
        // Arrange
        var (factory, _) = CreateFactory();
        var sut = BuildSut(factory);

        var fp1 = SeedFlight(); fp1.Datum = new DateOnly(2025, 3, 1);
        var fp2 = SeedFlight(); fp2.Datum = new DateOnly(2024, 1, 15);
        var fp3 = SeedFlight(); fp3.Datum = new DateOnly(2025, 1, 10);

        await sut.SaveAsync(fp1);
        await sut.SaveAsync(fp2);
        await sut.SaveAsync(fp3);

        // Act
        var result = await sut.GetAllWithNavAsync();

        // Assert — ascending order
        Assert.Equal(3, result.Count);
        Assert.True(result[0].Datum <= result[1].Datum);
        Assert.True(result[1].Datum <= result[2].Datum);
    }
}
