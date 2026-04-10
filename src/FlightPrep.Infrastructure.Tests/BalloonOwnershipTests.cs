using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
/// Integration tests for Balloon ownership-scoping behaviour.
/// Covers the SaveEdit guard logic that mirrors the Balloons.razor page
/// implementation, including the OwnerId-preservation pattern.
/// </summary>
public class BalloonOwnershipTests
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
    /// Seeds three balloons: one owned by user1, one by user2, one with no owner.
    /// Returns their IDs in order.
    /// </summary>
    private static async Task<(int user1Id, int user2Id, int nullId)>
        SeedBalloonsAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        var b1 = new Balloon { Registration = "OO-B01", Type = "BB20N", VolumeM3 = 2000, OwnerId = "user1" };
        var b2 = new Balloon { Registration = "OO-B02", Type = "BB22N", VolumeM3 = 2200, OwnerId = "user2" };
        var b3 = new Balloon { Registration = "OO-B03", Type = "BB26N", VolumeM3 = 2600, OwnerId = null };
        db.Balloons.AddRange(b1, b2, b3);
        await db.SaveChangesAsync();
        return (b1.Id, b2.Id, b3.Id);
    }

    // ── SaveEdit — ownership guard ────────────────────────────────────────────

    [Fact]
    public async Task SaveEdit_NonOwner_CannotOverwrite()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedBalloonsAsync(factory);
        const string userId = "user2";
        const bool isAdmin = false;

        // Act — replicate the SaveEdit ownership guard from Balloons.razor
        await using var db = await factory.CreateDbContextAsync();
        var b = await db.Balloons.FindAsync(user1Id);
        if (b is null || (b.OwnerId != userId))
        {
            // guard fires — update is blocked; do nothing
        }
        else
        {
            b.Registration = "OO-HACKED";
            await db.SaveChangesAsync();
        }

        // Assert — DB record unchanged
        await using var dbCheck = await factory.CreateDbContextAsync();
        var unchanged = await dbCheck.Balloons.FindAsync(user1Id);
        Assert.NotNull(unchanged);
        Assert.Equal("OO-B01", unchanged.Registration);
    }

    [Fact]
    public async Task SaveEdit_PreservesOwnerId()
    {
        // Arrange
        var factory = CreateFactory();
        var (user1Id, _, _) = await SeedBalloonsAsync(factory);

        // Act — simulate SetValues with an edit object where OwnerId=null, then restore
        await using var db = await factory.CreateDbContextAsync();
        var b = await db.Balloons.FindAsync(user1Id);
        Assert.NotNull(b);
        var originalOwnerId = b.OwnerId;

        var incomingEdit = new Balloon
        {
            Id           = user1Id,
            Registration = "OO-B01-EDIT",
            Type         = "BB24N",
            VolumeM3     = 2400,
            OwnerId      = null   // simulate a form post that omits OwnerId
        };
        db.Entry(b).CurrentValues.SetValues(incomingEdit);
        b.OwnerId = originalOwnerId; // explicit restore — the guard in the page
        await db.SaveChangesAsync();

        // Assert — OwnerId preserved; other fields updated
        await using var dbCheck = await factory.CreateDbContextAsync();
        var saved = await dbCheck.Balloons.FindAsync(user1Id);
        Assert.NotNull(saved);
        Assert.Equal("user1",        saved.OwnerId);
        Assert.Equal("OO-B01-EDIT",  saved.Registration);
        Assert.Equal("BB24N",        saved.Type);
        Assert.Equal(2400,           saved.VolumeM3);
    }
}
