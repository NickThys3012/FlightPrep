namespace FlightPrep.Domain.Models.Trajectory;

public enum TrajectoryDataSource { Manual, OpenMeteo, Hysplit }

public record SimulatedTrajectory(
    int AltitudeFt,
    string Color,
    List<TrajectoryPoint> Points,
    TrajectoryDataSource DataSource,
    DateTime SimulatedAt,
    int DurationMinutes,
    double? AscentRateMs = null,
    double? DescentRateMs = null);
