using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Tests;

/// <summary>
///     Integration tests for <see cref="FlightPreparationService" /> using the EF Core
///     in-memory provider.  Each test gets a unique named database, so tests are fully
///     isolated even when running in parallel.
/// </summary>
public class FlightPreparationServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>
    ///     Creates an <see cref="IDbContextFactory{AppDbContext}" /> backed by a unique
    ///     EF Core in-memory database so each test is fully isolated.
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

    private static Balloon SeedBalloon() => new() { Registration = "OO-TST", Type = "BB20N", VolumeM3 = 2000 };

    private static Pilot SeedPilot() => new() { Name = "Test Pilot", WeightKg = 80 };

    private static Location SeedLocation() => new() { Name = "Test Field" };

    private static FlightPreparation SeedFlight(
        Balloon? balloon = null,
        Pilot? pilot = null,
        Location? location = null) => new()
    {
        Datum = DateOnly.FromDateTime(DateTime.Today),
        Tijdstip = TimeOnly.FromDateTime(DateTime.Now),
        Balloon = balloon,
        Pilot = pilot,
        Location = location,
        CreatedByUserId = "owner-1"
    };

    // ── GetBalloonsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalloonsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetBalloonsAsync_EmptyDatabase_ReturnsEmptyList));
        var sut = BuildSut(factory);

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
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        db.Balloons.AddRange(
            new Balloon { Registration = "OO-ZZZ", Type = "TypeA", VolumeM3 = 1000 },
            new Balloon { Registration = "OO-AAA", Type = "TypeB", VolumeM3 = 2000 });
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
        var sut = BuildSut(factory);

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
        var sut = BuildSut(factory);

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
        Assert.Equal("Zara", result[1].Name);
    }

    // ── GetLocationsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetLocationsAsync_EmptyDatabase_ReturnsEmptyList));
        var sut = BuildSut(factory);

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
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        db.Locations.AddRange(
            new Location { Name = "Zottegem" },
            new Location { Name = "Aalst" });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetLocationsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Aalst", result[0].Name);
        Assert.Equal("Zottegem", result[1].Name);
    }

    // ── GetSummariesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummariesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_EmptyDatabase_ReturnsEmptyList));
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetSummariesAsync(null, true);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummariesAsync_WithFlights_ReturnsCorrectCount()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_WithFlights_ReturnsCorrectCount));
        var sut = BuildSut(factory);

        await sut.SaveAsync(SeedFlight());
        await sut.SaveAsync(SeedFlight());
        await sut.SaveAsync(SeedFlight());

        // Act
        var result = await sut.GetSummariesAsync(null, true);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetSummariesAsync_FlightWithNavProps_NullsReturnedWhenNotLinked()
    {
        // Arrange — flight has no balloon/pilot/location FK
        var factory = CreateFactory(nameof(GetSummariesAsync_FlightWithNavProps_NullsReturnedWhenNotLinked));
        var sut = BuildSut(factory);
        await sut.SaveAsync(SeedFlight());

        // Act
        var result = await sut.GetSummariesAsync(null, true);

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
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var balloon = SeedBalloon();
        var pilot = SeedPilot();
        var location = SeedLocation();
        db.Balloons.Add(balloon);
        db.Pilots.Add(pilot);
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var fp = SeedFlight();
        fp.BalloonId = balloon.Id;
        fp.PilotId = pilot.Id;
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
        var sut = BuildSut(factory);

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
        var sut = BuildSut(factory);
        var fp = SeedFlight();

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
        var sut = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }

    [Fact]
    public async Task SaveAsync_WithPassengers_PersistsPassengers()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_WithPassengers_PersistsPassengers));
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.Passengers.Add(new Passenger { Name = "Bob", WeightKg = 70 });
        fp.Passengers.Add(new Passenger { Name = "Carol", WeightKg = 55 });

        // Act
        var id = await sut.SaveAsync(fp);
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
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 0, SpeedKt = 8, DirectionDeg = 270 });
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 2000, SpeedKt = 12, DirectionDeg = 280 });

        // Act
        var id = await sut.SaveAsync(fp);
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
        var sut = BuildSut(factory);

        var pilot = SeedPilot();
        var fp = SeedFlight();
        fp.Pilot = pilot;
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
    public async Task SaveAsync_UpdateReplacesPassengers_OldPassengersRemoved()
    {
        // Arrange — save with two passengers
        var factory = CreateFactory(nameof(SaveAsync_UpdateReplacesPassengers_OldPassengersRemoved));
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.Passengers.Add(new Passenger { Name = "Dan", WeightKg = 80 });
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
        var sut = BuildSut(factory);

        var fp = SeedFlight();
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 0, SpeedKt = 5, DirectionDeg = 90 });
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
        var sut = BuildSut(factory);

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
        var sut = BuildSut(factory);
        var id = await sut.SaveAsync(SeedFlight());
        Assert.NotNull(await sut.GetByIdAsync(id));

        // Act
        await sut.DeleteAsync(id, "owner-1");

        // Assert
        Assert.Null(await sut.GetByIdAsync(id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_DoesNotThrow()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_NonExistingId_DoesNotThrow));
        var sut = BuildSut(factory);

        // Act & Assert
        var ex = await Record.ExceptionAsync(() => sut.DeleteAsync(99999, "owner-1"));
        Assert.Null(ex);
    }

    // ── GetFlightCountsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetFlightCountsAsync_EmptyDatabase_ReturnsAllZeros()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetFlightCountsAsync_EmptyDatabase_ReturnsAllZeros));
        var sut = BuildSut(factory);

        // Act
        var (total, thisYear, flown) = await sut.GetFlightCountsAsync("owner-1", false);

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
        var sut = BuildSut(factory);

        // 2 flights this year (1 flown), 1 flight from the past year
        var fp1 = SeedFlight();
        fp1.IsFlown = true;
        var fp2 = SeedFlight();
        fp2.IsFlown = false;
        var fp3 = SeedFlight();
        fp3.Datum = new DateOnly(2020, 1, 1);
        fp3.IsFlown = false;

        await sut.SaveAsync(fp1);
        await sut.SaveAsync(fp2);
        await sut.SaveAsync(fp3);

        // Act
        var (total, thisYear, flown) = await sut.GetFlightCountsAsync("owner-1", false);

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
        var sut = BuildSut(factory);

        var oldest = SeedFlight();
        oldest.Datum = new DateOnly(2024, 1, 1);
        var middle = SeedFlight();
        middle.Datum = new DateOnly(2024, 6, 1);
        var newest = SeedFlight();
        newest.Datum = new DateOnly(2025, 1, 1);

        await sut.SaveAsync(oldest);
        await sut.SaveAsync(middle);
        await sut.SaveAsync(newest);

        // Act — ask for 2 most recent
        var result = await sut.GetRecentAsync(2, "owner-1", false);

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
        var sut = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.GetRecentAsync(0, "owner-1", false));
    }

    [Fact]
    public async Task GetRecentAsync_NegativeCount_Throws()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetRecentAsync_NegativeCount_Throws));
        var sut = BuildSut(factory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.GetRecentAsync(-5, "owner-1", false));
    }

    // ── GetAllWithNavAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllWithNavAsync_OrderedByDatumAscending()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllWithNavAsync_OrderedByDatumAscending));
        var sut = BuildSut(factory);

        var fp1 = SeedFlight();
        fp1.Datum = new DateOnly(2025, 3, 1);
        var fp2 = SeedFlight();
        fp2.Datum = new DateOnly(2024, 1, 15);
        var fp3 = SeedFlight();
        fp3.Datum = new DateOnly(2025, 1, 10);

        await sut.SaveAsync(fp1);
        await sut.SaveAsync(fp2);
        await sut.SaveAsync(fp3);

        // Act
        var result = await sut.GetAllWithNavAsync(null, true);

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
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllWithNavAsync(null, true);

        // Assert
        Assert.Empty(result);
    }

    // ── PatchTrajectoryJsonAsync ──────────────────────────────────────────────

    [Fact]
    public async Task PatchTrajectoryJsonAsync_UpdatesJsonField()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchTrajectoryJsonAsync_UpdatesJsonField));
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
        // Arrange — set then clear
        var factory = CreateFactory(nameof(PatchTrajectoryJsonAsync_NullJson_ClearsField));
        var sut = BuildSut(factory);
        var id = await sut.SaveAsync(SeedFlight());
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
        var sut = BuildSut(factory);
        var id = await sut.SaveAsync(SeedFlight());
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
        var sut = BuildSut(factory);
        var id = await sut.SaveAsync(SeedFlight());

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
        var sut = BuildSut(factory);
        var id = await sut.SaveAsync(SeedFlight());

        // Act
        await sut.PatchFlownAsync(
            id,
            true,
            "Smooth landing",
            45,
            "Great flight",
            null, null, null, null);

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
        var factory = CreateFactory(nameof(PatchFlownAsync_NonExistingId_DoesNotThrow));
        var sut = BuildSut(factory);

        // Act & Assert — must complete gracefully (logs warning, returns)
        var ex = await Record.ExceptionAsync(() =>
            sut.PatchFlownAsync(99999, true, null, null, null, null, null, null, null));
        Assert.Null(ex);
    }

    [Fact]
    public async Task PatchFlownAsync_NullNotes_SetsNullFields()
    {
        // Arrange
        var factory = CreateFactory(nameof(PatchFlownAsync_NullNotes_SetsNullFields));
        var sut = BuildSut(factory);
        var id = await sut.SaveAsync(SeedFlight());

        // Act — patch with null notes/duration/remarks
        await sut.PatchFlownAsync(id, false, null, null, null, null, null, null, null);
        var loaded = await sut.GetByIdAsync(id);

        // Assert
        Assert.NotNull(loaded);
        Assert.False(loaded.IsFlown);
        Assert.Null(loaded.ActualLandingNotes);
        Assert.Null(loaded.ActualFlightDurationMinutes);
        Assert.Null(loaded.ActualRemarks);
    }

    // ── GetSummariesAsync — null userId guard ─────────────────────────────────

    [Fact]
    public async Task GetSummariesAsync_NullUserId_NonAdmin_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_NullUserId_NonAdmin_ReturnsEmptyList));
        var sut = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.Add(SeedFlight());
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetSummariesAsync(null, isAdmin: false);

        // Assert — null userId non-admin always returns empty, regardless of stored flights
        Assert.Empty(result);
    }

    // ── GetAllWithNavAsync — null userId guard ────────────────────────────────

    [Fact]
    public async Task GetAllWithNavAsync_NullUserId_NonAdmin_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllWithNavAsync_NullUserId_NonAdmin_ReturnsEmptyList));
        var sut = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.Add(SeedFlight());
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetAllWithNavAsync(null, isAdmin: false);

        // Assert
        Assert.Empty(result);
    }

    // ── SaveAsync — UPDATE path with images ───────────────────────────────────

    [Fact]
    public async Task SaveAsync_UpdateExistingFlight_ReplacesImages()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveAsync_UpdateExistingFlight_ReplacesImages));
        var sut = BuildSut(factory);
        var fp = SeedFlight();
        fp.Images = [new FlightImage { FileName = "before.jpg", ContentType = "image/jpeg", Data = [1, 2] }];
        var id = await sut.SaveAsync(fp);

        // Act — update with a different image
        var saved = await sut.GetByIdAsync(id);
        Assert.NotNull(saved);
        saved.Images = [new FlightImage { FileName = "after.jpg", ContentType = "image/jpeg", Data = [3, 4] }];
        await sut.SaveAsync(saved);

        // Assert — only the new image should be present
        var loaded = await sut.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Images);
        Assert.Equal("after.jpg", loaded.Images[0].FileName);
    }

    // ── GetSummariesPagedAsync ────────────────────────────────────────────────

    // Helper: build a flight owned by a user with optional IsFlown and date offset
    private static FlightPreparation MakeFlight(
        string userId,
        bool isFlown = false,
        int dateDeltaDays = 0) => new()
    {
        Datum           = DateOnly.FromDateTime(DateTime.Today.AddDays(dateDeltaDays)),
        Tijdstip        = TimeOnly.MinValue,
        CreatedByUserId = userId,
        IsFlown         = isFlown,
    };

    [Fact]
    public async Task GetSummariesPagedAsync_SinglePage_ReturnsAllItems()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_SinglePage_ReturnsAllItems));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(MakeFlight("u1"), MakeFlight("u1"), MakeFlight("u1"));
        await db.SaveChangesAsync();

        // Act
        var (items, total) = await sut.GetSummariesPagedAsync("u1", false, "alle", 1, 10);

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_RespectsPageSize_NeverReturnsMore()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_RespectsPageSize_NeverReturnsMore));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        for (var i = 0; i < 5; i++) db.FlightPreparations.Add(MakeFlight("u1"));
        await db.SaveChangesAsync();

        // Act — page size 2
        var (items, total) = await sut.GetSummariesPagedAsync("u1", false, "alle", 1, 2);

        // Assert
        Assert.Equal(5, total);   // total is always the unsliced count
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_SecondPage_SkipsFirstPageItems()
    {
        // Arrange — 5 flights; page 2 with pageSize 2 should return items 3-4
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_SecondPage_SkipsFirstPageItems));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        for (var i = 0; i < 5; i++) db.FlightPreparations.Add(MakeFlight("u1", dateDeltaDays: i));
        await db.SaveChangesAsync();

        var (page1, _) = await sut.GetSummariesPagedAsync("u1", false, "alle", 1, 2);
        var (page2, _) = await sut.GetSummariesPagedAsync("u1", false, "alle", 2, 2);

        // Assert — no IDs in common between pages
        var ids1 = page1.Select(f => f.Id).ToHashSet();
        Assert.All(page2, f => Assert.DoesNotContain(f.Id, ids1));
    }

    [Fact]
    public async Task GetSummariesPagedAsync_SortDescending_MostRecentFirst()
    {
        // Arrange — create 3 flights with different dates
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_SortDescending_MostRecentFirst));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(
            MakeFlight("u1", dateDeltaDays: 0),
            MakeFlight("u1", dateDeltaDays: -5),
            MakeFlight("u1", dateDeltaDays: -10));
        await db.SaveChangesAsync();

        // Act
        var (items, _) = await sut.GetSummariesPagedAsync("u1", false, "alle", 1, 10, sortDescending: true);

        // Assert — dates descending
        for (var i = 1; i < items.Count; i++)
            Assert.True(items[i].Datum <= items[i - 1].Datum,
                $"Expected descending dates but got {items[i - 1].Datum} then {items[i].Datum}");
    }

    [Fact]
    public async Task GetSummariesPagedAsync_SortAscending_OldestFirst()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_SortAscending_OldestFirst));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(
            MakeFlight("u1", dateDeltaDays: -10),
            MakeFlight("u1", dateDeltaDays: -5),
            MakeFlight("u1", dateDeltaDays: 0));
        await db.SaveChangesAsync();

        // Act
        var (items, _) = await sut.GetSummariesPagedAsync("u1", false, "alle", 1, 10, sortDescending: false);

        // Assert — dates ascending
        for (var i = 1; i < items.Count; i++)
            Assert.True(items[i].Datum >= items[i - 1].Datum,
                $"Expected ascending dates but got {items[i - 1].Datum} then {items[i].Datum}");
    }

    [Fact]
    public async Task GetSummariesPagedAsync_IsAdmin_ReturnsAllUsersFlights()
    {
        // Arrange — flights belonging to two different users
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_IsAdmin_ReturnsAllUsersFlights));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(MakeFlight("u1"), MakeFlight("u2"), MakeFlight("u3"));
        await db.SaveChangesAsync();

        // Act — admin sees everyone's flights
        var (items, total) = await sut.GetSummariesPagedAsync(null, isAdmin: true, "alle", 1, 10);

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_NonAdmin_ReturnsOnlyOwnFlights()
    {
        // Arrange — flights from two different users
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_NonAdmin_ReturnsOnlyOwnFlights));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(MakeFlight("u1"), MakeFlight("u1"), MakeFlight("u2"));
        await db.SaveChangesAsync();

        // Act — non-admin user "u1" only sees own flights
        var (items, total) = await sut.GetSummariesPagedAsync("u1", isAdmin: false, "alle", 1, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.All(items, f => Assert.Equal("u1", f.CreatedByUserId));
    }

    [Fact]
    public async Task GetSummariesPagedAsync_NonAdmin_ReturnsSharedFlights()
    {
        // Arrange — one flight owned by u2, shared with u1
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_NonAdmin_ReturnsSharedFlights));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        var flight = MakeFlight("u2");
        flight.Shares.Add(new FlightPreparationShare { SharedWithUserId = "u1" });
        db.FlightPreparations.Add(flight);
        await db.SaveChangesAsync();

        // Act — u1 can see the flight shared with them
        var (items, total) = await sut.GetSummariesPagedAsync("u1", isAdmin: false, "alle", 1, 10);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_StatusFilterGevlogen_ReturnsOnlyFlownFlights()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_StatusFilterGevlogen_ReturnsOnlyFlownFlights));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(
            MakeFlight("u1", isFlown: true),
            MakeFlight("u1", isFlown: false),
            MakeFlight("u1", isFlown: true));
        await db.SaveChangesAsync();

        // Act
        var (items, total) = await sut.GetSummariesPagedAsync("u1", false, "gevlogen", 1, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.All(items, f => Assert.True(f.IsFlown));
    }

    [Fact]
    public async Task GetSummariesPagedAsync_StatusFilterNietGevlogen_ReturnsOnlyNotFlownFlights()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_StatusFilterNietGevlogen_ReturnsOnlyNotFlownFlights));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(
            MakeFlight("u1", isFlown: true),
            MakeFlight("u1", isFlown: false),
            MakeFlight("u1", isFlown: false));
        await db.SaveChangesAsync();

        // Act
        var (items, total) = await sut.GetSummariesPagedAsync("u1", false, "niet-gevlogen", 1, 10);

        // Assert
        Assert.Equal(2, total);
        Assert.All(items, f => Assert.False(f.IsFlown));
    }

    [Fact]
    public async Task GetSummariesPagedAsync_NoMatchingFilter_ReturnsEmptyWithZeroTotal()
    {
        // Arrange — two not-flown flights; filter for flown
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_NoMatchingFilter_ReturnsEmptyWithZeroTotal));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.AddRange(MakeFlight("u1"), MakeFlight("u1"));
        await db.SaveChangesAsync();

        // Act
        var (items, total) = await sut.GetSummariesPagedAsync("u1", false, "gevlogen", 1, 10);

        // Assert
        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_LastPage_TotalReflectsFullCount()
    {
        // Arrange — 7 flights; retrieve page 2 with pageSize 5 (returns 2 items)
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_LastPage_TotalReflectsFullCount));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        for (var i = 0; i < 7; i++) db.FlightPreparations.Add(MakeFlight("u1"));
        await db.SaveChangesAsync();

        // Act
        var (items, total) = await sut.GetSummariesPagedAsync("u1", false, "alle", 2, 5);

        // Assert
        Assert.Equal(7, total);    // full count, not just this page
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_NullUserIdNonAdmin_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_NullUserIdNonAdmin_ReturnsEmpty));
        var sut     = BuildSut(factory);
        await using var db = await factory.CreateDbContextAsync();
        db.FlightPreparations.Add(MakeFlight("u1"));
        await db.SaveChangesAsync();

        // Act
        var (items, total) = await sut.GetSummariesPagedAsync(null, isAdmin: false, "alle", 1, 10);

        // Assert — non-admin with null userId always returns nothing
        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetSummariesPagedAsync_ZeroPage_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_ZeroPage_ThrowsArgumentOutOfRangeException));
        var sut     = BuildSut(factory);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.GetSummariesPagedAsync("u1", false, "alle", 0, 10));
    }

    [Fact]
    public async Task GetSummariesPagedAsync_ZeroPageSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesPagedAsync_ZeroPageSize_ThrowsArgumentOutOfRangeException));
        var sut     = BuildSut(factory);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.GetSummariesPagedAsync("u1", false, "alle", 1, 0));
    }
}
