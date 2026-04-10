using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Tests;

/// <summary>
///     Integration tests for <see cref="LocationService" /> using the EF Core in-memory provider.
///     Each test gets a unique named database to ensure full isolation.
/// </summary>
public class LocationServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static IDbContextFactory<AppDbContext> CreateFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
             .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    private static LocationService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory, NullLogger<LocationService>.Instance);

    private static async Task SeedLocationsAsync(IDbContextFactory<AppDbContext> factory, IEnumerable<Location> locations)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Locations.AddRange(locations);
        await db.SaveChangesAsync();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_AdminUser_ReturnsAllLocations()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_AdminUser_ReturnsAllLocations));
        await SeedLocationsAsync(factory, [
            new Location { Name = "Hasselt", OwnerId = "user-1" },
            new Location { Name = "Genk",    OwnerId = "user-2" }
        ]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: "user-1", isAdmin: true);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_NonAdminUser_ReturnsOnlyOwnLocations()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_NonAdminUser_ReturnsOnlyOwnLocations));
        await SeedLocationsAsync(factory, [
            new Location { Name = "Hasselt", OwnerId = "user-1" },
            new Location { Name = "Genk",    OwnerId = "user-2" }
        ]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: "user-1", isAdmin: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("Hasselt", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_NullUserId_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_NullUserId_ReturnsEmpty));
        await SeedLocationsAsync(factory, [new Location { Name = "Hasselt", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: null, isAdmin: false);

        // Assert
        Assert.Empty(result);
    }

    // ── AddAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidUser_AddsLocationWithOwnerId()
    {
        // Arrange
        var factory = CreateFactory(nameof(AddAsync_ValidUser_AddsLocationWithOwnerId));
        var sut = BuildSut(factory);
        var location = new Location { Name = "Bruges" };

        // Act
        await sut.AddAsync(location, "user-42");

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.Locations.SingleAsync();
        Assert.Equal("Bruges", saved.Name);
        Assert.Equal("user-42", saved.OwnerId);
    }

    [Fact]
    public async Task AddAsync_NullUserId_DoesNotInsert()
    {
        // Arrange
        var factory = CreateFactory(nameof(AddAsync_NullUserId_DoesNotInsert));
        var sut = BuildSut(factory);

        // Act
        await sut.AddAsync(new Location { Name = "Ghost Town" }, userId: null);

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        Assert.Empty(db.Locations);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Owner_UpdatesLocation()
    {
        // Arrange
        var factory = CreateFactory(nameof(UpdateAsync_Owner_UpdatesLocation));
        await SeedLocationsAsync(factory, [new Location { Name = "OldName", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Locations.SingleAsync();
        var edit = new Location { Id = existing.Id, Name = "NewName", OwnerId = "user-1" };

        // Act
        await sut.UpdateAsync(edit, "user-1", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        var updated = await db2.Locations.FindAsync(existing.Id);
        Assert.Equal("NewName", updated!.Name);
        Assert.Equal("user-1", updated.OwnerId); // OwnerId must not change
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_DoesNotUpdateLocation()
    {
        // Arrange
        var factory = CreateFactory(nameof(UpdateAsync_NonOwner_DoesNotUpdateLocation));
        await SeedLocationsAsync(factory, [new Location { Name = "OldName", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Locations.SingleAsync();
        var edit = new Location { Id = existing.Id, Name = "HackedName", OwnerId = "user-2" };

        // Act
        await sut.UpdateAsync(edit, "user-2", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        var unchanged = await db2.Locations.FindAsync(existing.Id);
        Assert.Equal("OldName", unchanged!.Name);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Owner_DeletesLocation()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_Owner_DeletesLocation));
        await SeedLocationsAsync(factory, [new Location { Name = "ToDelete", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Locations.SingleAsync()).Id;

        // Act
        await sut.DeleteAsync(id, "user-1", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Empty(db2.Locations);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_DoesNotDeleteLocation()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_NonOwner_DoesNotDeleteLocation));
        await SeedLocationsAsync(factory, [new Location { Name = "ToKeep", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Locations.SingleAsync()).Id;

        // Act
        await sut.DeleteAsync(id, "user-2", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Single(db2.Locations);
    }

    [Fact]
    public async Task DeleteAsync_AdminUser_DeletesAnyLocation()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_AdminUser_DeletesAnyLocation));
        await SeedLocationsAsync(factory, [new Location { Name = "AdminTarget", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Locations.SingleAsync()).Id;

        // Act — admin with a different userId
        await sut.DeleteAsync(id, "admin-99", isAdmin: true);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Empty(db2.Locations);
    }
}
