using FlightPrep.Domain.Models.ReleaseNotes;

namespace FlightPrep.Domain.Services;

public interface IReleaseNotesService
{
    Task<ReleaseNotesDocument> GetAsync();
}
