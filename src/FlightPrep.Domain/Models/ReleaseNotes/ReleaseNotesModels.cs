using System.Text.Json.Serialization;

namespace FlightPrep.Domain.Models.ReleaseNotes;

public class ReleaseNotesDocument
{
    public string CurrentVersion { get; set; } = "0.0.0";
    public List<ReleaseEntry> Entries { get; set; } = [];
}

public class ReleaseEntry
{
    public int Pr { get; set; }
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; } = "";
    public string Author { get; } = "";

    [JsonPropertyName("labels")] public List<string> Labels { get; } = [];

    public DateTime Date { get; }
}
