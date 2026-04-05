using FlightPrep.Domain.Models;
using FlightPrep.Infrastructure.Data;
using FlightPrep.Services;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Tests;

/// <summary>
///     Integration tests for GoNoGoService.GetSettingsAsync and SaveSettingsAsync
///     using an EF Core InMemory database.
/// </summary>
public class GoNoGoServiceAsyncTests
{
    private static IDbContextFactory<AppDbContext> CreateFactory(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestDbContextFactory(opts);
    }

    [Fact]
    public async Task GetSettingsAsync_NoRowInDb_ReturnsDefaultSettings()
    {
        // Arrange
        var factory = CreateFactory(nameof(GetSettingsAsync_NoRowInDb_ReturnsDefaultSettings));
        var sut = new GoNoGoService(factory);

        // Act
        var result = await sut.GetSettingsAsync("user-1");

        // Assert – null-coalesced default; just verify non-null with sensible property
        Assert.NotNull(result);
        Assert.True(result.WindRedKt > 0);
    }

    [Fact]
    public async Task SaveSettingsAsync_NewSettings_InsertsRowWithId1()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveSettingsAsync_NewSettings_InsertsRowWithId1));
        var sut = new GoNoGoService(factory);
        var settings = new GoNoGoSettings { WindRedKt = 20, VisRedKm = 1, CapeRedJkg = 800 };

        // Act
        await sut.SaveSettingsAsync(settings, "user-1");

        // Assert – row is retrievable and values match
        var loaded = await sut.GetSettingsAsync("user-1");
        Assert.NotNull(loaded);
        Assert.Equal(20, loaded.WindRedKt);
        Assert.Equal(1, loaded.VisRedKm);
        Assert.Equal(800, loaded.CapeRedJkg);
    }

    [Fact]
    public async Task SaveSettingsAsync_ExistingRow_UpdatesValues()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveSettingsAsync_ExistingRow_UpdatesValues));
        var sut = new GoNoGoService(factory);

        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 15 }, "user-1");

        // Act – save again with different values
        await sut.SaveSettingsAsync(new GoNoGoSettings { WindRedKt = 25, VisRedKm = 2 }, "user-1");

        // Assert – updated values persisted
        var loaded = await sut.GetSettingsAsync("user-1");
        Assert.Equal(25, loaded.WindRedKt);
        Assert.Equal(2, loaded.VisRedKm);
    }

    [Fact]
    public async Task SaveSettingsAsync_AllThresholds_PersistedCorrectly()
    {
        // Arrange
        var factory = CreateFactory(nameof(SaveSettingsAsync_AllThresholds_PersistedCorrectly));
        var sut = new GoNoGoService(factory);
        var input = new GoNoGoSettings
        {
            WindYellowKt = 8,
            WindRedKt = 14,
            VisYellowKm = 6,
            VisRedKm = 3,
            CapeYellowJkg = 400,
            CapeRedJkg = 700
        };

        // Act
        await sut.SaveSettingsAsync(input, "user-1");
        var result = await sut.GetSettingsAsync("user-1");

        // Assert
        Assert.Equal(8, result.WindYellowKt);
        Assert.Equal(14, result.WindRedKt);
        Assert.Equal(6, result.VisYellowKm);
        Assert.Equal(3, result.VisRedKm);
        Assert.Equal(400, result.CapeYellowJkg);
        Assert.Equal(700, result.CapeRedJkg);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> opts)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opts);
    }
}
