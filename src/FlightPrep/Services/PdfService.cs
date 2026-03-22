using System.Text.RegularExpressions;
using FlightPrep.Models;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FlightPrep.Services;

public class PdfService
{
    private static readonly string PrimaryColor = "#1a3a5c";
    private static readonly string LightBg = "#f0f4f8";

    public byte[] Generate(FlightPreparation fp)
    {
        Settings.License = LicenseType.Community;

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
                    AddSection(col, "1. Algemene Gegevens", new[]
                    {
                        ("Datum", fp.Datum.ToString("dd/MM/yyyy")),
                        ("Tijdstip (LT)", fp.Tijdstip.ToString("HH:mm")),
                        ("Ballon", fp.Balloon != null ? $"{fp.Balloon.Registration} – {fp.Balloon.Type} ({fp.Balloon.Volume})" : "–"),
                        ("Piloot / PIC", fp.Pilot?.Name ?? "–"),
                        ("Locatie", fp.Location?.Name ?? "–"),
                    });

                    AddSection(col, "2. Meteorologische Informatie", new[]
                    {
                        ("METAR", fp.Metar ?? "–"),
                        ("TAF", fp.Taf ?? "–"),
                        ("Wind per hoogte", fp.WindPerHoogte ?? "–"),
                        ("Neerslag / bewolking", fp.Neerslag ?? "–"),
                        ("Temperatuur", fp.TemperatuurC.HasValue ? $"{fp.TemperatuurC}°C" : "–"),
                        ("Dauwpunt", fp.DauwpuntC.HasValue ? $"{fp.DauwpuntC}°C" : "–"),
                        ("QNH", fp.QnhHpa.HasValue ? $"{fp.QnhHpa} hPa" : "–"),
                        ("Zichtbaarheid", fp.ZichtbaarheidKm.HasValue ? $"{fp.ZichtbaarheidKm} km" : "–"),
                        ("CAPE", fp.CapeJkg.HasValue ? $"{fp.CapeJkg} J/kg" : "–"),
                    });

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
                    col.Item().PaddingTop(6).Column(section =>
                    {
                        section.Item().Background(PrimaryColor).Padding(4)
                            .Text("5. Technische Controle").Bold().FontColor(Colors.White).FontSize(10);
                        var checks = new[]
                        {
                            (fp.BranderGetest, "Brander getest"),
                            (fp.GasflaconsGecontroleerd, "Gasflessen gevuld & gecontroleerd"),
                            (fp.BallonVisueel, "Ballon en mand visueel geïnspecteerd"),
                            (fp.VerankeringenGecontroleerd, "Verankeringen en touwen gecontroleerd"),
                            (fp.InstrumentenWerkend, "Instrumenten werkend"),
                        };
                        bool alt = false;
                        foreach (var (checked_, label) in checks)
                        {
                            section.Item().Background(alt ? LightBg : Colors.White).Padding(3)
                                .Text($"{(checked_ ? "☑" : "☐")}  {label}");
                            alt = !alt;
                        }
                    });

                    col.Item().PaddingTop(6).Column(section =>
                    {
                        section.Item().Background(PrimaryColor).Padding(4)
                            .Text("6. Pax Briefing").Bold().FontColor(Colors.White).FontSize(10);
                        section.Item().Background(Colors.White).Padding(4).Column(body =>
                            RenderHtmlToColumn(body, fp.PaxBriefing));
                    });

                    // Section 7 - Load Calculation
                    col.Item().PaddingTop(6).Column(section =>
                    {
                        section.Item().Background(PrimaryColor).Padding(4)
                            .Text("7. Load Berekening").Bold().FontColor(Colors.White).FontSize(10);
                        section.Item().Background(LightBg).Padding(3)
                            .Text($"Gewicht envelop+brander+mand+flessen: {fp.EnvelopeWeightKg?.ToString("F1") ?? "–"} kg");
                        section.Item().Background(Colors.White).Padding(3)
                            .Text($"Piloot: {fp.Pilot?.Name ?? "–"}  –  {fp.Pilot?.WeightKg?.ToString("F1") ?? "–"} kg");

                        // Passenger table header
                        section.Item().Background(Colors.Grey.Lighten2).Padding(3).Row(row =>
                        {
                            row.RelativeItem(3).Text("Passagier").Bold();
                            row.RelativeItem(1).AlignRight().Text("Gewicht (kg)").Bold();
                        });
                        bool alt = false;
                        foreach (var p in fp.Passengers.OrderBy(x => x.Order))
                        {
                            section.Item().Background(alt ? LightBg : Colors.White).Padding(3).Row(row =>
                            {
                                row.RelativeItem(3).Text(p.Name);
                                row.RelativeItem(1).AlignRight().Text(p.WeightKg.ToString("F1"));
                            });
                            alt = !alt;
                        }
                        section.Item().Background(Colors.Grey.Lighten3).Padding(3).Row(row =>
                        {
                            row.RelativeItem(3).Text("Totaal gewicht").Bold();
                            row.RelativeItem(1).AlignRight().Text($"{fp.TotaalGewicht:F1} kg").Bold();
                        });
                        section.Item().Background(Colors.White).Padding(3)
                            .Text($"Max Altitude: {(fp.MaxAltitudeFt.HasValue ? fp.MaxAltitudeFt + " ft" : "–")}  |  Lift units: {fp.LiftUnits?.ToString("F0") ?? "–"}  |  Totaal lift: {fp.TotaalLiftKg?.ToString("F1") ?? "–"} kg");
                        section.Item().Background(LightBg).Padding(3)
                            .Text(fp.LiftVoldoende ? "✅ Lift voldoende" : "❌ Lift onvoldoende").Bold()
                            .FontColor(fp.LiftVoldoende ? Colors.Green.Darken2 : Colors.Red.Darken2);
                        if (!string.IsNullOrWhiteSpace(fp.LoadNotes))
                            section.Item().Background(Colors.White).Padding(3).Text($"Notities: {fp.LoadNotes}");
                    });

                    AddSection(col, "8. Traject", new[]
                    {
                        ("Trajectnotities", fp.Traject ?? "–"),
                    });

                    // Traject images
                    var trajImgs = fp.Images.Where(i => i.Section == "Traject").ToList();
                    if (trajImgs.Count > 0)
                        AddImageGrid(col, trajImgs);

                    col.Item().PaddingTop(6).Column(section =>
                    {
                        section.Item().Background(PrimaryColor).Padding(4)
                            .Text("9. Ballonbulletin").Bold().FontColor(Colors.White).FontSize(10);
                        section.Item().Background(Colors.White).Padding(4)
                            .Text(fp.Ballonbulletin ?? "–").FontFamily("Courier New").FontSize(7.5f);
                    });
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
                    var bullet = attrs.Contains("ordered") ? "–" : "•";
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
