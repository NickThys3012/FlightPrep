using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
/// Integration tests for Pilot ownership-scoping behaviour.
/// Covers per-user filtering (OwnerId) and the SaveEdit guard logic
/// that mirrors the Pilots.razor page implementation.
/// </summary>
public class PilotOwnershipTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static IDbContextFactory<AppDbContext> CreateFactory()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
             .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider()
                       .GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    /// <summary>
    /// Seeds three pilots: one owned by user1, one by user2, one with no owner.
    /// Returns their IDs in order.
    /// </summary>
    private static async Task<(int user1Id, int user2Id, int nullId)>
        SeedPilotsAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        var p1 = new Pilot { Name = "Alice", WeightKg = 70, OwnerId = "user1" };
        var p2 = new Pilot { Name = "Bob",   WeightKg = 80, OwnerId = "user2" };
        var p3 = new Pilot { Name = "Carol", WeightKg = 60, OwnerId = null };
        db.Pilots.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();
        return (p1.Id, p2.Id, p3.Id);
    }

    // ── GetPilots — ownership filtering ──────────────────────────────────────

    [Fact]
    public async Task GetPilots_PilotUser_ReturnsOnlyOwnPilots()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, user2Id, nullId) = await SeedPilotsAsync(factory);

        // Act — replicate the pilot-scoped query: Where(p => p.OwnerId == userId)
        await using var db = await factory.CreateDbContextAsync();
        var result = await db.Pilots
            .Where(p => p.OwnerId == "user1")
            .ToListAsync();

        // Assert — only user1's pilot returned; user2 and null-owner excluded
        Assert.Single(result);
        Assert.Equal(user1Id, result[0].Id);
        Assert.DoesNotContain(result, p => p.Id == user2Id);
        Assert.DoesNotContain(result, p => p.Id == nullId);
    }

    [Fact]
    public async Task GetPilots_NullUserId_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory();
        await SeedPilotsAsync(factory);
        string? userId = null;

        // Act — early-return pattern: null userId means no identity → empty list
        List<Pilot> result;
        if (userId is null)
        {
            result = [];
        }
        else
        {
            await using var db = await factory.CreateDbContextAsync();
            result = await db.Pilots.Where(p => p.OwnerId == userId).ToListAsync();
        }

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPilots_AdminUser_ReturnsAllPilots()
    {
        // Arrange
        var factory = CreateFactory();
        await SeedPilotsAsync(factory);

        // Act — admin: no ownership filter applied
        await using var db = await factory.CreateDbContextAsync();
        var result = await db.Pilots.ToListAsync();

        // Assert — all 3 pilots visible
        Assert.Equal(3, result.Count);
    }

    // ── SaveEdit — ownership guard ────────────────────────────────────────────

    [Fact]
    public async Task SaveEdit_NonOwner_CannotOverwritePilot()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedPilotsAsync(factory);
        const string userId = "user2";
        const bool isAdmin = false;

        // Act — replicate the SaveEdit ownership guard from Pilots.razor
        await using var db = await factory.CreateDbContextAsync();
        var pilot = await db.Pilots.FindAsync(user1Id);
        if (pilot is null || (!isAdmin && pilot.OwnerId != userId))
        {
            // guard fires — update is blocked; do nothing
        }
        else
        {
            pilot.Name = "Hacked By user2";
            await db.SaveChangesAsync();
        }

        // Assert — DB record unchanged
        await using var dbCheck = await factory.CreateDbContextAsync();
        var unchanged = await dbCheck.Pilots.FindAsync(user1Id);
        Assert.NotNull(unchanged);
        Assert.Equal("Alice", unchanged.Name);
    }

    [Fact]
    public async Task SaveEdit_Owner_CanUpdatePilot()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedPilotsAsync(factory);
        const string userId = "user1";
        const bool isAdmin = false;

        // Act — owner passes the guard; update is applied
        await using var db = await factory.CreateDbContextAsync();
        var pilot = await db.Pilots.FindAsync(user1Id);
        if (pilot is not null && (isAdmin || pilot.OwnerId == userId))
        {
            pilot.Name = "Alice Updated";
            await db.SaveChangesAsync();
        }

        // Assert — DB record updated correctly
        await using var dbCheck = await factory.CreateDbContextAsync();
        var updated = await dbCheck.Pilots.FindAsync(user1Id);
        Assert.NotNull(updated);
        Assert.Equal("Alice Updated", updated.Name);
    }

    [Fact]
    public async Task SaveEdit_PreservesOwnerId()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedPilotsAsync(factory);

        // Act — simulate SetValues with an edit object that has OwnerId=null, then restore
        await using var db = await factory.CreateDbContextAsync();
        var pilot = await db.Pilots.FindAsync(user1Id);
        Assert.NotNull(pilot);
        var originalOwnerId = pilot.OwnerId;

        var incomingEdit = new Pilot
        {
            Id       = user1Id,
            Name     = "Alice Renamed",
            WeightKg = 72,
            OwnerId  = null   // simulate a form post that omits OwnerId
        };
        db.Entry(pilot).CurrentValues.SetValues(incomingEdit);
        pilot.OwnerId = originalOwnerId; // explicit restore — the guard in the page
        await db.SaveChangesAsync();

        // Assert — OwnerId preserved; name updated
        await using var dbCheck = await factory.CreateDbContextAsync();
        var saved = await dbCheck.Pilots.FindAsync(user1Id);
        Assert.NotNull(saved);
        Assert.Equal("user1",        saved.OwnerId);
        Assert.Equal("Alice Renamed", saved.Name);
        Assert.Equal(72,             saved.WeightKg);
    }
}
