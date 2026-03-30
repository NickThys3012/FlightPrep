namespace FlightPrep.Services;

/// <summary>
/// Holds pre-computed flight assessment values.
/// </summary>
/// <param name="TotaalGewicht">Total weight in kg (envelope + pilot + passengers).</param>
/// <param name="LiftVoldoende">True when total lift exceeds total weight.</param>
/// <param name="GoNoGo">Go/No-Go colour string: "green" | "yellow" | "red" | "unknown".</param>
public record FlightAssessment(
    double TotaalGewicht,
    bool LiftVoldoende,
    string GoNoGo);
