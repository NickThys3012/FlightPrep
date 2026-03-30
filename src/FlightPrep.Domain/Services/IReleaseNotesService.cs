using FlightPrep.Models.ReleaseNotes;

namespace FlightPrep.Services;

public interface IReleaseNotesService
{
    Task<ReleaseNotesDocument> GetAsync();
}
