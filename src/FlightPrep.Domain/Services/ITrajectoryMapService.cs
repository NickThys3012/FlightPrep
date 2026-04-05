namespace FlightPrep.Domain.Services;

public interface ITrajectoryMapService
{
    Task<byte[]?> RenderAsync(string? trajectorySimulationJson);
}
