using Microsoft.AspNetCore.Components;

namespace FlightPrep.Components;

public partial class FlightGoNoGoBadge : ComponentBase
{
    /// <summary>GoNoGo result string: "green", "yellow", "red", or "unknown".</summary>
    [Parameter]
    public string GoNoGoResult { get; set; } = "unknown";

    /// <summary>When true, prefixes the label with an emoji (✅/⚠️/🔴/⬜).</summary>
    [Parameter]
    public bool ShowEmoji { get; set; }

    private string CssClass => GoNoGoResult switch
    {
        "green" => "go-badge-go",
        "yellow" => "go-badge-caution",
        "red" => "go-badge-nogo",
        _ => "go-badge-unknown"
    };

    private string BadgeText => (GoNoGoResult, ShowEmoji) switch
    {
        ("green", true) => "✅ GO",
        ("yellow", true) => "⚠️ CAUTION",
        ("red", true) => "🔴 NO-GO",
        ("green", false) => "GO",
        ("yellow", false) => "CAUTION",
        ("red", false) => "NO-GO",
        _ => ShowEmoji ? "⬜ Go/No-Go onbekend" : "–"
    };

}
