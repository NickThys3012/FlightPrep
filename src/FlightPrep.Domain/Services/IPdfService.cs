using FlightPrep.Models;

namespace FlightPrep.Services;

public interface IPdfService
{
    Task<byte[]> GenerateAsync(FlightPreparation fp, byte[]? mapPng = null, CancellationToken ct = default);
}
