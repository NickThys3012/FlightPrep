using FlightPrep.Data;
using FlightPrep.Models;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Tests;

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
    private static IDbContextFactory<AppDbContext> CreateFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
             .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider()
                       .GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    private static FlightPreparationService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory, NullLogger<FlightPreparationService>.Instance);

    // ── Seed helpers ──────────────────────────────────────────────────────────

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
        Balloon?  balloon  = null,
        Pilot?    pilot    = null,
        Location? location = null) => new()
    {
        Datum    = DateOnly.FromDateTime(DateTime.Today),
        Tijdstip = TimeOnly.FromDateTime(DateTime.Now),
        Balloon  = balloon,
        Pilot    = pilot,
        Location = location
    };

    // ── GetBalloonsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalloonsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetBalloonsAsync_EmptyDatabase_ReturnsEmptyList));
        var sut     = BuildSut(factory);

        // Act
        var result = await sut.GetBalloonsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBalloonsAsync_TwoBalloons_ReturnsOrderedByRegistration()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetBalloonsAsync_TwoBalloons_ReturnsOrderedByRegistration));
        var sut     = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        db.Balloons.AddRange(
            new Balloon { Registration = "OO-ZZZ", Type = "TypeA", Volume = "1000M³" },
            new Balloon { Registration = "OO-AAA", Type = "TypeB", Volume = "2000M³" });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetBalloonsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("OO-AAA", result[0].Registration);
        Assert.Equal("OO-ZZZ", result[1].Registration);
    }

    // ── GetPilotsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPilotsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetPilotsAsync_EmptyDatabase_ReturnsEmptyList));
        var sut     = BuildSut(factory);

        // Act
        var result = await sut.GetPilotsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPilotsAsync_TwoPilots_ReturnsOrderedByName()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetPilotsAsync_TwoPilots_ReturnsOrderedByName));
        var sut     = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        db.Pilots.AddRange(
            new Pilot { Name = "Zara", WeightKg = 70 },
            new Pilot { Name = "Alice", WeightKg = 65 });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetPilotsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("Zara",  result[1].Name);
    }

    // ── GetLocationsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetLocationsAsync_EmptyDatabase_ReturnsEmptyList));
        var sut     = BuildSut(factory);

        // Act
        var result = await sut.GetLocationsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLocationsAsync_TwoLocations_ReturnsOrderedByName()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetLocationsAsync_TwoLocations_ReturnsOrderedByName));
        var sut     = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        db.Locations.AddRange(
            new Location { Name = "Zottegem" },
            new Location { Name = "Aalst" });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetLocationsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Aalst",    result[0].Name);
        Assert.Equal("Zottegem", result[1].Name);
    }

    // ── GetSummariesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummariesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_EmptyDatabase_ReturnsEmptyList));
        var sut     = BuildSut(factory);

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummariesAsync_WithFlights_ReturnsCorrectCount()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_WithFlights_ReturnsCorrectCount));
        var sut     = BuildSut(factory);

        await sut.SaveAsync(SeedFlight());
        await sut.SaveAsync(SeedFlight());
        await sut.SaveAsync(SeedFlight());

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetSummariesAsync_FlightWithNavProps_NullsReturnedWhenNotLinked()
    {
        // Arrange — flight has no balloon/pilot/location FK
        var factory = CreateFactory(nameof(GetSummariesAsync_FlightWithNavProps_NullsReturnedWhenNotLinked));
        var sut     = BuildSut(factory);
        await sut.SaveAsync(SeedFlight());

        // Act
        var result = await sut.GetSummariesAsync();

        // Assert — nav-prop names projected as null
        Assert.Single(result);
        Assert.Null(result[0].BalloonRegistration);
        Assert.Null(result[0].PilotName);
        Assert.Null(result[0].LocationName);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsFlightWithNavProps()
    {
        // Arrange — seed reference data, then link via FK
        var factory = CreateFactory(nameof(GetByIdAsync_ExistingId_ReturnsFlightWithNavProps));
        var sut     = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var balloon  = SeedBalloon();
        var pilot    = SeedPilot();
        var location = SeedLocation();
        db.Balloons.Add(balloon);
        db.Pilots.Add(pilot);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var fp = SeedFlight();
        fp.BalloonId  = balloon.Id;
        fp.PilotId    = pilot.Id;
        fp.LocationId = location.Id;
        fp.Passengers.Add(new Passenger { Name = "Alice", WeightKg = 65 });
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 1000, SpeedKt = 10, DirectionDeg = 270 });

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
        Assert.Single(loaded.WindLevels);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetByIdAsync_NonExistingId_ReturnsNull));
        var sut     = BuildSut(factory);

        // Act
        var result = await sut.GetByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    // ── SaveAsync (create) ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NewFlight_AssignsPositiveId()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_NewFlight_AssignsPositiveId));
        var sut     = BuildSut(factory);
        var fp      = SeedFlight();

        // Act
        var id = await sut.SaveAsync(fp);

        // Assert
        Assert.True(id > 0);
    }

    [Fact]
    public async Task SaveAsync_NullArgument_Throws()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_NullArgument_Throws));
        var sut     = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }

    [Fact]
    public async Task SaveAsync_WithPassengers_PersistsPassengers()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_WithPassengers_PersistsPassengers));
        var sut     = BuildSut(factory);

        var fp = SeedFlight();
        fp.Passengers.Add(new Passenger { Name = "Bob",   WeightKg = 70 });
        fp.Passengers.Add(new Passenger { Name = "Carol", WeightKg = 55 });

        // Act
        var id     = await sut.SaveAsync(fp);
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
        var factory = CreateFactory(nameof(SaveAsync_WithWindLevels_PersistsWindLevels));
        var sut     = BuildSut(factory);

        var fp = SeedFlight();
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 0,    SpeedKt = 8,  DirectionDeg = 270 });
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 2000, SpeedKt = 12, DirectionDeg = 280 });

        // Act
        var id     = await sut.SaveAsync(fp);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.WindLevels.Count);
    }

    [Fact]
    public async Task SaveAsync_NewFlight_NavPropsRestoredAfterSave()
    {
        // Arrange — verify the finally-block restores nav props on the caller's entity
        var factory = CreateFactory(nameof(SaveAsync_NewFlight_NavPropsRestoredAfterSave));
        var sut     = BuildSut(factory);

        var pilot = SeedPilot();
        var fp    = SeedFlight();
        fp.Pilot  = pilot;
        fp.Passengers.Add(new Passenger { Name = "X", WeightKg = 70 });

        // Act
        await sut.SaveAsync(fp);

        // Assert — pilot and passengers are still attached to fp after the call
        Assert.Same(pilot, fp.Pilot);
        Assert.Single(fp.Passengers);
    }

    // ── SaveAsync (update) ────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ExistingFlight_UpdatesFields()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_ExistingFlight_UpdatesFields));
        var sut     = BuildSut(factory);

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
    public async Task SaveAsync_UpdateReplacesPassengers_OldPassengersRemoved()
    {
        // Arrange — save with two passengers
        var factory = CreateFactory(nameof(SaveAsync_UpdateReplacesPassengers_OldPassengersRemoved));
        var sut     = BuildSut(factory);

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

        // Assert — only Frank should remain
        Assert.NotNull(updated);
        Assert.Single(updated.Passengers);
        Assert.Equal("Frank", updated.Passengers[0].Name);
    }

    [Fact]
    public async Task SaveAsync_UpdateReplacesWindLevels_OldLevelsRemoved()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_UpdateReplacesWindLevels_OldLevelsRemoved));
        var sut     = BuildSut(factory);

        var fp = SeedFlight();
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 0,    SpeedKt = 5, DirectionDeg = 90 });
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 1000, SpeedKt = 8, DirectionDeg = 180 });
        var id = await sut.SaveAsync(fp);

        var loaded = await sut.GetByIdAsync(id);
        Assert.NotNull(loaded);
        loaded.WindLevels.Clear();
        loaded.WindLevels.Add(new WindLevel { AltitudeFt = 500, SpeedKt = 6, DirectionDeg = 270 });

        // Act
        await sut.SaveAsync(loaded);
        var updated = await sut.GetByIdAsync(id);

        // Assert — only one level remains
        Assert.NotNull(updated);
        Assert.Single(updated.WindLevels);
        Assert.Equal(500, updated.WindLevels[0].AltitudeFt);
    }

    [Fact]
    public async Task SaveAsync_ExistingFlight_NavPropsRestoredAfterUpdate()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_ExistingFlight_NavPropsRestoredAfterUpdate));
        var sut     = BuildSut(factory);

        var fp = SeedFlight();
        fp.Passengers.Add(new Passenger { Name = "G", WeightKg = 60 });
        var id = await sut.SaveAsync(fp);

        var loaded = await sut.GetByIdAsync(id);
        Assert.NotNull(loaded);
        loaded.SurfaceWindSpeedKt = 5;

        // Act
        await sut.SaveAsync(loaded);

        // Assert — passengers still attached after update
        Assert.Single(loaded.Passengers);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingFlight_RemovesFromDatabase()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_ExistingFlight_RemovesFromDatabase));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());
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
        var factory = CreateFactory(nameof(DeleteAsync_NonExistingId_DoesNotThrow));
        var sut     = BuildSut(factory);

        // Act & Assert
        var ex = await Record.ExceptionAsync(() => sut.DeleteAsync(99999));
        Assert.Null(ex);
    }

    // ── GetFlightCountsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetFlightCountsAsync_EmptyDatabase_ReturnsAllZeros()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetFlightCountsAsync_EmptyDatabase_ReturnsAllZeros));
        var sut     = BuildSut(factory);

        // Act
        var (total, thisYear, flown) = await sut.GetFlightCountsAsync();

        // Assert
        Assert.Equal(0, total);
        Assert.Equal(0, thisYear);
        Assert.Equal(0, flown);
    }

    [Fact]
    public async Task GetFlightCountsAsync_MixedFlights_ReturnsCorrectCounts()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetFlightCountsAsync_MixedFlights_ReturnsCorrectCounts));
        var sut     = BuildSut(factory);

        // 2 flights this year (1 flown), 1 flight from past year
        var fp1 = SeedFlight(); fp1.IsFlown = true;
        var fp2 = SeedFlight(); fp2.IsFlown = false;
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
    public async Task GetRecentAsync_ReturnsLatestN_OrderedByDateDescending()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetRecentAsync_ReturnsLatestN_OrderedByDateDescending));
        var sut     = BuildSut(factory);

        var oldest = SeedFlight(); oldest.Datum = new DateOnly(2024, 1, 1);
        var middle = SeedFlight(); middle.Datum = new DateOnly(2024, 6, 1);
        var newest = SeedFlight(); newest.Datum = new DateOnly(2025, 1, 1);

        await sut.SaveAsync(oldest);
        await sut.SaveAsync(middle);
        await sut.SaveAsync(newest);

        // Act — ask for 2 most recent
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
        var factory = CreateFactory(nameof(GetRecentAsync_ZeroCount_Throws));
        var sut     = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.GetRecentAsync(0));
    }

    [Fact]
    public async Task GetRecentAsync_NegativeCount_Throws()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetRecentAsync_NegativeCount_Throws));
        var sut     = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.GetRecentAsync(-5));
    }

    // ── GetAllWithNavAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllWithNavAsync_OrderedByDatumAscending()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllWithNavAsync_OrderedByDatumAscending));
        var sut     = BuildSut(factory);

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

    [Fact]
    public async Task GetAllWithNavAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllWithNavAsync_EmptyDatabase_ReturnsEmptyList));
        var sut     = BuildSut(factory);

        // Act
        var result = await sut.GetAllWithNavAsync();

        // Assert
        Assert.Empty(result);
    }

    // ── PatchTrajectoryJsonAsync ──────────────────────────────────────────────

    [Fact]
    public async Task PatchTrajectoryJsonAsync_UpdatesJsonField()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchTrajectoryJsonAsync_UpdatesJsonField));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());
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
        // Arrange — set then clear
        var factory = CreateFactory(nameof(PatchTrajectoryJsonAsync_NullJson_ClearsField));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());
        await sut.PatchTrajectoryJsonAsync(id, """{"points":[1]}""");

        // Act
        await sut.PatchTrajectoryJsonAsync(id, null);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded.TrajectorySimulationJson);
    }

    // ── PatchKmlTrackAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task PatchKmlTrackAsync_UpdatesKmlField()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchKmlTrackAsync_UpdatesKmlField));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());
        const string kml = "<kml/>";

        // Act
        await sut.PatchKmlTrackAsync(id, kml);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(kml, loaded.KmlTrack);
    }

    [Fact]
    public async Task PatchKmlTrackAsync_NullArgument_Throws()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchKmlTrackAsync_NullArgument_Throws));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.PatchKmlTrackAsync(id, null!));
    }

    // ── PatchFlownAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task PatchFlownAsync_SetsFlownAndReportFields()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchFlownAsync_SetsFlownAndReportFields));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());

        // Act
        await sut.PatchFlownAsync(
            id,
            isFlown:         true,
            landingNotes:    "Smooth landing",
            durationMinutes: 45,
            remarks:         "Great flight");

        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.True(loaded.IsFlown);
        Assert.Equal("Smooth landing", loaded.ActualLandingNotes);
        Assert.Equal(45,               loaded.ActualFlightDurationMinutes);
        Assert.Equal("Great flight",   loaded.ActualRemarks);
    }

    [Fact]
    public async Task PatchFlownAsync_NonExistingId_DoesNotThrow()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchFlownAsync_NonExistingId_DoesNotThrow));
        var sut     = BuildSut(factory);

        // Act & Assert — must complete gracefully (logs warning, returns)
        var ex = await Record.ExceptionAsync(() =>
            sut.PatchFlownAsync(99999, true, null, null, null));
        Assert.Null(ex);
    }

    [Fact]
    public async Task PatchFlownAsync_NullNotes_SetsNullFields()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchFlownAsync_NullNotes_SetsNullFields));
        var sut     = BuildSut(factory);
        var id      = await sut.SaveAsync(SeedFlight());

        // Act — patch with null notes/duration/remarks
        await sut.PatchFlownAsync(id, isFlown: false, null, null, null);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.False(loaded.IsFlown);
        Assert.Null(loaded.ActualLandingNotes);
        Assert.Null(loaded.ActualFlightDurationMinutes);
        Assert.Null(loaded.ActualRemarks);
    }
}
