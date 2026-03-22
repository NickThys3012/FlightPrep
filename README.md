# FlightPrep ✈️

A Blazor Server web app for hot air balloon pilots to create, save, and export pre-flight preparation documents (Vaartvoorbereiding).

---

## Features

- **9-section accordion form** covering all pre-flight topics:
  1. General info (date, balloon, pilot, launch/landing sites)
  2. Meteo – METAR/TAF fetch + image upload (Stüve, sounding, etc.)
  3. NOTAMs
  4. Airspace
  5. Crew
  6. Pax Briefing – **rich text editor** (Quill)
  7. Load calculation (auto-computed lift/weight)
  8. Trajectory – image upload
  9. Emergency / remarks
- **PDF export** – generates a print-ready A4 document including uploaded images
- **Save & reload** – all flights stored in PostgreSQL; accessible from the flight list
- **Reference data settings** – manage balloons, pilots, and locations
- **Image upload** – photos stored directly in the database (no filesystem needed)

---

## Running locally

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Start

```bash
docker compose up --build
```

App is available at **http://localhost:8082**

The database migrations run automatically on startup. No manual setup needed.

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
│   │   ├── App.razor          # HTML shell; Bootstrap + Quill JS/CSS
│   │   ├── Layout/            # NavMenu, MainLayout
│   │   └── Pages/
│   │       ├── FlightEdit.razor   # Main prep form (9-section accordion)
│   │       ├── FlightList.razor   # Overview of all saved flights
│   │       ├── FlightView.razor   # Read-only view + PDF export
│   │       └── Settings/          # Balloon, Pilot, Location CRUD
│   ├── Data/
│   │   ├── AppDbContext.cs        # EF Core context
│   │   └── Migrations/            # Auto-applied on startup
│   ├── Models/                    # FlightPreparation, FlightImage, Balloon, Pilot, etc.
│   ├── Services/
│   │   ├── PdfService.cs          # QuestPDF document generation
│   │   └── WeatherService.cs      # METAR/TAF from aviationweather.gov
│   ├── Dockerfile
│   └── Program.cs
├── docker-compose.yml
├── infra/
│   ├── main.bicep             # Azure App Service B2 + PostgreSQL Flexible B1ms
│   └── deploy.sh              # One-shot Azure provisioning script
└── .github/
    └── workflows/
        └── ci-cd.yml          # Build + deploy pipeline
```

---

## Deploying to Azure

### 1 – Provision Azure infrastructure

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

> ⚠️ Note the DB password — you'll need it again if you re-run the script.

---

### 2 – Get the App Service Publish Profile

1. Go to the [Azure Portal](https://portal.azure.com)
2. Navigate to **App Services → flightprep-web**
3. Click **"Get publish profile"** (top toolbar) — this downloads a `.PublishSettings` XML file
4. Open the file in a text editor and **copy the entire contents**

---

### 3 – Add the GitHub repository secret

Go to **Settings → Secrets and variables → Actions** in your GitHub repo and add:

| Secret | Value |
|---|---|
| `AZURE_PUBLISH_PROFILE` | The full XML contents of the `.PublishSettings` file |

---

### 4 – Set the App Service name

In `.github/workflows/ci-cd.yml` the deploy step uses `app-name: flightprep-web`. If you changed the app name in `infra/main.bicep`, update it there too.

---

### 5 – Push to main → pipeline runs automatically

```bash
git push origin main
```

The **CI/CD** workflow will:
1. Restore, build, and publish the .NET app
2. Upload the artifact
3. Log in to Azure via OIDC (no password stored)
4. Deploy to App Service

You can monitor progress under **Actions** in your GitHub repo.

---

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | Set by docker-compose / Azure App Setting |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` | `Production` in Docker/Azure |

---

## Tech stack

| Layer | Technology |
|---|---|
| Framework | .NET 10 Blazor Server |
| Database | PostgreSQL 16 via EF Core (Npgsql) |
| PDF export | QuestPDF |
| Rich text | Quill 2.x (snow theme) |
| UI | Bootstrap 5 |
| Hosting | Azure App Service (Linux) |
| CI/CD | GitHub Actions + OIDC |
| Container | Docker / Docker Compose |
