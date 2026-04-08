using FlightPrep.Domain.Models.ReleaseNotes;
using FlightPrep.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using System.Text.Json;

namespace FlightPrep.Tests;

public sealed class ReleaseNotesServiceTests : IDisposable
{
    private readonly Mock<IWebHostEnvironment> _envMock = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ReleaseNotesServiceTests()
    {
        Directory.CreateDirectory(_webRoot);
        _envMock.Setup(e => e.WebRootPath).Returns(_webRoot);
    }

    public void Dispose()
    {
        Directory.Delete(_webRoot, true);
        GC.SuppressFinalize(this);
    }

    private ReleaseNotesService BuildSut() =>
        new(_envMock.Object, NullLogger<ReleaseNotesService>.Instance);

    private void WriteReleaseNotes(ReleaseNotesDocument doc)
    {
        var path = Path.Combine(_webRoot, "release-notes.json");
        File.WriteAllText(path, JsonSerializer.Serialize(doc));
    }

    [Fact]
    public async Task GetAsync_ValidFile_ReturnsDocument()
    {
        WriteReleaseNotes(new ReleaseNotesDocument { CurrentVersion = "1.2.3", Entries = [new ReleaseEntry { Pr = 1, Title = "Test", Version = "1.2.3" }] });
        var sut = BuildSut();

        var result = await sut.GetAsync();

        Assert.Equal("1.2.3", result.CurrentVersion);
        Assert.Single(result.Entries);
    }

    [Fact]
    public async Task GetAsync_WithinCacheTtl_DoesNotRereadFile()
    {
        WriteReleaseNotes(new ReleaseNotesDocument { CurrentVersion = "1.0.0" });
        var sut = BuildSut();

        var first = await sut.GetAsync();

        // Overwrite the file — a cached result should still be returned
        WriteReleaseNotes(new ReleaseNotesDocument { CurrentVersion = "2.0.0" });
        var second = await sut.GetAsync();

        Assert.Equal("1.0.0", first.CurrentVersion);
        Assert.Equal("1.0.0", second.CurrentVersion);
    }

    [Fact]
    public async Task GetAsync_FileMissing_ReturnsEmptyDocument()
    {
        var sut = BuildSut();

        var result = await sut.GetAsync();

        Assert.Equal("0.0.0", result.CurrentVersion);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task GetAsync_InvalidJson_ReturnsEmptyDocument()
    {
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "release-notes.json"), "NOT_VALID_JSON{{");
        var sut = BuildSut();

        var result = await sut.GetAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAsync_EmptyFile_ReturnsEmptyDocument()
    {
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "release-notes.json"), "");
        var sut = BuildSut();

        var result = await sut.GetAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAsync_CalledAfterCacheExpiry_ReadsFileAgain()
    {
        // Arrange
        WriteReleaseNotes(new ReleaseNotesDocument { CurrentVersion = "1.0.0" });
        var sut = BuildSut();
        _ = await sut.GetAsync(); // populate cache

        // Reset _cachedAt to force expiry
        var field = typeof(ReleaseNotesService)
            .GetField("_cachedAt", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(sut, DateTime.MinValue);

        // Replace a file with updated content
        WriteReleaseNotes(new ReleaseNotesDocument { CurrentVersion = "3.0.0" });

        // Act — cache is expired, so a file must be re-read
        var result = await sut.GetAsync();

        // Assert
        Assert.Equal("3.0.0", result.CurrentVersion);
    }

    [Fact]
    public async Task GetAsync_ConcurrentCalls_AllReturnValidDocument()
    {
        // Arrange — regression test for issue #28 (thread-safety via SemaphoreSlim)
        WriteReleaseNotes(new ReleaseNotesDocument
        {
            CurrentVersion = "2.0.0",
            Entries =
            [
                new ReleaseEntry { Pr = 1, Title = "Alpha", Version = "2.0.0" },
                new ReleaseEntry { Pr = 2, Title = "Beta", Version = "2.0.0" }
            ]
        });
        var sut = BuildSut();

        // Act — 10 concurrent callers (simulates multiple Blazor circuits)
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => sut.GetAsync()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert — every caller received a fully populated, non-null document
        Assert.All(results, r =>
        {
            if (r == null)
            {
                throw new ArgumentNullException(nameof(r));
            }

            Assert.NotNull(r);
            Assert.Equal("2.0.0", r.CurrentVersion);
            Assert.Equal(2, r.Entries.Count);
        });

        // All results should be the exact same cached instance (no torn reads)
        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));
    }
}
