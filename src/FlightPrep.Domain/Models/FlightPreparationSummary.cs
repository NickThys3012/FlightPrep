namespace FlightPrep.Domain.Models;

/// <summary>
///     Lightweight projection used by the FlightList page.
///     Contains only the fields needed for display and Go/No-Go computation.
///     No heavy navigation collections are loaded.
/// </summary>
public record FlightPreparationSummary(
    int Id,
    DateOnly Date,
    TimeOnly Time,
    bool IsFlown,
    string? BalloonRegistration,
    string? PilotName,
    string? LocationName,
    double? SurfaceWindSpeedKt,
    double? VisibilityKm,
    double? CapeJkg,
    string? CreatedByUserId)
{
    /// <summary>True when this prep belongs to another user and was shared with the current user.</summary>
    public bool IsShared { get; init; }

    /// <summary>Display name of the owner who shared this prep (null when not shared).</summary>
    public string? SharedByName { get; init; }
}
