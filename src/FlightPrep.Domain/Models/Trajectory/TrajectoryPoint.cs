namespace FlightPrep.Models.Trajectory;

public record TrajectoryPoint(
    double Lat,
    double Lon,
    double? AltitudeFt = null,
    int? ElapsedMinutes = null);
