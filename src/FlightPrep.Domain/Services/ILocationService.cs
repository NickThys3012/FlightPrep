using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

/// <summary>
///     Encapsulates all persistence operations for <see cref="Location" /> entities.
///     Pages must not access <c>IDbContextFactory</c> directly.
/// </summary>
public interface ILocationService
{
    /// <summary>Returns all locations visible to the caller, ordered by name.</summary>
    Task<List<Location>> GetAllAsync(string? userId, bool isAdmin);

    /// <summary>Updates an existing location. Ownership is enforced unless caller is admin.</summary>
    Task UpdateAsync(Location editLoc, string? userId, bool isAdmin);

    /// <summary>Adds a new location owned by <paramref name="userId" />.</summary>
    Task AddAsync(Location newLoc, string? userId);

    /// <summary>Deletes a location by id. Ownership is enforced unless caller is admin.</summary>
    Task DeleteAsync(int id, string? userId, bool isAdmin);
}
