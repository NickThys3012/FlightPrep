using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

public interface IFlightPreparationService
{
    Task<List<Balloon>> GetBalloonsAsync();
    Task<List<Pilot>> GetPilotsAsync();
    Task<List<Location>> GetLocationsAsync();
    Task<List<FlightPreparationSummary>> GetSummariesAsync(string? userId, bool isAdmin);
    Task<(List<FlightPreparationSummary> Items, int Total)> GetSummariesPagedAsync(
        string? userId, bool isAdmin, string statusFilter, int page, int pageSize);
    Task<FlightPreparation?> GetByIdAsync(int id);
    Task<int> SaveAsync(FlightPreparation fp);
    Task DeleteAsync(int id, string userId, bool isAdmin = false);
    Task PatchTrajectoryJsonAsync(int id, string? json);
    Task PatchKmlTrackAsync(int id, string kml);
    Task PatchFlownAsync(int id, bool isFlown, string? landingNotes, int? durationMinutes, string? remarks,
        double? fuelConsumptionL, string? landingLocationText, bool? visibleDefects, string? visibleDefectsNotes);
    Task<(int Total, int ThisYear, int Flown)> GetFlightCountsAsync(string? userId, bool isAdmin);
    Task<List<FlightPreparation>> GetRecentAsync(int count, string? userId, bool isAdmin);
    Task<List<FlightPreparation>> GetAllWithNavAsync(string? userId, bool isAdmin);

    // ── Sharing ───────────────────────────────────────────────────────────────
    Task<List<ApplicationUserSummary>> GetShareableUsersAsync(int flightId, string ownerId);
    Task<List<ApplicationUserSummary>> GetSharesAsync(int flightId, string ownerId);
    Task ShareAsync(int flightId, string ownerId, string targetUserId);
    Task RevokeShareAsync(int flightId, string ownerId, string targetUserId);
    Task<bool> IsSharedWithAsync(int flightId, string userId);
}
