# Copilot Instructions – FlightPrep

## What this app does

FlightPrep is a **Blazor Server** app (.NET 10) for Belgian hot-air balloon pilots to prepare and document flights. It
stores flight preparations in PostgreSQL, generates PDF reports, shows weather data and KML tracks on a Leaflet map, and
publishes to Azure App Service via GitHub Actions.

---

## Build & run commands

```bash
# Build main app
dotnet build src/FlightPrep/FlightPrep.csproj

# Run locally (needs a running Postgres — use docker compose)
docker compose up -d          # starts app on :8082 and Postgres on :5433
docker compose up -d --build  # rebuild image first

# Run unit tests (xUnit)
dotnet test src/FlightPrep.Tests/FlightPrep.Tests.csproj

# Run a single unit test by name
dotnet test src/FlightPrep.Tests/FlightPrep.Tests.csproj --filter "FullyQualifiedName~KmlServiceTests.ParseCoordinates_ValidKml"

# Run E2E tests (Playwright/NUnit) — requires app running on :8082
dotnet test src/FlightPrep.Tests.UI/FlightPrep.Tests.UI.csproj --settings src/FlightPrep.Tests.UI/NUnit.runsettings

# Run a single E2E test by name
dotnet test src/FlightPrep.Tests.UI/FlightPrep.Tests.UI.csproj --filter "Name=HomePageLoads"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project src/FlightPrep/FlightPrep.csproj
```

Migrations are applied **automatically** at startup (`db.Database.Migrate()` in `Program.cs`).

---

## Architecture

```
src/
  FlightPrep/             # Main Blazor Server app
    Components/
      App.razor           # Root: Leaflet + Chart.js + Quill setup, ALL global JS lives here
      Layout/             # NavMenu, MainLayout, ReconnectModal
      Pages/              # One .razor per route
    Data/
      AppDbContext.cs     # EF Core DbContext (PostgreSQL via Npgsql)
      Migrations/         # Auto-generated EF migrations
    Models/               # Plain C# classes, no annotations — relationships in OnModelCreating
    Services/             # Business logic, injected into pages
    wwwroot/
      app.css             # Custom CSS (fp-* utility classes)
      release-notes.json  # Updated by CI on every merged PR
  FlightPrep.Tests/       # xUnit unit tests
  FlightPrep.Tests.UI/    # Playwright E2E tests (NUnit)
.github/
  workflows/ci-cd.yml     # Single pipeline: build → release notes → publish → e2e → deploy
  scripts/
    generate-ai-description.mjs  # Calls GitHub Models API (gpt-4o-mini) for AI release notes
    update-release-notes.mjs     # Bumps version, prepends entry to release-notes.json
infra/
  main.bicep              # Azure infra (App Service + Postgres)
  deploy.sh               # Bicep deploy script
```

**Data flow:** Razor pages use `IDbContextFactory<AppDbContext>` (not `AppDbContext` directly) — always call
`await dbFactory.CreateDbContextAsync()` inside a `using` block. Services are short-lived; never store a `DbContext`
instance as a field.

---

## Key conventions

### Razor pages

- Every interactive page starts with `@rendermode InteractiveServer`
- Services are injected with `@inject` at the top, before `<PageTitle>`
- Pages use `IDbContextFactory<AppDbContext>` — never raw `AppDbContext`
- `OnAfterRenderAsync(bool firstRender)` is the place for JS interop calls (map, chart init)
- Guard JS calls with a local `bool mapInitialized` flag to prevent double-init on re-renders

### Services

| Service               | Lifetime               | Notes                                                                                        |
|-----------------------|------------------------|----------------------------------------------------------------------------------------------|
| `WeatherFetchService` | Scoped                 | Uses named `HttpClient`s: `"aviationweather"` + `"openmeteo"`                                |
| `WeatherService`      | Transient (HttpClient) | Registered via `AddHttpClient<WeatherService>()`                                             |
| `GoNoGoService`       | Scoped                 | Reads/writes `GoNoGoSettings` (singleton row, id=1)                                          |
| `PdfService`          | Scoped                 | Uses QuestPDF Community license                                                              |
| `KmlService`          | Singleton              | Stateless XML parser — safe as singleton                                                     |
| `WeatherService`      | Transient (HttpClient) | Registered via `AddHttpClient<WeatherService>()` — fetches METAR/TAF via aviationweather.gov |
| `SunriseService`      | Singleton              | NOAA solar algorithm, pure math, no I/O                                                      |
| `ReleaseNotesService` | Singleton              | Reads `wwwroot/release-notes.json`, 5-min in-memory cache                                    |

### JavaScript interop

All global JS functions live in `App.razor` inside one `<script>` block:

- `initFlightMap(mapId, points)` — Leaflet map with OFM airspace overlay + Overpass power lines
- `initAltitudeChart(canvasId, points)` — Chart.js altitude profile
- `quillInit(id, html)` / `quillGetHtml(id)` — Rich text editor for Pax Briefing
- `downloadFileFromBytes(fileName, contentType, base64)` — Triggers file download
- `initLogboekCharts(...)` — Bar charts on Logboek page

Never use `IJSRuntime.InvokeVoidAsync` in `OnInitializedAsync` — always use `OnAfterRenderAsync`.

### Leaflet map layers

The map has a layer control (top-right). Two overlay layers:

- **✈ Luchtruim (OFM)** — Open Flightmaps tile URL:
  `https://nwy-tiles-api.prod.newaydata.com/tiles/{z}/{x}/{y}.png?path=latest/aero/latest` (the `latest` suffix at the
  end is required)
- **⚡ Hoogspanning** — Fetched dynamically from Overpass API as GeoJSON; loads on layer enable and map pan/zoom

### Go/No-Go logic

`FlightPreparation.GoNoGo` is a computed property (not persisted). Thresholds are configurable via `GoNoGoSettings` (
id=1 row). The default hardcoded fallback: wind ≥15kt = red, vis <3km = red, CAPE >500 J/kg = red.

### Models

- Computed helpers (`TotaalGewicht`, `LiftVoldoende`, `GoNoGo`) are explicitly ignored in `OnModelCreating` with
  `.Ignore()`
- `FlightPreparation` is the central model: one-to-many with `Passenger`, `FlightImage`, `WindLevel`
- `KmlTrack` stores raw KML XML as a string column; parsed at runtime by `KmlService`
- `PaxBriefing` stores HTML (from Quill editor)

### Release notes / versioning

`src/FlightPrep/wwwroot/release-notes.json` is the source of truth. CI updates it on every merge to `main`:

- `[feature]` in PR title → major bump (X+1.0.0)
- `[refactor]` → minor (X.X+1.0)
- `[BUG]` / `[fix]` → patch (X.X.X+1)
- AI description via GitHub Models API (`gpt-4o-mini`); falls back to PR body

### CI/CD pipeline

Single job chain in `.github/workflows/ci-cd.yml`:

1. **build** — restore → build → unit tests → generate release notes → publish artifact → commit updated JSON to main
2. **e2e** — spin up `docker compose`, run Playwright tests (skipped when commit message contains `[skip e2e]`)
3. **deploy** — Azure App Service deploy (runs even if e2e was skipped, as long as build succeeded)

The `build` job needs these `permissions`: `contents: write`, `models: read`, `pull-requests: read`.

### Local Postgres

Connection string: `Host=db;Port=5433;Database=flightprep;Username=fpuser;Password=fppass`  
For local `dotnet run` (outside Docker), use `Host=localhost` instead.

### PWA

The app is installable as a PWA. `wwwroot/service-worker.js` handles offline caching. `wwwroot/manifest.json` + icons in
`wwwroot/icons/`. The service worker is registered in `App.razor` and served with `Cache-Control: no-cache` via custom
`StaticFileOptions` in `Program.cs`.

### SignalR message size

The default SignalR 32 KB limit is raised to 5 MB in `Program.cs` to support map PNG capture (KML track screenshots). If
you add features that send large payloads over the Blazor circuit, this limit may need revisiting.
