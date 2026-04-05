using FlightPrep.Data;
using FlightPrep.Models;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="GoNoGoService"/> per-user Get/Save logic.
/// Each test gets a unique in-memory database for full isolation.
/// </summary>
public class GoNoGoServiceTests
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

    private static GoNoGoService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    // ── GetSettingsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsAsync_NewUser_ReturnsDefaultSettings()
    {
        // Arrange — no row in DB for this userId
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetSettingsAsync("brand-new-user");

        // Assert — must return defaults (not null)
        Assert.NotNull(result);
        Assert.Equal(15,  result.WindRedKt);
        Assert.Equal(10,  result.WindYellowKt);
        Assert.Equal(3,   result.VisRedKm);
        Assert.Equal(5,   result.VisYellowKm);
        Assert.Equal(500, result.CapeRedJkg);
        Assert.Equal(300, result.CapeYellowJkg);
    }

    [Fact]
    public async Task GetSettingsAsync_ExistingUser_ReturnsPersistedSettings()
    {
        // Arrange — save custom settings for user1, then get them back
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var custom = new GoNoGoSettings
        {
            WindRedKt     = 20,
            WindYellowKt  = 12,
            VisRedKm      = 2,
            VisYellowKm   = 4,
            CapeRedJkg    = 600,
            CapeYellowJkg = 400
        };
        await sut.SaveSettingsAsync(custom, "user1");

        // Act
        var result = await sut.GetSettingsAsync("user1");

        // Assert — custom values must be returned, not defaults
        Assert.NotNull(result);
        Assert.Equal(20,  result.WindRedKt);
        Assert.Equal(12,  result.WindYellowKt);
        Assert.Equal(2,   result.VisRedKm);
        Assert.Equal(4,   result.VisYellowKm);
        Assert.Equal(600, result.CapeRedJkg);
        Assert.Equal(400, result.CapeYellowJkg);
    }

    [Fact]
    public async Task GetSettingsAsync_NullUserId_ReturnsDefaultSettings()
    {
        // Arrange — no row in DB; null userId should give global defaults
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        // Act
        var result = await sut.GetSettingsAsync(null);

        // Assert — defaults returned without exception
        Assert.NotNull(result);
        Assert.Equal(15,  result.WindRedKt);
        Assert.Equal(10,  result.WindYellowKt);
        Assert.Equal(3,   result.VisRedKm);
        Assert.Equal(5,   result.VisYellowKm);
        Assert.Equal(500, result.CapeRedJkg);
        Assert.Equal(300, result.CapeYellowJkg);
    }

    [Fact]
    public async Task GetSettingsAsync_DifferentUsers_GetDifferentSettings()
    {
        // Arrange — save different WindRedKt for user1 and user2
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 20 }, "user1");
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 25 }, "user2");

        // Act
        var r1 = await sut.GetSettingsAsync("user1");
        var r2 = await sut.GetSettingsAsync("user2");

        // Assert — each user gets their own values
        Assert.Equal(20, r1.WindRedKt);
        Assert.Equal(25, r2.WindRedKt);
    }

    // ── SaveSettingsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveSettingsAsync_NewUser_InsertsRow()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        // Act
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 18, WindYellowKt = 11 }, "new-user");

        // Assert — row exists in DB with correct values
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.GoNoGoSettings.FirstOrDefaultAsync(g => g.UserId == "new-user");
        Assert.NotNull(row);
        Assert.Equal(18, row.WindRedKt);
        Assert.Equal(11, row.WindYellowKt);
    }

    [Fact]
    public async Task SaveSettingsAsync_ExistingUser_UpdatesRow()
    {
        // Arrange — first save
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 15 }, "user1");

        // Act — second save with different value
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 22 }, "user1");

        // Assert — exactly 1 row with the latest value (not 2 rows)
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.GoNoGoSettings.Where(g => g.UserId == "user1").ToListAsync();
        Assert.Single(rows);
        Assert.Equal(22, rows[0].WindRedKt);
    }

    [Fact]
    public async Task SaveSettingsAsync_ResetToDefaults_PreservesIdAndUserId()
    {
        // Arrange — save custom settings and capture the assigned DB id
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 20 }, "user1");

        await using var dbRead = await factory.CreateDbContextAsync();
        var original = await dbRead.GoNoGoSettings.FirstAsync(g => g.UserId == "user1");
        var originalId = original.Id;

        // Act — reset using a brand-new object (Id=0, default thresholds)
        await sut.SaveSettingsAsync(new GoNoGoSettings { UserId = "user1" }, "user1");

        // Assert — same PK, correct UserId, default thresholds
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.GoNoGoSettings.FirstOrDefaultAsync(g => g.UserId == "user1");
        Assert.NotNull(row);
        Assert.Equal(originalId, row.Id);      // Id must NOT be 0 or a new PK
        Assert.Equal("user1",    row.UserId);  // UserId must be preserved
        Assert.Equal(15,         row.WindRedKt); // default value
    }

    [Fact]
    public async Task SaveSettingsAsync_NullUserId_InsertsOrUpdatesNullRow()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        // Act — save twice with null userId
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 18 }, null);
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 22 }, null);

        // Assert — exactly 1 row with UserId=null carrying the latest value
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.GoNoGoSettings.Where(g => g.UserId == null).ToListAsync();
        Assert.Single(rows);
        Assert.Null(rows[0].UserId);
        Assert.Equal(22, rows[0].WindRedKt);
    }
}
