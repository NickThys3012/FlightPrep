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

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = authState.User.IsInRole("Admin");

        _goNoGoSettings = await GoNoGoSvc.GetSettingsAsync(userId);

        var (total, thisYear, flown) = await FpSvc.GetFlightCountsAsync();
        _totalFlights = total;
        _flightsThisYear = thisYear;
        _flightsFlown = flown;

        _recentFlights = await FpSvc.GetRecentAsync(5, userId, isAdmin);
    }

}
