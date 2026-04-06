using FlightPrep.Domain.Models;
using FlightPrep.Domain.Services;
using FlightPrep.Infrastructure.Services;
using Moq;

namespace FlightPrep.Tests;

/// <summary>
///     Smoke tests for PdfService.GenerateOfpAsync.
///     QuestPDF layout is not unit-tested; we only assert the output is a non-empty byte array.
/// </summary>
public class PdfServiceOfpTests
{
    private static PdfService BuildSut()
    {
        var sunriseMock    = new Mock<ISunriseService>();
        var mapMock        = new Mock<ITrajectoryMapService>();
        var assessmentMock = new Mock<IFlightAssessmentService>();

        // GenerateOfpAsync does not call these services, but they are required by the constructor
        return new PdfService(sunriseMock.Object, mapMock.Object, assessmentMock.Object);
    }

    [Fact]
    public async Task GenerateOfpAsync_MinimalFlight_ReturnsNonEmptyPdf()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum   = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = TimeOnly.FromTimeSpan(TimeSpan.FromHours(8)),
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_FlightWithPassengers_ReturnsNonEmptyPdf()
    {
        // Arrange
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum            = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip         = TimeOnly.FromTimeSpan(TimeSpan.FromHours(9)),
            PicWeightKg      = 80,
            OFPEnvelopeWeightKg = 250,
            OFPBasketWeightKg   = 70,
            OFPBurnerWeightKg   = 18,
            CylindersWeightKg   = 28,
        };
        fp.Passengers.Add(new Passenger { Name = "Alice", WeightKg = 65 });
        fp.Passengers.Add(new Passenger { Name = "Bob",   WeightKg = 80 });
        fp.Passengers.Add(new Passenger { Name = "Carol", WeightKg = 72 });

        // Act
        var result = await sut.GenerateOfpAsync(fp, passengerEquipmentKg: 7);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NullBalloonAndPilot_DoesNotThrow()
    {
        // Arrange – all optional navigation properties are null
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = TimeOnly.FromTimeSpan(TimeSpan.FromHours(7)),
            Balloon  = null,
            Pilot    = null,
            Location = null,
            PicWeightKg         = null,
            OFPEnvelopeWeightKg = null,
            OFPBasketWeightKg   = null,
            OFPBurnerWeightKg   = null,
            CylindersWeightKg   = null,
        };

        // Act
        var exception = await Record.ExceptionAsync(() => sut.GenerateOfpAsync(fp));

        // Assert – must not throw
        Assert.Null(exception);
    }
}
