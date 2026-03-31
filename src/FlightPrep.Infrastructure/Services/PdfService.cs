using System.Text.Json;
using System.Text.RegularExpressions;
using FlightPrep.Models;
using FlightPrep.Models.Trajectory;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FlightPrep.Services;

public class PdfService(ISunriseService sunriseSvc, ITrajectoryMapService mapSvc, IGoNoGoService goNoGoSvc, IFlightAssessmentService assessmentSvc) : IPdfService
{
    private static readonly string PrimaryColor = "#1a3a5c";
    private static readonly string LightBg = "#f0f4f8";

    public async Task<byte[]> GenerateAsync(FlightPreparation fp, byte[]? mapPng = null, CancellationToken ct = default)
    {
        // Generate trajectory map server-side if the caller didn't provide a pre-rendered one
        mapPng ??= await mapSvc.RenderAsync(fp.TrajectorySimulationJson);

        Settings.License = LicenseType.Community;

        // Compute sunrise/sunset if location has coords
        (TimeOnly Sunrise, TimeOnly Sunset)? sunriseSunset = null;
        var loc = fp.Location;
        if (loc?.Latitude.HasValue == true && loc.Longitude.HasValue)
            sunriseSunset = sunriseSvc.Calculate(fp.Datum, loc.Latitude!.Value, loc.Longitude!.Value);

        var assessment = await assessmentSvc.ComputeAsync(fp);
        var gng = assessment.GoNoGo;

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
                    var gngBg = gng == "green" ? Colors.Green.Darken1 : gng == "yellow" ? Colors.Yellow.Darken1 : gng == "red" ? Colors.Red.Darken1 : Colors.Grey.Medium;
                    var gngText = gng == "green" ? "GO" : gng == "yellow" ? "CAUTION" : gng == "red" ? "NO-GO" : "Go/No-Go onbekend";
                    col.Item().PaddingBottom(4).Background(gngBg).Padding(5)
                        .Text(gngText).Bold().FontSize(11).FontColor(Colors.White);

                    // Build Section 1 rows including optional sunrise/sunset
                    var sec1Rows = new List<(string, string)>
                    {
                        ("Datum", fp.Datum.ToString("dd/MM/yyyy")),
                        ("Tijdstip (LT)", fp.Tijdstip.ToString("HH:mm")),
                        ("Ballon", fp.Balloon != null ? $"{fp.Balloon.Registration} – {fp.Balloon.Type} ({(fp.Balloon.VolumeM3.HasValue ? $"{fp.Balloon.VolumeM3:F0} m³" : "–")})" : "–"),
                        ("Piloot / PIC", fp.Pilot?.Name ?? "–"),
                        ("Locatie", fp.Location?.Name ?? "–"),
                        ("Veld eigenaar gemeld", fp.VeldEigenaarGemeld ? "Ja" : "Nee / NVT"),
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
                        ("CAPE", fp.CapeJkg.HasValue ? $"{fp.CapeJkg} J/kg" : "–"),
                    };
                    AddSection(col, "2. Meteorologische Informatie", sec2Rows.ToArray());

                    // Wind levels table
                    if (fp.WindLevels.Count > 0)
                    {
                        col.Item().PaddingTop(4).Column(wSection =>
                        {
                            wSection.Item().Background(LightBg).Padding(3).Row(row =>
                            {
                                row.RelativeItem(2).Text("Hoogte (ft)").Bold();
                                row.RelativeItem(2).Text("Richting (°)").Bold();
                                row.RelativeItem(2).Text("Snelheid (kt)").Bold();
                                row.RelativeItem(2).Text("Temp (°C)").Bold();
                            });
                            bool alt = false;
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
                        AddImageGrid(col, meteoImgs);

                    AddSection(col, "3. Luchtruim en NOTAMs", new[]
                    {
                        ("NOTAMs gecontroleerd", fp.NotamsGecontroleerd),
                        ("Luchtruimstructuur", fp.Luchtruimstructuur ?? "–"),
                        ("Beperkingen / gesloten zones", fp.Beperkingen ?? "–"),
                        ("Obstakels", fp.Obstakels ?? "–"),
                    });

                    AddSection(col, "4. Veiligheid en Communicatie", new[]
                    {
                        ("EHBO-kit en vuurblusser", fp.EhboEnBlusser),
                        ("Passagierslijst ingevuld", fp.PassagierslijstIngevuld),
                        ("Vluchtplan ingediend", fp.VluchtplanIngediend),
                    });

                    // Section 5 - Technische Controle with checkboxes
                    col.Item().PaddingTop(6).Background(PrimaryColor).Padding(4)
                        .Text("5. Technische Controle").Bold().FontColor(Colors.White).FontSize(10);
                    var checks = new[]
                    {
                        (fp.BranderGetest, "Brander getest"),
                        (fp.GasflaconsGecontroleerd, "Gasflessen gevuld & gecontroleerd"),
                        (fp.BallonVisueel, "Ballon en mand visueel geïnspecteerd"),
                        (fp.VerankeringenGecontroleerd, "Verankeringen en touwen gecontroleerd"),
                        (fp.InstrumentenWerkend, "Instrumenten werkend"),
                    };
                    bool alt5 = false;
                    foreach (var (checked_, label) in checks)
                    {
                        col.Item().Background(alt5 ? LightBg : Colors.White).Padding(3)
                            .Text($"{(checked_ ? "[JA]" : "[NEE]")}  {label}");
                        alt5 = !alt5;
                    }

                    col.Item().PaddingTop(6).Background(PrimaryColor).Padding(4)
                        .Text("6. Pax Briefing").Bold().FontColor(Colors.White).FontSize(10);
                    col.Item().Background(Colors.White).Padding(4).Column(body =>
                        RenderHtmlToColumn(body, fp.PaxBriefing));

                    // Section 7 - Load Calculation (items added directly to outer col for proper page-break support)
                    col.Item().PaddingTop(6).Background(PrimaryColor).Padding(4)
                        .Text("7. Load Berekening").Bold().FontColor(Colors.White).FontSize(10);
                    col.Item().Background(LightBg).Padding(3)
                        .Text($"Gewicht envelop+brander+mand+flessen: {fp.EnvelopeWeightKg?.ToString("F1") ?? "–"} kg");
                    col.Item().Background(Colors.White).Padding(3)
                        .Text($"Piloot: {fp.Pilot?.Name ?? "–"}  –  {fp.Pilot?.WeightKg?.ToString("F1") ?? "–"} kg");

                    // Passenger table header
                    col.Item().Background(Colors.Grey.Lighten2).Padding(3).Row(row =>
                    {
                        row.RelativeItem(3).Text("Passagier").Bold();
                        row.RelativeItem(1).Text(t => { t.AlignRight(); t.Span("Gewicht (kg)").Bold(); });
                    });
                    bool altLoad = false;
                    foreach (var p in fp.Passengers.OrderBy(x => x.Order))
                    {
                        var pWeight = p.WeightKg > 0 ? p.WeightKg.ToString("F1") : "–";
                        col.Item().Background(altLoad ? LightBg : Colors.White).Padding(3).Row(row =>
                        {
                            row.RelativeItem(3).Text(p.Name);
                            row.RelativeItem(1).Text(t => { t.AlignRight(); t.Span(pWeight); });
                        });
                        altLoad = !altLoad;
                    }
                    var totalWeight = $"{assessment.TotaalGewicht:F1} kg";
                    col.Item().Background(Colors.Grey.Lighten3).Padding(3).Row(row =>
                    {
                        row.RelativeItem(3).Text("Totaal gewicht").Bold();
                        row.RelativeItem(1).Text(t => { t.AlignRight(); t.Span(totalWeight).Bold(); });
                    });
                    col.Item().Background(Colors.White).Padding(3)
                        .Text($"Max Altitude: {(fp.MaxAltitudeFt.HasValue ? fp.MaxAltitudeFt + " ft" : "–")}  |  Lift units: {fp.LiftUnits?.ToString("F0") ?? "–"}  |  Totaal lift: {fp.TotaalLiftKg?.ToString("F1") ?? "–"} kg");
                    col.Item().Background(LightBg).Padding(3)
                        .Text(assessment.LiftVoldoende ? "Lift voldoende" : "Lift onvoldoende").Bold()
                        .FontColor(assessment.LiftVoldoende ? Colors.Green.Darken2 : Colors.Red.Darken2);

                    // ISA lift calculation block — only when all inputs are present
                    if (fp.Balloon?.VolumeM3.HasValue == true &&
                        fp.Balloon?.InternalEnvelopeTempC.HasValue == true &&
                        fp.Location?.ElevationM.HasValue == true &&
                        fp.TemperatuurC.HasValue &&
                        fp.MaxAltitudeFt.HasValue)
                    {
                        var A  = fp.MaxAltitudeFt.Value * 0.3048;
                        var Eg = fp.Location.ElevationM.Value;
                        var Tg = fp.TemperatuurC.Value;
                        var Ti = fp.Balloon.InternalEnvelopeTempC.Value;
                        var V  = fp.Balloon.VolumeM3.Value;
                        var lr = LiftCalculator.Calculate(A, Eg, Tg, Ti, V);

                        col.Item().Background(LightBg).Padding(3)
                            .Text($"Ti (inw. temp): {Ti:F0}°C  |  Ballonvolume: {V:F0} m³");
                        col.Item().Background(Colors.White).Padding(3)
                            .Text($"ISA Ta @ max alt: {lr.AmbientTempAtAltC:F1}°C  |  ISA P @ max alt: {lr.PressureHpa:F1} hPa");

                        col.Item().Background(LightBg).Padding(3)
                            .Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(8));
                                t.Line($"Stap 1¹: Ta@alt = {Tg:F1} − 0,0065×({A:F0}−{Eg:F0}) = {lr.AmbientTempAtAltC:F1}°C");
                                t.Line($"Stap 2¹: P@alt = 1013,25×(1−0,0065×{A:F0}/288,15)^5,256 = {lr.PressureHpa:F1} hPa");
                                t.Line($"Stap 3¹: L = 0,3484×{V:F0}×{lr.PressureHpa:F1}×(1/(Ta+273)−1/(Ti+273)) = {lr.TotalLiftKg:F1} kg");
                            });
                        col.Item().Background(Colors.White).Padding(3)
                            .Text(t =>
                            {
                                t.DefaultTextStyle(s => s.FontSize(7).Italic().FontColor(Colors.Grey.Darken1));
                                t.Line("¹ Formule: Belgian Hot Air Balloon Flight Manual,");
                                t.Span("Amendment 18, Appendix 2, p. A2-1 (Cameron Balloons Ltd.)");
                            });
                    }

                    if (!string.IsNullOrWhiteSpace(fp.LoadNotes))
                        col.Item().Background(Colors.White).Padding(3).Text($"Notities: {fp.LoadNotes}");

                    AddSection(col, "8. Traject", new[]
                    {
                        ("Trajectnotities", fp.Traject ?? "–"),
                    });

                    // Simulated trajectories summary
                    if (!string.IsNullOrWhiteSpace(fp.TrajectorySimulationJson))
                    {
                        try
                        {
                            var allTrajs = JsonSerializer.Deserialize<List<SimulatedTrajectory>>(fp.TrajectorySimulationJson);
                            if (allTrajs != null && allTrajs.Count > 0)
                            {
                                col.Item().PaddingTop(4)
                                    .Text("Trajectsimulaties").Bold().FontSize(8.5f).FontColor(PrimaryColor);

                                bool alt2 = false;
                                foreach (var t in allTrajs.Where(t => t.Points.Count > 0))
                                {
                                    var last  = t.Points[^1];
                                    var src   = t.DataSource == TrajectoryDataSource.Hysplit
                                                    ? "Open-Meteo 3D"
                                                    : t.DataSource == TrajectoryDataSource.OpenMeteo
                                                        ? "Open-Meteo"
                                                        : "Dead-reckoning";
                                    var landing = $"{last.Lat:F4}°N  {last.Lon:F4}°E";
                                    var altRow  = $"{t.AltitudeFt} ft  |  {src}  |  {t.DurationMinutes} min  |  berekend {t.SimulatedAt:dd/MM/yyyy HH:mm}  |  landing ≈ {landing}";

                                    col.Item()
                                       .Background(alt2 ? LightBg : Colors.White)
                                       .Padding(2)
                                       .Text(altRow)
                                       .FontSize(8);
                                    alt2 = !alt2;
                                }
                            }
                        }
                        catch { /* malformed JSON — skip silently */ }
                    }

                    // Trajectory map image
                    if (mapPng != null)
                        col.Item().PaddingTop(6).Image(mapPng).FitWidth();

                    // Traject images
                    var trajImgs = fp.Images.Where(i => i.Section == "Traject").ToList();
                    if (trajImgs.Count > 0)
                        AddImageGrid(col, trajImgs);

                    col.Item().PaddingTop(6).Background(PrimaryColor).Padding(4)
                        .Text("9. Ballonbulletin").Bold().FontColor(Colors.White).FontSize(10);
                    col.Item().Background(Colors.White).Padding(4)
                        .Text(fp.Ballonbulletin ?? "–").FontFamily("Courier New").FontSize(7.5f);

                    if (fp.IsFlown)
                    {
                        col.Item().PaddingTop(6).Background(Colors.Green.Darken1).Padding(4)
                            .Text("Vluchtverslag").Bold().FontColor(Colors.White).FontSize(10);
                        col.Item().Background(LightBg).Padding(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("Werkelijke landing").Bold();
                            row.RelativeItem(2).Text(fp.ActualLandingNotes ?? "–");
                        });
                        col.Item().Background(Colors.White).Padding(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("Vluchtduur").Bold();
                            row.RelativeItem(2).Text(fp.ActualFlightDurationMinutes.HasValue ? $"{fp.ActualFlightDurationMinutes} min" : "–");
                        });
                        col.Item().Background(LightBg).Padding(3).Row(row =>
                        {
                            row.RelativeItem(1).Text("Opmerkingen").Bold();
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
        for (int start = 0; start < images.Count; start += perRow)
        {
            var batch = images.Skip(start).Take(perRow).ToList();
            col.Item().PaddingTop(4).Row(row =>
            {
                foreach (var img in batch)
                    row.RelativeItem().Padding(3).Image(img.Data).FitArea();
                // keep columns balanced when batch is smaller than perRow
                for (int i = batch.Count; i < perRow; i++)
                    row.RelativeItem();
            });
        }
    }

    private static void AddSection(ColumnDescriptor col, string title, (string Label, string Value)[] rows)
    {
        col.Item().PaddingTop(6).Column(section =>
        {
            section.Item().Background(PrimaryColor).Padding(4)
                .Text(title).Bold().FontColor(Colors.White).FontSize(10);
            bool alt = false;
            foreach (var (label, value) in rows)
            {
                section.Item().Background(alt ? "#f0f4f8" : Colors.White).Padding(3).Row(row =>
                {
                    row.RelativeItem(1).Text(label).Bold();
                    row.RelativeItem(2).Text(value);
                });
                alt = !alt;
            }
        });
    }

    /// <summary>
    /// Renders Quill-generated HTML into a QuestPDF ColumnDescriptor,
    /// preserving headings, bullet/ordered lists and indent levels.
    /// </summary>
    private static void RenderHtmlToColumn(ColumnDescriptor body, string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) { body.Item().Text("–"); return; }

        // Remove Quill's internal UI marker spans (empty, just for visual bullets)
        var clean = Regex.Replace(html,
            @"<span\s[^>]*class=""ql-ui""[^>]*>.*?</span>",
            "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Extract block elements one by one
        var blockRx = new Regex(
            @"<(h[1-6]|p|li)([^>]*)>(.*?)</\1>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        bool any = false;
        foreach (Match m in blockRx.Matches(clean))
        {
            var tag   = m.Groups[1].Value.ToLowerInvariant();
            var attrs = m.Groups[2].Value;
            // Strip all remaining inline tags, decode entities
            var text  = Regex.Replace(m.Groups[3].Value, "<[^>]+>", "")
                             .Replace("&nbsp;", " ").Replace("&amp;", "&")
                             .Replace("&lt;", "<").Replace("&gt;", ">")
                             .Replace("&quot;", "\"").Trim();

            if (string.IsNullOrWhiteSpace(text)) continue;
            any = true;

            var indentMatch = Regex.Match(attrs, @"ql-indent-(\d+)");
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
                    body.Item().PaddingLeft(8 + indent).Text($"{bullet} {text}").FontSize(9); break;
                default: // p
                    body.Item().PaddingTop(1).PaddingLeft(indent).Text(text).FontSize(9); break;
            }
        }

        if (!any) body.Item().Text("–");
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "–";
        var text = Regex.Replace(html, @"<span\s[^>]*class=""ql-ui""[^>]*>.*?</span>", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"</?(p|h[1-6]|li|br|ul|ol|div)[^>]*>",
            m => m.Value.StartsWith("</") || m.Value.Contains("br") ? "\n" : "",
            RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", "");
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&")
                   .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
        return Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
    }
}
