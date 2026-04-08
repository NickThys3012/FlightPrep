using FlightPrep.Domain.Models;
using FlightPrep.Domain.Models.Trajectory;
using FlightPrep.Domain.Services;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

// ReSharper disable InconsistentNaming

namespace FlightPrep.Infrastructure.Services;

public partial class PdfService(ISunriseService sunriseSvc, ITrajectoryMapService mapSvc, IFlightAssessmentService assessmentSvc) : IPdfService
{
    private const string PrimaryColor = "#1a3a5c";
    private const string LightBg = "#f0f4f8";

    public async Task<byte[]> GenerateAsync(FlightPreparation fp, byte[]? mapPng = null, string? userId = null)
    {
        // Generate a trajectory map server-side if the caller didn't provide a pre-rendered one
        mapPng ??= await mapSvc.RenderAsync(fp.TrajectorySimulationJson);

        Settings.License = LicenseType.Community;

        // Compute sunrise/sunset if the location has coords
        (TimeOnly Sunrise, TimeOnly Sunset)? sunriseSunset = null;
        var loc = fp.Location;
        if (loc?.Latitude.HasValue == true && loc.Longitude.HasValue)
        {
            sunriseSunset = sunriseSvc.Calculate(fp.Datum, loc.Latitude!.Value, loc.Longitude!.Value);
        }

        var (totaalGewicht, liftVoldoende, goNoGo) = await assessmentSvc.ComputeAsync(fp, userId);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().AlignCenter().Text("Vaartvoorbereiding Ballonvaart")
                            .Bold().FontSize(18).FontColor(PrimaryColor);
                        col.Item().AlignCenter().Text(
                                $"{fp.Datum:dd/MM/yyyy}  –  {fp.Tijdstip:HH:mm} LT" +
                                (fp.Balloon != null ? $"  |  {fp.Balloon.Registration} ({fp.Balloon.Type})" : "") +
                                (fp.Pilot != null ? $"  |  {fp.Pilot.Name}" : ""))
                            .FontSize(10);
                        col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(PrimaryColor);
                    });
                });

                page.Content().Column(col =>
                {
                    // Go/No-Go indicator
                    var gngBg = goNoGo switch
                    {
                        "green" => Colors.Green.Darken1,
                        "yellow" => Colors.Yellow.Darken1,
                        "red" => Colors.Red.Darken1,
                        _ => Colors.Grey.Medium
                    };
                    var gngText = goNoGo switch
                    {
                        "green" => "GO",
                        "yellow" => "CAUTION",
                        "red" => "NO-GO",
                        _ => "Go/No-Go onbekend"
                    };
                    col.Item().PaddingBottom(4).Background(gngBg).Padding(5)
                        .Text(gngText).Bold().FontSize(11).FontColor(Colors.White);

                    // Build Section 1 rows including optional sunrise/sunset
                    var sec1Rows = new List<(string, string)>
                    {
                        ("Datum", fp.Datum.ToString("dd/MM/yyyy")),
                        ("Tijdstip (LT)", fp.Tijdstip.ToString("HH:mm")),
                        ("Ballon",
                            fp.Balloon != null
                                ? $"{fp.Balloon.Registration} – {fp.Balloon.Type} ({(fp.Balloon.VolumeM3.HasValue ? $"{fp.Balloon.VolumeM3:F0} m³" : "–")})"
                                : "–"),
                        ("Piloot / PIC", fp.Pilot?.Name ?? "–"),
                        ("Locatie", fp.Location?.Name ?? "–"),
                        ("Veld eigenaar gemeld", fp.VeldEigenaarGemeld ? "Ja" : "Nee / NVT")
                    };
                    if (sunriseSunset.HasValue)
                    {
                        sec1Rows.Add(("Zonsopgang (UTC)", sunriseSunset.Value.Sunrise.ToString("HH:mm")));
                        sec1Rows.Add(("Zonsondergang (UTC)", sunriseSunset.Value.Sunset.ToString("HH:mm")));
                    }

                    AddSection(col, "1. Algemene Gegevens", sec1Rows.ToArray());

                    // Section 2 meteo rows
                    var sec2Rows = new List<(string, string)>
                    {
                        ("METAR", fp.Metar ?? "–"),
                        ("TAF", fp.Taf ?? "–"),
                        ("Wind per hoogte", fp.WindPerHoogte ?? "–"),
                        ("Neerslag / bewolking", fp.Neerslag ?? "–"),
                        ("Windrichting oppervlak", fp.SurfaceWindDirectionDeg.HasValue ? $"{fp.SurfaceWindDirectionDeg}°" : "–"),
                        ("Windsnelheid oppervlak", fp.SurfaceWindSpeedKt.HasValue ? $"{fp.SurfaceWindSpeedKt} kt" : "–"),
                        ("Temperatuur", fp.TemperatuurC.HasValue ? $"{fp.TemperatuurC}°C" : "–"),
                        ("Dauwpunt", fp.DauwpuntC.HasValue ? $"{fp.DauwpuntC}°C" : "–"),
                        ("QNH", fp.QnhHpa.HasValue ? $"{fp.QnhHpa} hPa" : "–"),
                        ("Zichtbaarheid", fp.ZichtbaarheidKm.HasValue ? $"{fp.ZichtbaarheidKm} km" : "–"),
                        ("CAPE", fp.CapeJkg.HasValue ? $"{fp.CapeJkg} J/kg" : "–")
                    };
                    AddSection(col, "2. Meteorologische Informatie", sec2Rows.ToArray());

                    // Wind levels table
                    if (fp.WindLevels.Count > 0)
                    {
                        col.Item().PaddingTop(4).ShowEntire().Column(wSection =>
                        {
                            wSection.Item().Background(LightBg).Padding(3).Row(row =>
                            {
                                row.RelativeItem(2).Text("Hoogte (ft)").Bold();
                                row.RelativeItem(2).Text("Richting (°)").Bold();
                                row.RelativeItem(2).Text("Snelheid (kt)").Bold();
                                row.RelativeItem(2).Text("Temp (°C)").Bold();
                            });
                            var alt = false;
                            foreach (var wl in fp.WindLevels.OrderBy(w => w.Order))
                            {
                                wSection.Item().Background(alt ? LightBg : Colors.White).Padding(3).Row(row =>
                                {
                                    row.RelativeItem(2).Text(wl.AltitudeFt.ToString());
                                    row.RelativeItem(2).Text(wl.DirectionDeg?.ToString() ?? "–");
                                    row.RelativeItem(2).Text(wl.SpeedKt?.ToString() ?? "–");
                                    row.RelativeItem(2).Text(wl.TempC?.ToString("F1") ?? "–");
                                });
                                alt = !alt;
                            }
                        });
                    }

                    // Meteo images
                    var meteoImgs = fp.Images.Where(i => i.Section == "Meteo").ToList();
                    if (meteoImgs.Count > 0)
                    {
                        AddImageGrid(col, meteoImgs);
                    }

                    AddSection(col, "3. Luchtruim en NOTAMs", [
                        ("NOTAMs gecontroleerd", fp.NotamsGecontroleerd),
                        ("Luchtruimstructuur", fp.Luchtruimstructuur ?? "–"),
                        ("Beperkingen / gesloten zones", fp.Beperkingen ?? "–"),
                        ("Obstakels", fp.Obstakels ?? "–")
                    ]);

                    AddSection(col, "4. Veiligheid en Communicatie", [
                        ("EHBO-kit en vuurblusser", fp.EhboEnBlusser),
                        ("Passagierslijst ingevuld", fp.PassagierslijstIngevuld),
                        ("Vluchtplan ingediend", fp.VluchtplanIngediend)
                    ]);

                    // Section 5 - Technische Controle with checkboxes
                    col.Item().PaddingTop(6).ShowEntire().Background(PrimaryColor).Padding(4)
                        .Text("5. Technische Controle").Bold().FontColor(Colors.White).FontSize(10);
                    var checks = new[]
                    {
                        (fp.BranderGetest, "Brander getest"), (fp.GasflaconsGecontroleerd, "Gasflessen gevuld & gecontroleerd"),
                        (fp.BallonVisueel, "Ballon en mand visueel geïnspecteerd"), (fp.VerankeringenGecontroleerd, "Verankeringen en touwen gecontroleerd"),
                        (fp.InstrumentenWerkend, "Instrumenten werkend")
                    };
                    var alt5 = false;
                    foreach (var (@checked, label) in checks)
                    {
                        col.Item().Background(alt5 ? LightBg : Colors.White).Padding(3)
                            .Text($"{(@checked ? "[JA]" : "[NEE]")}  {label}");
                        alt5 = !alt5;
                    }

                    col.Item().PaddingTop(6).ShowEntire().Background(PrimaryColor).Padding(4)
                        .Text("6. Pax Briefing").Bold().FontColor(Colors.White).FontSize(10);
                    col.Item().ShowEntire().Background(Colors.White).Padding(4).Column(body =>
                        RenderHtmlToColumn(body, fp.PaxBriefing));

                    // Section 7 - Load Calculation (items added directly to outer col for proper page-break support)
                    col.Item().PaddingTop(6).ShowEntire().Background(PrimaryColor).Padding(4)
                        .Text("7. Load Berekening").Bold().FontColor(Colors.White).FontSize(10);
                    col.Item().Background(LightBg).Padding(3)
                        .Text($"Gewicht envelop+brander+mand+flessen: {fp.EnvelopeWeightKg?.ToString("F1") ?? "–"} kg");
                    col.Item().Background(Colors.White).Padding(3)
                        .Text($"Piloot: {fp.Pilot?.Name ?? "–"}  –  {fp.Pilot?.WeightKg?.ToString("F1") ?? "–"} kg");

                    // Passenger table header
                    col.Item().Background(Colors.Grey.Lighten2).Padding(3).Row(row =>
                    {
                        row.RelativeItem(3).Text("Passagier").Bold();
                        row.RelativeItem().Text(t =>
                        {
                            t.AlignRight();
                            t.Span("Gewicht (kg)").Bold();
                        });
                    });
                    var altLoad = false;
                    foreach (var p in fp.Passengers.OrderBy(x => x.Order))
                    {
                        var pWeight = p.WeightKg > 0 ? p.WeightKg.ToString("F1") : "–";
                        col.Item().Background(altLoad ? LightBg : Colors.White).Padding(3).Row(row =>
                        {
                            row.RelativeItem(3).Text(p.Name);
                            row.RelativeItem().Text(t =>
                            {
                                t.AlignRight();
                                t.Span(pWeight);
                            });
                        });
                        altLoad = !altLoad;
                    }

                    var totalWeight = $"{totaalGewicht:F1} kg";
                    col.Item().Background(Colors.Grey.Lighten3).Padding(3).Row(row =>
                    {
                        row.RelativeItem(3).Text("Totaal gewicht").Bold();
                        row.RelativeItem().Text(t =>
                        {
                            t.AlignRight();
                            t.Span(totalWeight).Bold();
                        });
                    });
                    col.Item().Background(Colors.White).Padding(3)
                        .Text(
                            $"Max Altitude: {(fp.MaxAltitudeFt.HasValue ? fp.MaxAltitudeFt + " ft" : "–")}  |  Lift units: {fp.LiftUnits?.ToString("F0") ?? "–"}  |  Totaal lift: {fp.TotaalLiftKg?.ToString("F1") ?? "–"} kg");
                    col.Item().Background(LightBg).Padding(3)
                        .Text(liftVoldoende ? "Lift voldoende" : "Lift onvoldoende").Bold()
                        .FontColor(liftVoldoende ? Colors.Green.Darken2 : Colors.Red.Darken2);

                    // ISA lift calculation block — only when all inputs are present
                    if (fp.Balloon?.VolumeM3.HasValue == true &&
                        fp.Balloon?.InternalEnvelopeTempC.HasValue == true &&
                        fp.Location?.ElevationM.HasValue == true &&
                        fp is { TemperatuurC: not null, MaxAltitudeFt: not null })
                    {
                        var A = fp.MaxAltitudeFt.Value * 0.3048;
                        var Eg = fp.Location.ElevationM.Value;
                        var Tg = fp.TemperatuurC.Value;
                        var Ti = fp.Balloon.InternalEnvelopeTempC.Value;
                        var V = fp.Balloon.VolumeM3.Value;
                        var lr = LiftCalculator.Calculate(A, Eg, Tg, Ti, V);

                        col.Item().Background(LightBg).Padding(3)
                            .Text($"Ti (inw. temp): {Ti:F0}°C  |  Ballonvolume: {V:F0} m³");
                        col.Item().Background(Colors.White).Padding(3)
                            .Text($"ISA Ta @ max alt: {lr.AmbientTempAtAltC:F1}°C  |  ISA P @ max alt: {lr.PressureHpa:F1} hPa");

                        col.Item().Background(Colors.White).Padding(3)
                            .Text("Berekend via ISA-formule (Bijlage 2, Hot Air Balloon Flight Manual, Cameron Balloons Ltd.)")
                            .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                    }

                    if (!string.IsNullOrWhiteSpace(fp.LoadNotes))
                    {
                        col.Item().Background(Colors.White).Padding(3).Text($"Notities: {fp.LoadNotes}");
                    }

                    AddSection(col, "8. Traject", [
                        ("Trajectnotities", fp.Traject ?? "–")
                    ]);

                    // Simulated trajectories summary
                    if (!string.IsNullOrWhiteSpace(fp.TrajectorySimulationJson))
                    {
                        try
                        {
                            var allTrajs = JsonSerializer.Deserialize<List<SimulatedTrajectory>>(fp.TrajectorySimulationJson);
                            if (allTrajs is { Count: > 0 })
                            {
                                col.Item().PaddingTop(4)
                                    .Text("Trajectsimulaties").Bold().FontSize(8.5f).FontColor(PrimaryColor);

                                var alt2 = false;
                                foreach (var t in allTrajs.Where(t => t.Points.Count > 0))
                                {
                                    var last = t.Points[^1];
                                    var src = t.DataSource switch
                                    {
                                        TrajectoryDataSource.Hysplit => "Open-Meteo 3D",
                                        TrajectoryDataSource.OpenMeteo => "Open-Meteo",
                                        _ => "Dead-reckoning"
                                    };
                                    var landing = $"{last.Lat:F4}°N  {last.Lon:F4}°E";
                                    var altRow = $"{t.AltitudeFt} ft  |  {src}  |  {t.DurationMinutes} min  |  berekend {t.SimulatedAt:dd/MM/yyyy HH:mm}  |  landing ≈ {landing}";

                                    col.Item()
                                        .Background(alt2 ? LightBg : Colors.White)
                                        .Padding(2)
                                        .Text(altRow)
                                        .FontSize(8);
                                    alt2 = !alt2;
                                }
                            }
                        }
                        catch
                        {
                            /* malformed JSON — skip silently */
                        }
                    }

                    // Trajectory map image — TrajectoryMapService enforces ≥4:3 landscape
                    // aspect ratio, so FitWidth is always safe on an A4 page
                    if (mapPng != null)
                    {
                        col.Item().PageBreak();
                    }

                    if (mapPng != null)
                    {
                        col.Item().Image(mapPng).FitWidth();
                    }

                    // Traject images
                    var trajImgs = fp.Images.Where(i => i.Section == "Traject").ToList();
                    if (trajImgs.Count > 0)
                    {
                        AddImageGrid(col, trajImgs);
                    }

                    col.Item().PaddingTop(6).ShowEntire().Background(PrimaryColor).Padding(4)
                        .Text("9. Ballonbulletin").Bold().FontColor(Colors.White).FontSize(10);
                    col.Item().ShowEntire().Background(Colors.White).Padding(4)
                        .Text(fp.Ballonbulletin ?? "–").FontFamily("Courier New").FontSize(7.5f);

                    if (!fp.IsFlown)
                    {
                        return;
                    }

                    {
                        col.Item().PaddingTop(6).ShowEntire().Background(Colors.Green.Darken1).Padding(4)
                            .Text("Vluchtverslag").Bold().FontColor(Colors.White).FontSize(10);
                        col.Item().Background(LightBg).Padding(3).Row(row =>
                        {
                            row.RelativeItem().Text("Werkelijke landing").Bold();
                            row.RelativeItem(2).Text(fp.ActualLandingNotes ?? "–");
                        });
                        col.Item().Background(Colors.White).Padding(3).Row(row =>
                        {
                            row.RelativeItem().Text("Vluchtduur").Bold();
                            row.RelativeItem(2).Text(fp.ActualFlightDurationMinutes.HasValue ? $"{fp.ActualFlightDurationMinutes} min" : "–");
                        });
                        col.Item().Background(LightBg).Padding(3).Row(row =>
                        {
                            row.RelativeItem().Text("Opmerkingen").Bold();
                            row.RelativeItem(2).Text(fp.ActualRemarks ?? "–");
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Pagina ");
                    x.CurrentPageNumber();
                    x.Span(" van ");
                    x.TotalPages();
                    x.Span($"  –  Gegenereerd op {DateTime.Now:dd/MM/yyyy HH:mm}");
                });
            });
        }).GeneratePdf();
    }

    private static void AddImageGrid(ColumnDescriptor col, List<FlightImage> images)
    {
        const int perRow = 2; // 2-per-row gives larger images than 3
        for (var start = 0; start < images.Count; start += perRow)
        {
            var batch = images.Skip(start).Take(perRow).ToList();
            col.Item().PaddingTop(4).Row(row =>
            {
                foreach (var img in batch)
                {
                    row.RelativeItem().Padding(3).Image(img.Data).FitArea();
                }

                // keep columns balanced when the batch is smaller than perRow
                for (var i = batch.Count; i < perRow; i++)
                {
                    row.RelativeItem();
                }
            });
        }
    }

    private static void AddSection(ColumnDescriptor col, string title, (string Label, string Value)[] rows) =>
        col.Item().PaddingTop(6).ShowEntire().Column(section =>
        {
            section.Item().Background(PrimaryColor).Padding(4)
                .Text(title).Bold().FontColor(Colors.White).FontSize(10);
            var alt = false;
            foreach (var (label, value) in rows)
            {
                section.Item().Background(alt ? "#f0f4f8" : Colors.White).Padding(3).Row(row =>
                {
                    row.RelativeItem().Text(label).Bold();
                    row.RelativeItem(2).Text(value);
                });
                alt = !alt;
            }
        });

    /// <summary>
    ///     Renders Quill-generated HTML into a QuestPDF ColumnDescriptor,
    ///     preserving headings, bullet/ordered lists, and indent levels.
    /// </summary>
    private static void RenderHtmlToColumn(ColumnDescriptor body, string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            body.Item().Text("–");
            return;
        }

        // Remove Quill's internal UI marker spans (empty, just for visual bullets)
        var clean = QuillUiMarkerRegex().Replace(html, "");

        // Extract block elements one by one
        var blockRx = ExtractBlockElementsRegex();

        var any = false;
        foreach (Match m in blockRx.Matches(clean))
        {
            var tag = m.Groups[1].Value.ToLowerInvariant();
            var attrs = m.Groups[2].Value;
            // Strip all remaining inline tags, decode entities
            var text = AnyHtmlTagRegex().Replace(m.Groups[3].Value, "")
                .Replace("&nbsp;", " ").Replace("&amp;", "&")
                .Replace("&lt;", "<").Replace("&gt;", ">")
                .Replace("&quot;", "\"").Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            any = true;

            var indentMatch = QuillIndentLevelRegex().Match(attrs);
            var indent = indentMatch.Success ? int.Parse(indentMatch.Groups[1].Value) * 12 : 0;

            switch (tag)
            {
                case "h1":
                    body.Item().PaddingTop(6).Text(text).Bold().FontSize(13); break;
                case "h2":
                    body.Item().PaddingTop(5).Text(text).Bold().FontSize(11); break;
                case "h3":
                    body.Item().PaddingTop(4).Text(text).Bold().FontSize(10); break;
                case "li":
                    var bullet = attrs.Contains("ordered") ? "-" : "*";
                    body.Item().PaddingLeft(8 + indent).Text($"{bullet} {text}").FontSize(9);
                    break;
                default: // p
                    body.Item().PaddingTop(1).PaddingLeft(indent).Text(text).FontSize(9); break;
            }
        }

        if (!any)
        {
            body.Item().Text("–");
        }
    }

    public Task<byte[]> GenerateOfpAsync(FlightPreparation fp, double passengerEquipmentKg = 7)
    {
        ArgumentNullException.ThrowIfNull(fp);
        Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial));

                // ── Header ──────────────────────────────────────────────────
                page.Header().Element(hdr =>
                {
                    hdr.Column(hdrCol =>
                    {
                        // Row 1: OPERATOR | HOT AIR BALLOON (Reg + ICAO) | TYPE | DATE
                        hdrCol.Item().Border(0.5f).Row(row =>
                        {
                            row.RelativeItem(3).BorderRight(0.5f).Padding(3).Column(c =>
                            {
                                c.Item().Text("OPERATOR").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(fp.OperatorName ?? "—").FontSize(8);
                            });
                            row.RelativeItem(4).BorderRight(0.5f).Padding(3).Column(c =>
                            {
                                c.Item().Text("HOT AIR BALLOON").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                var regIcao = string.Join("  ",
                                    new[] { fp.Balloon?.Registration, fp.Location?.IcaoCode }
                                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                                c.Item().Text(string.IsNullOrWhiteSpace(regIcao) ? "—" : regIcao).FontSize(8);
                            });
                            row.RelativeItem(2).BorderRight(0.5f).Padding(3).Column(c =>
                            {
                                c.Item().Text("TYPE").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(fp.Balloon?.Type ?? "—").FontSize(8);
                            });
                            row.RelativeItem(2).Padding(3).Column(c =>
                            {
                                c.Item().Text("DATE").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(fp.Datum.ToString("ddd d-MM-yy")).FontSize(8);
                            });
                        });
                        // Row 2: PIC | TAKEOFF LOCATION | LANDING LOCATION | TAKEOFF TIME / LANDING TIME
                        hdrCol.Item().BorderLeft(0.5f).BorderRight(0.5f).BorderBottom(0.5f).Row(row =>
                        {
                            row.RelativeItem(3).BorderRight(0.5f).Padding(3).Column(c =>
                            {
                                c.Item().Text("PIC").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(fp.Pilot?.Name ?? "—").FontSize(8);
                            });
                            row.RelativeItem(3).BorderRight(0.5f).Padding(3).Column(c =>
                            {
                                c.Item().Text("TAKEOFF LOCATION").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(fp.Location?.Name ?? "—").FontSize(8);
                            });
                            row.RelativeItem(3).BorderRight(0.5f).Padding(3).Column(c =>
                            {
                                c.Item().Text("LANDING LOCATION").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                c.Item().Text(Blank(fp.LandingLocationText)).FontSize(8);
                            });
                            row.RelativeItem(2).Padding(3).Column(c =>
                            {
                                c.Item().Text("TAKEOFF / LANDING").Bold().FontSize(7).FontColor(Colors.Grey.Darken1);
                                var takeoff = fp.Tijdstip.ToString("HH:mm");
                                var landing = fp.PlannedLandingTime?.ToString("HH:mm") ?? "—";
                                c.Item().Text($"{takeoff} / {landing}").FontSize(8);
                            });
                        });
                        // Title
                        hdrCol.Item().PaddingTop(4).AlignCenter()
                            .Text("OPERATIONAL FLIGHT PLAN").Bold().FontSize(13).FontColor(PrimaryColor);
                        hdrCol.Item().AlignCenter()
                            .Text("LOAD CALCULATIONS AND PASSENGER LIST").FontSize(9).FontColor(Colors.Grey.Darken1);
                        hdrCol.Item().PaddingVertical(3).LineHorizontal(1).LineColor(PrimaryColor);
                    });
                });

                // ── Content ──────────────────────────────────────────────────
                page.Content().Column(col =>
                {
                    // ── Passenger list ──────────────────────────────────────
                    col.Item().PaddingBottom(2).Text("PASSENGER MANIFEST").Bold().FontSize(9).FontColor(PrimaryColor);
                    col.Item().Border(0.5f).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(22); // No
                            cols.RelativeColumn(5);  // NAME
                            cols.RelativeColumn(2);  // WEIGHT
                            cols.ConstantColumn(20); // C
                            cols.ConstantColumn(20); // A
                            cols.ConstantColumn(20); // T
                        });

                        table.Header(hdr =>
                        {
                            hdr.Cell().Element(HeaderCell).Text("No").Bold().FontColor(Colors.White).FontSize(8);
                            hdr.Cell().Element(HeaderCell).Text("NAME").Bold().FontColor(Colors.White).FontSize(8);
                            hdr.Cell().Element(HeaderCell).Text("WEIGHT (kg)").Bold().FontColor(Colors.White).FontSize(8);
                            hdr.Cell().Element(HeaderCell).AlignCenter().Text("(C)").Bold().FontColor(Colors.White).FontSize(8);
                            hdr.Cell().Element(HeaderCell).AlignCenter().Text("(A)").Bold().FontColor(Colors.White).FontSize(8);
                            hdr.Cell().Element(HeaderCell).AlignCenter().Text("(T)").Bold().FontColor(Colors.White).FontSize(8);
                        });

                        var altRow = false;
                        double passengerTotalKg = 0;
                        for (var i = 0; i < fp.Passengers.Count; i++)
                        {
                            var p = fp.Passengers[i];
                            var bg = altRow ? LightBg : "#FFFFFF";
                            var totalPaxWeight = p.WeightKg + passengerEquipmentKg;
                            passengerTotalKg += totalPaxWeight;
                            table.Cell().Background(bg).Padding(3).Text($"{i + 1}").FontSize(8);
                            table.Cell().Background(bg).Padding(3).Text(p.Name).FontSize(8);
                            table.Cell().Background(bg).Padding(3).Text($"{p.WeightKg:F0} (+{passengerEquipmentKg:F0}kg)").FontSize(8);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(p.IsChild ? "✓" : "").FontSize(8);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(p.NeedsAssistance ? "✓" : "").FontSize(8);
                            table.Cell().Background(bg).Padding(3).AlignCenter().Text(p.IsTransport ? "✓" : "").FontSize(8);
                            altRow = !altRow;
                        }

                        // Footer — totals row
                        var picWeight = fp.PicWeightKg ?? fp.Pilot?.WeightKg ?? 0;
                        var grandTotal = passengerTotalKg + picWeight;
                        table.Cell().ColumnSpan(2).Background(LightBg).Padding(3).Text("TOTALS (PAX + PIC)").Bold().FontSize(8);
                        table.Cell().Background(LightBg).Padding(3).Text($"{grandTotal:F0} kg").Bold().FontSize(8);
                        table.Cell().ColumnSpan(3).Background(LightBg).Padding(3).Text("").FontSize(8);
                        return;

                        // Header row
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(PrimaryColor).Padding(3);
                    });
                    col.Item().PaddingTop(2).Text("(C) CHILD   (A) ASSISTANCE   (T) TRANSPORT")
                        .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingTop(6).ShowEntire().Row(mainRow =>
                    {
                        // ── Left column: weather + fuel + load ──────────────
                        mainRow.RelativeItem(3).Column(left =>
                        {
                            // Weather conditions block
                            left.Item().PaddingBottom(2).Text("WEATHER CONDITIONS").Bold().FontSize(9).FontColor(PrimaryColor);
                            left.Item().Border(0.5f).Table(wTable =>
                            {
                                wTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(3);
                                });

                                wTable.Cell().Element(WLabelCell).Text("SOURCE").Bold().FontSize(7);
                                wTable.Cell().Element(WValueCell).Text("Meteoblue").FontSize(8);

                                wTable.Cell().Element(WLabelCell).Text("DATE").Bold().FontSize(7);
                                wTable.Cell().Element(WValueCell).Text(fp.Datum.ToString("d-MM-yyyy")).FontSize(8);

                                wTable.Cell().Element(WLabelCell).Text("VISIBILITY").Bold().FontSize(7);
                                wTable.Cell().Element(WValueCell)
                                    .Text(fp.ZichtbaarheidKm.HasValue ? $"{fp.ZichtbaarheidKm} km" : "—").FontSize(8);

                                wTable.Cell().Element(WLabelCell).Text("CLOUDS").Bold().FontSize(7);
                                wTable.Cell().Element(WValueCell)
                                    .Text(!string.IsNullOrWhiteSpace(fp.Neerslag) ? fp.Neerslag : "—").FontSize(8);

                                wTable.Cell().Element(WLabelCell).Text("TEMP").Bold().FontSize(7);
                                wTable.Cell().Element(WValueCell)
                                    .Text(fp.TemperatuurC.HasValue ? $"{fp.TemperatuurC} °C" : "—").FontSize(8);

                                wTable.Cell().Element(WLabelCell).Text("QNH").Bold().FontSize(7);
                                wTable.Cell().Element(WValueCell)
                                    .Text(fp.QnhHpa.HasValue ? $"{fp.QnhHpa} hPa" : "—").FontSize(8);

                                // Wind levels (up to 3)
                                var windLevels = fp.WindLevels.OrderBy(w => w.AltitudeFt).Take(3).ToList();
                                if (windLevels.Count == 0)
                                {
                                    wTable.Cell().Element(WLabelCell).Text("SURFACE WIND").Bold().FontSize(7);
                                    string surfaceWindText;
                                    if (fp.SurfaceWindDirectionDeg.HasValue || fp.SurfaceWindSpeedKt.HasValue)
                                    {
                                        var dir = fp.SurfaceWindDirectionDeg?.ToString("D3") ?? "---";
                                        var spd = fp.SurfaceWindSpeedKt?.ToString("F0").PadLeft(2, '0') ?? "--";
                                        surfaceWindText = $"{dir}/{spd}kt";
                                    }
                                    else
                                    {
                                        surfaceWindText = "—";
                                    }
                                    wTable.Cell().Element(WValueCell).Text(surfaceWindText).FontSize(8);
                                }
                                else
                                {
                                    for (var wi = 0; wi < windLevels.Count; wi++)
                                    {
                                        var wl = windLevels[wi];
                                        var label = wi == 0 ? "SURFACE" : $"{wl.AltitudeFt}FT";
                                        var dir = wl.DirectionDeg?.ToString("D3") ?? "---";
                                        var spd = wl.SpeedKt?.ToString("F0").PadLeft(2, '0') ?? "--";
                                        wTable.Cell().Element(WLabelCell).Text(label).Bold().FontSize(7);
                                        wTable.Cell().Element(WValueCell).Text($"{dir}/{spd}kt").FontSize(8);
                                    }
                                }

                                return;

                                static IContainer WValueCell(IContainer c) =>
                                    c.Background(Colors.White).Padding(3);

                                static IContainer WLabelCell(IContainer c) =>
                                    c.Background(LightBg).Padding(3);
                            });

                            // Derive planned duration from takeoff and landing times
                            string plannedTime;
                            if (fp.PlannedLandingTime.HasValue)
                            {
                                var rawMinutes = (fp.PlannedLandingTime.Value.ToTimeSpan() - fp.Tijdstip.ToTimeSpan()).TotalMinutes;
                                // Handle past-midnight flights (negative result)
                                if (rawMinutes < 0) rawMinutes += 24 * 60;
                                var durationMinutes = (int)rawMinutes;
                                plannedTime = $"{durationMinutes} min";
                            }
                            else
                            {
                                plannedTime = "—";
                            }

                            // Fuel calculations
                            left.Item().PaddingTop(6).PaddingBottom(2).Text("FUEL CALCULATIONS").Bold().FontSize(9).FontColor(PrimaryColor);
                            left.Item().Border(0.5f).Table(fTable =>
                            {
                                fTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                });
                                fTable.Header(hdr =>
                                {
                                    hdr.Cell().Element(FHdrCell).Text("PLANNED TIME").Bold().FontColor(Colors.White).FontSize(7);
                                    hdr.Cell().Element(FHdrCell).Text("FUEL AVAILABLE").Bold().FontColor(Colors.White).FontSize(7);
                                    hdr.Cell().Element(FHdrCell).Text("FUEL REQUIRED").Bold().FontColor(Colors.White).FontSize(7);
                                    hdr.Cell().Element(FHdrCell).Text("CONSUMPTION").Bold().FontColor(Colors.White).FontSize(7);
                                });
                                fTable.Cell().Background(Colors.White).Padding(3)
                                    .Text(plannedTime).FontSize(8);
                                fTable.Cell().Background(Colors.White).Padding(3)
                                    .Text(fp.FuelAvailableMinutes.HasValue ? $"{fp.FuelAvailableMinutes} min" : "—").FontSize(8);
                                fTable.Cell().Background(Colors.White).Padding(3)
                                    .Text(fp.FuelRequiredMinutes.HasValue ? $"{fp.FuelRequiredMinutes} min" : "—").FontSize(8);
                                fTable.Cell().Background(Colors.White).Padding(3)
                                    .Text($"{BlankNum(fp.FuelConsumptionL)} L").FontSize(8);
                                return;

                                static IContainer FHdrCell(IContainer c) =>
                                    c.Background(PrimaryColor).Padding(3);
                            });

                            // Load calculations
                            left.Item().PaddingTop(6).PaddingBottom(2).Text("LOAD CALCULATIONS").Bold().FontSize(9).FontColor(PrimaryColor);
                            left.Item().Border(0.5f).Table(lTable =>
                            {
                                lTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(3);
                                });

                                lTable.Cell().Element(LLabelCell).Text("TAKEOFF TEMP").Bold().FontSize(7);
                                lTable.Cell().Element(LValueCell)
                                    .Text(fp.TemperatuurC.HasValue ? $"{fp.TemperatuurC} °C" : "—").FontSize(8);

                                lTable.Cell().Element(LLabelCell).Text("QNH").Bold().FontSize(7);
                                lTable.Cell().Element(LValueCell)
                                    .Text(fp.QnhHpa.HasValue ? $"{fp.QnhHpa} hPa" : "—").FontSize(8);

                                lTable.Cell().Element(LLabelCell).Text("ELEVATION").Bold().FontSize(7);
                                var elevFt = fp.Location?.ElevationM.HasValue == true
                                    ? $"{fp.Location.ElevationM.Value * 3.28084:F0} ft"
                                    : "—";
                                lTable.Cell().Element(LValueCell).Text(elevFt).FontSize(8);

                                lTable.Cell().Element(LLabelCell).Text("MAX ALTITUDE").Bold().FontSize(7);
                                lTable.Cell().Element(LValueCell)
                                    .Text(fp.MaxAltitudeFt.HasValue ? $"{fp.MaxAltitudeFt} ft" : "—").FontSize(8);

                                lTable.Cell().Element(LLabelCell).Text("ENVELOPE VOLUME").Bold().FontSize(7);
                                lTable.Cell().Element(LValueCell)
                                    .Text(fp.Balloon?.VolumeM3.HasValue == true ? $"{fp.Balloon.VolumeM3} m³" : "—").FontSize(8);

                                lTable.Cell().Element(LLabelCell).Text("LIFT AVAILABLE").Bold().FontSize(7);
                                lTable.Cell().Element(LValueCell)
                                    .Text(fp.TotaalLiftKg.HasValue ? $"{fp.TotaalLiftKg:F0} kg" : "—").FontSize(8);

                                lTable.Cell().Element(LLabelCell).Text("LIFT REQUIRED").Bold().FontSize(7);
                                lTable.Cell().Element(LValueCell)
                                    .Text($"{fp.TotaalGewichtOFP(passengerEquipmentKg):F0} kg").FontSize(8);
                                return;

                                static IContainer LValueCell(IContainer c) =>
                                    c.Background(Colors.White).Padding(3);

                                static IContainer LLabelCell(IContainer c) =>
                                    c.Background(LightBg).Padding(3);
                            });
                        });

                        mainRow.ConstantItem(8); // spacer

                        // ── Right column: equipment weights ──────────────────
                        mainRow.RelativeItem(2).Column(right =>
                        {
                            right.Item().PaddingBottom(2).Text("EQUIPMENT WEIGHTS").Bold().FontSize(9).FontColor(PrimaryColor);
                            right.Item().Border(0.5f).Table(eTable =>
                            {
                                eTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                });
                                eTable.Header(hdr =>
                                {
                                    hdr.Cell().Element(EHdrCell).Text("COMPONENT").Bold().FontColor(Colors.White).FontSize(7);
                                    hdr.Cell().Element(EHdrCell).Text("WEIGHT").Bold().FontColor(Colors.White).FontSize(7);
                                });

                                eTable.Cell().Background(LightBg).Padding(3).Text("ENVELOPE").FontSize(8);
                                eTable.Cell().Background(Colors.White).Padding(3).Text(FmtKg(fp.OFPEnvelopeWeightKg)).FontSize(8);

                                eTable.Cell().Background(LightBg).Padding(3).Text("BASKET").FontSize(8);
                                eTable.Cell().Background(Colors.White).Padding(3).Text(FmtKg(fp.OFPBasketWeightKg)).FontSize(8);

                                eTable.Cell().Background(LightBg).Padding(3).Text("BURNER").FontSize(8);
                                eTable.Cell().Background(Colors.White).Padding(3).Text(FmtKg(fp.OFPBurnerWeightKg)).FontSize(8);

                                eTable.Cell().Background(LightBg).Padding(3).Text("CYLINDERS").FontSize(8);
                                eTable.Cell().Background(Colors.White).Padding(3).Text(FmtKg(fp.CylindersWeightKg)).FontSize(8);

                                var picW = fp.PicWeightKg ?? fp.Pilot?.WeightKg;
                                eTable.Cell().Background(LightBg).Padding(3).Text("PIC").FontSize(8);
                                eTable.Cell().Background(Colors.White).Padding(3).Text(FmtKg(picW)).FontSize(8);
                                return;

                                static string FmtKg(double? v) => v.HasValue ? $"{v:F0} kg" : "—";

                                static IContainer EHdrCell(IContainer c) =>
                                    c.Background(PrimaryColor).Padding(3);
                            });

                            // Last minute updates
                            right.Item().PaddingTop(6).PaddingBottom(2).Text("LAST MINUTE UPDATES").Bold().FontSize(9).FontColor(PrimaryColor);
                            right.Item().Border(0.5f).MinHeight(40).Padding(3).Text("").FontSize(8);
                        });
                    });

                    // ── Signature block ──────────────────────────────────────
                    col.Item().PaddingTop(8).ShowEntire().Border(0.5f).Padding(6).Column(sig =>
                    {
                        sig.Item().Text("SIGNATURE").Bold().FontSize(9).FontColor(PrimaryColor);
                        sig.Item().PaddingTop(2).Text("The pilot's signature confirms the following:").FontSize(8);
                        sig.Item().Text("(a) Operating site suitability for balloon and operation.").FontSize(8);
                        sig.Item().Text("(b) Reserve fuel or ballast sufficiency for safe landing.").FontSize(8);
                        sig.Item().Text("(c) Passengers briefed on normal and emergency procedures.").FontSize(8);
                        sig.Item().Text("(d) Meteorological and aeronautical information reviewed, including alternatives.").FontSize(8);
                        sig.Item().Text("(e) Pilot and Crew members are fit, communicative, and not incapacitated.").FontSize(8);
                        sig.Item().PaddingTop(10).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                                c.Item().AlignCenter().Text("SIGNATURE / DATE").FontSize(7).FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    // ── Post-flight record ────────────────────────────────────
                    col.Item().PaddingTop(6).ShowEntire().Border(0.5f).Table(pfTable =>
                    {
                        pfTable.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(5);
                        });
                        pfTable.Header(hdr =>
                        {
                            hdr.Cell().ColumnSpan(2).Element(PFHdrCell)
                                .Text("AFTER-FLIGHT RECORD").Bold().FontColor(Colors.White).FontSize(7);
                        });

                        pfTable.Cell().Element(PFLabelCell).Text("ACTUAL LANDING").Bold().FontSize(7);
                        pfTable.Cell().Element(PFValueCell).Text(Blank(fp.ActualLandingNotes)).FontSize(8);

                        pfTable.Cell().Element(PFLabelCell).Text("ACTUAL DURATION").Bold().FontSize(7);
                        pfTable.Cell().Element(PFValueCell)
                            .Text(!fp.IsFlown
                                ? "________________"
                                : fp.ActualFlightDurationMinutes.HasValue
                                    ? $"{fp.ActualFlightDurationMinutes} min"
                                    : "—")
                            .FontSize(8);

                        pfTable.Cell().Element(PFLabelCell).Text("POST-FLIGHT REMARKS").Bold().FontSize(7);
                        pfTable.Cell().Element(PFValueCell).Text(Blank(fp.ActualRemarks)).FontSize(8);
                        return;

                        static IContainer PFValueCell(IContainer c) =>
                            c.Background(Colors.White).Padding(3);

                        static IContainer PFLabelCell(IContainer c) =>
                            c.Background(LightBg).Padding(3);

                        static IContainer PFHdrCell(IContainer c) =>
                            c.Background(PrimaryColor).Padding(3);
                    });

                    // ── After flight / Visible defects ──────────────────────
                    col.Item().PaddingTop(6).ShowEntire().Border(0.5f).Table(dTable =>
                    {
                        dTable.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3); // VISIBLE DEFECTS label / DEFECT / CERTIFICATE
                            cols.RelativeColumn(2); // YES-NO / ACTION / DEFECT
                            cols.RelativeColumn(2); // DATE / (merged) / AUTHORITY
                            cols.RelativeColumn();  // INITIALS
                            cols.RelativeColumn();  // DATE (cert)
                        });

                        // Row 1 — section title spanning all 5 columns
                        dTable.Cell().ColumnSpan(5).Element(DHeaderCell)
                            .Text("AFTER FLIGHT").Bold().FontColor(Colors.White).FontSize(7);

                        // Row 2 — VISIBLE DEFECTS (col 1) | YES/NO (cols 2–3) | DATE (cols 4–5)
                        dTable.Cell().Element(DLabelCell)
                            .Text("VISIBLE DEFECTS").Bold().FontSize(7);
                        dTable.Cell().ColumnSpan(2).Element(DValueCell)
                            .Text(BlankBool(fp.VisibleDefects)).FontSize(8);
                        dTable.Cell().ColumnSpan(2).Element(DValueCell)
                            .Text(fp.IsFlown ? fp.Datum.ToString("d-MM-yyyy") : "________________").FontSize(8);

                        // Row 3 — SIGNATURE label row
                        dTable.Cell().ColumnSpan(5).Element(DLabelCell)
                            .Text("SIGNATURE").Bold().FontSize(7);
                        // Row 4 — Signature fill area
                        dTable.Cell().ColumnSpan(5).Background(Colors.White).MinHeight(24).Padding(3)
                            .Text("").FontSize(8);

                        // Row 5 — DEFECT / ACTION labels
                        dTable.Cell().ColumnSpan(2).Element(DLabelCell)
                            .Text("DEFECT").Bold().FontSize(7);
                        dTable.Cell().ColumnSpan(3).Element(DLabelCell)
                            .Text("ACTION").Bold().FontSize(7);
                        // Row 6 — Defect / action data
                        dTable.Cell().ColumnSpan(2).Background(Colors.White).MinHeight(20).Padding(3)
                            .Text(Blank(fp.VisibleDefectsNotes)).FontSize(8);
                        dTable.Cell().ColumnSpan(3).Background(Colors.White).MinHeight(20).Padding(3)
                            .Text("").FontSize(8);

                        // Row 7 — CERTIFICATE sub-header
                        dTable.Cell().Element(DLabelCell).Text("CERTIFICATE").Bold().FontSize(6);
                        dTable.Cell().Element(DLabelCell).Text("DEFECT").Bold().FontSize(6);
                        dTable.Cell().Element(DLabelCell).Text("AUTHORITY").Bold().FontSize(6);
                        dTable.Cell().Element(DLabelCell).Text("INITIALS").Bold().FontSize(6);
                        dTable.Cell().Element(DLabelCell).Text("DATE").Bold().FontSize(6);
                        // Row 8 — Maintenance fill data
                        dTable.Cell().Background(Colors.White).MinHeight(20).Padding(3).Text("").FontSize(8);
                        dTable.Cell().Background(Colors.White).MinHeight(20).Padding(3).Text("").FontSize(8);
                        dTable.Cell().Background(Colors.White).MinHeight(20).Padding(3).Text("").FontSize(8);
                        dTable.Cell().Background(Colors.White).MinHeight(20).Padding(3).Text("").FontSize(8);
                        dTable.Cell().Background(Colors.White).MinHeight(20).Padding(3).Text("").FontSize(8);

                        // Row 9 — PART-ML disclaimer
                        dTable.Cell().ColumnSpan(5).Background(LightBg).Padding(4)
                            .Text("THIS CONFIRMS THAT THE SPECIFIED ACTIONS WERE EXECUTED ACCORDING PART-ML AND THAT THE AIRCRAFT IS DECLARED AS READY FOR THE NEXT FLIGHT.")
                            .Bold().FontSize(6).Italic();
                        return;

                        static IContainer DValueCell(IContainer c) =>
                            c.Background(Colors.White).Padding(3);

                        static IContainer DLabelCell(IContainer c) =>
                            c.Background(LightBg).Padding(3);

                        static IContainer DHeaderCell(IContainer c) =>
                            c.Background(PrimaryColor).Padding(3);
                    });
                });

                // ── Footer (empty — disclaimer moved into After Flight table) ─
            });
        }).GeneratePdf();

        return Task.FromResult(pdfBytes);

        string BlankBool(bool? value) =>
            !fp.IsFlown    ? "________________" :
            value.HasValue ? (value.Value ? "Ja" : "Neen") :
            "—";

        string BlankNum(double? value) =>
            !fp.IsFlown    ? "________________" :
            value.HasValue ? value.Value.ToString("0.#") :
            "—";

        // Post-flight blank helpers — show fill line when flight is not yet marked as flown
        string Blank(string? value) =>
            !fp.IsFlown                      ? "________________" :
            string.IsNullOrWhiteSpace(value) ? "—" :
            value!;
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "–";
        }

        var text = QuillUiMarkerRegex().Replace(html, "");
        text = BlockLevelHtmlTagRegex().Replace(text, m => m.Value.StartsWith("</") || m.Value.Contains("br") ? "\n" : "");
        text = AnyHtmlTagRegex().Replace(text, "");
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&")
            .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
        return ExcessiveNewLinesRegex().Replace(text, "\n\n").Trim();
    }

    [GeneratedRegex("""<span\s[^>]*class="ql-ui"[^>]*>.*?</span>""", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-BE")]
    private static partial Regex QuillUiMarkerRegex();
    [GeneratedRegex(@"<(h[1-6]|p|li)([^>]*)>(.*?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-BE")]
    private static partial Regex ExtractBlockElementsRegex();
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyHtmlTagRegex();
    [GeneratedRegex(@"ql-indent-(\d+)")]
    private static partial Regex QuillIndentLevelRegex();
    [GeneratedRegex("</?(p|h[1-6]|li|br|ul|ol|div)[^>]*>", RegexOptions.IgnoreCase, "en-BE")]
    private static partial Regex BlockLevelHtmlTagRegex();
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewLinesRegex();
}
