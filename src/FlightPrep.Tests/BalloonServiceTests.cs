using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Tests;

/// <summary>
///     Integration tests for <see cref="BalloonService" /> using the EF Core in-memory provider.
///     Each test gets a unique named database to ensure full isolation.
/// </summary>
public class BalloonServiceTests
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

    private static BalloonService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory, NullLogger<BalloonService>.Instance);

    private static async Task SeedBalloonsAsync(IDbContextFactory<AppDbContext> factory, IEnumerable<Balloon> balloons)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Balloons.AddRange(balloons);
        await db.SaveChangesAsync();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_AdminUser_ReturnsAllBalloons()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_AdminUser_ReturnsAllBalloons));
        await SeedBalloonsAsync(factory, [
            new Balloon { Registration = "OO-A01", Type = "BB20", OwnerId = "user-1" },
            new Balloon { Registration = "OO-B02", Type = "BB30", OwnerId = "user-2" }
        ]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: "user-1", isAdmin: true);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_NonAdminUser_ReturnsOnlyOwnBalloons()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_NonAdminUser_ReturnsOnlyOwnBalloons));
        await SeedBalloonsAsync(factory, [
            new Balloon { Registration = "OO-A01", Type = "BB20", OwnerId = "user-1" },
            new Balloon { Registration = "OO-B02", Type = "BB30", OwnerId = "user-2" }
        ]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: "user-1", isAdmin: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("OO-A01", result[0].Registration);
    }

    [Fact]
    public async Task GetAllAsync_NullUserId_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetAllAsync_NullUserId_ReturnsEmpty));
        await SeedBalloonsAsync(factory, [new Balloon { Registration = "OO-A01", Type = "BB20", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetAllAsync(userId: null, isAdmin: false);

        // Assert
        Assert.Empty(result);
    }

    // ── AddAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidUser_AddsBalloonWithOwnerId()
    {
        // Arrange
        var factory = CreateFactory(nameof(AddAsync_ValidUser_AddsBalloonWithOwnerId));
        var sut = BuildSut(factory);
        var balloon = new Balloon { Registration = "OO-NEW", Type = "BB25" };

        // Act
        await sut.AddAsync(balloon, "user-42");

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        var saved = await db.Balloons.SingleAsync();
        Assert.Equal("OO-NEW", saved.Registration);
        Assert.Equal("user-42", saved.OwnerId);
    }

    [Fact]
    public async Task AddAsync_NullUserId_DoesNotInsert()
    {
        // Arrange
        var factory = CreateFactory(nameof(AddAsync_NullUserId_DoesNotInsert));
        var sut = BuildSut(factory);

        // Act
        await sut.AddAsync(new Balloon { Registration = "OO-GHOST", Type = "BB20" }, userId: null);

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        Assert.Empty(db.Balloons);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Owner_UpdatesBalloon()
    {
        // Arrange
        var factory = CreateFactory(nameof(UpdateAsync_Owner_UpdatesBalloon));
        await SeedBalloonsAsync(factory, [new Balloon { Registration = "OO-OLD", Type = "BB20", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Balloons.SingleAsync();
        var edit = new Balloon { Id = existing.Id, Registration = "OO-NEW", Type = "BB30", OwnerId = "user-1" };

        // Act
        await sut.UpdateAsync(edit, "user-1", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        var updated = await db2.Balloons.FindAsync(existing.Id);
        Assert.Equal("OO-NEW", updated!.Registration);
        Assert.Equal("BB30", updated.Type);
        Assert.Equal("user-1", updated.OwnerId); // OwnerId must not change
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_DoesNotUpdateBalloon()
    {
        // Arrange
        var factory = CreateFactory(nameof(UpdateAsync_NonOwner_DoesNotUpdateBalloon));
        await SeedBalloonsAsync(factory, [new Balloon { Registration = "OO-OLD", Type = "BB20", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Balloons.SingleAsync();
        var edit = new Balloon { Id = existing.Id, Registration = "OO-HACKED", Type = "BB99", OwnerId = "user-2" };

        // Act
        await sut.UpdateAsync(edit, "user-2", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        var unchanged = await db2.Balloons.FindAsync(existing.Id);
        Assert.Equal("OO-OLD", unchanged!.Registration);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Owner_DeletesBalloon()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_Owner_DeletesBalloon));
        await SeedBalloonsAsync(factory, [new Balloon { Registration = "OO-DEL", Type = "BB20", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Balloons.SingleAsync()).Id;

        // Act
        await sut.DeleteAsync(id, "user-1", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Empty(db2.Balloons);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_DoesNotDeleteBalloon()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_NonOwner_DoesNotDeleteBalloon));
        await SeedBalloonsAsync(factory, [new Balloon { Registration = "OO-KEEP", Type = "BB20", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Balloons.SingleAsync()).Id;

        // Act
        await sut.DeleteAsync(id, "user-2", isAdmin: false);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Single(db2.Balloons);
    }

    [Fact]
    public async Task DeleteAsync_AdminUser_DeletesAnyBalloon()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_AdminUser_DeletesAnyBalloon));
        await SeedBalloonsAsync(factory, [new Balloon { Registration = "OO-ADM", Type = "BB20", OwnerId = "user-1" }]);
        var sut = BuildSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var id = (await db.Balloons.SingleAsync()).Id;

        // Act — admin with a different userId
        await sut.DeleteAsync(id, "admin-99", isAdmin: true);

        // Assert
        await using var db2 = await factory.CreateDbContextAsync();
        Assert.Empty(db2.Balloons);
    }
}
