using OpenTelemetry;
using System.Diagnostics;

namespace FlightPrep.Telemetry;

/// <summary>
/// OpenTelemetry processor that enriches every trace span with flight and pilot context.
/// Runs at HTTP level only — Blazor circuit messages do not carry an HttpContext.
/// </summary>
public sealed class FlightTelemetryInitializer : BaseProcessor<Activity>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FlightTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    public override void OnStart(Activity data)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;

        // Tag authenticated pilot identity (forward-compatible — auth not yet implemented)
        if (ctx.User.Identity?.IsAuthenticated == true)
            data.SetTag("enduser.id", ctx.User.Identity.Name);

        // Enrich with flight ID from route when on a flight detail/edit page
        // Route parameter is named "Id" for /flights/{Id:int} and /flights/{Id:int}/edit
        if (ctx.Request.RouteValues.TryGetValue("Id", out var flightId)
            && flightId is not null)
        {
            data.SetTag("flightId", flightId.ToString()!);
        }
    }
}
