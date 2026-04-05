using FlightPrep.Models;

namespace FlightPrep.Services;

public interface IPdfService
{
    Task<byte[]> GenerateAsync(FlightPreparation fp, byte[]? mapPng = null, string? userId = null, CancellationToken ct = default);
}
