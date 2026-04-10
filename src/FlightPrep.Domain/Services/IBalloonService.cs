using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

/// <summary>
///     Encapsulates all persistence operations for <see cref="Balloon" /> entities.
///     Pages must not access <c>IDbContextFactory</c> directly.
/// </summary>
public interface IBalloonService
{
    /// <summary>Returns all balloons visible to the caller, ordered by registration.</summary>
    Task<List<Balloon>> GetAllAsync(string? userId, bool isAdmin);

    /// <summary>Updates an existing balloon. Ownership is enforced unless caller is admin.</summary>
    Task UpdateAsync(Balloon editBalloon, string? userId, bool isAdmin);

    /// <summary>Adds a new balloon owned by <paramref name="userId" />.</summary>
    Task AddAsync(Balloon newBalloon, string? userId);

    /// <summary>Deletes a balloon by id. Ownership is enforced unless caller is admin.</summary>
    Task DeleteAsync(int id, string? userId, bool isAdmin);
}
