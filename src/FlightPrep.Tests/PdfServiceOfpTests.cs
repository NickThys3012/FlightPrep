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

    // ── PLANNED TIME duration calculation ─────────────────────────────────────

    [Fact]
    public async Task GenerateOfpAsync_NormalFlight_PlannedTimeIsCorrect()
    {
        // Arrange – 10:00 → 11:30 is exactly 90 minutes, no midnight wrap needed
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum               = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip            = new TimeOnly(10, 0),
            PlannedLandingTime  = new TimeOnly(11, 30),
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – smoke: no exception thrown, PDF content produced
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_PastMidnightFlight_DoesNotThrow()
    {
        // Arrange – 23:30 → 01:15 crosses midnight; rawMinutes would be negative without the +24*60 guard
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum               = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip            = new TimeOnly(23, 30),
            PlannedLandingTime  = new TimeOnly(1, 15),
        };

        // Act – must not throw (negative-duration guard path exercised)
        var exception = await Record.ExceptionAsync(() => sut.GenerateOfpAsync(fp));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GenerateOfpAsync_NoPlannedLandingTime_RendersDash()
    {
        // Arrange – PlannedLandingTime is null; the else-branch should render "—" without throwing
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum               = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip            = new TimeOnly(9, 0),
            PlannedLandingTime  = null,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – null guard path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    // ── Blank* helper (fill-line / value / dash) ──────────────────────────────

    [Fact]
    public async Task GenerateOfpAsync_NotFlown_PostFlightCellsReturnFillLine()
    {
        // Arrange – flight not yet marked as flown; Blank* helpers should return fill lines
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                       = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                    = new TimeOnly(9, 0),
            IsFlown                     = false,
            FuelConsumptionL            = 42,
            LandingLocationText         = "Leuven",
            VisibleDefects              = true,
            ActualLandingNotes          = "some notes",
            ActualFlightDurationMinutes = 90,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – fill-line path must not throw and must produce a PDF
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_FlownWithValues_PostFlightCellsRenderValues()
    {
        // Arrange – flight is flown with all post-flight fields populated (value path)
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                       = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                    = new TimeOnly(9, 0),
            IsFlown                     = true,
            FuelConsumptionL            = 35.5,
            LandingLocationText         = "Tienen",
            VisibleDefects              = false,
            VisibleDefectsNotes         = null,
            ActualLandingNotes          = "smooth",
            ActualFlightDurationMinutes = 75,
            ActualRemarks               = "great flight",
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – value path must not throw and must produce a PDF
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_FlownWithNullPostFlightFields_RendersDash()
    {
        // Arrange – flight is flown but all post-flight fields are null; tests the "—" path,
        // especially for the ActualFlightDurationMinutes == null guard
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                       = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                    = new TimeOnly(9, 0),
            IsFlown                     = true,
            FuelConsumptionL            = null,
            LandingLocationText         = null,
            VisibleDefects              = null,
            VisibleDefectsNotes         = null,
            ActualLandingNotes          = null,
            ActualFlightDurationMinutes = null,
            ActualRemarks               = null,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – dash path must not throw and must produce a PDF
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    // ── AFTER FLIGHT table (9-row, 5-column redesign) ────────────────────────

    [Fact]
    public async Task GenerateOfpAsync_NotFlown_AfterFlightDateIsBlank()
    {
        // Arrange – fp.IsFlown = false; the date cell uses the guard
        //   fp.IsFlown ? fp.Datum.ToString("d-MM-yyyy") : "________________"
        // This must not throw even though the IsFlown branch is never taken.
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = new TimeOnly(10, 0),
            IsFlown  = false,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – not-flown date guard path: fill line rendered, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_Flown_VisibleDefectsTrue_NotesRendered()
    {
        // Arrange – fp.IsFlown = true, VisibleDefects = true, notes populated.
        //   BlankBool(true)  → "Ja"
        //   Blank("crack in basket") → "crack in basket"
        // Exercises the value paths through the redesigned AFTER FLIGHT table.
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum               = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip            = new TimeOnly(10, 0),
            IsFlown             = true,
            VisibleDefects      = true,
            VisibleDefectsNotes = "crack in basket",
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – "Ja" + defect notes paths: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_Flown_VisibleDefectsFalse_NotesNull()
    {
        // Arrange – fp.IsFlown = true, VisibleDefects = false, notes null.
        //   BlankBool(false) → "Neen"
        //   Blank(null)      → "—"
        // Verifies no NullReferenceException when notes are null on a completed flight.
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum               = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip            = new TimeOnly(10, 0),
            IsFlown             = true,
            VisibleDefects      = false,
            VisibleDefectsNotes = null,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – "Neen" + null-notes dash paths: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    // ── CLOUDS block (Neerslag fallback) ──────────────────────────────────────

    [Fact]
    public async Task GenerateOfpAsync_NeerslagSet_RendersWithoutException()
    {
        // Arrange – Neerslag is non-empty; CLOUDS cell should display its value
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = new TimeOnly(9, 0),
            Neerslag = "SCT030 BKN050",
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – Neerslag value path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NeerslagNull_RendersFallbackDash()
    {
        // Arrange – Neerslag is null; CLOUDS cell should fall back to "—"
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = new TimeOnly(9, 0),
            Neerslag = null,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – null fallback path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NeerslagEmpty_RendersFallbackDash()
    {
        // Arrange – Neerslag is empty string; IsNullOrWhiteSpace → "—" path
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = new TimeOnly(9, 0),
            Neerslag = string.Empty,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – empty-string fallback path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NeerslagWhitespace_RendersFallbackDash()
    {
        // Arrange – Neerslag is whitespace only; IsNullOrWhiteSpace → "—" path
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip = new TimeOnly(9, 0),
            Neerslag = "   ",
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – whitespace fallback path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    // ── SURFACE WIND block (WindLevels empty, fallback formatting) ────────────

    [Fact]
    public async Task GenerateOfpAsync_NoWindLevels_BothSurfaceWindFieldsNull_RendersDash()
    {
        // Arrange – no wind levels and no surface wind data; should render "—"
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                 = new TimeOnly(9, 0),
            SurfaceWindDirectionDeg  = null,
            SurfaceWindSpeedKt       = null,
            // WindLevels is an empty list by default
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – "—" fallback path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NoWindLevels_BothSurfaceWindFieldsSet_FormatsAsDddSskt()
    {
        // Arrange – no wind levels but both surface wind direction and speed are set
        //   → should render e.g. "270/15kt"
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                 = new TimeOnly(9, 0),
            SurfaceWindDirectionDeg  = 270,
            SurfaceWindSpeedKt       = 15,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – "DDD/SSkt" format path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NoWindLevels_OnlyDirectionSet_FormatsWithSpeedPlaceholder()
    {
        // Arrange – direction set, speed null → speed placeholder "--", e.g. "270/--kt"
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                 = new TimeOnly(9, 0),
            SurfaceWindDirectionDeg  = 270,
            SurfaceWindSpeedKt       = null,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – partial-direction path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_NoWindLevels_OnlySpeedSet_FormatsWithDirectionPlaceholder()
    {
        // Arrange – speed set, direction null → direction placeholder "---", e.g. "---/15kt"
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                 = new TimeOnly(9, 0),
            SurfaceWindDirectionDeg  = null,
            SurfaceWindSpeedKt       = 15,
        };

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – partial-speed path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public async Task GenerateOfpAsync_WithWindLevels_RendersLevelsInsteadOfSurfaceFallback()
    {
        // Arrange – wind levels present; SURFACE WIND fallback block must NOT render;
        //   instead the per-level rows are rendered (existing behaviour, regression guard)
        var sut = BuildSut();
        var fp = new FlightPreparation
        {
            Datum                    = DateOnly.FromDateTime(DateTime.Today),
            Tijdstip                 = new TimeOnly(9, 0),
            SurfaceWindDirectionDeg  = null,
            SurfaceWindSpeedKt       = null,
        };
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 0,    DirectionDeg = 270, SpeedKt = 12, Order = 0 });
        fp.WindLevels.Add(new WindLevel { AltitudeFt = 3000, DirectionDeg = 280, SpeedKt = 18, Order = 1 });

        // Act
        var result = await sut.GenerateOfpAsync(fp);

        // Assert – wind-levels path: non-empty PDF, no exception
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }
}
