using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Tests;

/// <summary>
///     Integration tests for <see cref="PilotService" /> using the EF Core in-memory provider.
///     Each test gets a unique named database to ensure full isolation.
/// </summary>
public class PilotServiceTests
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

    private static PilotService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory, NullLogger<PilotService>.Instance);

    private static async Task SeedPilotsAsync(IDbContextFactory<AppDbContext> factory, IEnumerable<Pilot> pilots)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Pilots.AddRange(pilots);
        await db.SaveChangesAsync();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_AdminUser_ReturnsAllPilots()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_AdminUser_ReturnsAllPilots));
        await SeedPilotsAsync(factory, [
            new Pilot { Name = "Alice", OwnerId = "user-1" },
            new Pilot { Name = "Bob",   OwnerId = "user-2" }
        ]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: "user-1", isAdmin: true);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_NonAdminUser_ReturnsOnlyOwnPilots()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_NonAdminUser_ReturnsOnlyOwnPilots));
        await SeedPilotsAsync(factory, [
            new Pilot { Name = "Alice", OwnerId = "user-1" },
            new Pilot { Name = "Bob",   OwnerId = "user-2" }
        ]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: "user-1", isAdmin: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("Alice", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_NullUserId_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_NullUserId_ReturnsEmpty));
        await SeedPilotsAsync(factory, [new Pilot { Name = "Alice", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: null, isAdmin: false);

        // Assert
        Assert.Empty(result);
    }

    // ── AddAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidUser_AddsPilotWithOwnerId()
    {
        // Arrange
        var factory = CreateFactory(nameof(AddAsync_ValidUser_AddsPilotWithOwnerId));
        var sut = BuildSut(factory);
        var pilot = new Pilot { Name = "Charlie" };

        // Act
        await sut.AddAsync(pilot, "user-42");

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.Pilots.SingleAsync();
        Assert.Equal("Charlie", saved.Name);
        Assert.Equal("user-42", saved.OwnerId);
    }

    [Fact]
    public async Task AddAsync_NullUserId_DoesNotInsert()
    {
        // Arrange
        var factory = CreateFactory(nameof(AddAsync_NullUserId_DoesNotInsert));
        var sut = BuildSut(factory);

        // Act
        await sut.AddAsync(new Pilot { Name = "Ghost" }, userId: null);

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        Assert.Empty(db.Pilots);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Owner_UpdatesPilot()
    {
        // Arrange
        var factory = CreateFactory(nameof(UpdateAsync_Owner_UpdatesPilot));
        await SeedPilotsAsync(factory, [new Pilot { Name = "Original", WeightKg = 70, OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Pilots.SingleAsync();
        var edit = new Pilot { Id = existing.Id, Name = "Updated", WeightKg = 80, OwnerId = "user-1" };

        // Act
        await sut.UpdateAsync(edit, "user-1", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        var updated = await db2.Pilots.FindAsync(existing.Id);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal(80, updated.WeightKg);
        Assert.Equal("user-1", updated.OwnerId); // OwnerId must not change
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_DoesNotUpdatePilot()
    {
        // Arrange
        var factory = CreateFactory(nameof(UpdateAsync_NonOwner_DoesNotUpdatePilot));
        await SeedPilotsAsync(factory, [new Pilot { Name = "Original", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Pilots.SingleAsync();
        var edit = new Pilot { Id = existing.Id, Name = "Hacked", OwnerId = "user-2" };

        // Act
        await sut.UpdateAsync(edit, "user-2", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        var unchanged = await db2.Pilots.FindAsync(existing.Id);
        Assert.Equal("Original", unchanged!.Name);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Owner_DeletesPilot()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_Owner_DeletesPilot));
        await SeedPilotsAsync(factory, [new Pilot { Name = "Alice", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Pilots.SingleAsync()).Id;

        // Act
        await sut.DeleteAsync(id, "user-1", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Empty(db2.Pilots);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_DoesNotDeletePilot()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_NonOwner_DoesNotDeletePilot));
        await SeedPilotsAsync(factory, [new Pilot { Name = "Alice", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Pilots.SingleAsync()).Id;

        // Act
        await sut.DeleteAsync(id, "user-2", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Single(db2.Pilots);
    }

    [Fact]
    public async Task DeleteAsync_AdminUser_DeletesAnyPilot()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_AdminUser_DeletesAnyPilot));
        await SeedPilotsAsync(factory, [new Pilot { Name = "Alice", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Pilots.SingleAsync()).Id;

        // Act — admin with a different userId
        await sut.DeleteAsync(id, "admin-99", isAdmin: true);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Empty(db2.Pilots);
    }
}
