namespace FlightPrep.Domain.Models.Trajectory;

public record TrajectoryPoint(
    double Lat,
    double Lon,
    double? AltitudeFt = null,
    int? ElapsedMinutes = null);
