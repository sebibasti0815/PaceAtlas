using System.Globalization;
using System.Text.Json;
using PaceAtlas;

namespace PaceAtlas.IPC;

/// <summary>Single owner for API operations. Never expose arbitrary SQL or Entry.Save to callers.</summary>
public sealed class PaceAtlasApi
{
    private readonly Store store;
    private readonly DataChangeNotifier? notifier;
    private readonly object gate = new();
    private readonly Func<string, object>? pacingCommand;
    public PaceAtlasApi(Store store, DataChangeNotifier? notifier = null, Func<string, object>? pacingCommand = null)
    {
        this.store = store;
        this.notifier = notifier;
        this.pacingCommand = pacingCommand;
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static T? Read<T>(JsonElement value) => value.Deserialize<T>(Json);
    private static string Required(string? s, string name) => !string.IsNullOrWhiteSpace(s) ? s : throw new ArgumentException($"Missing {name}");
    public object Execute(string method, JsonElement args)
    {
        lock (gate)
        {
            return method switch
            {
                "system.ping" => new { ok = true },
                "system.info" => new { name = "PaceAtlas", apiVersion = 1, transport = "named-pipe", version = typeof(Store).Assembly.GetName().Version?.ToString() },
                "pacing.command" => pacingCommand?.Invoke(Required((Read<PacingRequest>(args) ?? new()).Action, "action")) ?? throw new InvalidOperationException("Pacing controller unavailable"),
                "day.summary" => DaySummary(),
                "nutrition.foods.list" => NutritionFoods(),
                "food.search" => SearchNutritionFoods(Read<FoodSearchRequest>(args) ?? new()),
                "food.get" => GetNutritionFood(Read<FoodGetRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "nutrition.meals.list" => NutritionMeals(),
                "nutrition.meal.create" => CreateNutritionMeal(Read<NutritionMealRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "notes.create" => CreateQuickNote(Read<QuickNoteRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "quick.context" => QuickContext(),
                "rest.active.list" => ActiveRest(),
                "rest.start" => StartRest(Read<RestStartRequest>(args) ?? new RestStartRequest()),
                "rest.stop" => StopRest(Read<RestStopRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "activity.rest.transition" => TransitionToRest(Read<TransitionRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "history.list" => HistoryList(Read<HistoryListRequest>(args) ?? new HistoryListRequest()),
                "history.get" => HistoryGet(Read<HistoryGetRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "symptoms.trend" => SymptomTrend(Read<SymptomTrendRequest>(args) ?? new()),
                "medications.next" => NextMedication(),
                "pacing.load.summary" => PacingLoadSummary(),
                "protection.recent.list" => ProtectionRecent(),
                "activities.protection.add" => AddActivityProtection(Read<ActivityProtectionRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "activities.note.append" => AppendActivityNote(Read<ActivityNoteRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "status.get" => store.All().Where(e => e.Kind == "Zustand").Take(1).Select(e => new { e.Id, e.Start, e.Note, state = JsonSerializer.Deserialize<StateData>(e.Data) }).FirstOrDefault() ?? (object)new { },
                "status.options.list" => new { symptoms = store.ChoiceOptions("symptoms", new[] { "Erschöpfung", "Brain Fog", "Schmerzen", "Geräuschempfindlichkeit", "Ohrgeräusche", "Lichtempfindlichkeit", "Atemprobleme", "Schwindel/Kreislauf", "Herzrasen", "Zittern", "Kältegefühl/Schüttelfrost", "Angst/Unruhe", "Hyperarousal", "Nicht erholsamer Schlaf", "Sehkraft" }), painLocations = store.ChoiceOptions("pain_locations", new[] { "Finger", "Hände", "Unterarme", "Oberarme", "Kopf", "Nacken", "Rücken", "Beine", "Füße", "Sonstige" }) },
                "status.create" => CreateStatus(Read<StatusRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "activities.templates.list" => store.ActivityTemplates(),
                "activities.options.list" => new { dimensions = store.ChoiceOptions("activity_dimensions", new[] { "Körperlich", "Kognitiv", "Sozial", "Akustisch", "Visuell", "Emotional/Stress", "Fahrt/Transport" }), hearingProtection = store.ChoiceOptions("hearing_protection", new[] { "NC-Kopfhörer", "Loop", "Calmer" }) },
                "activities.active.list" => ActiveActivities(),
                "activities.custom.create" => CreateCustomActivity(Read<CustomActivityRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "activities.start" => StartActivity(Read<ActivityStartRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "activities.stop" => StopActivity(Read<ActivityStopRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "measures.recent.list" => RecentMeasures(),
                "measures.options.list" => new { measures = store.ChoiceOptions("measures", new[] { "Ibuprofen", "Naproxen", "Cannabis", "Benzodiazepin", "Sonstige" }), reasons = store.ChoiceOptions("symptoms", new[] { "Erschöpfung", "Brain Fog", "Schmerzen", "Geräuschempfindlichkeit", "Atemprobleme", "Schwindel/Kreislauf", "Angst/Unruhe" }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray() },
                "measures.create" => CreateMeasure(Read<MeasureRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "medications.pending.list" => Pending(),
                "medications.taken.list" => Taken(),
                "medications.undo" => Undo(Read<MedicationConfirmRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "medications.confirm" => Confirm(Read<MedicationConfirmRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                "medications.confirmDue" => ConfirmDue(Read<MedicationDueRequest>(args) ?? throw new ArgumentException("Missing parameters")),
                _ => throw new MissingMethodException(method)
            };
        }
    }
    // Nutrition commands use the same Store and rule priority as WinUI; no separate Raycast database.
    private object NutritionFoods()
    {
        var foods = store.Foods();
        var rules = store.FoodRules().Where(r => !string.IsNullOrWhiteSpace(r.Pattern)).ToArray();
        return foods.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).Select(f =>
        {
            var rule = rules.Where(r => f.Name.Contains(r.Pattern, StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(r => r.Priority).ThenByDescending(r => r.Pattern.Length).FirstOrDefault();
            return new { f.Id, f.Name, f.GlycemicLoadPer100G, f.CarbsPer100G, f.GlycemicIndex,
                decision = rule?.Decision, ruleNote = rule?.Note, f.Source };
        }).ToArray();
    }
    private object SearchNutritionFoods(FoodSearchRequest req)
    {
        var query = req.Query?.Trim() ?? "";
        if (query.Length < 2) return Array.Empty<object>();
        if (query.Length > 200) throw new ArgumentException("Search text too long");
        return store.SearchFoods(query, Math.Clamp(req.Limit, 1, 30))
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => new { f.Id, f.Name, f.GlycemicLoadPer100G }).ToArray();
    }
    private object GetNutritionFood(FoodGetRequest req)
    {
        var food = store.SearchFoods(null, 1, req.Id).FirstOrDefault()
            ?? throw new KeyNotFoundException("Food not found");
        var rule = store.FoodRules().Where(r => !string.IsNullOrWhiteSpace(r.Pattern) &&
                food.Name.Contains(r.Pattern, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(r => r.Priority).ThenByDescending(r => r.Pattern.Length).FirstOrDefault();
        return new { food.Id, food.Name, food.GlycemicLoadPer100G, food.CarbsPer100G,
            food.GlycemicIndex, decision = rule?.Decision, ruleNote = rule?.Note, food.Source };
    }
    public sealed class FoodSearchRequest { public string? Query { get; set; } public int Limit { get; set; } = 30; }
    public sealed class FoodGetRequest { public long Id { get; set; } }

    private object NutritionMeals() => store.Meals().Where(m => m.Status is "template" or "planned")
        .Select(m => new { m.Id, m.Name, m.Status, m.Note, m.Ingredients }).ToArray();
    private object CreateNutritionMeal(NutritionMealRequest req)
    {
        var name = Required(req.Name, "name").Trim();
        if (name.Length > 200 || req.Note?.Length > 10000) throw new ArgumentException("Text too long");
        if (req.Status is not ("consumed" or "planned")) throw new ArgumentException("Invalid meal status");
        var available = store.Foods().ToDictionary(f => f.Id);
        var ingredients = new List<MealIngredient>();
        foreach (var item in req.Ingredients ?? new())
        {
            if (!available.TryGetValue(item.FoodId, out var food)) throw new ArgumentException("Unknown food");
            if (!double.IsFinite(item.Grams) || item.Grams <= 0 || item.Grams > 10000) throw new ArgumentException("Invalid portion size");
            ingredients.Add(new MealIngredient { FoodId = food.Id, Name = food.Name, Grams = item.Grams,
                CarbsPer100G = food.CarbsPer100G, GlycemicLoadPer100G = food.GlycemicLoadPer100G, BlsCode = food.BlsCode, NutrientSource = food.Source });
        }
        if (ingredients.Count == 0) throw new ArgumentException("At least one ingredient required");
        var meal = new MealRecord { Name = name, At = DateTime.Now, Status = req.Status, Note = req.Note?.Trim() ?? "", Ingredients = ingredients };
        store.SaveMeal(meal);
        return new { saved = true, meal.Name, meal.At };
    }
    private object CreateQuickNote(QuickNoteRequest req)
    {
        var note = Required(req.Text, "text").Trim();
        if (note.Length > 10000) throw new ArgumentException("Note too long");
        var entry = new Entry { Kind = "Notiz", Start = DateTime.Now, Note = note, Data = "{}" };
        store.Save(entry);
        notifier?.Publish(DataChangeKind.Status);
        return new { saved = true, entry.Id, entry.Start };
    }
    // The WinUI language setting is shared with IPC clients. No UI dependency is needed.
    private static string HistoryLanguage()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PaceAtlas.WinUI", "language.txt");
            var language = File.ReadAllText(path).Trim();
            return language == "en" ? "en" : "de";
        }
        catch (IOException) { return "de"; }
        catch (UnauthorizedAccessException) { return "de"; }
    }
    private static string L(string de, string en, string language) => language == "en" ? en : de;
    private static string LocalKind(string kind, string language) => kind switch
    {
        "Zustand" => L("Zustand", "Condition", language),
        "Notiz" => L("Schnellnotiz", "Quick note", language),
        "Aktivität" => L("Aktivität", "Activity", language),
        "Ruhe" => L("Ruhe", "Rest", language),
        "Maßnahme" => L("Maßnahme", "Intervention", language),
        _ => kind
    };
    private static string Severity(int n, string lang) => n switch
    {
        0 => L("keine", "none", lang), 1 => L("leicht", "mild", lang),
        2 => L("mittel", "moderate", lang), 3 => L("stark", "severe", lang),
        4 => L("extrem", "extreme", lang), _ => L("nicht beurteilt", "not assessed", lang)
    };
    private static string IntensityLabel(int n, string lang) => n switch
    {
        0 => L("gering", "low", lang), 1 => L("mittel", "moderate", lang),
        2 => L("hoch", "high", lang), 3 => L("sehr hoch", "very high", lang),
        _ => L("nicht angegeben", "not specified", lang)
    };
    private static string LocalOption(string value, string lang)
    {
        if (lang != "en") return value;
        return value switch
        {
            "Körperlich" => "Physical", "Kognitiv" => "Cognitive", "Sozial" => "Social",
            "Akustisch" => "Auditory", "Visuell" => "Visual", "Emotional/Stress" => "Emotional/stress",
            "Fahrt/Transport" => "Travel/transport", "Erschöpfung" => "Fatigue",
            "Schmerzen" => "Pain", "Geräuschempfindlichkeit" => "Sound sensitivity",
            "Lichtempfindlichkeit" => "Light sensitivity", "Atemprobleme" => "Breathing difficulties",
            "Angst/Unruhe" => "Anxiety/restlessness", "Nicht erholsamer Schlaf" => "Unrefreshing sleep",
            "Hände" => "Hands", "Finger" => "Fingers", "Kopf" => "Head",
            "Beine" => "Legs", "Füße" => "Feet", "Rücken" => "Back",
            "Nacken" => "Neck", "Unterarme" => "Forearms", "Oberarme" => "Upper arms",
            "NC-Kopfhörer" => "Noise-cancelling headphones", _ => value
        };
    }
    private static object[] HistoryFields(Entry entry, string lang)
    {
        var fields = new List<object>();
        void Add(string de, string en, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) fields.Add(new { label = L(de, en, lang), value });
        }
        if (entry.Kind == "Zustand")
        {
            var d = JsonSerializer.Deserialize<StateData>(entry.Data);
            if (d is null) return fields.ToArray();
            var overall = new[] { L("gut", "good", lang), L("leicht eingeschränkt", "slightly impaired", lang),
                L("mittel", "moderate", lang), L("schlecht", "poor", lang), L("sehr schlecht", "very poor", lang) };
            Add("Allgemeinzustand", "Overall condition", d.Overall >= 0 && d.Overall < overall.Length ? overall[d.Overall] : null);
            Add("PEM", "PEM", d.Pem switch { 0 => L("nein", "no", lang), 1 => L("vermutet", "suspected", lang), 2 => L("erkannt", "confirmed", lang), _ => null });
            Add("Crash", "Crash", d.Crash ? L("Ja", "Yes", lang) : L("Nein", "No", lang));
            var symptoms = d.Symptoms.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                .Where(x => d.SymptomSeverity(x.Key) >= 0)
                .Select(x => $"{LocalOption(x.Key, lang)}: {Severity(d.SymptomSeverity(x.Key), lang)}").ToArray();
            Add("Symptome", "Symptoms", symptoms.Length == 0 ? L("Keine beurteilt", "None assessed", lang) : string.Join("\n", symptoms));
            Add("Am stärksten einschränkend", "Most limiting symptom", string.IsNullOrWhiteSpace(d.MostLimitingSymptom) ? null : LocalOption(d.MostLimitingSymptom, lang));
            Add("Schmerzorte", "Pain locations", d.PainLocations.Count == 0 ? null : string.Join(", ", d.PainLocations.Select(x => LocalOption(x, lang))));
        }
        else if (entry.Kind is "Aktivität" or "Ruhe")
        {
            var d = JsonSerializer.Deserialize<IntervalData>(entry.Data);
            if (d is null) return fields.ToArray();
            Add("Belastungsarten / Ruheformen", "Activity / rest types", string.Join(", ", d.Dimensions.Select(x => LocalOption(x, lang))));
            if (entry.Kind == "Aktivität") Add("Intensität", "Intensity", IntensityLabel(d.Intensity, lang));
            if (entry.Kind == "Ruhe") Add("Erholung", "Recovery", d.Recovery switch { 0 => L("nicht bewertet", "not rated", lang), 1 => L("keine", "none", lang), 2 => L("etwas", "some", lang), 3 => L("mittel", "moderate", lang), 4 => L("deutlich", "substantial", lang), _ => null });
            Add("Schutzmaßnahmen", "Protective measures", string.Join(", ", d.HearingProtection.Select(x => LocalOption(x, lang))));
            Add("Status", "Status", entry.End is null ? L("Läuft", "Ongoing", lang) : L("Abgeschlossen", "Completed", lang));
        }
        else if (entry.Kind == "Maßnahme")
        {
            var d = JsonSerializer.Deserialize<MeasureData>(entry.Data);
            if (d is null) return fields.ToArray();
            Add("Maßnahme", "Intervention", d.Name);
            Add("Gründe", "Reasons", string.Join(", ", d.ReasonSymptoms.Select(x => LocalOption(x, lang))));
            Add("Weitere Gründe", "Other reasons", d.ReasonOther);
            Add("Ziel", "Goal", d.Goal);
            Add("Ergebnis", "Outcome", d.Outcome);
            Add("Verlaufsnotiz", "Review note", d.ReviewNote);
        }
        return fields.ToArray();
    }
    // Read-only timeline: expose interpreted values, never raw storage schema.
    private object HistoryList(HistoryListRequest req)
    {
        if (req.Offset < 0 || req.Limit is < 1 or > 100) throw new ArgumentException("Invalid history pagination");
        var language = HistoryLanguage();
        var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Zustand", "Aktivität", "Ruhe", "Maßnahme", "Notiz" };
        var entries = store.All().Where(e => kinds.Contains(e.Kind)).ToList();
        var page = entries.Skip(req.Offset).Take(req.Limit).Select(e => new
        {
            e.Id, e.Kind, e.Start, e.End,
            title = HistoryTitle(e), summary = HistorySummary(e, language),
            hasNote = !string.IsNullOrWhiteSpace(e.Note)
        }).ToArray();
        return new { language, items = page, total = entries.Count,
            nextOffset = req.Offset + page.Length < entries.Count ? (int?)(req.Offset + page.Length) : null };
    }
    private object HistoryGet(HistoryGetRequest req)
    {
        var entry = store.All().FirstOrDefault(e => e.Id == req.Id &&
            (e.Kind is "Zustand" or "Aktivität" or "Ruhe" or "Maßnahme" or "Notiz"))
            ?? throw new ArgumentException("History entry not found");
        var lang = HistoryLanguage();
        object[] fields;
        try { fields = HistoryFields(entry, lang); }
        catch (JsonException) { fields = Array.Empty<object>(); }
        return new { entry.Id, entry.Kind, entry.Start, entry.End, language = lang,
            kindLabel = LocalKind(entry.Kind, lang), title = HistoryTitle(entry),
            summary = HistorySummary(entry, lang), note = entry.Note, fields };
    }

    private string HistoryTitle(Entry entry)
    {
        if (entry.Kind == "Notiz") return "Schnellnotiz";
        if (entry.Kind == "Zustand") return "Zustand";
        if (entry.Kind == "Ruhe") return "Ruhe";
        if (entry.Kind == "Maßnahme")
        {
            try { return JsonSerializer.Deserialize<MeasureData>(entry.Data)?.Name ?? "Maßnahme"; }
            catch (JsonException) { return "Maßnahme"; }
        }
        if (entry.Kind == "Aktivität")
        {
            try { return ResolveActivityName(entry, JsonSerializer.Deserialize<IntervalData>(entry.Data), store.ActivityTemplates()); }
            catch (JsonException) { return $"Aktivität {entry.Id}"; }
        }
        return entry.Kind;
    }

    private static string HistorySummary(Entry entry, string lang)
    {
        if (entry.Kind == "Notiz")
        {
            var note = (entry.Note ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return note.Length > 110 ? note[..109] + "…" : note;
        }
        try
        {
            if (entry.Kind == "Zustand")
            {
                var d = JsonSerializer.Deserialize<StateData>(entry.Data);
                if (d is null) return "";
                var overall = new[] { L("gut", "good", lang), L("leicht eingeschränkt", "slightly impaired", lang), L("mittel", "moderate", lang), L("schlecht", "poor", lang), L("sehr schlecht", "very poor", lang) };
                var label = d.Overall >= 0 && d.Overall < overall.Length ? overall[d.Overall] : L("unbekannt", "unknown", lang);
                var count = d.Symptoms.Count(x => d.SymptomSeverity(x.Key) > 0);
                return $"{label} · {(d.Crash ? "Crash · " : "")}{(d.Pem == 2 ? "PEM · " : d.Pem == 1 ? L("PEM vermutet · ", "PEM suspected · ", lang) : "")}{count} {L("Symptome", "symptoms", lang)}";
            }
            if (entry.Kind is "Aktivität" or "Ruhe")
            {
                var d = JsonSerializer.Deserialize<IntervalData>(entry.Data);
                var intensity = new[] { L("gering", "low", lang), L("mittel", "moderate", lang), L("hoch", "high", lang), L("sehr hoch", "very high", lang) };
                var label = d is not null && d.Intensity >= 0 && d.Intensity < intensity.Length ? intensity[d.Intensity] : "";
                var dimensions = d is null ? "" : string.Join(", ", d.Dimensions.Select(x => LocalOption(x, lang)));
                return string.Join(" · ", new[] { dimensions, label, entry.End is null ? L("läuft", "ongoing", lang) : L("abgeschlossen", "completed", lang) }.Where(x => !string.IsNullOrEmpty(x)));
            }
            if (entry.Kind == "Maßnahme")
            {
                var d = JsonSerializer.Deserialize<MeasureData>(entry.Data);
                return d is null ? "" : string.Join(", ", d.ReasonSymptoms.Select(x => LocalOption(x, lang)));
            }
        }
        catch (JsonException) { /* Legacy rows remain visible. */ }
        return "";
    }

    // Only recorded symptom values are considered. Missing/unassessed values are not zero.
    private object SymptomTrend(SymptomTrendRequest req)
    {
        var name = Required(req.Name, "name").Trim();
        if (name.Length > 200) throw new ArgumentException("Symptom name too long");
        var days = Math.Clamp(req.Days, 1, 90);
        var from = DateTime.Today.AddDays(1 - days);
        var points = new List<object>();
        foreach (var entry in store.All().Where(e => e.Kind == "Zustand" && e.Start >= from)
                     .OrderBy(e => e.Start))
        {
            try
            {
                var state = JsonSerializer.Deserialize<StateData>(entry.Data);
                if (state is null) continue;
                var severity = state.SymptomSeverity(name);
                if (severity < 0) continue;
                points.Add(new { entry.Id, entry.Start, severity, label = Severity(severity, HistoryLanguage()) });
            }
            catch (JsonException) { /* Skip malformed legacy records. */ }
        }
        return new { name, days, language = HistoryLanguage(), points };
    }
    public sealed class SymptomTrendRequest { public string? Name { get; set; } public int Days { get; set; } = 14; }

    private object NextMedication()
    {
        var now = DateTime.Now;
        var day = DateOnly.FromDateTime(now);
        // Use the same pending logic as the medication confirmation screen.
        // This preserves the scheduled-time matching semantics for multiple intakes.
        var pending = store.MedicationPlans().Where(p => p.IsActiveOn(day)).ToArray();
        var taken = store.All().Where(e => e.Kind == "Einnahme" && e.Start.Date == now.Date)
            .Select(e => (entry: e, data: TryIntake(e.Data)))
            .Where(x => x.data?.Status is "taken" or "Taken" or "Genommen").ToArray();
        var due = pending.Where(p => !taken.Any(x => x.data!.PlanId == p.Id && x.entry.Start.ToString("HH:mm") == p.Time))
            .Select(p => new { plan = p, time = TimeSpan.TryParseExact(p.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? (TimeSpan?)t : null })
            .Where(x => x.time is not null).OrderBy(x => x.time).ThenBy(x => x.plan.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return new { language = HistoryLanguage(),
            next = due.Select(x => new { x.plan.Id, x.plan.Name, x.plan.Time, x.plan.Dose, x.plan.Quantity, x.plan.Form, overdue = x.time < now.TimeOfDay }).ToArray(),
            pending = due.Length, overdue = due.Count(x => x.time < now.TimeOfDay) };
    }

    // Descriptive exposure summary only. No claim of clinical PEM prediction.
    private object PacingLoadSummary()
    {
        var since = DateTime.Now.AddHours(-48);
        var activities = store.All().Where(e => e.Kind == "Aktivität" && e.Start >= since)
            .OrderByDescending(e => e.Start).Take(100).Select(e => {
                IntervalData? data = null;
                try { data = JsonSerializer.Deserialize<IntervalData>(e.Data); } catch (JsonException) { }
                var end = e.End ?? DateTime.Now;
                var minutes = Math.Max(0, (end - e.Start).TotalMinutes);
                return new { e.Id, e.Start, end = e.End, minutes = Math.Round(minutes, 0), intensity = data?.Intensity,
                    dimensions = data?.Dimensions ?? new List<string>(), protection = data?.HearingProtection ?? new List<string>() };
            }).ToArray();
        return new { language = HistoryLanguage(), windowHours = 48, count = activities.Length,
            totalMinutes = Math.Round(activities.Sum(x => x.minutes)), activities,
            disclaimer = "Deskriptive Belastungsübersicht, keine medizinische PEM-Risikoprognose." };
    }

    private object ProtectionRecent()
    {
        var since = DateTime.Now.AddDays(-7);
        var items = store.All().Where(e => (e.Kind == "Aktivität" || e.Kind == "Ruhe") && e.Start >= since)
            .OrderByDescending(e => e.Start).Take(100).Select(e => {
                List<string> measures = new();
                try {
                    if (e.Kind == "Aktivität") measures = JsonSerializer.Deserialize<IntervalData>(e.Data)?.HearingProtection ?? new();
                    else measures = JsonSerializer.Deserialize<SleepData>(e.Data)?.HearingProtection ?? new();
                } catch (JsonException) { }
                return new { e.Id, e.Kind, e.Start, protection = measures };
            }).Where(x => x.protection.Count > 0).ToArray();
        return new { language = HistoryLanguage(), items };
    }

    // Add protection to a running activity without replacing existing measures.
    private object AddActivityProtection(ActivityProtectionRequest req)
    {
        var allowed = store.ChoiceOptions("hearing_protection", new[] { "NC-Kopfhörer", "Loop", "Calmer" });
        var requested = req.Protection ?? new List<string>();
        if (requested.Count == 0 || requested.Count > 20 || requested.Any(p => string.IsNullOrWhiteSpace(p) ||
            !allowed.Contains(p, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("Invalid protection selection");
        var entry = store.All().FirstOrDefault(e => e.Id == req.Id && e.Kind == "Aktivität" && e.End is null)
            ?? throw new ArgumentException("Running activity not found");
        var data = JsonSerializer.Deserialize<IntervalData>(entry.Data) ?? throw new ArgumentException("Invalid activity data");
        data.HearingProtection ??= new List<string>();
        foreach (var protection in requested)
            if (!data.HearingProtection.Contains(protection, StringComparer.OrdinalIgnoreCase))
                data.HearingProtection.Add(protection);
        entry.Data = JsonSerializer.Serialize(data);
        store.Save(entry);
        notifier?.Publish(DataChangeKind.Activities);
        return new { saved = true, entry.Id, protection = data.HearingProtection };
    }
    public sealed class ActivityProtectionRequest
    {
        public long Id { get; set; }
        public List<string>? Protection { get; set; }
    }

    private object AppendActivityNote(ActivityNoteRequest req)
    {
        var note = Required(req.Note, "note").Trim();
        if (note.Length > 5000) throw new ArgumentException("Note too long");
        var entry = store.All().FirstOrDefault(e => e.Id == req.Id && e.Kind == "Aktivität" && e.End is null)
            ?? throw new ArgumentException("Running activity not found");
        entry.Note = string.IsNullOrWhiteSpace(entry.Note) ? note : entry.Note.TrimEnd() + Environment.NewLine + note;
        store.Save(entry);
        notifier?.Publish(DataChangeKind.Activities);
        return new { saved = true, entry.Id };
    }
    public sealed class ActivityNoteRequest { public long Id { get; set; } public string? Note { get; set; } }

    private object CreateStatus(StatusRequest req)
    {
        if (req.CaptureMode is not ("Full" or "Quick")) throw new ArgumentException("Unknown capture mode");
        if (req.CaptureMode == "Quick" && (req.Symptoms?.Any(kv => kv.Value != -1) == true || req.Pulse.HasValue))
            throw new ArgumentException("Quick pain capture must leave symptoms unassessed and pulse unset");
        if (req.Overall is < 0 or > 4 || req.Pem is < 0 or > 2) throw new ArgumentException("Invalid overall/PEM");
        if (req.Symptoms?.Any(kv => kv.Value is < -1 or > 4) == true) throw new ArgumentException("Invalid symptom severity");
        var allowedSymptoms = store.ChoiceOptions("symptoms", new[] { "Erschöpfung", "Brain Fog", "Schmerzen", "Geräuschempfindlichkeit", "Ohrgeräusche", "Lichtempfindlichkeit", "Atemprobleme", "Schwindel/Kreislauf", "Herzrasen", "Zittern", "Kältegefühl/Schüttelfrost", "Angst/Unruhe", "Hyperarousal", "Nicht erholsamer Schlaf", "Sehkraft" });
        var allowedPain = store.ChoiceOptions("pain_locations", new[] { "Finger", "Hände", "Unterarme", "Oberarme", "Kopf", "Nacken", "Rücken", "Beine", "Füße", "Sonstige" });
        if (req.Symptoms?.Keys.Any(x => !allowedSymptoms.Contains(x)) == true || req.PainLocations?.Any(x => !allowedPain.Contains(x)) == true)
            throw new ArgumentException("Unknown symptom or pain location");
        if (!string.IsNullOrWhiteSpace(req.MostLimitingSymptom) && (req.Symptoms is null || !req.Symptoms.TryGetValue(req.MostLimitingSymptom, out var severity) || severity <= 0))
            throw new ArgumentException("Most limiting symptom must be present and rated above zero");
        var entry = new Entry { Kind = "Zustand", CaptureSource = "Raycast", CaptureMode = req.CaptureMode, Start = DateTime.Now, Note = req.Note ?? "", Data = JsonSerializer.Serialize(new StateData { Overall = req.Overall, Pem = req.Pem, Crash = req.Crash, SymptomValuesVersion = 2, Symptoms = req.Symptoms ?? new(), Pulse = req.Pulse, PainLocations = req.PainLocations ?? new(), MostLimitingSymptom = string.IsNullOrWhiteSpace(req.MostLimitingSymptom) ? null : req.MostLimitingSymptom }) };
        store.Save(entry); notifier?.Publish(DataChangeKind.Status); return new { saved = true, entry.Start };
    }
    // WinUI currently persists the dimensions/intensity/protection but not the selected
    // template name. Only identify a template if the saved values match uniquely.
    // Otherwise describe the activity rather than inventing a potentially wrong name.
    private object ActiveActivities()
    {
        var templates = store.ActivityTemplates();
        return store.All().Where(e => e.Kind == "Aktivität" && e.End is null)
            .Select(e =>
            {
                IntervalData? data = null;
                try { data = JsonSerializer.Deserialize<IntervalData>(e.Data); }
                catch (JsonException) { /* Legacy or malformed row: keep it visible. */ }
                var name = ResolveActivityName(e, data, templates);
                return new { e.Id, e.Start, e.Note, displayNote = ActivityDisplayNote(e.Note, name), name, data };
            }).ToArray();
    }

    private static string ResolveActivityName(Entry entry, IntervalData? data, List<ActivityTemplate> templates)
    {
        var note = entry.Note?.Trim() ?? "";
        var firstLine = note.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0].Trim();
        // Raycast's template-start path explicitly prepends the template name.
        var explicitTemplate = templates.FirstOrDefault(t =>
            string.Equals(t.Name, firstLine, StringComparison.OrdinalIgnoreCase));
        if (explicitTemplate is not null) return explicitTemplate.Name;

        if (data is not null)
        {
            static bool SameSet(IEnumerable<string> a, IEnumerable<string> b) =>
                a.ToHashSet(StringComparer.OrdinalIgnoreCase)
                 .SetEquals(b);
            var matches = templates.Where(t => t.Intensity == data.Intensity
                && SameSet(t.Dimensions, data.Dimensions)
                && SameSet(t.HearingProtection, data.HearingProtection)).Take(2).ToArray();
            if (matches.Length == 1) return matches[0].Name;
            if (data.Dimensions.Count > 0) return string.Join(", ", data.Dimensions);
        }
        return $"Aktivität {entry.Id}";
    }

    // The Raycast creation path stores the activity title as the first note line.
    // Do not display that title twice, but preserve genuine WinUI notes verbatim.
    private static string ActivityDisplayNote(string? note, string name)
    {
        if (string.IsNullOrWhiteSpace(note)) return "";
        var lines = note.Trim().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length > 0 && string.Equals(lines[0].Trim(), name, StringComparison.OrdinalIgnoreCase))
            return string.Join("\n", lines.Skip(1)).Trim();
        return note.Trim();
    }

    private static string JoinActivityNote(string name, string? note) => string.IsNullOrWhiteSpace(note) ? name : name + Environment.NewLine + note.Trim();
    private object StartActivity(ActivityStartRequest req)
    {
        var name = Required(req.Template, "template");
        var template = store.ActivityTemplates().FirstOrDefault(t => t.Name == name) ?? throw new ArgumentException("Unknown activity template");
        var entry = new Entry { Kind = "Aktivität", CaptureSource = "Raycast", CaptureMode = "Quick", Start = DateTime.Now, Note = JoinActivityNote(name, req.Note), Data = JsonSerializer.Serialize(new IntervalData { Dimensions = template.Dimensions, Intensity = template.Intensity, HearingProtection = template.HearingProtection }) };
        store.Save(entry); notifier?.Publish(DataChangeKind.Activities); return new { started = true, entry.Start };
    }
    private object CreateCustomActivity(CustomActivityRequest req)
    {
        var name = Required(req.Name, "name").Trim();
        if (name.Length > 200) throw new ArgumentException("Activity name too long");
        if (req.Intensity is < 0 or > 3) throw new ArgumentException("Invalid intensity");
        if (!DateTime.TryParse(req.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("Invalid activity start time");
        if (start > DateTime.Now.AddMinutes(1)) throw new ArgumentException("Activity cannot start in the future");
        DateTime? end = null;
        if (req.Completed)
        {
            if (!DateTime.TryParse(req.End, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
                throw new ArgumentException("Invalid activity end time");
            if (parsedEnd < start || parsedEnd > DateTime.Now.AddMinutes(1))
                throw new ArgumentException("End must be after start and cannot be in the future");
            end = parsedEnd;
        }
        var allowedDimensions = store.ChoiceOptions("activity_dimensions", new[] { "Körperlich", "Kognitiv", "Sozial", "Akustisch", "Visuell", "Emotional/Stress", "Fahrt/Transport" });
        var allowedProtection = store.ChoiceOptions("hearing_protection", new[] { "NC-Kopfhörer", "Loop", "Calmer" });
        if (req.Dimensions?.Any(x => !allowedDimensions.Contains(x)) == true || req.HearingProtection?.Any(x => !allowedProtection.Contains(x)) == true)
            throw new ArgumentException("Unknown activity dimension or protection");
        var entry = new Entry { Kind = "Aktivität", CaptureSource = "Raycast", CaptureMode = "Quick", Start = start, End = end, Note = JoinActivityNote(name, req.Note),
            Data = JsonSerializer.Serialize(new IntervalData { Intensity = req.Intensity,
                Dimensions = req.Dimensions?.Distinct().ToList() ?? new(),
                HearingProtection = req.HearingProtection?.Distinct().ToList() ?? new() }) };
        store.Save(entry); notifier?.Publish(DataChangeKind.Activities);
        return new { saved = true, entry.Start, entry.End };
    }
    private object StopActivity(ActivityStopRequest req)
    {
        var entry = store.All().FirstOrDefault(e => e.Id == req.Id && e.Kind == "Aktivität") ?? throw new ArgumentException("Unknown activity");
        if (entry.End is not null) return new { stopped = true, alreadyStopped = true };
        var end = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(req.End))
        {
            if (!DateTime.TryParseExact(req.End, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
                throw new ArgumentException("Invalid end time. Expected yyyy-MM-ddTHH:mm");
        }
        if (end < entry.Start) throw new ArgumentException("End must not precede activity start");
        if (end > DateTime.Now.AddMinutes(1)) throw new ArgumentException("End cannot be in the future");
        entry.End = end; if (!string.IsNullOrWhiteSpace(req.Note)) entry.Note = string.IsNullOrWhiteSpace(entry.Note) ? req.Note.Trim() : entry.Note.TrimEnd() + Environment.NewLine + req.Note.Trim(); store.Save(entry); notifier?.Publish(DataChangeKind.Activities); return new { stopped = true, entry.End };
    }
    private object RecentMeasures()
    {
        return store.All().Where(e => e.Kind == "Maßnahme").Take(30).Select(e =>
        {
            MeasureData? data;
            try { data = JsonSerializer.Deserialize<MeasureData>(e.Data); }
            catch (JsonException) { data = null; }
            return new { e.Id, name = data?.Name ?? "", note = e.Note,
                reasons = data?.ReasonSymptoms ?? new List<string>(), e.Start };
        }).Where(e => !string.IsNullOrWhiteSpace(e.name)).ToArray();
    }

    private object CreateMeasure(MeasureRequest req)
    {
        var name = Required(req.Name, "name").Trim();
        var measures = store.ChoiceOptions("measures", new[] { "Ibuprofen", "Naproxen", "Cannabis", "Benzodiazepin", "Sonstige" });
        var reasons = store.ChoiceOptions("symptoms", new[] { "Erschöpfung", "Brain Fog", "Schmerzen", "Geräuschempfindlichkeit", "Atemprobleme", "Schwindel/Kreislauf", "Angst/Unruhe" }).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!measures.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Unknown measure");
        if (req.Reasons?.Any(x => !reasons.Contains(x)) == true) throw new ArgumentException("Unknown reason");
        if (req.Note?.Length > 10000) throw new ArgumentException("Note too long");
        var entry = new Entry { Kind = "Maßnahme", Start = DateTime.Now, Note = req.Note ?? "",
            Data = JsonSerializer.Serialize(new MeasureData { Name = name, Dose = "", TrackProgress = false,
                ReasonSymptoms = req.Reasons?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new(), Status = "active" }) };
        store.Save(entry);
        notifier?.Publish(DataChangeKind.Measures);
        return new { saved = true, entry.Id, entry.Start };
    }
    private object Pending()
    {
        var day = DateOnly.FromDateTime(DateTime.Today);
        var taken = store.All().Where(e => e.Kind == "Einnahme" && DateOnly.FromDateTime(e.Start) == day).Select(e => (e, data: TryIntake(e.Data))).Where(x => x.data is not null).ToList();
        return store.MedicationPlans().Where(p => p.IsActiveOn(day) && !taken.Any(x => x.data!.PlanId == p.Id && x.e.Start.ToString("HH:mm") == p.Time))
            .Select(p => new { p.Id, p.Name, p.Time, p.Dose, p.Quantity, p.Form }).ToArray();
    }
    private object Taken()
    {
        var day = DateTime.Today;
        return store.All().Where(e => e.Kind == "Einnahme" && e.Start.Date == day)
            .Select(e => (entry: e, data: TryIntake(e.Data)))
            .Where(x => x.data?.Status is "taken" or "Taken" or "Genommen")
            .Select(x => new { id = x.entry.Id, planId = x.data!.PlanId, name = x.data.Name,
                time = x.entry.Start.ToString("HH:mm"), dose = x.data.ActualDose,
                quantity = x.data.ActualQuantity, form = x.data.Form }).ToArray();
    }
    private object Undo(MedicationConfirmRequest req)
    {
        // Undo an individual intake entry, never an entire medication plan.
        var entry = store.All().FirstOrDefault(e => e.Id == req.IntakeId && e.Kind == "Einnahme"
            && e.Start.Date == DateTime.Today) ?? throw new ArgumentException("Unknown intake for today");
        if (TryIntake(entry.Data)?.Status is not ("taken" or "Taken" or "Genommen"))
            throw new InvalidOperationException("Intake is not marked taken");
        store.Delete(entry.Id); // Rebuilds stock movements in the same SQLite transaction.
        notifier?.Publish(DataChangeKind.Medications);
        return new { undone = true };
    }
    private static IntakeData? TryIntake(string json) { try { return JsonSerializer.Deserialize<IntakeData>(json); } catch (JsonException) { return null; } }
    private object Confirm(MedicationConfirmRequest req)
    {
        var plan = store.MedicationPlans().FirstOrDefault(p => p.Id == req.PlanId && p.IsActiveOn(DateOnly.FromDateTime(DateTime.Today))) ?? throw new ArgumentException("Unknown or inactive medication plan");
        if (!TimeSpan.TryParseExact(plan.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var time)) throw new ArgumentException("Invalid plan time");
        var day = DateTime.Today;
        var previous = store.All().Where(e => e.Kind == "Einnahme" && e.Start.Date == day).ToList();
        var existing = previous.FirstOrDefault(e => TryIntake(e.Data)?.PlanId == plan.Id && e.Start.TimeOfDay == time);
        if (existing is not null)
        {
            var status = TryIntake(existing.Data)?.Status;
            if (status == "taken") return new { confirmed = true, alreadyConfirmed = true };
            throw new InvalidOperationException("An intake already exists with another status; edit it in PaceAtlas.");
        }
        var data = new IntakeData { PlanId = plan.Id, ProductId = plan.ProductId, Name = plan.Name, PlannedDose = plan.Dose, Quantity = plan.Quantity, Form = plan.Form, Goals = plan.Goals, ActualDose = plan.Dose, ActualQuantity = plan.Quantity, Status = "taken" };
        if (!decimal.TryParse(plan.Quantity, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var quantity) || quantity <= 0 || string.IsNullOrWhiteSpace(plan.Dose)) throw new ArgumentException("Plan has no valid dose/quantity; confirm in PaceAtlas.");
        var entry = new Entry { Kind = "Einnahme", Start = day.Add(time), Data = JsonSerializer.Serialize(data) };
        store.SaveIntakes(day, Array.Empty<Entry>(), new[] { entry });
        notifier?.Publish(DataChangeKind.Medications);
        return new { confirmed = true, alreadyConfirmed = false };
    }
    private object ConfirmDue(MedicationDueRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Time)) throw new ArgumentException("Time required (HH:mm)");
        var ids = store.MedicationPlans().Where(p => p.Time == req.Time && p.IsActiveOn(DateOnly.FromDateTime(DateTime.Today))).Select(p => p.Id).ToArray();
        var count = 0;
        foreach (var id in ids) { Confirm(new MedicationConfirmRequest { PlanId = id }); count++; }
        return new { confirmed = count };
    }

    private object ActiveRest() => store.All().Where(e => e.Kind == "Ruhe" && e.End is null)
        .Select(e => new { e.Id, e.Start, e.Note }).ToArray();
    private object StartRest(RestStartRequest req)
    {
        var start = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(req.Start) && !DateTime.TryParseExact(req.Start, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            throw new ArgumentException("Invalid start time");
        if (start > DateTime.Now.AddMinutes(1)) throw new ArgumentException("Start cannot be in the future");
        var entry = new Entry { Kind = "Ruhe", Start = start, Note = req.Note?.Trim() ?? "",
            Data = JsonSerializer.Serialize(new IntervalData { Dimensions = new List<string> { "Ruhe" }, Intensity = 0 }) };
        store.Save(entry); notifier?.Publish(DataChangeKind.Activities);
        return new { saved = true, entry.Id, entry.Start };
    }
    private object StopRest(RestStopRequest req)
    {
        var entry = store.All().FirstOrDefault(e => e.Id == req.Id && e.Kind == "Ruhe") ?? throw new ArgumentException("Unknown rest entry");
        if (entry.End is not null) return new { stopped = true, alreadyStopped = true };
        var end = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(req.End) && !DateTime.TryParseExact(req.End, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
            throw new ArgumentException("Invalid end time");
        if (end < entry.Start || end > DateTime.Now.AddMinutes(1)) throw new ArgumentException("Invalid end time");
        entry.End = end;
        if (!string.IsNullOrWhiteSpace(req.Note)) entry.Note = string.IsNullOrWhiteSpace(entry.Note) ? req.Note.Trim() : entry.Note.TrimEnd() + Environment.NewLine + req.Note.Trim();
        store.Save(entry); notifier?.Publish(DataChangeKind.Activities);
        return new { stopped = true, entry.End };
    }
    private object TransitionToRest(TransitionRequest req)
    {
        var activity = store.All().FirstOrDefault(e => e.Id == req.ActivityId && e.Kind == "Aktivität" && e.End is null)
            ?? throw new ArgumentException("Active activity not found");
        // Both entries use one transition timestamp; rollback is not guaranteed by Store.Save.
        var now = DateTime.Now;
        activity.End = now;
        store.Save(activity);
        var rest = new Entry { Kind = "Ruhe", Start = now, Note = req.Note?.Trim() ?? "",
            Data = JsonSerializer.Serialize(new IntervalData { Dimensions = new List<string> { "Ruhe" }, Intensity = 0 }) };
        store.Save(rest); notifier?.Publish(DataChangeKind.Activities);
        return new { transitioned = true, activityId = activity.Id, restId = rest.Id, time = now };
    }
    private object DaySummary()
    {
        var today = DateTime.Today;
        var entries = store.All().Where(e => e.Start.Date == today).ToArray();
        var activities = entries.Where(e => e.Kind == "Aktivität").ToArray();
        var rests = entries.Where(e => e.Kind == "Ruhe").ToArray();
        var now = DateTime.Now;
        static double Minutes(Entry e, DateTime now) => Math.Max(0, ((e.End ?? now) - e.Start).TotalMinutes);
        var state = entries.FirstOrDefault(e => e.Kind == "Zustand");
        StateData? condition = null;
        if (state is not null) { try { condition = JsonSerializer.Deserialize<StateData>(state.Data); } catch (JsonException) { } }
        return new { language = HistoryLanguage(), date = today, stateRecorded = state is not null,
            overall = condition?.Overall, pem = condition?.Pem, crash = condition?.Crash,
            activities = activities.Length, activityMinutes = (int)Math.Round(activities.Sum(e => Minutes(e, now))),
            restEntries = rests.Length, restMinutes = (int)Math.Round(rests.Sum(e => Minutes(e, now))),
            activeActivities = activities.Count(e => e.End is null), activeRest = rests.Count(e => e.End is null),
            pendingMedications = ((System.Collections.IEnumerable)Pending()).Cast<object>().Count() };
    }
    private object QuickContext()
    {
        var entries = store.All();
        return new { activities = ActiveActivities(), rests = ActiveRest(), pendingMedications = ((System.Collections.IEnumerable)Pending()).Cast<object>().Count(),
            lastEntries = entries.Take(5).Select(e => new { e.Id, e.Kind, e.Start, title = HistoryTitle(e) }).ToArray() };
    }
    public sealed class QuickNoteRequest { public string? Text { get; set; } }
    public sealed class NutritionIngredientRequest { public long FoodId { get; set; } public double Grams { get; set; } }
    public sealed class NutritionMealRequest { public string? Name { get; set; } public string Status { get; set; } = "consumed"; public string? Note { get; set; } public List<NutritionIngredientRequest>? Ingredients { get; set; } }
    public sealed class PacingRequest { public string? Action { get; set; } }
    public sealed class RestStartRequest { public string? Start { get; set; } public string? Note { get; set; } }
    public sealed class RestStopRequest { public long Id { get; set; } public string? End { get; set; } public string? Note { get; set; } }
    public sealed class TransitionRequest { public long ActivityId { get; set; } public string? Note { get; set; } }

    public sealed class HistoryListRequest { public int Offset { get; set; } public int Limit { get; set; } = 30; }
    public sealed class HistoryGetRequest { public long Id { get; set; } }
    public sealed class MeasureRequest { public string? Name { get; set; } public List<string>? Reasons { get; set; } public string? Note { get; set; } }
    public sealed class StatusRequest { public string CaptureMode { get; set; } = "Full"; public int Overall { get; set; } = 2; public int Pem { get; set; } public bool Crash { get; set; } public Dictionary<string,int>? Symptoms { get; set; } public List<string>? PainLocations { get; set; } public int? Pulse { get; set; } public string? MostLimitingSymptom { get; set; } public string? Note { get; set; } }
    public sealed class CustomActivityRequest { public string? Name { get; set; } public int Intensity { get; set; } = 2; public bool Completed { get; set; } public string? Start { get; set; } public string? End { get; set; } public List<string>? Dimensions { get; set; } public List<string>? HearingProtection { get; set; } public string? Note { get; set; } }
    public sealed class ActivityStartRequest { public string? Template { get; set; } public string? Note { get; set; } }
    public sealed class ActivityStopRequest { public long Id { get; set; } public string? Note { get; set; } public string? End { get; set; } }
    public sealed class MedicationConfirmRequest { public long PlanId { get; set; } public long IntakeId { get; set; } }
    public sealed class MedicationDueRequest { public string? Time { get; set; } }
}
