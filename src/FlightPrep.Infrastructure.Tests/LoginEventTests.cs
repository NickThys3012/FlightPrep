using FlightPrep.Data;
using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FlightPrep.Infrastructure.Tests;

/// <summary>
/// Integration tests for the <see cref="LoginEvent"/> entity and the
/// "last-50 ordered by timestamp DESC" query used by UserManagement.razor.
///
/// The query logic is replicated directly here (see <see cref="LoadLoginEventsAsync"/>)
/// because UserManagement is a Blazor component and is excluded from unit test scope.
/// This validates the persistence contract independently of the fire-and-forget
/// <c>RecordLoginEvent</c> path in LoginModel.
/// </summary>
public class LoginEventTests
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
    /// Replicates the query from <c>UserManagement.LoadLoginEvents</c>:
    /// returns the most recent 50 login events ordered by Timestamp DESC.
    /// </summary>
    private static async Task<List<LoginEvent>> LoadLoginEventsAsync(
        IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.LoginEvents
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .ToListAsync();
    }

    // ── Entity defaults ───────────────────────────────────────────────────────

    [Fact]
    public void LoginEvent_DefaultTimestamp_IsUtc()
    {
        // Arrange & Act
        var evt = new LoginEvent();

        // Assert — default is DateTime.UtcNow which has Kind == Utc
        Assert.Equal(DateTimeKind.Utc, evt.Timestamp.Kind);
    }

    // ── Persistence round-trip ────────────────────────────────────────────────

    [Fact]
    public async Task LoginEvent_PersistedAndReloaded_AllPropertiesMatch()
    {
        // Arrange
        var factory = CreateFactory();
        var timestamp = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var original = new LoginEvent
        {
            Email         = "pilot@example.com",
            UserId        = "user-abc",
            Timestamp     = timestamp,
            Success       = false,
            IpAddress     = "192.168.1.1",
            FailureReason = "InvalidPassword"
        };

        await using var dbWrite = await factory.CreateDbContextAsync();
        dbWrite.LoginEvents.Add(original);
        await dbWrite.SaveChangesAsync();
        var savedId = original.Id;

        // Act
        await using var dbRead = await factory.CreateDbContextAsync();
        var loaded = await dbRead.LoginEvents.FindAsync(savedId);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("pilot@example.com", loaded.Email);
        Assert.Equal("user-abc",          loaded.UserId);
        Assert.Equal(timestamp,           loaded.Timestamp);
        Assert.False(loaded.Success);
        Assert.Equal("192.168.1.1",       loaded.IpAddress);
        Assert.Equal("InvalidPassword",   loaded.FailureReason);
    }

    // ── LoadLoginEvents query logic ───────────────────────────────────────────

    [Fact]
    public async Task LoadLoginEvents_EmptyTable_ReturnsEmptyList()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var result = await LoadLoginEventsAsync(factory);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadLoginEvents_ReturnsLast50OrderedByTimestampDesc()
    {
        // Arrange — seed 10 events with known timestamps
        var factory = CreateFactory();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = await factory.CreateDbContextAsync();
        for (int i = 0; i < 10; i++)
        {
            db.LoginEvents.Add(new LoginEvent
            {
                Email     = $"user{i}@example.com",
                Timestamp = baseTime.AddHours(i),
                Success   = true
            });
        }
        await db.SaveChangesAsync();

        // Act
        var result = await LoadLoginEventsAsync(factory);

        // Assert — 10 events returned; newest first
        Assert.Equal(10, result.Count);
        for (int i = 0; i < result.Count - 1; i++)
            Assert.True(result[i].Timestamp >= result[i + 1].Timestamp,
                $"Event at index {i} should be newer than index {i + 1}");
    }

    [Fact]
    public async Task LoadLoginEvents_MoreThan50Events_ReturnsExactly50()
    {
        // Arrange — seed 60 events
        var factory = CreateFactory();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = await factory.CreateDbContextAsync();
        for (int i = 0; i < 60; i++)
        {
            db.LoginEvents.Add(new LoginEvent
            {
                Email     = $"user{i}@example.com",
                Timestamp = baseTime.AddHours(i),
                Success   = i % 2 == 0
            });
        }
        await db.SaveChangesAsync();

        // Act
        var result = await LoadLoginEventsAsync(factory);

        // Assert — hard cap of 50 enforced
        Assert.Equal(50, result.Count);
    }

    [Fact]
    public async Task LoadLoginEvents_MoreThan50Events_ReturnsNewestFirst()
    {
        // Arrange — seed 60 events; the newest 50 are hours 10..59
        var factory = CreateFactory();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = await factory.CreateDbContextAsync();
        for (int i = 0; i < 60; i++)
        {
            db.LoginEvents.Add(new LoginEvent
            {
                Email     = $"user{i}@example.com",
                Timestamp = baseTime.AddHours(i),
                Success   = true
            });
        }
        await db.SaveChangesAsync();

        // Act
        var result = await LoadLoginEventsAsync(factory);

        // Assert — most recent event is hour 59; oldest in the result is hour 10
        Assert.Equal(baseTime.AddHours(59), result.First().Timestamp);
        Assert.Equal(baseTime.AddHours(10), result.Last().Timestamp);
    }
}
