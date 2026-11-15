# RAYCAST – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="raycast-0-22-7-3-notes"></a>

## Version 0.22.7.3 – NOTES

# PaceAtlas 0.22.7.3 – Schnellnotizen im Raycast-Verlauf

- Schnellnotizen erhalten ein Stift-Icon im gleichen Stil wie andere Verlaufstypen.
- Anzeigename „Schnellnotiz“ (Englisch: „Quick note“).
- Notiztext als gekürzte Vorschau, maximal 110 Zeichen, Zeilenumbrüche entfernt.
- Kein leerer Trennpunkt bei fehlendem Vorschautext.
- Schnellnotizen sind über den Eintragstypfilter erreichbar.
- Bestehende Daten und Synchronisationsschema unverändert.

Windows-/Raycast-Laufzeittest steht aus.

<a id="raycast-0-22-7-1-buildfix"></a>

## Version 0.22.7.1 – BUILDFIX

# PaceAtlas 0.22.7.1 – Raycast Buildfix

- Entfernt UTF-8-BOM aus `PaceAtlas.Raycast/package.json`.
- Synchronisiert `package.json` mit der zentralen Version `0.22.7.1`.
- `installer/build-installer.ps1` prüft JSON-Kodierung, Syntax und Versionsgleichheit vor dem Build.
- Keine Änderungen an der Datenbank oder Synchronisationslogik.
- Windows-Build und Raycast-Laufzeittest sind noch ausstehend.

<a id="raycast-0-21-9-1-changes"></a>

## Version 0.21.9.1 – CHANGES

# 0.21.9.1 – Verlauf

- Automatisches Nachladen bei Auswahl nahe dem Ende (5 Einträge).
- Manueller Ladeeintrag mit Enter oder Klick; Auswahl bleibt am Seitenübergang.
- Schutz vor parallelen Abfragen und wiederholtem Auto-Nachladen derselben Seite.
- Bestehende API-Paginierung mit 30 Einträgen bleibt erhalten.

<a id="raycast-0-21-8-changes"></a>

## Version 0.21.8 – CHANGES

# PaceAtlas 0.21.8 – Strukturierte Schmerz-Schnellerfassung

- Schmerz-Schnellerfassung speichert reguläre Zustandseinträge über `status.create`, nicht als Freitext-Schnellnotiz.
- Allgemeinzustand, PEM, Crash, Schmerzorte (Mehrfachauswahl), Notizen; kein Puls und keine Symptombewertung. Symptome fehlen im Dictionary und werden durch `SymptomSeverity` als `-1` (nicht beurteilt) interpretiert.
- Eintragsmetadaten als separate SQLite-Spalten `capture_source` und `capture_mode`, idempotente Migration bestehender Einträge auf `WinUI`/`Full` (bewusste historische Konvention).
- Neue Raycast-Zustände: `Raycast`/`Full` oder `Raycast`/`Quick`; neue Raycast-Aktivitäten: `Raycast`/`Quick`.
- Die bestehenden `Entry`-Lese-/Speicherwege berücksichtigen die neuen Spalten, auch bei Änderungen.
- Noch offen: Metadaten für alle übrigen IPC-Schreibwege und eine spätere geräteübergreifende Synchronisierung; kein Sync-Schema eingeführt.
- Die derzeitige Schmerz-Datenstruktur speichert Schmerzorte ohne individuelle Schmerzstärken. Hier werden **keine** neuen Stärke-Werte erfunden.
- Nicht unter Windows gebaut oder ausgeführt. Vor Installation Datenbank sichern.

<a id="raycast-0-21-7-changes"></a>

## Version 0.21.7 – CHANGES

# 0.21.7

- Schnellaktionen: zwei feste Einträge, danach alphabetisch nach sichtbarem Titel.
- Schmerz-Schnellerfassung: speichert ausdrücklich eine Schmerzbeobachtung als Schnellnotiz; noch keine strukturierte Schmerzbewertung.
- PEM-Belastungshinweise: zeigt dokumentierte Belastungen der letzten 48 Stunden, ausdrücklich keine Risikoprognose.
- Keine Änderung an Datenbankschema oder bestehenden IPC-Methoden.
- Windows- und Raycast-Build noch nicht ausgeführt.

<a id="raycast-0-21-6-changes"></a>

## Version 0.21.6 – CHANGES

# PaceAtlas 0.21.6 – Schnellaktionen und Wiederholen

- „Zustand erfassen“ und „Aktivität erfassen“ fest als erste zwei Schnellaktionen angepinnt.
- „Aktivität erfassen“ öffnet unmittelbar das bestehende Formular mit Vorlagenwahl.
- Weitere Schnellaktionen bleiben alphabetisch sortiert; kontextabhängige Bereiche folgen darunter.
- Neue Schnellaktion „Letzte Maßnahme wiederholen“: letzte 30 Maßnahmen auswählen, vor erneutem Speichern Notiz überprüfen, neuer Eintrag mit aktuellem Zeitpunkt.
- Keine automatische Wiederholung von Medikamenten oder Zuständen; keine stillen Schreiboperationen.
- Windows- und Raycast-Build müssen auf dem Zielsystem geprüft werden.

<a id="raycast-0-21-5-changes"></a>

## Version 0.21.5 – CHANGES

# PaceAtlas 0.21.5 – Schnellaktionen und Schutzmaßnahmen

- Schnellaktionen alphabetisch nach sichtbarem Titel sortiert. Kontextbereiche bleiben gesondert.
- Neue Aktion „Schutzmaßnahmen ergänzen“ für laufende Aktivitäten.
- Schutzmaßnahmen werden über `activities.protection.add` zur bestehenden Auswahl hinzugefügt, ohne vorhandene Werte zu löschen.
- Eingaben werden serverseitig gegen die in PaceAtlas konfigurierten Schutzmaßnahmen validiert.
- Fehler in `package.json` korrigiert: `pacing-load` und `protection-recent` waren versehentlich unter `preferences` statt `commands` eingetragen.
- Versionsnummern synchronisiert.

**Nicht enthalten:** eigenständige Schutzmaßnahmen ohne Aktivitätsbezug, PEM-Risikoprognose, Wiederholung alter Einträge.

**Tests:** JSON-Schema und Quellstruktur geprüft; .NET-/Raycast-Windows-Build und Laufzeittest ausstehend.

<a id="raycast-0-21-4-changes"></a>

## Version 0.21.4 – CHANGES

# PaceAtlas 0.21.4

- Offene Medikamenteneinnahmen: alle offenen Positionen, sortiert nach Uhrzeit und Name; einzelne Einnahmen direkt bestätigen.
- Gemeinsame Pending-Semantik für die nächste Einnahme und die Medikamentenliste.
- Neue rein lesende Belastungsübersicht der letzten 48 Stunden (keine medizinische PEM-Prognose).
- Neue rein lesende Übersicht dokumentierter Schutzmaßnahmen der letzten sieben Tage.
- Schnellaktionen erweitert.

## Tests
Nicht unter Windows oder in Raycast ausgeführt. Bitte insbesondere identische Uhrzeiten, Mehrfacheinnahmen und ältere Einträge prüfen.

<a id="raycast-0-21-3-changes"></a>

## Version 0.21.3 – CHANGES

# PaceAtlas 0.21.3 – Raycast-Ausbau (erste Teilstufe)

- Symptomverlauf (7/14/30/90 Tage), unbewertete Symptome nicht als null interpretieren.
- Nächste offene Medikamenteneinnahme inklusive Anzahl offener und überfälliger Einnahmen.
- Laufenden Aktivitäten eine Notiz hinzufügen, ohne bestehende Notizen zu überschreiben.
- Alle drei Befehle in Schnellaktionen integriert.

## Noch offen
Schmerz-Schnellerfassung, PEM-Risiken, Schutzmaßnahmen, Wiederholungsfunktionen, vollständige Sprachintegration und technische Konsolidierung folgen in weiteren Teilstufen.

## Testhinweise
Nicht auf Windows/.NET oder Raycast ausgeführt. Bitte insbesondere Symptomverlauf und Medikamentenstatus testen.

<a id="raycast-0-21-2-food-search"></a>

## Version 0.21.2 – FOOD-SEARCH

# 0.21.2: Lebensmittelprüfung

- Neue IPC-Methoden `food.search` (mindestens zwei Zeichen, höchstens 30 Ergebnisse) und `food.get` (Einzeldetails).
- Serverseitige SQLite-Suche über foods, bls_foods und off_foods; keine vollständige Katalogmaterialisierung.
- Raycast: 300 ms Debounce, alte Ergebnisse ignorieren, Details erst nach Auswahl laden.
- Die bisherige API `nutrition.foods.list` bleibt aus Kompatibilitätsgründen erhalten, wird von Raycast aber nicht mehr verwendet.
- Kein Windows-/Raycast-Laufzeittest in dieser Umgebung.

<a id="raycast-0-21-1-meal-removal"></a>

## Version 0.21.1 – MEAL-REMOVAL

Raycast-Hotfix zu 0.21.1

Die eingeschränkte Funktion "Mahlzeit erfassen" (nur ein Lebensmittel) wurde aus dem Raycast-Manifest, den Schnellaktionen und den Quellen entfernt. Die vollwertige Mahlzeitenerfassung in der Windows-Anwendung bleibt unverändert. Die bestehenden API-Endpunkte bleiben aus Kompatibilitätsgründen bestehen.

Nach Installation/Update Raycast-Erweiterung neu bauen.

<a id="raycast-0-21-1-changes"></a>

## Version 0.21.1 – CHANGES

# PaceAtlas 0.21.1 – Ernährung und Schnellnotizen

- Raycast: **Schnellnotiz**, **Lebensmittel prüfen**, **Mahlzeit erfassen**.
- IPC: `notes.create`, `nutrition.foods.list`, `nutrition.meals.list`, `nutrition.meal.create`.
- Lebensmittelregeln werden nach Priorität und Musterlänge wie in WinUI ausgewertet. GL ist pro 100 g ausgewiesen, nicht als Portionswert.
- Mahlzeit-Schnellerfassung unterstützt zunächst **ein Lebensmittel pro Eintrag**; für mehrere Zutaten bitte WinUI verwenden. Es wird direkt `Store.SaveMeal` verwendet.
- Schnellnotizen werden als Einträge vom Typ `Notiz` im bestehenden Entry-Store gespeichert und im IPC-Verlauf angezeigt. Der WinUI-Verlauf muss für diesen neuen Typ noch angepasst werden.
- Alle Änderungen verwenden die zentrale Datenhaltung. Kein separater Raycast-Datenspeicher.
- Nicht auf Windows kompiliert oder zur Laufzeit getestet.

<a id="raycast-0-21-0-changes"></a>

## Version 0.21.0 – CHANGES

# PaceAtlas 0.21.0 – Raycast Command Center (erste Ausbaustufe)

- Neuer Raycast-Befehl „Schnellaktionen“ mit Verweisen auf die bisherigen Befehle, Tagesstatus, Pacing und Ruhe sowie kontextbezogenen laufenden Aktivitäten und den letzten fünf Einträgen.
- Ruhephasen können über IPC gestartet und beendet werden. Ende und Beginn sind editierbar und auf die lokale Uhrzeit vorbelegt. Die Daten werden als bestehende `Ruhe`-Einträge gespeichert.
- Neuer Tagesstatus: letzter dokumentierter Zustand, Anzahl und Zeit der Aktivitäten und Ruhephasen sowie offene Medikamente. Nicht erfasste Zeit wird nicht als Ruhe interpretiert.
- Pacing-Timer: IPC-Kommandos lesen und steuern die **existierende** WinUI-Timerinstanz via Dispatcher. Keine zweite Timerimplementierung.
- Kombinierte Schnellaktion „Aktivität beenden und Ruhe starten“ mit identischem Übergangszeitpunkt. **Noch nicht atomar**: Bei einem Schreibfehler nach Beenden der Aktivität kann die Ruhephase fehlen. Diese Aktion erst nach Test produktiv nutzen.
- Verlauf: Start und Ende stehen in einer Zeile; gleiches Enddatum wird weggelassen.
- Die weiteren Funktionen des genehmigten Ausbaus (Ernährung, Schmerz-Schnellerfassung, Lebensmittelprüfung, PEM-Hinweise, Symptomverlauf, Wiederholungen) sind für 0.21.1/0.21.2 vorgesehen und **nicht** Bestandteil dieser ZIP.

## Testschwerpunkte

1. Timerstatus und Timersteuerung aus Raycast mit gleichzeitig geöffneter WinUI prüfen.
2. Ruhephasen erstellen und beenden; anschließend in WinUI prüfen.
3. Tagesstatus auf Plausibilität prüfen, insbesondere laufende Intervalle.
4. Schnellaktionen und Verlauf öffnen.
5. Die kombinierte Aktion vorerst nur mit Testdaten ausführen.

Nicht unter Windows kompiliert oder zur Laufzeit getestet.

<a id="raycast-0-20-8-changes"></a>

## Version 0.20.8 – CHANGES

# PaceAtlas 0.20.8 – Raycast

- Laufende Aktivitäten: Beim Beenden erscheint ein Formular mit editierbarer Endzeit (HH:mm), vorbelegt beim Öffnen mit der aktuellen lokalen Uhrzeit. Notizen lassen sich ergänzen.
- Die API `activities.stop` akzeptiert optional `end` im Format `yyyy-MM-ddTHH:mm`; ohne Angabe bleibt der bisherige Jetzt-Zeitpunkt erhalten. Start-/Endzeit-Reihenfolge und Zukunftszeiten werden serverseitig geprüft.
- Verlauf: Beim ersten Laden wird der neueste Eintrag ausgewählt. Nachladen erhält die aktuelle Auswahl, Filterwechsel wählt bei Bedarf den ersten sichtbaren Eintrag.

Die Änderungen wurden statisch geprüft; ein Windows-/Raycast-Laufzeittest ist noch erforderlich.
