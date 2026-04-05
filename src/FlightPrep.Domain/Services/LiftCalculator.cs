// ReSharper disable InconsistentNaming

namespace FlightPrep.Domain.Services;

/// <summary>
///     ISA-based total lift formula — Cameron Hot Air Balloon Flight Manual,
///     Amendment 18, Appendix 2, Page A2-1.
/// </summary>
public static class LiftCalculator
{
    private const double MaxTi = 100.0;

    /// <param name="A">Maximum planned flight altitude AMSL in metres</param>
    /// <param name="Eg">Take-off site elevation AMSL in metres</param>
    /// <param name="Tg">Ambient temperature at take-off site in °C (= FlightPreparation.TemperatuurC)</param>
    /// <param name="Ti">Average internal envelope temperature in °C — capped at 100°C</param>
    /// <param name="V">Envelope volume in m³</param>
    public static LiftResult Calculate(double A, double Eg, double Tg, double Ti, double V)
    {
        Ti = Math.Clamp(Ti, 0.0, MaxTi);
        var ta_at = Tg - (0.0065 * (A - Eg));
        var P = 1013.25 * Math.Pow(1.0 - (0.0065 * A / 288.15), 5.256);
        var L = 0.3484 * V * P * ((1.0 / (ta_at + 273.15)) - (1.0 / (Ti + 273.15)));
        return new LiftResult(Math.Round(ta_at, 2), Math.Round(P, 2), Math.Round(L, 1));
    }
}

public record LiftResult(double AmbientTempAtAltC, double PressureHpa, double TotalLiftKg);
