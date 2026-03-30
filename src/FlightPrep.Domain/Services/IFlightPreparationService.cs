using FlightPrep.Models;

namespace FlightPrep.Services;

public interface IFlightPreparationService
{
    Task<List<Balloon>> GetBalloonsAsync();
    Task<List<Pilot>> GetPilotsAsync();
    Task<List<Location>> GetLocationsAsync();
    Task<List<FlightPreparationSummary>> GetSummariesAsync();
    Task<FlightPreparation?> GetByIdAsync(int id);
    Task<int> SaveAsync(FlightPreparation fp);
    Task DeleteAsync(int id);
    Task PatchTrajectoryJsonAsync(int id, string? json);
    Task PatchKmlTrackAsync(int id, string kml);
    Task PatchFlownAsync(int id, bool isFlown, string? landingNotes, int? durationMinutes, string? remarks);
    Task<(int Total, int ThisYear, int Flown)> GetFlightCountsAsync();
    Task<List<FlightPreparation>> GetRecentAsync(int count);
    Task<List<FlightPreparation>> GetAllWithNavAsync();
}
