using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Components;
using System.Security.Claims;

namespace FlightPrep.Components.Pages;

public partial class FlightList : ComponentBase
{
    private List<FlightPreparationSummary>? _flights;
    private GoNoGoSettings _goNoGoSettings = new();
    private int? _deleteId;
    private bool _sortDesc = true;
    private int _currentPage = 1;
    private const int PageSize = 20;
    private string? _userId;
    private bool _isAdmin;
    private string _statusFilter = "alle"; // "alle" | "gevlogen" | "niet-gevlogen" | "gedeeld"

    private IEnumerable<FlightPreparationSummary> FilteredFlights =>
        _statusFilter switch
        {
            "gevlogen"      => _flights!.Where(f => f.IsFlown),
            "niet-gevlogen" => _flights!.Where(f => !f.IsFlown),
            "gedeeld"       => _flights!.Where(f => f.IsShared),
            _               => _flights!
        };

    private IEnumerable<FlightPreparationSummary> SortedFlights =>
        _sortDesc
            ? FilteredFlights.OrderByDescending(f => f.Datum).ThenByDescending(f => f.Tijdstip)
            : FilteredFlights.OrderBy(f => f.Datum).ThenBy(f => f.Tijdstip);

    private int TotalPages => (int)Math.Ceiling(FilteredFlights.Count() / (double)PageSize);

    private IEnumerable<FlightPreparationSummary> PagedFlights =>
        SortedFlights.Skip((_currentPage - 1) * PageSize).Take(PageSize);

    private void ToggleSort()
    {
        _sortDesc = !_sortDesc;
        _currentPage = 1;
    }

    private void SetFilter(string filter)
    {
        _statusFilter = filter;
        _currentPage = 1;
    }

    protected override async Task OnInitializedAsync() => await LoadFlights();

    private string GetGoNoGo(FlightPreparationSummary f) =>
        GoNoGoSvc.Compute(f.SurfaceWindSpeedKt, f.ZichtbaarheidKm, f.CapeJkg, _goNoGoSettings);

    private async Task LoadFlights()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _isAdmin = authState.User.IsInRole("Admin");

        _goNoGoSettings = await GoNoGoSvc.GetSettingsAsync(_userId);

        _flights = await FpSvc.GetSummariesAsync(_userId, _isAdmin);
    }

    private void ConfirmDelete(int id) => _deleteId = id;

    private async Task DeleteFlight()
    {
        if (!_deleteId.HasValue) return;
        await FpSvc.DeleteAsync(_deleteId.Value, _userId!, _isAdmin);
        _deleteId = null;
        await LoadFlights();
    }
}
