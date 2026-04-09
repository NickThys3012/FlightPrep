using FlightPrep.Domain.Models;

namespace FlightPrep.Domain.Services;

public interface IPdfService
{
    Task<byte[]> GenerateAsync(FlightPreparation fp, byte[]? mapPng = null, string? userId = null, string locale = "nl-BE");
    Task<byte[]> GenerateOfpAsync(FlightPreparation fp, double passengerEquipmentKg = 7);
}
