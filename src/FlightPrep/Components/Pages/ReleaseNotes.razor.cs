using FlightPrep.Domain.Models.ReleaseNotes;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Text;

namespace FlightPrep.Components.Pages;

public partial class ReleaseNotes : ComponentBase
{
     private List<ReleaseEntry>? _entries;
    private string _currentVersion = "";

    protected override async Task OnInitializedAsync()
    {
        var doc = await ReleaseNotesSvc.GetAsync();
        _entries = doc.Entries;
        _currentVersion = doc.CurrentVersion;
    }

    // Converts markdown-style description (intro line + "- bullet" lines) to HTML.
    private static string RenderDescription(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var html = new StringBuilder();
        var inList = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                if (!inList)
                {
                    html.Append("<ul class=\"mb-0 ps-3\">");
                    inList = true;
                }

                html.Append($"<li>{WebUtility.HtmlEncode(line[2..])}</li>");
            }
            else
            {
                if (inList)
                {
                    html.Append("</ul>");
                    inList = false;
                }

                html.Append($"<p class=\"mb-2\">{WebUtility.HtmlEncode(line)}</p>");
            }
        }

        if (inList) html.Append("</ul>");
        return html.ToString();
    }

    private static string GetLabelClass(string label)
    {
        return label.ToLower() switch
        {
            "feature" or "enhancement" => "bg-primary",
            "bug" or "fix" or "bugfix" => "bg-danger",
            "docs" or "documentation" => "bg-success",
            "chore" or "maintenance" => "bg-secondary",
            "refactor" => "bg-info text-dark",
            "breaking" or "breaking change" => "bg-warning text-dark",
            _ => "bg-secondary"
        };
    }

    private static string GetRelativeDate(DateTime date)
    {
        var diff = DateTime.UtcNow - date.ToUniversalTime();
        return diff.TotalDays switch
        {
            < 1 => "vandaag",
            < 2 => "gisteren",
            < 7 => $"{(int)diff.TotalDays} dagen geleden",
            < 30 => $"{(int)(diff.TotalDays / 7)} weken geleden",
            < 365 => $"{(int)(diff.TotalDays / 30)} maanden geleden",
            _ => $"{(int)(diff.TotalDays / 365)} jaar geleden"
        };
    }
}
