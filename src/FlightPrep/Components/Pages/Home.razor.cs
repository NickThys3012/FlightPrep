using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace FlightPrep.Components.Pages;

public partial class Home : ComponentBase
{
    private int _totalFlights;
    private int _flightsThisYear;
    private int _flightsFlown;
    private List<FlightPreparation>? _recentFlights;
    private GoNoGoSettings _goNoGoSettings = new();
    private string? _userId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = authState.User.IsInRole("Admin");

        _goNoGoSettings = await GoNoGoSvc.GetSettingsAsync(_userId);

        var (total, thisYear, flown) = await FpSvc.GetFlightCountsAsync(_userId, isAdmin);
        _totalFlights = total;
        _flightsThisYear = thisYear;
        _flightsFlown = flown;

        _recentFlights = await FpSvc.GetRecentAsync(5, _userId, isAdmin);
    }

}
