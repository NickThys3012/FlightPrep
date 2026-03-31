namespace FlightPrep.Services;

public interface IKmlService
{
    List<TrackPoint> ParseCoordinates(string kml);
    (double MaxAltM, double MinAltM, double DistanceKm) ComputeStats(List<TrackPoint> pts);
}
