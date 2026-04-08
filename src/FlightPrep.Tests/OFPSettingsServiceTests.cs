using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Tests;

/// <summary>
///     Integration tests for OFPSettingsService using an EF Core InMemory database.
/// </summary>
public class OFPSettingsServiceTests
{
    private static TestDbContextFactory CreateFactory(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestDbContextFactory(opts);
    }

    // ── GetSettingsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsAsync_NoRowInDb_ReturnsDefaultWith7KgOffset()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSettingsAsync_NoRowInDb_ReturnsDefaultWith7KgOffset));
        var sut = new OFPSettingsService(factory);

        // Act
        var result = await sut.GetSettingsAsync("any-user");

        // Assert – falls back to new OFPSettings() which has default 7
        Assert.NotNull(result);
        Assert.Equal(7, result.PassengerEquipmentWeightKg);
    }

    [Fact]
    public async Task GetSettingsAsync_GlobalDefaultRowExists_ReturnsItForNullUser()
    {
        // Arrange – seed a UserId=null row
        var factory = CreateFactory(nameof(GetSettingsAsync_GlobalDefaultRowExists_ReturnsItForNullUser));
        await using (var db = factory.CreateDbContext())
        {
            db.OfpSettings.Add(new OFPSettings { UserId = null, PassengerEquipmentWeightKg = 12 });
            await db.SaveChangesAsync();
        }
        var sut = new OFPSettingsService(factory);

        // Act
        var result = await sut.GetSettingsAsync(null);

        // Assert
        Assert.Equal(12, result.PassengerEquipmentWeightKg);
    }

    [Fact]
    public async Task GetSettingsAsync_PerUserRowExists_ReturnsItForThatUser()
    {
        // Arrange – seed a per-user row
        var factory = CreateFactory(nameof(GetSettingsAsync_PerUserRowExists_ReturnsItForThatUser));
        await using (var db = factory.CreateDbContext())
        {
            db.OfpSettings.Add(new OFPSettings { UserId = "user-42", PassengerEquipmentWeightKg = 9 });
            await db.SaveChangesAsync();
        }
        var sut = new OFPSettingsService(factory);

        // Act
        var result = await sut.GetSettingsAsync("user-42");

        // Assert
        Assert.Equal(9, result.PassengerEquipmentWeightKg);
    }

    [Fact]
    public async Task GetSettingsAsync_NoPerUserRow_FallsBackToGlobalDefault()
    {
        // Arrange – seed only a global (UserId=null) row
        var factory = CreateFactory(nameof(GetSettingsAsync_NoPerUserRow_FallsBackToGlobalDefault));
        await using (var db = factory.CreateDbContext())
        {
            db.OfpSettings.Add(new OFPSettings { UserId = null, PassengerEquipmentWeightKg = 15 });
            await db.SaveChangesAsync();
        }
        var sut = new OFPSettingsService(factory);

        // Act – query for a user that has no dedicated row
        var result = await sut.GetSettingsAsync("user-without-settings");

        // Assert – global default returned
        Assert.Equal(15, result.PassengerEquipmentWeightKg);
    }

    // ── SaveSettingsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveSettingsAsync_NewUser_InsertsRow()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveSettingsAsync_NewUser_InsertsRow));
        var sut = new OFPSettingsService(factory);
        var settings = new OFPSettings { PassengerEquipmentWeightKg = 11 };

        // Act
        await sut.SaveSettingsAsync(settings, "new-user");

        // Assert – row exists in DB with correct value
        await using var db = factory.CreateDbContext();
        var row = await db.OfpSettings.FirstOrDefaultAsync(o => o.UserId == "new-user");
        Assert.NotNull(row);
        Assert.Equal(11, row.PassengerEquipmentWeightKg);
    }

    [Fact]
    public async Task SaveSettingsAsync_ExistingUser_UpdatesRow()
    {
        // Arrange – pre-insert a row for the user
        var factory = CreateFactory(nameof(SaveSettingsAsync_ExistingUser_UpdatesRow));
        var sut = new OFPSettingsService(factory);
        await sut.SaveSettingsAsync(new OFPSettings { PassengerEquipmentWeightKg = 8 }, "existing-user");

        // Act – update with a new value
        await sut.SaveSettingsAsync(new OFPSettings { PassengerEquipmentWeightKg = 20 }, "existing-user");

        // Assert – value changed and no duplicate row created
        await using var db = factory.CreateDbContext();
        var rows = await db.OfpSettings.Where(o => o.UserId == "existing-user").ToListAsync();
        Assert.Single(rows);
        Assert.Equal(20, rows[0].PassengerEquipmentWeightKg);
    }

    [Fact]
    public async Task SaveSettingsAsync_FirstSaveAfterFallback_DoesNotCollideWithGlobalRow()
    {
        // Arrange – seed a global default row (UserId=null)
        var factory = CreateFactory(nameof(SaveSettingsAsync_FirstSaveAfterFallback_DoesNotCollideWithGlobalRow));
        await using (var db = factory.CreateDbContext())
        {
            db.OfpSettings.Add(new OFPSettings { UserId = null, PassengerEquipmentWeightKg = 7 });
            await db.SaveChangesAsync();
        }
        var sut = new OFPSettingsService(factory);

        // Act – save for a new user; must not throw a DbUpdateException / PK collision
        var exception = await Record.ExceptionAsync(
            () => sut.SaveSettingsAsync(new OFPSettings { PassengerEquipmentWeightKg = 10 }, "brand-new-user"));

        // Assert – no exception and two rows exist
        Assert.Null(exception);
        await using var db2 = factory.CreateDbContext();
        Assert.Equal(2, await db2.OfpSettings.CountAsync());
    }

    [Fact]
    public async Task SaveSettingsAsync_NullUserId_UpsertGlobalDefault()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveSettingsAsync_NullUserId_UpsertGlobalDefault));
        var sut = new OFPSettingsService(factory);

        // Act – save twice for null userId
        await sut.SaveSettingsAsync(new OFPSettings { PassengerEquipmentWeightKg = 5 }, null);
        await sut.SaveSettingsAsync(new OFPSettings { PassengerEquipmentWeightKg = 6 }, null);

        // Assert – exactly one row, updated value
        await using var db = factory.CreateDbContext();
        var rows = await db.OfpSettings.Where(o => o.UserId == null).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(6, rows[0].PassengerEquipmentWeightKg);
    }

    // ── Cascade delete ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_CascadesOFPSettingsRow()
    {
        // Arrange – seed a user + an OFP settings row linked to that user
        const string userId = "user-cascade-test";
        var factory = CreateFactory(nameof(DeleteUser_CascadesOFPSettingsRow));

        await using (var db = factory.CreateDbContext())
        {
            db.Users.Add(new ApplicationUser
            {
                Id                 = userId,
                UserName           = "cascade@test.be",
                NormalizedUserName = "CASCADE@TEST.BE",
                Email              = "cascade@test.be",
                NormalizedEmail    = "CASCADE@TEST.BE",
                SecurityStamp      = Guid.NewGuid().ToString()
            });
            db.OfpSettings.Add(new OFPSettings
            {
                UserId                      = userId,
                PassengerEquipmentWeightKg  = 7
            });
            await db.SaveChangesAsync();
        }

        // Act – load both entities into a fresh context so EF's change-tracker
        //       can apply the cascade, then delete the user
        await using (var db = factory.CreateDbContext())
        {
            // Load OFPSettings into the tracker so the cascade fires in-memory
            _ = await db.OfpSettings.Where(o => o.UserId == userId).ToListAsync();

            var user = await db.Users.FindAsync(userId);
            Assert.NotNull(user);   // guard: row must exist before delete
            db.Users.Remove(user!);
            await db.SaveChangesAsync();
        }

        // Assert – OFP settings row must be gone
        await using var verify = factory.CreateDbContext();
        var remaining = await verify.OfpSettings.Where(o => o.UserId == userId).ToListAsync();
        Assert.Empty(remaining);
    }

    // ── Inner helper ──────────────────────────────────────────────────────────

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> opts)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opts);
    }
}
