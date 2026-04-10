using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Services;
using Moq;

namespace FlightPrep.Tests;

/// <summary>
///     Smoke tests for <see cref="PdfService.GenerateAsync" /> (the full flight-prep PDF,
///     distinct from GenerateOfpAsync which is tested in PdfServiceOfpTests).
///     Issue #33 — confirms non-empty byte[] and no exception on red GoNoGo flights.
///     QuestPDF community licence is set inside <see cref="PdfService.GenerateAsync" />.
/// </summary>
public class PdfServiceTests
{
    private static PdfService BuildSut(string goNoGo = "green")
    {
        var sunriseMock = new Mock<ISunriseService>();
        var mapMock     = new Mock<ITrajectoryMapService>();
        mapMock.Setup(m => m.RenderAsync(It.IsAny<string?>()))
               .ReturnsAsync((byte[]?)null);

        var assessmentMock = new Mock<IFlightAssessmentService>();
        assessmentMock
            .Setup(a => a.ComputeAsync(It.IsAny<FlightPreparation>(), It.IsAny<string?>()))
            .ReturnsAsync(new FlightAssessment(0, false, goNoGo));

        return new PdfService(sunriseMock.Object, mapMock.Object, assessmentMock.Object);
    }

    private static FlightPreparation MinimalFlight() => new()
    {
        Date = DateOnly.FromDateTime(DateTime.Today),
        Time = new TimeOnly(9, 0)
    };

    // ── Issue #33 tests ───────────────────────────────────────────────────────

    /// <summary>
    ///     Calling GenerateAsync on a minimal flight must return a non-empty byte array.
    /// </summary>
    [Fact]
    public async Task PdfService_MinimalFlight_GeneratesPdfBytes()
    {
        // Arrange
        var sut = BuildSut("green");
        var fp  = MinimalFlight();

        // Act
        var result = await sut.GenerateAsync(fp);

        // Assert — must be a non-empty PDF byte array
        Assert.NotNull(result);
        Assert.True(result.Length > 0, "GenerateAsync must return a non-empty byte array");
    }

    /// <summary>
    ///     When the GoNoGo assessment returns "red", GenerateAsync must complete
    ///     without throwing — the red-badge rendering path must be safe.
    /// </summary>
    [Fact]
    public async Task PdfService_GoNoGoRed_PdfDoesNotThrow()
    {
        // Arrange — mock assessment always returns "red"
        var sut = BuildSut("red");
        var fp  = MinimalFlight();
        // Give the flight data that would normally trigger red
        fp.SurfaceWindSpeedKt = 20;
        fp.VisibilityKm    = 1;
        fp.CapeJkg            = 600;

        // Act
        var exception = await Record.ExceptionAsync(() => sut.GenerateAsync(fp));

        // Assert — no exception on a red-flagged flight
        Assert.Null(exception);
    }
}
