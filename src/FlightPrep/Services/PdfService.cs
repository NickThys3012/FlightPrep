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
                        section.Item().Background(Colors.White).Padding(4)
                            .Text(fp.PaxBriefing ?? "–").FontFamily("Courier New");
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
}
