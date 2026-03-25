# FlightPrep ✈️

A Blazor Server web app for hot air balloon pilots to create, manage, and export pre-flight preparation documents (*Vaartvoorbereiding*).

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
- **In-app handleiding** – user manual with screenshots at `/handleiding`

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
├── src/FlightPrep/
│   ├── Components/
│   │   ├── App.razor              # HTML shell; Bootstrap, Quill, Leaflet, Chart.js
│   │   ├── Layout/                # NavMenu, MainLayout
│   │   └── Pages/
│   │       ├── FlightEdit.razor   # Pre-flight prep form (9-section accordion)
│   │       ├── FlightList.razor   # Overview of all saved flights
│   │       ├── FlightView.razor   # Read-only view, KML track, PDF export
│   │       ├── Logboek.razor      # Statistics dashboard (feature/logboek)
│   │       └── Settings/          # Balloon, Pilot, Location CRUD
│   ├── Data/
│   │   ├── AppDbContext.cs        # EF Core context
│   │   └── Migrations/            # Auto-applied on startup
│   ├── Models/                    # FlightPreparation, Balloon, Pilot, Location, etc.
│   ├── Services/
│   │   ├── PdfService.cs          # QuestPDF A4 document generation
│   │   ├── KmlService.cs          # KML parser + flight stats
│   │   ├── SunriseService.cs      # NOAA solar algorithm
│   │   ├── WeatherService.cs      # Background meteo image fetching
│   │   ├── WeatherFetchService.cs # METAR/TAF + Open-Meteo forecast
│   │   └── GoNoGoService.cs       # Go/No-Go decision logic
│   ├── wwwroot/
│   │   ├── app.css                # Aviation theme
│   │   ├── manifest.json          # PWA manifest (feature/pwa)
│   │   ├── service-worker.js      # PWA service worker (feature/pwa)
│   │   └── icons/                 # PWA icons
│   ├── Dockerfile
│   └── Program.cs
├── docker-compose.yml
├── infra/
│   ├── main.bicep                 # Azure App Service B2 + PostgreSQL Flexible B1ms
│   └── deploy.sh                  # One-shot Azure provisioning script
└── .github/
    └── workflows/
        ├── ci-cd.yml              # Build → E2E tests (Playwright) → deploy
        └── release-notes.yml      # AI release notes on PR merge
```

---

## CI/CD & Release Notes

### Pipeline overview

```
Push / PR merge → main
  ├─ build job     → dotnet build + unit tests + publish artifact
  ├─ e2e job       → Playwright E2E tests (skipped for release-note commits)
  └─ deploy job    → Azure App Service  (runs when e2e succeeds OR is skipped)
```

### Release notes automation

When a PR is **merged to main**, a second workflow runs automatically:

```
PR merged → main
  1. Fetch list of changed files (gh CLI)
  2. Call GitHub Models API (gpt-4o-mini) with PR title, body, labels
     → AI generates a Dutch description (≤ 3 sentences)
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
