# FlightPrep ✈️

A Blazor Server web app for hot air balloon pilots to create, manage, and export pre-flight preparation documents.

---

## Features

### Pre-flight preparation
- **9-section accordion form** covering all pre-flight topics:
  1. General info (date, balloon, pilot, launch/landing site, field owner notification)
  2. Meteo – METAR/TAF auto-fetch + image upload (Stüve, sounding, etc.)
  3. NOTAMs
  4. Airspace
  5. Crew
  6. Pax Briefing – **rich text editor** (Quill)
  7. Load calculation (auto-computed lift/weight balance)
  8. Trajectory – image upload
  9. Emergency / remarks
- **Sunrise/sunset** times auto-calculated from location coordinates (NOAA algorithm, matches AIP values)
- **Go/No-Go** decision badge computed from form data

### After a flight
- **KML flight track** upload (e.g. from the Hot Air app) with an interactive Leaflet map
- **Altitude profile chart** (in feet) with map crosshair synced to chart hover

### Export
- **PDF export** – print-ready A4 document with all sections, uploaded images, and passenger weights

### Other
- **Save & reload** – all flights stored in PostgreSQL; accessible from the flight list
- **Reference data settings** – manage balloons, pilots, and locations (with ICAO code + coordinates)
- **Image upload** – photos stored in the database (no filesystem required)
- **In-app manual** – user manual with screenshots at `/handleiding`
- **PWA** – installable as a Progressive Web App with offline support
- **Application Insights** – server-side Serilog sink + client-side JS SDK for end-to-end telemetry

---

## Running locally

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Start

```bash
docker compose up --build
```

App is available at **http://localhost:8082**

Database migrations run automatically on startup — no manual setup needed.

### Stop

```bash
docker compose down
```

---

## Project structure

```
flightPrep/
├── src/
│   ├── FlightPrep/                        # Blazor Server app (presentation + DI root)
│   │   ├── Components/
│   │   │   ├── App.razor                  # HTML shell; Bootstrap, Quill, Leaflet, Chart.js
│   │   │   ├── Layout/                    # NavMenu, MainLayout
│   │   │   └── Pages/
│   │   │       ├── FlightEdit.razor       # Pre-flight prep form (9-section accordion)
│   │   │       ├── FlightList.razor       # Overview of all saved flights
│   │   │       ├── FlightView.razor       # Read-only view, KML track, PDF export
│   │   │       ├── Logboek.razor          # Statistics dashboard
│   │   │       ├── Admin/                 # Admin-only pages (User Management)
│   │   │       └── Settings/              # Balloon, Pilot, Location CRUD
│   │   ├── Pages/                         # Razor Pages for auth (Login, Register, Logout)
│   │   ├── Services/
│   │   │   ├── PdfService.cs              # QuestPDF A4 document generation
│   │   │   ├── GoNoGoService.cs           # Go/No-Go decision logic
│   │   │   └── AdminSeeder.cs             # Role + admin user seed on startup
│   │   ├── Telemetry/
│   │   │   └── FlightTelemetryInitializer.cs  # OTel processor enriching spans with flightId
│   │   ├── wwwroot/
│   │   │   ├── app.css                    # Aviation theme
│   │   │   ├── manifest.json              # PWA manifest
│   │   │   ├── service-worker.js          # PWA service worker
│   │   │   └── icons/                     # PWA icons
│   │   ├── Dockerfile
│   │   └── Program.cs
│   ├── FlightPrep.Domain/                 # Domain models + service interfaces
│   │   ├── Models/                        # FlightPreparation, Balloon, Pilot, Location, etc.
│   │   └── Services/                      # IFlightPreparationService, etc.
│   ├── FlightPrep.Infrastructure/         # EF Core, migrations, Identity
│   │   └── Data/
│   │       ├── AppDbContext.cs            # IdentityDbContext<ApplicationUser>
│   │       ├── ApplicationUser.cs         # ASP.NET Core Identity user with IsApproved flag
│   │       └── Migrations/                # Auto-applied on startup
│   ├── FlightPrep.Tests/                  # xUnit unit tests
│   ├── FlightPrep.Domain.Tests/           # xUnit domain model tests
│   ├── FlightPrep.Infrastructure.Tests/   # xUnit EF Core / service tests
│   └── FlightPrep.Tests.UI/              # Playwright E2E tests (NUnit)
├── docker-compose.yml
├── infra/
│   ├── main.bicep                         # Azure App Service B2 + PostgreSQL Flexible B1ms
│   └── deploy.sh                          # One-shot Azure provisioning script
└── .github/
    └── workflows/
        └── ci-cd.yml                      # Build → unit tests → release notes → E2E → deploy
```

---

## CI/CD & Release Notes

### Pipeline overview

```
Push / PR merge → main
  └─ ci-cd.yml
       ├─ build job     → dotnet build + unit tests + generate release notes + publish artifact
       ├─ e2e job       → Playwright E2E tests (skipped when commit message contains [skip e2e])
       └─ deploy job    → Azure App Service (runs when e2e succeeds OR is skipped)
```

### Release notes automation

When a PR is **merged to main**, the build job automatically:

```
PR merged → main
  1. Fetch list of changed files (gh CLI)
  2. Call GitHub Models API (gpt-4o-mini) with PR title, body, labels
     → AI generates a description (≤ 3 sentences)
     → fallback: PR body or title if API is unavailable
  3. Bump version in release-notes.json
       [feature] / label feature  →  major  (X+1.0.0)
       [refactor] / label refactor →  minor  (X.X+1.0)
       [BUG] / label bug           →  patch  (X.X.X+1)
  4. Commit release-notes.json to main  [skip e2e]
  5. CI re-runs: build ✓  →  e2e SKIPPED  →  deploy ✓
     ↳ updated release notes are live within ~3 minutes
```

The `/release-notes` page reads the baked-in JSON from the Docker image (always up-to-date after deploy).

> **Note:** No extra secrets required — the workflow uses the built-in `GITHUB_TOKEN` for both the GitHub Models API and the git push.

---

## Deploy to Azure

### 1 – Provision infrastructure

You need the [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in (`az login`).

```bash
export RESOURCE_GROUP=flightprep-rg
export LOCATION=swedencentral
export DB_PASSWORD=<choose-a-strong-password>

bash infra/deploy.sh
```

This creates:
- **Resource group** `flightprep-rg`
- **App Service Plan** (Linux B2)
- **App Service** `flightprep-web` running .NET 10
- **PostgreSQL Flexible Server** 16 (B1ms, 32 GB)
- **Database** `flightprep`

The connection string is injected automatically as an App Setting.

> ⚠️ Note the DB password — you'll need it if you re-run the script.

### 2 – Add the GitHub secret

1. Azure Portal → **App Services → flightprep-web** → **Get publish profile** → copy the XML
2. GitHub repo → **Settings → Secrets and variables → Actions** → add:

| Secret | Value |
|---|---|
| `AZURE_PUBLISH_PROFILE` | Full XML contents of the `.PublishSettings` file |

### 3 – Push to main

```bash
git push origin main
```

The CI/CD pipeline will build, run Playwright E2E tests, and deploy to Azure automatically.

---

## Environment variables

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Application Insights connection string (optional) |
| `SEED_ADMIN_USERNAME` | Email address for the seeded admin user (optional) |
| `SEED_ADMIN_PASSWORD` | Password for the seeded admin user (optional) |

---

## Tech stack

| Layer | Technology |
|---|---|
| Framework | .NET 10 Blazor Server |
| Database | PostgreSQL 16 via EF Core (Npgsql) |
| PDF export | QuestPDF |
| Rich text | Quill 2.x |
| Maps | Leaflet.js |
| Charts | Chart.js 4 |
| UI | Bootstrap 5 |
| Hosting | Azure App Service (Linux) |
| CI/CD | GitHub Actions + OIDC (Playwright E2E) |
| Container | Docker / Docker Compose |
