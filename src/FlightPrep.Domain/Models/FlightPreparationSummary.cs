namespace FlightPrep.Domain.Models;

/// <summary>
///     Lightweight projection used by the FlightList page.
///     Contains only the fields needed for display and Go/No-Go computation.
///     No heavy navigation collections are loaded.
/// </summary>
public record FlightPreparationSummary(
    int Id,
    DateOnly Datum,
    TimeOnly Tijdstip,
    bool IsFlown,
    string? BalloonRegistration,
    string? PilotName,
    string? LocationName,
    double? SurfaceWindSpeedKt,
    double? ZichtbaarheidKm,
    double? CapeJkg,
    string? CreatedByUserId);
