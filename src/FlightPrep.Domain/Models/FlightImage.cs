namespace FlightPrep.Models;

public class FlightImage
{
    public int Id { get; set; }
    public int FlightPreparationId { get; set; }
    public FlightPreparation? FlightPreparation { get; set; }

    /// <summary>"Meteo" or "Traject"</summary>
    public string Section { get; set; } = "";

    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] Data { get; set; } = [];
    public int Order { get; set; }
}
