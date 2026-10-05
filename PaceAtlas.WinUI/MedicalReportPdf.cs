using System.Globalization;
using System.Text.Json;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PaceAtlas;

namespace PaceAtlas.WinUI;

internal sealed record MedicalReportOptions(DateTime Start, DateTime End, bool English,
    bool Nutrition, bool Medication, bool History, bool PersonalNotes, string AppointmentTopics);

internal static class MedicalReportPdf
{
    private static readonly Color Navy = Color.Parse("#1F2B3E");
    private static readonly Color Teal = Color.Parse("#167789");
    private static readonly Color Light = Color.Parse("#EAF3F6");
    private static readonly Color Pale = Color.Parse("#F5F8FA");
    private static readonly Color Grey = Color.Parse("#526478");
    private static readonly Color Rule = Color.Parse("#D7E2E8");

    public static void Write(string path, MedicalReportOptions options, List<Entry> all,
        List<MealRecord> allMeals, List<MedicationPlan> plans)
    {
        string L(string de, string en) => options.English ? en : de;
        string Date(DateTime date) => date.ToString(options.English ? "MM/dd/yyyy" : "dd.MM.yyyy",
            CultureInfo.InvariantCulture);
        string Num(double number) => number.ToString("0.#", CultureInfo.GetCultureInfo(options.English ? "en-US" : "de-DE"));
        var range = all.Where(e => e.Start.Date >= options.Start && e.Start.Date <= options.End)
            .OrderBy(e => e.Start).ToArray();
        var states = range.Where(e => e.Kind == "Zustand").Select(e => (Entry: e, Data: State(e)))
            .ToArray();
        var activities = range.Where(e => e.Kind == "Aktivität").ToArray();
        var precedingActivities = all.Where(e => e.Kind == "Aktivität" && e.End is { } finish &&
            finish >= options.Start.AddHours(-48) && finish <= options.End.AddDays(1)).ToArray();
        var rest = range.Where(e => e.Kind == "Ruhe").ToArray();
        var sleep = rest.Where(ConditionAnalysis.IsSleep).ToArray();
        var consumed = allMeals.Where(m => m.Status == "consumed" && m.At.Date >= options.Start &&
            m.At.Date <= options.End).OrderBy(m => m.At).ToArray();
        var days = (options.End - options.Start).Days + 1;
        var documented = states.Select(s => s.Entry.Start.Date).Distinct().Count();
        var pem = states.Count(s => s.Data.Pem == 2);
        var crashes = states.Count(s => s.Data.Crash);
        var names = states.SelectMany(s => s.Data.Symptoms.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new { Name = name,
                Assessed = states.Count(s => s.Data.SymptomSeverity(name) >= 0),
                Present = states.Count(s => s.Data.SymptomSeverity(name) > 0),
                Severe = states.Count(s => s.Data.SymptomSeverity(name) >= 3) })
            .Where(s => s.Present > 0).OrderByDescending(s => s.Present)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

        var document = new Document();
        document.Info.Title = L("Pace Atlas - Arztbericht", "Pace Atlas - Medical report");
        document.Info.Subject = $"{Date(options.Start)} - {Date(options.End)}";
        var normal = document.Styles["Normal"]!;
        var normalFont = normal.Font!;
        normalFont.Name = "Segoe UI";
        normalFont.Size = 9;
        normalFont.Color = Navy;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.55);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.55);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.1);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.75);

        var header = section.Headers.Primary.AddParagraph(L("PACE ATLAS  /  ARZTBERICHT", "PACE ATLAS  /  MEDICAL REPORT"));
        header.Format.Font.Bold = true;
        header.Format.Font.Size = 8;
        header.Format.Font.Color = Grey;
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.Format.Font.Color = Grey;
        footer.Format.TabStops.AddTabStop(Unit.FromCentimeter(17.8), TabAlignment.Right);
        footer.AddText(L("Selbstdokumentation - keine ärztliche Diagnose", "Self-reported data - not a medical diagnosis"));
        footer.AddTab();
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        Title(section, L("Überblick", "Overview"), options, Date);
        MetricTiles(section, ( $"{documented} / {days}", L("Tage dokumentiert", "days documented")),
            ($"{pem}", L("PEM-Markierungen", "PEM marks")),
            ($"{crashes}", L("Crash-Markierungen", "crash marks")));
        Heading(section, L("Was im Zeitraum auffällt", "Highlights in this period"));
        Paragraph(section, names.Length == 0
            ? L("Keine Beschwerden als vorhanden erfasst.", "No symptoms recorded as present.")
            : L("Häufig dokumentiert: ", "Frequently recorded: ") + string.Join("; ", names.Take(3)
                .Select(s => $"{SymptomName(s.Name, options.English)} {s.Present}/{s.Assessed}")) + ".");
        var limiting = states.Select(s => s.Data.MostLimitingSymptom).Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count())
            .Take(3).ToArray();
        if (limiting.Length > 0)
            Paragraph(section, L("Selbst als am stärksten einschränkend angegeben: ", "Self-reported as most limiting: ") +
                string.Join("; ", limiting.Select(g => $"{SymptomName(g.Key, options.English)} ({g.Count()}×)")) + ".");
        Heading(section, L("PEM im Verlauf", "PEM over time"));
        WeeklyTable(section, options, states, Date);
        Heading(section, L("Für den nächsten Termin", "For the next appointment"));
        Box(section, string.IsNullOrWhiteSpace(options.AppointmentTopics)
            ? L("Keine Fragen oder Themen ergänzt.", "No questions or topics added.")
            : options.AppointmentTopics);

        section.AddPageBreak();
        Title(section, L("Zustand und Symptome", "Condition and symptoms"), options, Date);
        Paragraph(section, L("Punktuelle Selbsteinschätzungen. Lücken gelten nicht als beschwerdefreie Zeit.",
            "Point-in-time self-reports. Gaps do not mean symptom-free time."));
        Heading(section, L("Häufigkeit und Datenabdeckung", "Frequency and data coverage"));
        if (names.Length == 0) Paragraph(section, L("Keine beurteilten Symptome im Zeitraum.", "No assessed symptoms in this period."));
        else
        {
            var rows = names.Take(8).Select(s => new[] { SymptomName(s.Name, options.English),
                $"{s.Present} / {s.Assessed}", $"{s.Severe}",
                new string('■', Math.Max(1, (int)Math.Round(8.0 * s.Present / s.Assessed))) });
            Table(section, [L("Symptom", "Symptom"), L("Vorhanden / beurteilt", "Present / assessed"),
                L("Stärke 3–4", "Severity 3–4"), L("Anteil", "Share")], rows, [5.5, 4.3, 2.8, 2.4]);
            if (names.Length > 8) Paragraph(section, L($"Weitere {names.Length - 8} Symptome sind in den Rohdaten vorhanden.",
                $"{names.Length - 8} additional symptoms occur in the source data."));
        }
        Box(section, L("18/21 bedeutet: 18-mal vorhanden bei 21 beurteilten Einträgen. Nicht beurteilte Angaben zählen nicht.",
            "18/21 means present in 18 of 21 assessed entries. Unassessed values do not count."));
        Heading(section, L("Allgemeinzustand nach Kalenderwoche", "Overall condition by week"));
        WeeklyCondition(section, options, states, Num);

        section.AddPageBreak();
        Title(section, L("Belastung und Erholung", "Activity and recovery"), options, Date);
        MetricTiles(section, ($"{activities.Length}", L("Aktivitäten", "activities")),
            ($"{rest.Length}", L("Ruheintervalle", "rest intervals")),
            ($"{sleep.Length}", L("Schlafintervalle", "sleep intervals")));
        Paragraph(section, L("Schlaf ist als Ruheform erfasst. Laufende Intervalle haben keine bekannte abgeschlossene Dauer.",
            "Sleep is recorded as a type of rest. Ongoing intervals have no known completed duration."));
        Heading(section, L("Zeitliche Nähe zu abgeschlossenen Aktivitäten", "Timing after completed activities"));
        var windows = new[] { (0, 12), (12, 24), (24, 48) };
        Table(section, [L("Abstand", "Interval"), L("PEM-Einträge mit Aktivität", "PEM entries with activity")],
            windows.Select(w => new[] { $"{w.Item1}–{w.Item2} h",
                $"{states.Count(s => s.Data.Pem == 2 && precedingActivities.Any(a => a.End is { } finish &&
                    (s.Entry.Start - finish).TotalHours >= w.Item1 &&
                    (s.Entry.Start - finish).TotalHours < w.Item2))} / {pem}" }), [6.0, 9.0]);
        Box(section, L("Die Zählungen können sich überschneiden, wenn mehrere Aktivitäten demselben PEM-Eintrag vorausgingen. Zeitliche Nähe belegt keine Ursache.",
            "Counts can overlap when several activities precede one PEM entry. Timing does not establish causation."));

        if (options.Nutrition)
        {
            section.AddPageBreak();
            Title(section, L("Ernährung", "Nutrition"), options, Date);
            Paragraph(section, L("Optionaler Abschnitt. Nur als gegessen gespeicherte Mahlzeiten zählen; fehlende Mahlzeiten werden nicht ergänzt.",
                "Optional section. Only meals saved as eaten count; missing meals are not inferred."));
            Heading(section, L("Erfasste Mahlzeiten", "Recorded meals"));
            MetricTiles(section, ($"{consumed.Length}", L("gegessene Mahlzeiten", "eaten meals")),
                ($"{consumed.Select(m => m.At.Date).Distinct().Count()} / {days}", L("Tage mit Mahlzeit", "days with meals")));
            var completeDays = consumed.GroupBy(m => m.At.Date).Select(g => new
            {
                Day = g.Key,
                Complete = g.All(m => m.Ingredients.Count > 0 && m.Ingredients.All(i => i.CarbsPer100G.HasValue)),
                Carbs = g.Sum(m => m.Ingredients.Sum(i => (i.CarbsPer100G ?? 0) * i.Grams / 100))
            }).Where(d => d.Complete).ToArray();
            Heading(section, L("Kohlenhydrate aus dokumentierten Mahlzeiten", "Carbohydrates in recorded meals"));
            Paragraph(section, completeDays.Length == 0
                ? L("Keine Tage mit vollständig bekannten Kohlenhydratwerten.", "No days with complete carbohydrate data.")
                : L($"Ø {Num(completeDays.Average(d => d.Carbs))} g pro Tag mit vollständig bekannten Werten ({completeDays.Length} Tage).",
                    $"Average {Num(completeDays.Average(d => d.Carbs))} g per day with complete values ({completeDays.Length} days)."));
            var byWeek = completeDays.GroupBy(d => (int)((d.Day - options.Start).TotalDays / 7))
                .OrderBy(g => g.Key).Take(12).Select(g => new[] { $"{Date(options.Start.AddDays(g.Key * 7))} – " +
                    Date(options.Start.AddDays(Math.Min(days - 1, g.Key * 7 + 6))),
                    $"{Num(g.Average(d => d.Carbs))} g", $"{g.Count()}" });
            Table(section, [L("Zeitraum", "Period"), L("Ø KH / Tag", "Avg. carbs / day"),
                L("Tage", "Days")], byWeek, [7.0, 5.0, 3.0]);
            if (completeDays.Any(d => (d.Day - options.Start).TotalDays >= 84))
                Paragraph(section, L("Die Tabelle zeigt höchstens die ersten zwölf Wochen mit Daten.",
                    "The table shows at most the first twelve weeks with data."));
            Paragraph(section, L("Der Tageswert kann weitere, nicht erfasste Mahlzeiten nicht berücksichtigen.",
                "Daily totals cannot include meals that were not recorded."));
            Heading(section, L("Glykämische Last und Datenabdeckung", "Glycemic load and data coverage"));
            var knownGl = consumed.Count(m => m.Ingredients.Count > 0 &&
                m.Ingredients.All(i => i.GlycemicLoadPer100G.HasValue));
            Box(section, L($"GL-Schätzung für {knownGl} von {consumed.Length} Mahlzeiten möglich. GI/GL fehlen bei manchen Lebensmitteln; fehlende Werte werden nicht ersetzt.",
                $"GL estimate available for {knownGl} of {consumed.Length} meals. GI/GL values are missing for some foods and are never filled in."));
        }

        if (options.Medication || options.History)
        {
            section.AddPageBreak();
            Title(section, L("Medikamente und Anhang", "Medication and appendix"), options, Date);
            if (options.Medication)
            {
                Heading(section, L("Einnahmeplan am Ende des Berichtszeitraums", "Medication plan at end of report period"));
                var active = plans.Where(p => p.IsActiveOn(DateOnly.FromDateTime(options.End)))
                    .OrderBy(p => p.Time).ThenBy(p => p.Name).ToArray();
                if (active.Length == 0) Paragraph(section, L("Kein aktiver Einnahmeplan hinterlegt.", "No active medication plan recorded."));
                else Table(section, [L("Präparat", "Product"), L("Zeit", "Time"), L("Plan", "Schedule")],
                    active.Select(p => new[] { p.Name, p.Time, string.Join(" ", new[] { p.Quantity, p.Dose, p.Form }
                        .Where(x => !string.IsNullOrWhiteSpace(x))) }), [6.6, 3.2, 5.2]);
                var measures = range.Where(e => e.Kind == "Maßnahme").Select(e => Measure(e))
                    .Where(m => m is not null).ToArray();
                if (measures.Length > 0)
                {
                    Heading(section, L("Dokumentierte Maßnahmen", "Recorded interventions"));
                    Table(section, [L("Maßnahme", "Intervention"), L("Beobachtung", "Observation")],
                        measures.Take(8).Select(m => new[] { m!.Name, string.IsNullOrWhiteSpace(m.Outcome)
                            ? L("Keine dokumentiert", "None documented") : m.Outcome }), [6.0, 9.0]);
                }
            }
            if (options.History)
            {
                Heading(section, L("Auszug aus Tagen mit PEM oder Crash", "Days with PEM or crash (selection)"));
                var notable = states.Where(s => s.Data.Pem == 2 || s.Data.Crash).Take(12).ToArray();
                if (notable.Length == 0) Paragraph(section, L("Keine entsprechend markierten Einträge.", "No entries marked accordingly."));
                else if (options.PersonalNotes)
                    Table(section, [L("Datum", "Date"), L("Allgemein", "Overall"), "PEM / Crash",
                        L("Notiz", "Note")], notable.Select(s => new[] { Date(s.Entry.Start),
                            s.Data.Overall.ToString(CultureInfo.InvariantCulture) + " / 4",
                            (s.Data.Pem == 2 ? "PEM" : "") + (s.Data.Crash ? " · Crash" : ""),
                            Truncate(s.Entry.Note, 85) }), [3.0, 3.3, 3.3, 5.4]);
                else Table(section, [L("Datum", "Date"), L("Allgemein", "Overall"), "PEM / Crash"],
                    notable.Select(s => new[] { Date(s.Entry.Start),
                        s.Data.Overall.ToString(CultureInfo.InvariantCulture) + " / 4",
                        (s.Data.Pem == 2 ? "PEM" : "") + (s.Data.Crash ? " · Crash" : "") }), [4.0, 5.0, 6.0]);
            }
            Heading(section, L("Datenbasis und Hinweise", "Data and interpretation"));
            Paragraph(section, L($"{documented} von {days} Tagen mit Zustandseintrag. Fehlende Angaben werden nicht als Beschwerdefreiheit ausgelegt. Die Auswertung ersetzt keine ärztliche Diagnose und trifft keine Kausalaussagen.",
                $"Condition entries on {documented} of {days} days. Missing values do not imply symptom-free time. This report is not a diagnosis and makes no causal claims."));
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        // Keep a canceled or failed render from replacing an existing report.
        var temporary = path + ".tmp";
        try
        {
            renderer.PdfDocument.Save(temporary);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static StateData State(Entry entry)
    {
        try { return JsonSerializer.Deserialize<StateData>(entry.Data) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static MeasureData? Measure(Entry entry)
    {
        try { return JsonSerializer.Deserialize<MeasureData>(entry.Data); }
        catch (JsonException) { return null; }
    }

    private static string SymptomName(string name, bool english) => english
        ? Localization.Translate(name, "en") : name;
    private static string Truncate(string value, int length) => value.Length <= length
        ? value : value[..(length - 1)] + "…";

    private static void Title(Section section, string heading, MedicalReportOptions options, Func<DateTime, string> date)
    {
        var banner = section.AddTable();
        banner.AddColumn(Unit.FromCentimeter(17.8));
        var row = banner.AddRow();
        var cell = row.Cells[0];
        cell.Shading.Color = Navy;
        row.TopPadding = Unit.FromPoint(14);
        row.BottomPadding = Unit.FromPoint(16);
        var kicker = cell.AddParagraph(options.English ? "PACE ATLAS  /  MEDICAL REPORT" : "PACE ATLAS  /  ARZTBERICHT");
        kicker.Format.Font.Size = 8;
        kicker.Format.Font.Bold = true;
        kicker.Format.Font.Color = Color.Parse("#BFE4E9");
        kicker.Format.LeftIndent = Unit.FromPoint(18);
        kicker.Format.RightIndent = Unit.FromPoint(14);
        kicker.Format.SpaceAfter = Unit.FromPoint(5);
        var title = cell.AddParagraph(heading);
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Colors.White;
        title.Format.LeftIndent = Unit.FromPoint(18);
        title.Format.RightIndent = Unit.FromPoint(14);
        var sub = section.AddParagraph($"{date(options.Start)} – {date(options.End)}  ·  " +
            (options.English ? "Created " : "Erstellt ") + date(DateTime.Today));
        sub.Format.Font.Color = Teal;
        sub.Format.Font.Bold = true;
        sub.Format.SpaceBefore = Unit.FromPoint(12);
        sub.Format.SpaceAfter = Unit.FromPoint(17);
    }

    private static void Heading(Section section, string heading)
    {
        var p = section.AddParagraph(heading);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 11;
        p.Format.Font.Color = Navy;
        p.Format.SpaceBefore = Unit.FromPoint(12);
        p.Format.SpaceAfter = Unit.FromPoint(8);
        p.Format.KeepWithNext = true;
        p.Format.Borders.Bottom.Width = Unit.FromPoint(.7);
        p.Format.Borders.Bottom.Color = Rule;
    }

    private static void Paragraph(Section section, string value)
    {
        var p = section.AddParagraph(value);
        p.Format.SpaceAfter = Unit.FromPoint(9);
        p.Format.LineSpacingRule = LineSpacingRule.AtLeast;
        p.Format.LineSpacing = Unit.FromPoint(13);
    }

    private static void Box(Section section, string value)
    {
        var box = section.AddTable();
        box.AddColumn(Unit.FromCentimeter(17.8));
        var row = box.AddRow();
        var cell = row.Cells[0];
        cell.Shading.Color = Light;
        row.TopPadding = Unit.FromPoint(9);
        row.BottomPadding = Unit.FromPoint(9);
        var p = cell.AddParagraph(value);
        p.Format.Font.Bold = true;
        p.Format.Font.Color = Teal;
        p.Format.LeftIndent = Unit.FromPoint(12);
        p.Format.RightIndent = Unit.FromPoint(12);
        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(5);
    }

    private static void MetricTiles(Section section, params (string Value, string Label)[] metrics)
    {
        var table = section.AddTable();
        foreach (var _ in metrics) table.AddColumn(Unit.FromCentimeter(17.8 / metrics.Length));
        var row = table.AddRow();
        row.Shading.Color = Light;
        row.TopPadding = Unit.FromPoint(12);
        row.BottomPadding = Unit.FromPoint(12);
        for (int i = 0; i < metrics.Length; i++)
        {
            var cell = row.Cells[i];
            var value = cell.AddParagraph(metrics[i].Value);
            value.Format.Font.Bold = true;
            value.Format.Font.Size = 17;
            value.Format.Font.Color = Teal;
            value.Format.LeftIndent = Unit.FromPoint(12);
            var label = cell.AddParagraph(metrics[i].Label);
            label.Format.Font.Size = 8;
            label.Format.Font.Color = Grey;
            label.Format.LeftIndent = Unit.FromPoint(12);
        }
        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(5);
    }

    private static void Table(Section section, string[] headers, IEnumerable<string[]> data, double[] widths)
    {
        var table = section.AddTable();
        table.Borders.Bottom.Color = Rule;
        table.Borders.Bottom.Width = Unit.FromPoint(.35);
        for (var i = 0; i < widths.Length; i++) table.AddColumn(Unit.FromCentimeter(widths[i]));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Light;
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Navy;
        header.TopPadding = Unit.FromPoint(6);
        header.BottomPadding = Unit.FromPoint(6);
        for (var i = 0; i < headers.Length; i++) header.Cells[i].AddParagraph(headers[i]);
        var rowIndex = 0;
        foreach (var values in data)
        {
            var row = table.AddRow();
            if (rowIndex++ % 2 == 1) row.Shading.Color = Pale;
            row.TopPadding = Unit.FromPoint(5);
            row.BottomPadding = Unit.FromPoint(5);
            for (var i = 0; i < widths.Length; i++)
            {
                row.Cells[i].AddParagraph(values[i]);
                if (values[i].Length > 0 && values[i].All(ch => ch == '■'))
                    row.Cells[i].Format.Font.Color = Teal;
            }
        }
        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static void WeeklyTable(Section section, MedicalReportOptions options,
        (Entry Entry, StateData Data)[] states, Func<DateTime, string> date)
    {
        string L(string de, string en) => options.English ? en : de;
        var rows = Enumerable.Range(0, Math.Min(8, (options.End - options.Start).Days / 7 + 1))
            .Select(index => new
            {
                Begin = options.Start.AddDays(index * 7),
                End = options.Start.AddDays(Math.Min((options.End - options.Start).Days, index * 7 + 6))
            }).Select(bucket => new[] { $"{date(bucket.Begin)} – {date(bucket.End)}",
                states.Count(s => s.Data.Pem == 2 && s.Entry.Start.Date >= bucket.Begin &&
                    s.Entry.Start.Date <= bucket.End).ToString(CultureInfo.InvariantCulture) });
        Table(section, [L("Zeitraum", "Period"), "PEM"], rows, [9.0, 6.0]);
        if ((options.End - options.Start).Days >= 56)
            Paragraph(section, L("Die Tabelle zeigt die ersten acht Wochen; die Gesamtzahl steht oben.",
                "The table shows the first eight weeks; the overall count appears above."));
    }

    private static void WeeklyCondition(Section section, MedicalReportOptions options,
        (Entry Entry, StateData Data)[] states, Func<double, string> number)
    {
        string L(string de, string en) => options.English ? en : de;
        var rows = states.GroupBy(s => (int)((s.Entry.Start.Date - options.Start).TotalDays / 7))
            .OrderBy(g => g.Key).Take(8).Select(g => new[] { $"{g.Key + 1}",
                number(g.Average(s => s.Data.Overall)) + " / 4", g.Count().ToString(CultureInfo.InvariantCulture) });
        Table(section, [L("Woche", "Week"), L("Ø Allgemein", "Avg. overall"),
            L("Erfassungen", "Entries")], rows, [4.0, 6.0, 5.0]);
        Paragraph(section, L("Höhere Werte bedeuten einen schlechteren dokumentierten Allgemeinzustand. Es sind Mittelwerte vorhandener Erfassungen, keine lückenlose Messung.",
            "Higher values indicate worse recorded overall condition. These are averages of available entries, not continuous measurements."));
    }
}
