namespace FlightPrep.Models;

public class WindLevel
{
    public int Id { get; set; }
    public int FlightPreparationId { get; set; }
    public FlightPreparation? FlightPreparation { get; set; }
    public int AltitudeFt { get; set; }
    public int? DirectionDeg { get; set; }
    public int? SpeedKt { get; set; }
    public double? TempC { get; set; }
    public int Order { get; set; }
}
