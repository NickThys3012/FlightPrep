using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FlightPrep.Domain.Models;

namespace FlightPrep.Tests;

/// <summary>
///     Exercises every property on each model so that the coverlet can measure the setters/getters.
///     These tests also verify that default values are sane.
/// </summary>
public class ModelPropertyTests
{
    [Fact]
    public void Balloon_Properties_SetAndGetRoundTrip()
    {
        var b = new Balloon
        {
            Id = 1,
            Registration = "OO-BAL",
            Type = "Cameron A-180",
            VolumeM3 = 180,
            EnvelopeOnlyWeightKg = 150.0,
            BasketWeightKg = 60.5,
            BurnerWeightKg = 25.0,
            CylindersWeightKg = 15.0
        };

        Assert.Equal(1, b.Id);
        Assert.Equal("OO-BAL", b.Registration);
        Assert.Equal("Cameron A-180", b.Type);
        Assert.Equal(180, b.VolumeM3);
        Assert.Equal(250.5, b.EmptyWeightKg);
    }

    [Fact]
    public void Balloon_EmptyWeightKg_IsNullByDefault() => Assert.Null(new Balloon().EmptyWeightKg);

    [Fact]
    public void Location_Properties_SetAndGetRoundTrip()
    {
        var l = new Location
        {
            Id = 2,
            Name = "Brussel",
            IcaoCode = "EBBR",
            AirspaceNotes = "CTR active",
            Latitude = 50.9,
            Longitude = 4.48
        };

        Assert.Equal(2, l.Id);
        Assert.Equal("Brussel", l.Name);
        Assert.Equal("EBBR", l.IcaoCode);
        Assert.Equal("CTR active", l.AirspaceNotes);
        Assert.Equal(50.9, l.Latitude);
        Assert.Equal(4.48, l.Longitude);
    }

    [Fact]
    public void Location_NullableFields_AreNullByDefault()
    {
        var l = new Location();

        Assert.Null(l.IcaoCode);
        Assert.Null(l.AirspaceNotes);
        Assert.Null(l.Latitude);
        Assert.Null(l.Longitude);
    }

    [Fact]
    public void FlightImage_Properties_SetAndGetRoundTrip()
    {
        var data = new byte[] { 1, 2, 3 };
        var img = new FlightImage
        {
            Id = 3,
            FlightPreparationId = 5,
            Section = "Meteo",
            FileName = "metar.png",
            ContentType = "image/png",
            Data = data,
            Order = 1
        };

        Assert.Equal(3, img.Id);
        Assert.Equal(5, img.FlightPreparationId);
        Assert.Equal("Meteo", img.Section);
        Assert.Equal("metar.png", img.FileName);
        Assert.Equal("image/png", img.ContentType);
        Assert.Equal(data, img.Data);
        Assert.Equal(1, img.Order);
    }

    [Fact]
    public void FlightImage_FlightPreparation_NavigationProperty_CanBeSet()
    {
        var fp = new FlightPreparation();
        var img = new FlightImage { FlightPreparation = fp };

        Assert.Same(fp, img.FlightPreparation);
    }

    [Fact]
    public void WindLevel_Properties_SetAndGetRoundTrip()
    {
        var wl = new WindLevel
        {
            Id = 4,
            FlightPreparationId = 7,
            AltitudeFt = 2000,
            DirectionDeg = 270,
            SpeedKt = 12,
            TempC = -5.5,
            Order = 2
        };

        Assert.Equal(4, wl.Id);
        Assert.Equal(7, wl.FlightPreparationId);
        Assert.Equal(2000, wl.AltitudeFt);
        Assert.Equal(270, wl.DirectionDeg);
        Assert.Equal(12, wl.SpeedKt);
        Assert.Equal(-5.5, wl.TempC);
        Assert.Equal(2, wl.Order);
    }

    [Fact]
    public void WindLevel_FlightPreparation_NavigationProperty_CanBeSet()
    {
        var fp = new FlightPreparation();
        var wl = new WindLevel { FlightPreparation = fp };

        Assert.Same(fp, wl.FlightPreparation);
    }

    [Fact]
    public void Passenger_Properties_SetAndGetRoundTrip()
    {
        var fp = new FlightPreparation();
        var p = new Passenger
        {
            Id = 10,
            FlightPreparationId = 5,
            FlightPreparation = fp,
            Name = "Jan Janssen",
            WeightKg = 75.5,
            Order = 0
        };

        Assert.Equal(10, p.Id);
        Assert.Equal(5, p.FlightPreparationId);
        Assert.Same(fp, p.FlightPreparation);
        Assert.Equal("Jan Janssen", p.Name);
        Assert.Equal(75.5, p.WeightKg);
        Assert.Equal(0, p.Order);
    }

    [Fact]
    public void Pilot_Properties_SetAndGetRoundTrip()
    {
        var p = new Pilot { Id = 11, Name = "Nick Thys", WeightKg = 80.0 };

        Assert.Equal(11, p.Id);
        Assert.Equal("Nick Thys", p.Name);
        Assert.Equal(80.0, p.WeightKg);
    }

    [Fact]
    public void Pilot_WeightKg_IsNullByDefault() => Assert.Null(new Pilot().WeightKg);

    [Fact]
    public void GoNoGoSettings_DefaultValues_AreCorrect()
    {
        var s = new GoNoGoSettings();

        Assert.Equal(0, s.Id);
        Assert.Equal(10, s.WindYellowKt);
        Assert.Equal(15, s.WindRedKt);
        Assert.Equal(5, s.VisYellowKm);
        Assert.Equal(3, s.VisRedKm);
        Assert.Equal(300, s.CapeYellowJkg);
        Assert.Equal(500, s.CapeRedJkg);
    }

    [Fact]
    public void GoNoGoSettings_Properties_CanBeOverridden()
    {
        var s = new GoNoGoSettings
        {
            WindYellowKt = 8,
            WindRedKt = 12,
            VisYellowKm = 4,
            VisRedKm = 2,
            CapeYellowJkg = 250,
            CapeRedJkg = 450
        };

        Assert.Equal(8, s.WindYellowKt);
        Assert.Equal(12, s.WindRedKt);
        Assert.Equal(4, s.VisYellowKm);
        Assert.Equal(2, s.VisRedKm);
        Assert.Equal(250, s.CapeYellowJkg);
        Assert.Equal(450, s.CapeRedJkg);
    }

    [Fact]
    public void FlightPreparation_AllProperties_CanBeSetAndRead()
    {
        var fp = new FlightPreparation
        {
            Id = 1,
            Date = new DateOnly(2026, 3, 26),
            Time = new TimeOnly(9, 0),
            BalloonId = 2,
            PilotId = 3,
            LocationId = 4,
            FieldOwnerNotified = true,
            SurfaceWindSpeedKt = 8,
            SurfaceWindDirectionDeg = 220,
            Metar = "EBBR 261020Z",
            Taf = "TAF EBBR",
            WindByAltitude = "220/08",
            Precipitation = "NONE",
            TemperatureC = 15.0,
            DewPointC = 8.0,
            QnhHpa = 1013.25,
            VisibilityKm = 15,
            CapeJkg = 50,
            NotamsGecontroleerd = "JA",
            AirspaceStructure = "CTR Brussel",
            Restrictions = "Geen",
            Obstacles = "Windmolens N",
            FirstAidAndExtinguisher = "JA",
            PassengerListFilled = "JA",
            FlightPlanFiled = "JA",
            BurnerTested = true,
            GasCylindersChecked = true,
            BalloonVisual = true,
            MooringsChecked = true,
            InstrumentsWorking = true,
            PaxBriefing = "<p>Welkom</p>",
            EnvelopeWeightKg = 200,
            MaxAltitudeFt = 3000,
            LiftUnits = 600,
            TotaalLiftKg = 600,
            LoadNotes = "OK",
            Route = "Noord",
            IsFlown = true,
            ActualLandingNotes = "Veldje",
            ActualFlightDurationMinutes = 75,
            ActualRemarks = "Mooi",
            KmlTrack = "<kml/>",
            BalloonBulletin = "Bulletin tekst"
        };

        Assert.Equal(1, fp.Id);
        Assert.Equal(new DateOnly(2026, 3, 26), fp.Date);
        Assert.True(fp.FieldOwnerNotified);
        Assert.Equal("EBBR 261020Z", fp.Metar);
        Assert.Equal("TAF EBBR", fp.Taf);
        Assert.Equal("JA", fp.NotamsGecontroleerd);
        Assert.True(fp.BurnerTested);
        Assert.True(fp.IsFlown);
        Assert.Equal(75, fp.ActualFlightDurationMinutes);
        Assert.Equal("<kml/>", fp.KmlTrack);
        Assert.Equal(600, fp.TotaalLiftKg);
    }

    [Fact]
    public void FlightPreparation_DefaultCollections_AreEmpty()
    {
        var fp = new FlightPreparation();

        Assert.Empty(fp.Passengers);
        Assert.Empty(fp.Images);
        Assert.Empty(fp.WindLevels);
    }

    // ── Column attribute preservation (Dutch → English rename, issue #34) ────

    /// <summary>
    ///     Verifies that each renamed English property still carries the original
    ///     Dutch <see cref="ColumnAttribute"/> so EF Core maps to the unchanged
    ///     database column and no migration is required.
    /// </summary>
    [Theory]
    [InlineData(nameof(FlightPreparation.Date),                   "Datum")]
    [InlineData(nameof(FlightPreparation.Time),                   "Tijdstip")]
    [InlineData(nameof(FlightPreparation.Precipitation),          "Neerslag")]
    [InlineData(nameof(FlightPreparation.TemperatureC),           "TemperatuurC")]
    [InlineData(nameof(FlightPreparation.DewPointC),              "DauwpuntC")]
    [InlineData(nameof(FlightPreparation.VisibilityKm),           "ZichtbaarheidKm")]
    [InlineData(nameof(FlightPreparation.AirspaceStructure),      "Luchtruimstructuur")]
    [InlineData(nameof(FlightPreparation.Restrictions),           "Beperkingen")]
    [InlineData(nameof(FlightPreparation.Obstacles),              "Obstakels")]
    [InlineData(nameof(FlightPreparation.FirstAidAndExtinguisher),"EhboEnBlusser")]
    [InlineData(nameof(FlightPreparation.PassengerListFilled),    "PassagierslijstIngevuld")]
    [InlineData(nameof(FlightPreparation.FlightPlanFiled),        "VluchtplanIngediend")]
    [InlineData(nameof(FlightPreparation.BurnerTested),           "BranderGetest")]
    [InlineData(nameof(FlightPreparation.GasCylindersChecked),    "GasflaconsGecontroleerd")]
    [InlineData(nameof(FlightPreparation.BalloonVisual),          "BallonVisueel")]
    [InlineData(nameof(FlightPreparation.MooringsChecked),        "VerankeringenGecontroleerd")]
    [InlineData(nameof(FlightPreparation.InstrumentsWorking),     "InstrumentenWerkend")]
    [InlineData(nameof(FlightPreparation.Route),                  "Traject")]
    [InlineData(nameof(FlightPreparation.BalloonBulletin),        "Ballonbulletin")]
    [InlineData(nameof(FlightPreparation.FieldOwnerNotified),     "VeldEigenaarGemeld")]
    [InlineData(nameof(FlightPreparation.WindByAltitude),         "WindPerHoogte")]
    public void FlightPreparation_RenamedProperty_HasOriginalDutchColumnAttribute(
        string propertyName, string expectedColumnName)
    {
        // Arrange
        var prop = typeof(FlightPreparation).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        // Act
        var attr = prop?.GetCustomAttribute<ColumnAttribute>();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal(expectedColumnName, attr.Name);
    }

    // ── English property round-trip (all 21 renamed properties) ─────────────

    [Fact]
    public void FlightPreparation_RenamedStringProperties_RoundTrip()
    {
        // Arrange & Act
        var fp = new FlightPreparation
        {
            Precipitation     = "RAIN",
            AirspaceStructure = "CTR Brussel",
            Restrictions      = "Geen",
            Obstacles         = "Windmolens N",
            WindByAltitude    = "220/08",
            Route             = "Noord",
            BalloonBulletin   = "Bulletin 2024-01",
        };

        // Assert
        Assert.Equal("RAIN",           fp.Precipitation);
        Assert.Equal("CTR Brussel",    fp.AirspaceStructure);
        Assert.Equal("Geen",           fp.Restrictions);
        Assert.Equal("Windmolens N",   fp.Obstacles);
        Assert.Equal("220/08",         fp.WindByAltitude);
        Assert.Equal("Noord",          fp.Route);
        Assert.Equal("Bulletin 2024-01", fp.BalloonBulletin);
    }

    [Fact]
    public void FlightPreparation_RenamedNumericProperties_RoundTrip()
    {
        // Arrange & Act
        var fp = new FlightPreparation
        {
            TemperatureC = 18.5,
            DewPointC    = 10.0,
            VisibilityKm = 12.3,
        };

        // Assert
        Assert.Equal(18.5, fp.TemperatureC);
        Assert.Equal(10.0, fp.DewPointC);
        Assert.Equal(12.3, fp.VisibilityKm);
    }

    [Fact]
    public void FlightPreparation_RenamedBoolProperties_RoundTrip()
    {
        // Arrange & Act
        var fp = new FlightPreparation
        {
            FieldOwnerNotified = true,
            BurnerTested       = true,
            GasCylindersChecked = true,
            BalloonVisual      = true,
            MooringsChecked    = true,
            InstrumentsWorking = true,
        };

        // Assert
        Assert.True(fp.FieldOwnerNotified);
        Assert.True(fp.BurnerTested);
        Assert.True(fp.GasCylindersChecked);
        Assert.True(fp.BalloonVisual);
        Assert.True(fp.MooringsChecked);
        Assert.True(fp.InstrumentsWorking);
    }

    [Fact]
    public void FlightPreparation_RenamedStatusProperties_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var fp = new FlightPreparation();

        // Assert — defaults defined in the model must be preserved after rename
        Assert.Equal("NEE", fp.FirstAidAndExtinguisher);
        Assert.Equal("NEE", fp.PassengerListFilled);
        Assert.Equal("NVT", fp.FlightPlanFiled);
    }

    [Fact]
    public void FlightPreparation_RenamedStatusProperties_CanBeOverridden()
    {
        // Arrange & Act
        var fp = new FlightPreparation
        {
            FirstAidAndExtinguisher = "JA",
            PassengerListFilled     = "JA",
            FlightPlanFiled         = "JA",
        };

        // Assert
        Assert.Equal("JA", fp.FirstAidAndExtinguisher);
        Assert.Equal("JA", fp.PassengerListFilled);
        Assert.Equal("JA", fp.FlightPlanFiled);
    }

    [Fact]
    public void FlightPreparation_RenamedDateAndTimeProperties_RoundTrip()
    {
        // Arrange
        var date = new DateOnly(2025, 6, 1);
        var time = new TimeOnly(7, 30);

        // Act
        var fp = new FlightPreparation { Date = date, Time = time };

        // Assert
        Assert.Equal(date, fp.Date);
        Assert.Equal(time, fp.Time);
    }
}
