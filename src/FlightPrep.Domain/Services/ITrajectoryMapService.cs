namespace FlightPrep.Services;

public interface ITrajectoryMapService
{
    Task<byte[]?> RenderAsync(string? trajectorySimulationJson);
}
