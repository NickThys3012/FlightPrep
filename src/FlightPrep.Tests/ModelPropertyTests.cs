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
            EmptyWeightKg = 250.5
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
            Datum = new DateOnly(2026, 3, 26),
            Tijdstip = new TimeOnly(9, 0),
            BalloonId = 2,
            PilotId = 3,
            LocationId = 4,
            VeldEigenaarGemeld = true,
            SurfaceWindSpeedKt = 8,
            SurfaceWindDirectionDeg = 220,
            Metar = "EBBR 261020Z",
            Taf = "TAF EBBR",
            WindPerHoogte = "220/08",
            Neerslag = "NONE",
            TemperatuurC = 15.0,
            DauwpuntC = 8.0,
            QnhHpa = 1013.25,
            ZichtbaarheidKm = 15,
            CapeJkg = 50,
            NotamsGecontroleerd = "JA",
            Luchtruimstructuur = "CTR Brussel",
            Beperkingen = "Geen",
            Obstakels = "Windmolens N",
            EhboEnBlusser = "JA",
            PassagierslijstIngevuld = "JA",
            VluchtplanIngediend = "JA",
            BranderGetest = true,
            GasflaconsGecontroleerd = true,
            BallonVisueel = true,
            VerankeringenGecontroleerd = true,
            InstrumentenWerkend = true,
            PaxBriefing = "<p>Welkom</p>",
            EnvelopeWeightKg = 200,
            MaxAltitudeFt = 3000,
            LiftUnits = 600,
            TotaalLiftKg = 600,
            LoadNotes = "OK",
            Traject = "Noord",
            IsFlown = true,
            ActualLandingNotes = "Veldje",
            ActualFlightDurationMinutes = 75,
            ActualRemarks = "Mooi",
            KmlTrack = "<kml/>",
            Ballonbulletin = "Bulletin tekst"
        };

        Assert.Equal(1, fp.Id);
        Assert.Equal(new DateOnly(2026, 3, 26), fp.Datum);
        Assert.True(fp.VeldEigenaarGemeld);
        Assert.Equal("EBBR 261020Z", fp.Metar);
        Assert.Equal("TAF EBBR", fp.Taf);
        Assert.Equal("JA", fp.NotamsGecontroleerd);
        Assert.True(fp.BranderGetest);
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
}
