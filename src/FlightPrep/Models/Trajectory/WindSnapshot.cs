using FlightPrep.Models;

namespace FlightPrep.Models.Trajectory;

/// <summary>One hourly wind observation at multiple pressure levels.</summary>
public record WindSnapshot(DateTime TimeUtc, IReadOnlyList<WindLevel> Levels);
