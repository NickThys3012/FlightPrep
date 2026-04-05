namespace FlightPrep.Domain.Services;

public interface ISunriseService
{
    (TimeOnly Sunrise, TimeOnly Sunset) Calculate(DateOnly date, double latDeg, double lonDeg);
}
