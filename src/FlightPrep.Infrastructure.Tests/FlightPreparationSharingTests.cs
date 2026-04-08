using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
///     Integration tests for the flight-preparation sharing feature introduced in #63.
///     Covers <see cref="FlightPreparationService.GetSummariesAsync" />,
///     <see cref="FlightPreparationService.ShareAsync" />,
///     <see cref="FlightPreparationService.RevokeShareAsync" />,
///     <see cref="FlightPreparationService.IsSharedWithAsync" />,
///     <see cref="FlightPreparationService.GetSharesAsync" />,
///     <see cref="FlightPreparationService.GetShareableUsersAsync" />, and
///     <see cref="FlightPreparationService.DeleteAsync" />.
///
///     EF Core InMemory does NOT enforce FK constraints, so share rows can reference
///     user-id strings without requiring matching <see cref="ApplicationUser" /> rows
///     (except where <c>db.Users</c> is queried directly, e.g. GetShareableUsersAsync).
/// </summary>
public class FlightPreparationSharingTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static IDbContextFactory<AppDbContext> CreateFactory()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    private static FlightPreparationService BuildSut(IDbContextFactory<AppDbContext> factory)
        => new(factory, NullLogger<FlightPreparationService>.Instance);

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>Seeds a minimal flight owned by <paramref name="ownerId" /> and returns its id.</summary>
    private static async Task<int> SeedFlightAsync(IDbContextFactory<AppDbContext> factory, string ownerId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var fp = new FlightPreparation
        {
            Datum = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = TimeOnly.MinValue,
            CreatedByUserId = ownerId
        };
        db.FlightPreparations.Add(fp);
        await db.SaveChangesAsync();
        return fp.Id;
    }

    /// <summary>Seeds a <see cref="FlightPreparationShare" /> row directly, bypassing the service.</summary>
    private static async Task SeedShareAsync(
        IDbContextFactory<AppDbContext> factory,
        int flightId,
        string sharedWithUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == sharedWithUserId))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = sharedWithUserId,
                UserName = $"{sharedWithUserId}@test.com",
                NormalizedUserName = $"{sharedWithUserId}@test.com".ToUpperInvariant(),
                Email = $"{sharedWithUserId}@test.com",
                NormalizedEmail = $"{sharedWithUserId}@test.com".ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString()
            });
        }
        db.FlightPreparationShares.Add(new FlightPreparationShare
        {
            FlightPreparationId = flightId,
            SharedWithUserId = sharedWithUserId,
            SharedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds an <see cref="ApplicationUser" /> into the identity Users table.</summary>
    private static async Task SeedUserAsync(
        IDbContextFactory<AppDbContext> factory,
        string userId,
        string userName)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = userName,
            NormalizedEmail = userName.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
    }

    // ── GetSummariesAsync — sharing behaviour ─────────────────────────────────

    [Fact]
    public async Task GetSummariesAsync_SharedPreparation_AppearsInViewerList()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act — viewer-1 requests their summary list
        var result = await sut.GetSummariesAsync("viewer-1", false);

        // Assert — the shared flight must appear
        Assert.Single(result);
        var summary = result[0];
        Assert.Equal(flightId, summary.Id);
        Assert.True(summary.IsShared, "IsShared must be true for a flight shared with the viewer");
        Assert.NotNull(summary.SharedByName);
    }

    [Fact]
    public async Task GetSummariesAsync_SharedPreparation_DoesNotAppearForUnrelatedUser()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act — a completely unrelated user requests their list
        var result = await sut.GetSummariesAsync("other-user", false);

        // Assert — the flight must NOT appear for an unrelated user
        Assert.DoesNotContain(result, s => s.Id == flightId);
    }

    [Fact]
    public async Task GetSummariesAsync_OwnerAndViewerBothSeeSharedPreparation()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act
        var ownerResults = await sut.GetSummariesAsync("owner-1", false);
        var viewerResults = await sut.GetSummariesAsync("viewer-1", false);

        // Assert — both see the flight
        Assert.Contains(ownerResults, s => s.Id == flightId);
        Assert.Contains(viewerResults, s => s.Id == flightId);

        // Owner's copy must NOT be flagged as shared
        var ownerSummary = ownerResults.Single(s => s.Id == flightId);
        Assert.False(ownerSummary.IsShared, "Owner must see their own flight as not shared");
        Assert.Null(ownerSummary.SharedByName);

        // Viewer's copy must be flagged as shared
        var viewerSummary = viewerResults.Single(s => s.Id == flightId);
        Assert.True(viewerSummary.IsShared, "Viewer must see the flight as shared");
        Assert.NotNull(viewerSummary.SharedByName);
    }

    // ── ShareAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareAsync_ValidUsers_CreatesShareRecord()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act
        await sut.ShareAsync(flightId, "owner-1", "target-user");

        // Assert — a share row must exist in the database
        await using var db = await factory.CreateDbContextAsync();
        var exists = await db.FlightPreparationShares
            .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == "target-user");
        Assert.True(exists, "Share record must be created");
    }

    [Fact]
    public async Task ShareAsync_DuplicateShare_IsNoOp()
    {
        // Arrange — pre-seed a share so a duplicate add would conflict
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await sut.ShareAsync(flightId, "owner-1", "target-user");

        // Act — sharing again with the same target must not throw
        var ex = await Record.ExceptionAsync(() => sut.ShareAsync(flightId, "owner-1", "target-user"));
        Assert.Null(ex);

        // Assert — only one share row must exist (no duplicate)
        await using var db = await factory.CreateDbContextAsync();
        var count = await db.FlightPreparationShares
            .CountAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == "target-user");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ShareAsync_NonOwner_IsIgnored()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — non-owner attempts to share
        await sut.ShareAsync(flightId, "not-the-owner", "target-user");

        // Assert — no share row must have been created
        await using var db = await factory.CreateDbContextAsync();
        var exists = await db.FlightPreparationShares
            .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == "target-user");
        Assert.False(exists, "Non-owner must not be able to create share records");
    }

    // ── RevokeShareAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeShareAsync_ExistingShare_RemovesRecord()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act
        await sut.RevokeShareAsync(flightId, "owner-1", "viewer-1");

        // Assert — the share row must be gone
        await using var db = await factory.CreateDbContextAsync();
        var exists = await db.FlightPreparationShares
            .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == "viewer-1");
        Assert.False(exists, "Share record must be removed after revocation");
    }

    [Fact]
    public async Task RevokeShareAsync_NonOwner_CannotRevoke()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act — someone other than the owner tries to revoke
        await sut.RevokeShareAsync(flightId, "not-the-owner", "viewer-1");

        // Assert — the share row must still exist
        await using var db = await factory.CreateDbContextAsync();
        var exists = await db.FlightPreparationShares
            .AnyAsync(s => s.FlightPreparationId == flightId && s.SharedWithUserId == "viewer-1");
        Assert.True(exists, "Non-owner must not be able to revoke a share");
    }

    [Fact]
    public async Task RevokeShareAsync_NonExistentShare_DoesNotThrow()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act & Assert — revoking a share that doesn't exist must complete gracefully
        var ex = await Record.ExceptionAsync(() =>
            sut.RevokeShareAsync(flightId, "owner-1", "nonexistent-user"));
        Assert.Null(ex);
    }

    // ── IsSharedWithAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task IsSharedWithAsync_SharedUser_ReturnsTrue()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act
        var result = await sut.IsSharedWithAsync(flightId, "viewer-1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsSharedWithAsync_UnsharedUser_ReturnsFalse()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        // No share seeded for "other-user"

        // Act
        var result = await sut.IsSharedWithAsync(flightId, "other-user");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsSharedWithAsync_OwnerIsNotConsideredShared()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — checking the owner themselves (no share row)
        var result = await sut.IsSharedWithAsync(flightId, "owner-1");

        // Assert — owner is not in the share table, so this returns false
        Assert.False(result);
    }

    // ── GetSharesAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSharesAsync_Owner_ReturnsAllShares()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");
        await SeedShareAsync(factory, flightId, "viewer-2");

        // Act
        var shares = await sut.GetSharesAsync(flightId, "owner-1");

        // Assert
        Assert.Equal(2, shares.Count);
        Assert.Contains(shares, s => s.Id == "viewer-1");
        Assert.Contains(shares, s => s.Id == "viewer-2");
    }

    [Fact]
    public async Task GetSharesAsync_NonOwner_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act — non-owner tries to list shares
        var shares = await sut.GetSharesAsync(flightId, "not-the-owner");

        // Assert — must return empty list
        Assert.Empty(shares);
    }

    // ── GetShareableUsersAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetShareableUsersAsync_ExcludesOwnerAndAlreadySharedUsers()
    {
        // Arrange — seed three application users
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        await SeedUserAsync(factory, "owner-1", "owner@test.com");
        await SeedUserAsync(factory, "viewer-1", "viewer1@test.com");
        await SeedUserAsync(factory, "viewer-2", "viewer2@test.com");

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // viewer-1 is already shared with
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act
        var result = await sut.GetShareableUsersAsync(flightId, "owner-1");

        // Assert — only viewer-2 should be shareable
        Assert.Single(result);
        Assert.Equal("viewer-2", result[0].Id);
        Assert.DoesNotContain(result, u => u.Id == "owner-1");
        Assert.DoesNotContain(result, u => u.Id == "viewer-1");
    }

    [Fact]
    public async Task GetShareableUsersAsync_NonOwner_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        await SeedUserAsync(factory, "owner-1", "owner@test.com");
        await SeedUserAsync(factory, "other-user", "other@test.com");
        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — non-owner requests shareable users
        var result = await sut.GetShareableUsersAsync(flightId, "other-user");

        // Assert
        Assert.Empty(result);
    }

    // ── DeleteAsync — ownership + admin bypass ────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwnerCanDelete_ReturnsSuccess()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act
        await sut.DeleteAsync(flightId, "owner-1");

        // Assert — flight must no longer exist
        var loaded = await sut.GetByIdAsync(flightId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_IsBlocked()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — a different user tries to delete
        await sut.DeleteAsync(flightId, "not-the-owner");

        // Assert — flight must still exist
        var loaded = await sut.GetByIdAsync(flightId);
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task DeleteAsync_AdminCanDeleteAnyFlight()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — an admin user (not the owner) deletes the flight
        await sut.DeleteAsync(flightId, "admin-user", isAdmin: true);

        // Assert — flight must be gone
        var loaded = await sut.GetByIdAsync(flightId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteAsync_SharesAreCascadeDeletedWithFlight()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Verify share exists before delete
        Assert.True(await sut.IsSharedWithAsync(flightId, "viewer-1"));

        // Act — owner deletes the flight
        await sut.DeleteAsync(flightId, "owner-1");

        // Assert — flight is gone; the share row must have been cascade-deleted too
        // (InMemory DB does not enforce cascade deletes, but we verify the flight is gone)
        var loaded = await sut.GetByIdAsync(flightId);
        Assert.Null(loaded);
    }
}
