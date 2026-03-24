namespace FlightPrep.Models;

public class FlightPreparation
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Section 1 - Algemene Gegevens
    public DateOnly Datum { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly Tijdstip { get; set; } = TimeOnly.FromDateTime(DateTime.Now);
    public int? BalloonId { get; set; }
    public Balloon? Balloon { get; set; }
    public int? PilotId { get; set; }
    public Pilot? Pilot { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public bool VeldEigenaarGemeld { get; set; }

    // Section 2 - Meteorologische Informatie
    public double? SurfaceWindSpeedKt { get; set; }
    public int? SurfaceWindDirectionDeg { get; set; }
    public string? Metar { get; set; }
    public string? Taf { get; set; }
    public string? WindPerHoogte { get; set; }
    public string? Neerslag { get; set; }
    public double? TemperatuurC { get; set; }
    public double? DauwpuntC { get; set; }
    public double? QnhHpa { get; set; }
    public double? ZichtbaarheidKm { get; set; }
    public double? CapeJkg { get; set; }

    // Section 3 - Luchtruim en NOTAMs
    public string NotamsGecontroleerd { get; set; } = "NEE";
    public string? Luchtruimstructuur { get; set; }
    public string? Beperkingen { get; set; }
    public string? Obstakels { get; set; }

    // Section 4 - Veiligheid en Communicatie
    public string EhboEnBlusser { get; set; } = "NEE";
    public string PassagierslijstIngevuld { get; set; } = "NEE";
    public string VluchtplanIngediend { get; set; } = "NVT";

    // Section 5 - Technische Controle
    public bool BranderGetest { get; set; }
    public bool GasflaconsGecontroleerd { get; set; }
    public bool BallonVisueel { get; set; }
    public bool VerankeringenGecontroleerd { get; set; }
    public bool InstrumentenWerkend { get; set; }

    // Section 6 - Pax Briefing
    public string? PaxBriefing { get; set; }

    // Section 7 - Load Calculation
    public double? EnvelopeWeightKg { get; set; }
    public List<Passenger> Passengers { get; set; } = new();
    public int? MaxAltitudeFt { get; set; }
    public double? LiftUnits { get; set; }
    public double? TotaalLiftKg { get; set; }
    public string? LoadNotes { get; set; }

    // Section 8 - Traject
    public string? Traject { get; set; }

    // Trajectory prediction
    public string? TrajectoryLevels { get; set; }  // comma-separated hPa levels, e.g. "850,800,750"
    public int TrajectoryDurationMin { get; set; } = 90;

    // Mark as flown
    public bool IsFlown { get; set; }
    public string? ActualLandingNotes { get; set; }
    public int? ActualFlightDurationMinutes { get; set; }
    public string? ActualRemarks { get; set; }

    // KML flight track
    public string? KmlTrack { get; set; }

    // Images (stored in separate table)
    public List<FlightImage> Images { get; set; } = new();

    // Wind profile (stored in separate table)
    public List<WindLevel> WindLevels { get; set; } = new();

    // Section 9 - Ballonbulletin
    public string? Ballonbulletin { get; set; }

    // Computed helpers (not mapped)
    public double TotaalGewicht =>
        (EnvelopeWeightKg ?? 0)
        + (Pilot?.WeightKg ?? 0)
        + Passengers.Sum(p => p.WeightKg);

    public bool LiftVoldoende =>
        TotaalLiftKg.HasValue && TotaalLiftKg.Value > TotaalGewicht;

    public string GoNoGo
    {
        get
        {
            bool hasData = SurfaceWindSpeedKt.HasValue || ZichtbaarheidKm.HasValue || CapeJkg.HasValue;
            if (!hasData) return "unknown";
            bool red = (SurfaceWindSpeedKt >= 15) || (ZichtbaarheidKm < 3) || (CapeJkg > 500);
            bool yellow = (SurfaceWindSpeedKt >= 10) || (ZichtbaarheidKm < 5) || (CapeJkg > 300);
            return red ? "red" : yellow ? "yellow" : "green";
        }
    }
}
