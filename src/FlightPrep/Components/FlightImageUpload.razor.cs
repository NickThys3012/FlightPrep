using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Specialized;

namespace FlightPrep.Components;

public partial class FlightImageUpload : ComponentBase
{
    /// <summary>Human-readable label shown above the file picker.</summary>
    [Parameter]
    public string Label { get; set; } = "";

    /// <summary>Section name stored on the <see cref="BitVector32.Section" /> property (e.g. "Meteo" or "Traject").</summary>
    [Parameter]
    public string Section { get; set; } = "";

    /// <summary>The image list this component manages. Mutations (add/remove) are applied in-place.</summary>
    [Parameter]
    public List<FlightImage> Images { get; set; } = null!;

    /// <summary>Rose after any adding or remove, so the parent can call StateHasChanged if needed.</summary>
    [Parameter]
    public EventCallback ImagesChanged { get; set; }

    private const long MaxImageBytes = 15 * 1024 * 1024; // 15 MB per image

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Images);
    }

    private async Task HandleUpload(InputFileChangeEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Images);
        foreach (var file in e.GetMultipleFiles(20))
        {
            if (file.Size > MaxImageBytes) continue;
            using var ms = new MemoryStream();
            await file.OpenReadStream(MaxImageBytes).CopyToAsync(ms);
            Images.Add(new FlightImage
            {
                Section = Section,
                FileName = file.Name,
                ContentType = file.ContentType,
                Data = ms.ToArray(),
                Order = Images.Count
            });
        }

        await ImagesChanged.InvokeAsync();
    }

    private async Task RemoveImage(FlightImage img)
    {
        Images.Remove(img);
        await ImagesChanged.InvokeAsync();
    }
}
