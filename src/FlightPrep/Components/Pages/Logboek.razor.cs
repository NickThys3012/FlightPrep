using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace FlightPrep.Components.Pages;

public partial class Logboek : ComponentBase
{
       [Inject] private NavigationManager NavManager { get; set; } = null!;

    private List<FlightPreparation>? _flights;
    private int _totalFlights;
    private int _flownFlights;
    private int _plannedFlights;
    private int _totalMinutes;
    private IGrouping<string, FlightPreparation>? _mostUsedLocation;
    private IGrouping<string, FlightPreparation>? _mostUsedBalloon;
    private List<(string Label, int Count)>? _flightsByMonth;
    private List<(string Label, int Count)>? _flightsByLocation;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = authState.User.IsInRole("Admin");

        _flights = await FpSvc.GetAllWithNavAsync(userId, isAdmin);

        _totalFlights = _flights.Count;
        _flownFlights = _flights.Count(f => f.IsFlown);
        _plannedFlights = _flights.Count(f => !f.IsFlown);
        _totalMinutes = _flights.Where(f => f.ActualFlightDurationMinutes.HasValue)
            .Sum(f => f.ActualFlightDurationMinutes!.Value);

        _mostUsedLocation = _flights.Where(f => f.Location != null)
            .GroupBy(f => f.Location!.Name)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        _mostUsedBalloon = _flights.Where(f => f.Balloon != null)
            .GroupBy(f => f.Balloon!.Registration)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        _flightsByMonth = _flights
            .GroupBy(f => new { f.Datum.Year, f.Datum.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => ($"{g.Key.Year}-{g.Key.Month:D2}", g.Count()))
            .ToList();

        _flightsByLocation = _flights.Where(f => f.Location != null)
            .GroupBy(f => f.Location!.Name)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => (g.Key, g.Count()))
            .ToList();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _flightsByMonth != null)
        {
            await Js.InvokeVoidAsync("initLogboekCharts",
                _flightsByMonth.Select(x => x.Label).ToArray(),
                _flightsByMonth.Select(x => x.Count).ToArray(),
                _flightsByLocation!.Select(x => x.Label).ToArray(),
                _flightsByLocation!.Select(x => x.Count).ToArray());
        }
    }

    private static string FormatMinutes(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        return h > 0 ? $"{h}u {m}m" : $"{m}m";
    }

    private void Nav(int id)
    {
        NavManager.NavigateTo($"/flights/{id}");
    }

}
