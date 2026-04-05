using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
///     Integration tests for the ownership-scoping behaviour added in #44.
///     Covers <see cref="FlightPreparationService.GetSummariesAsync" /> and
///     <see cref="FlightPreparationService.GetAllWithNavAsync" />.
///     EF Core InMemory does NOT enforce FK constraints, so flights can reference
///     user-id strings without a matching ApplicationUser row in the database.
/// </summary>
public class FlightPreparationServiceOwnershipTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static IDbContextFactory<AppDbContext>
        CreateFactory()
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

    /// <summary>
    ///     Seeds three flights directly via DbContext:
    ///     • one owned by "user1"
    ///     • one owned by "user2"
    ///     • one with no owner (null)
    ///     Returns the seeded IDs in order: (user1Id, user2Id, nullId).
    /// </summary>
    private static async Task<(int user1Id, int user2Id, int nullId)>
        SeedOwnershipFlightsAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        var fp1 = new FlightPreparation { Datum = new DateOnly(2025, 1, 1), Tijdstip = TimeOnly.MinValue, CreatedByUserId = "user1" };
        var fp2 = new FlightPreparation { Datum = new DateOnly(2025, 2, 1), Tijdstip = TimeOnly.MinValue, CreatedByUserId = "user2" };
        var fp3 = new FlightPreparation { Datum = new DateOnly(2025, 3, 1), Tijdstip = TimeOnly.MinValue, CreatedByUserId = null };

        db.FlightPreparations.AddRange(fp1, fp2, fp3);
        await db.SaveChangesAsync();
        return (fp1.Id, fp2.Id, fp3.Id);
    }

    // ── GetSummariesAsync — admin view ────────────────────────────────────────

    [Fact]
    public async Task GetSummariesAsync_AdminUser_ReturnsAllFlights()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        await SeedOwnershipFlightsAsync(factory);

        // Act
        var result = await sut.GetSummariesAsync("user1", true);

        // Assert — admin sees all 3 regardless of CreatedByUserId
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetSummariesAsync_PilotUser_ReturnsOnlyOwnFlights()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        var (user1Id, _, nullId) = await SeedOwnershipFlightsAsync(factory);

        // Act
        var result = await sut.GetSummariesAsync("user1", false);

        // Assert — pilot sees only their own flights; null-owner flights are NOT shown
        Assert.Single(result);
        Assert.Contains(result, s => s.Id == user1Id);
        Assert.DoesNotContain(result, s => s.Id == nullId);
    }

    [Fact]
    public async Task GetSummariesAsync_PilotUser_DoesNotReturnOtherPilotsFlights()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        var (_, user2Id, _) = await SeedOwnershipFlightsAsync(factory);

        // Act
        var result = await sut.GetSummariesAsync("user1", false);

        // Assert — user2's flight must NOT appear in user1's results
        Assert.DoesNotContain(result, s => s.Id == user2Id);
    }

    [Fact]
    public async Task GetSummariesAsync_NullUserId_NonAdmin_ReturnsNoFlights()
    {
        // Arrange — unauthenticated context (userId = null, not an admin)
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        await SeedOwnershipFlightsAsync(factory);

        // Act
        var result = await sut.GetSummariesAsync(null, false);

        // Assert — no flights visible without a user identity
        Assert.Empty(result);
    }

    // ── GetAllWithNavAsync — admin view ───────────────────────────────────────

    [Fact]
    public async Task GetAllWithNavAsync_AdminUser_ReturnsAllFlights()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        await SeedOwnershipFlightsAsync(factory);

        // Act
        var result = await sut.GetAllWithNavAsync("admin", true);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetAllWithNavAsync_PilotUser_ReturnsOnlyOwnFlights()
    {
        // Arrange
        var factory = CreateFactory();
        var sut = BuildSut(factory);
        var (user1Id, user2Id, nullId) = await SeedOwnershipFlightsAsync(factory);

        // Act
        var result = await sut.GetAllWithNavAsync("user1", false);

        // Assert — pilot sees only their own flights
        Assert.Single(result);
        Assert.Contains(result, f => f.Id == user1Id);
        Assert.DoesNotContain(result, f => f.Id == nullId);
        Assert.DoesNotContain(result, f => f.Id == user2Id);
    }
}
