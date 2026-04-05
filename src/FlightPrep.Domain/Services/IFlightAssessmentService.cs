using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

public interface IFlightAssessmentService
{
    Task<FlightAssessment> ComputeAsync(FlightPreparation fp, string? userId = null);
    FlightAssessment Compute(FlightPreparation fp, GoNoGoSettings settings);
}
