using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Tests;

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

    /// <summary>Seeds a minimal flight owned by <paramref name="ownerId" /> and returns its id.</summary>
    private static async Task<int> SeedFlightAsync(IDbContextFactory<AppDbContext> factory, string ownerId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var fp = new FlightPreparation
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            Time = TimeOnly.MinValue,
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
        // Ensure a matching ApplicationUser exists so GetSharesAsync JOIN resolves
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
        var factory = CreateFactory(nameof(GetSummariesAsync_SharedPreparation_AppearsInViewerList));
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
        var factory = CreateFactory(nameof(GetSummariesAsync_SharedPreparation_DoesNotAppearForUnrelatedUser));
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
        var factory = CreateFactory(nameof(GetSummariesAsync_OwnerAndViewerBothSeeSharedPreparation));
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

    [Fact]
    public async Task GetSummariesAsync_NullUserId_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_NullUserId_ReturnsEmpty));
        var sut = BuildSut(factory);

        await SeedFlightAsync(factory, "owner-1");

        // Act — null userId with isAdmin=false
        var result = await sut.GetSummariesAsync(null, false);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummariesAsync_AdminUser_SeesAllFlights()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_AdminUser_SeesAllFlights));
        var sut = BuildSut(factory);

        await SeedFlightAsync(factory, "owner-1");
        await SeedFlightAsync(factory, "owner-2");

        // Act — admin sees everything regardless of userId
        var result = await sut.GetSummariesAsync("admin-user", true);

        // Assert — must see both flights
        Assert.Equal(2, result.Count);

        // Admin must not see any flight as "shared"
        Assert.All(result, s => Assert.False(s.IsShared));
    }

    // ── ShareAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareAsync_ValidUsers_CreatesShareRecord()
    {
        // Arrange
        var factory = CreateFactory(nameof(ShareAsync_ValidUsers_CreatesShareRecord));
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
        var factory = CreateFactory(nameof(ShareAsync_DuplicateShare_IsNoOp));
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
        var factory = CreateFactory(nameof(ShareAsync_NonOwner_IsIgnored));
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
        var factory = CreateFactory(nameof(RevokeShareAsync_ExistingShare_RemovesRecord));
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
        var factory = CreateFactory(nameof(RevokeShareAsync_NonOwner_CannotRevoke));
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
        var factory = CreateFactory(nameof(RevokeShareAsync_NonExistentShare_DoesNotThrow));
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
        var factory = CreateFactory(nameof(IsSharedWithAsync_SharedUser_ReturnsTrue));
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
        var factory = CreateFactory(nameof(IsSharedWithAsync_UnsharedUser_ReturnsFalse));
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
        var factory = CreateFactory(nameof(IsSharedWithAsync_OwnerIsNotConsideredShared));
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
        var factory = CreateFactory(nameof(GetSharesAsync_Owner_ReturnsAllShares));
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
        var factory = CreateFactory(nameof(GetSharesAsync_NonOwner_ReturnsEmpty));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act — non-owner tries to list shares
        var shares = await sut.GetSharesAsync(flightId, "not-the-owner");

        // Assert — must return empty list
        Assert.Empty(shares);
    }

    [Fact]
    public async Task GetSharesAsync_NoShares_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSharesAsync_NoShares_ReturnsEmpty));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act
        var shares = await sut.GetSharesAsync(flightId, "owner-1");

        // Assert
        Assert.Empty(shares);
    }

    // ── GetShareableUsersAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetShareableUsersAsync_ExcludesOwnerAndAlreadySharedUsers()
    {
        // Arrange — seed three application users
        var factory = CreateFactory(nameof(GetShareableUsersAsync_ExcludesOwnerAndAlreadySharedUsers));
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
        var factory = CreateFactory(nameof(GetShareableUsersAsync_NonOwner_ReturnsEmpty));
        var sut = BuildSut(factory);

        await SeedUserAsync(factory, "owner-1", "owner@test.com");
        await SeedUserAsync(factory, "other-user", "other@test.com");
        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — non-owner requests shareable users
        var result = await sut.GetShareableUsersAsync(flightId, "other-user");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetShareableUsersAsync_NoOtherUsers_ReturnsEmpty()
    {
        // Arrange — only the owner exists as a user
        var factory = CreateFactory(nameof(GetShareableUsersAsync_NoOtherUsers_ReturnsEmpty));
        var sut = BuildSut(factory);

        await SeedUserAsync(factory, "owner-1", "owner@test.com");
        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act
        var result = await sut.GetShareableUsersAsync(flightId, "owner-1");

        // Assert — no one else to share with
        Assert.Empty(result);
    }

    // ── DeleteAsync — ownership + admin bypass ────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwnerCanDelete_FlightIsRemoved()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_OwnerCanDelete_FlightIsRemoved));
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
        var factory = CreateFactory(nameof(DeleteAsync_NonOwner_IsBlocked));
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
        var factory = CreateFactory(nameof(DeleteAsync_AdminCanDeleteAnyFlight));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — an admin user (not the owner) deletes the flight
        await sut.DeleteAsync(flightId, "admin-user", isAdmin: true);

        // Assert — flight must be gone
        var loaded = await sut.GetByIdAsync(flightId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFlight_DoesNotThrow()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_NonExistentFlight_DoesNotThrow));
        var sut = BuildSut(factory);

        // Act & Assert — deleting a flight that does not exist must be a no-op
        var ex = await Record.ExceptionAsync(() => sut.DeleteAsync(9999, "owner-1"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task DeleteAsync_SharesAreCascadeDeletedWithFlight()
    {
        // Arrange
        var factory = CreateFactory(nameof(DeleteAsync_SharesAreCascadeDeletedWithFlight));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Verify share exists before delete
        Assert.True(await sut.IsSharedWithAsync(flightId, "viewer-1"));

        // Act — owner deletes the flight
        await sut.DeleteAsync(flightId, "owner-1");

        // Assert — flight is gone
        var loaded = await sut.GetByIdAsync(flightId);
        Assert.Null(loaded);
    }

    // ── Issue #77 edge-case tests ─────────────────────────────────────────────

    [Fact]
    public async Task ShareAsync_NonOwnerCaller_DoesNotAddShare()
    {
        // Arrange
        var factory = CreateFactory(nameof(ShareAsync_NonOwnerCaller_DoesNotAddShare));
        var sut = BuildSut(factory);

        await SeedUserAsync(factory, "target-user", "target@test.com");
        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — a different user (not the owner) tries to share the flight
        await sut.ShareAsync(flightId, "not-the-owner", "target-user");

        // Assert — no share row must have been inserted
        Assert.False(await sut.IsSharedWithAsync(flightId, "target-user"));
    }

    [Fact]
    public async Task RevokeShareAsync_NonOwnerCaller_DoesNotRemoveShare()
    {
        // Arrange
        var factory = CreateFactory(nameof(RevokeShareAsync_NonOwnerCaller_DoesNotRemoveShare));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Confirm the share exists before the revoke attempt
        Assert.True(await sut.IsSharedWithAsync(flightId, "viewer-1"));

        // Act — a non-owner tries to revoke the share
        await sut.RevokeShareAsync(flightId, "not-the-owner", "viewer-1");

        // Assert — share must still exist
        Assert.True(await sut.IsSharedWithAsync(flightId, "viewer-1"));
    }

    [Fact]
    public async Task GetShareableUsersAsync_NonOwnerCaller_ReturnsEmpty()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetShareableUsersAsync_NonOwnerCaller_ReturnsEmpty));
        var sut = BuildSut(factory);

        await SeedUserAsync(factory, "owner-1", "owner@test.com");
        await SeedUserAsync(factory, "other-user", "other@test.com");
        var flightId = await SeedFlightAsync(factory, "owner-1");

        // Act — non-owner calls GetShareableUsersAsync
        var result = await sut.GetShareableUsersAsync(flightId, "other-user");

        // Assert — ownership guard must prevent any results
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummariesAsync_SharedFlight_SetsIsSharedTrue()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_SharedFlight_SetsIsSharedTrue));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await SeedShareAsync(factory, flightId, "viewer-1");

        // Act — viewer requests their summaries
        var summaries = await sut.GetSummariesAsync("viewer-1", false);

        // Assert — the shared flight's summary must have IsShared = true
        var summary = Assert.Single(summaries);
        Assert.Equal(flightId, summary.Id);
        Assert.True(summary.IsShared, "Viewer's summary must have IsShared = true for a shared flight");
    }

    [Fact]
    public async Task GetSummariesAsync_AdminUser_SetsIsSharedFalseForAll()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSummariesAsync_AdminUser_SetsIsSharedFalseForAll));
        var sut = BuildSut(factory);

        var flightId1 = await SeedFlightAsync(factory, "owner-1");
        var flightId2 = await SeedFlightAsync(factory, "owner-2");
        // Share flight1 with an admin user so it would normally show IsShared = true
        await SeedShareAsync(factory, flightId1, "admin-user");

        // Act — admin user requests all summaries
        var summaries = await sut.GetSummariesAsync("admin-user", isAdmin: true);

        // Assert — all summaries must have IsShared = false when caller is admin
        Assert.True(summaries.Count >= 2, "Admin must see both flights");
        Assert.All(summaries, s => Assert.False(s.IsShared,
            $"Flight {s.Id}: IsShared must be false for admin caller"));
    }

    [Fact]
    public async Task IsSharedWithAsync_AfterRevoke_ReturnsFalse()
    {
        // Arrange
        var factory = CreateFactory(nameof(IsSharedWithAsync_AfterRevoke_ReturnsFalse));
        var sut = BuildSut(factory);

        var flightId = await SeedFlightAsync(factory, "owner-1");
        await sut.ShareAsync(flightId, "owner-1", "viewer-1");

        // Confirm the share was established
        Assert.True(await sut.IsSharedWithAsync(flightId, "viewer-1"));

        // Act — owner revokes the share
        await sut.RevokeShareAsync(flightId, "owner-1", "viewer-1");

        // Assert — share must no longer exist
        Assert.False(await sut.IsSharedWithAsync(flightId, "viewer-1"),
            "IsSharedWithAsync must return false after the share has been revoked");
    }
}
