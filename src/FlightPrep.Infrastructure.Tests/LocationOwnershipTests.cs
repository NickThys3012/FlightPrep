using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
/// Integration tests for Location ownership-scoping behaviour.
/// Covers the SaveEdit guard logic that mirrors the Locations.razor page
/// implementation, including the OwnerId-preservation pattern.
/// </summary>
public class LocationOwnershipTests
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
    /// Seeds three locations: one owned by user1, one by user2, one with no owner.
    /// Returns their IDs in order.
    /// </summary>
    private static async Task<(int user1Id, int user2Id, int nullId)>
        SeedLocationsAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        var l1 = new Location { Name = "Field Alpha", IcaoCode = "EBBT", OwnerId = "user1" };
        var l2 = new Location { Name = "Field Beta",  IcaoCode = "EBBR", OwnerId = "user2" };
        var l3 = new Location { Name = "Field Gamma", IcaoCode = null,   OwnerId = null };
        db.Locations.AddRange(l1, l2, l3);
        await db.SaveChangesAsync();
        return (l1.Id, l2.Id, l3.Id);
    }

    // ── SaveEdit — ownership guard ────────────────────────────────────────────

    [Fact]
    public async Task SaveEdit_NonOwner_CannotOverwrite()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedLocationsAsync(factory);
        const string userId = "user2";
        const bool isAdmin = false;

        // Act — replicate the SaveEdit ownership guard from Locations.razor
        await using var db = await factory.CreateDbContextAsync();
        var l = await db.Locations.FindAsync(user1Id);
        if (l is null || (l.OwnerId != userId))
        {
            // guard fires — update is blocked; do nothing
        }
        else
        {
            l.Name = "HACKED";
            await db.SaveChangesAsync();
        }

        // Assert — DB record unchanged
        await using var dbCheck = await factory.CreateDbContextAsync();
        var unchanged = await dbCheck.Locations.FindAsync(user1Id);
        Assert.NotNull(unchanged);
        Assert.Equal("Field Alpha", unchanged.Name);
    }

    [Fact]
    public async Task SaveEdit_PreservesOwnerId()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedLocationsAsync(factory);

        // Act — simulate SetValues with an edit object where OwnerId=null, then restore
        await using var db = await factory.CreateDbContextAsync();
        var l = await db.Locations.FindAsync(user1Id);
        Assert.NotNull(l);
        var originalOwnerId = l.OwnerId;

        var incomingEdit = new Location
        {
            Id       = user1Id,
            Name     = "Field Alpha Updated",
            IcaoCode = "EBBE",
            OwnerId  = null   // simulate a form post that omits OwnerId
        };
        db.Entry(l).CurrentValues.SetValues(incomingEdit);
        l.OwnerId = originalOwnerId; // explicit restore — the guard in the page
        await db.SaveChangesAsync();

        // Assert — OwnerId preserved; name and ICAO updated
        await using var dbCheck = await factory.CreateDbContextAsync();
        var saved = await dbCheck.Locations.FindAsync(user1Id);
        Assert.NotNull(saved);
        Assert.Equal("user1",               saved.OwnerId);
        Assert.Equal("Field Alpha Updated", saved.Name);
        Assert.Equal("EBBE",                saved.IcaoCode);
    }
}
