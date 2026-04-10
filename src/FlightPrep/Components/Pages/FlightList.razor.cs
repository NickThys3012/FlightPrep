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
    private int _totalCount;
    private string? _userId;
    private bool _isAdmin;
    private string _statusFilter = "alle"; // "alle" | "gevlogen" | "niet-gevlogen" | "gedeeld"

    private int TotalPages => (int)Math.Ceiling(_totalCount / (double)PageSize);

    // Count helpers for the filter bar — loaded separately so the badges stay accurate
    private int _totalAll;
    private int _totalFlown;
    private int _totalNotFlown;
    private int _totalShared;

    private async Task ToggleSort()
    {
        _sortDesc = !_sortDesc;
        _currentPage = 1;
        await LoadFlights();
    }

    private async Task SetFilter(string filter)
    {
        _statusFilter = filter;
        _currentPage = 1;
        await LoadFlights();
    }

    protected override async Task OnInitializedAsync() => await LoadFlights();

    private string GetGoNoGo(FlightPreparationSummary f) =>
        GoNoGoSvc.Compute(f.SurfaceWindSpeedKt, f.VisibilityKm, f.CapeJkg, _goNoGoSettings);

    private async Task LoadFlights()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _isAdmin = authState.User.IsInRole("Admin");

        _goNoGoSettings = await GoNoGoSvc.GetSettingsAsync(_userId);

        // Server-side pagination — only fetch the current page from the DB
        var (items, total) = await FpSvc.GetSummariesPagedAsync(
            _userId, _isAdmin, _statusFilter, _currentPage, PageSize, _sortDesc);

        _flights   = items;
        _totalCount = total;

        // Fetch counts for the filter bar badges using the unpaged method
        var allFlights = await FpSvc.GetSummariesAsync(_userId, _isAdmin);
        _totalAll      = allFlights.Count;
        _totalFlown    = allFlights.Count(f => f.IsFlown);
        _totalNotFlown = allFlights.Count(f => !f.IsFlown);
        _totalShared   = allFlights.Count(f => f.IsShared);
    }

    private async Task GoToPage(int page)
    {
        _currentPage = Math.Clamp(page, 1, Math.Max(1, TotalPages));
        await LoadFlights();
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
