using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

/// <summary>
///     Encapsulates all persistence operations for <see cref="Pilot" /> entities.
///     Pages must not access <c>IDbContextFactory</c> directly.
/// </summary>
public interface IPilotService
{
    /// <summary>Returns all pilots visible to the caller, ordered by name.</summary>
    Task<List<Pilot>> GetAllAsync(string? userId, bool isAdmin);

    /// <summary>Updates an existing pilot. Ownership is enforced unless caller is admin.</summary>
    Task UpdateAsync(Pilot editPilot, string? userId, bool isAdmin);

    /// <summary>Adds a new pilot owned by <paramref name="userId" />.</summary>
    Task AddAsync(Pilot newPilot, string? userId);

    /// <summary>Deletes a pilot by id. Ownership is enforced unless caller is admin.</summary>
    Task DeleteAsync(int id, string? userId, bool isAdmin);
}
