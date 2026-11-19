# Interventionsanalyse · erste Version

Unter **Maßnahmen** werden bestehende Maßnahmeneinträge um optionale Phasen ergänzt: Name/Variante, Dosis, Beginn, optionales Ende, Pause und Notiz. Phasen dürfen sich nicht überschneiden. Ende ist ein exklusiver Zeitpunkt; eine anschließende Phase kann genau dort beginnen. Änderungen im Phasendialog werden erst mit **Maßnahme speichern** dauerhaft gespeichert. Bisherige Einträge bleiben ohne Migration lesbar; Phasen liegen im vorhandenen Maßnahmen-JSON und werden damit zusammen gesichert und synchronisiert.

Unter **Auswertung → Interventionsanalyse**, direkt vor **Automatische Einordnung**, stehen alle Maßnahmen zur Auswahl. Der Auswahltext enthält denselben Phasenhinweis wie der Verlauf, zum Beispiel „Phase 1 ‚Pflaster‘ läuft · 7 mg“ oder die Phasenanzahl, wenn keine Phase aktiv ist.

Ausgangszeitraum und Vergleichszeitraum haben jeweils eine **eigene Phasenauswahl**. Eine Auswahl übernimmt ausschließlich die Datumsgrenzen auf dieser Seite; die andere Seite bleibt unverändert. Offene Phasen reichen bis heute, Enden um Mitternacht gehören nicht mehr zur Phase und werden auf den vorherigen Kalendertag abgebildet. Noch nicht begonnene Phasen haben keinen auswertbaren Zeitraum. Bei ausgewählter Phase sind die Datumsfelder schreibgeschützt; **Freier Zeitraum** erhält die bisherigen Werte und erlaubt deren Bearbeitung. Ohne Phasenauswahl gelten zunächst die bisherigen Vorschläge (bis zu 28 Tage ab Maßnahmenbeginn und ein gleich langer Ausgangszeitraum davor).

Enddaten in der Analyse sind einschließlich. Der Ausgangszeitraum muss vor dem Vergleich liegen; Überschneidungen werden abgelehnt. Der globale Zeitraum der übrigen Auswertung schränkt diese Analyse nicht ein. Da auf Tagesebene verglichen wird, können bei Phasengrenzen innerhalb eines Tages auch Einträge außerhalb der genauen Uhrzeitgrenzen enthalten sein; darauf weist die Oberfläche hin. Vorhandene Auswahl und freie Datumswerte bleiben bei Datenaktualisierungen derselben Maßnahme erhalten; entfernte Phasen wechseln auf einen freien Zeitraum.

## Gespeicherte Auswahl

Änderungen an Maßnahme, beiden Phasenauswahlen, freien Von/Bis-Daten und PEM-Zeitfenster werden automatisch lokal in `%LOCALAPPDATA%\PaceAtlas.WinUI\intervention-analysis.json` gespeichert. Jede Maßnahme behält ihre eigene Auswahl; die zuletzt gewählte Maßnahme und das PEM-Zeitfenster werden beim Neustart wiederhergestellt. Phasen werden über Beginn und Name zugeordnet. Entfernte oder umbenannte Phasen wechseln auf einen freien Zeitraum mit den zuletzt gespeicherten Daten. Offene Phasen reichen beim Wiederherstellen bis heute. Die Ergebnisberechnung startet beim Öffnen und nach Änderungen automatisch, mit kurzer Verzögerung zum Zusammenfassen direkt aufeinanderfolgender Änderungen. Der Vergleichsbutton entfällt. Hinweise und Tagesverlauf sind beim ersten Öffnen geschlossen; während automatischer Aktualisierungen bleibt ihr aktueller Klappzustand erhalten. Die Auswahl ist eine lokale Anzeigeeinstellung und wird nicht als Gesundheitsdatensatz synchronisiert.

Auch die übrige Auswertung merkt sich ihre Anzeigeoptionen in der bestehenden Datei `%LOCALAPPDATA%\PaceAtlas\analysis-display.json`: globaler Zeitraum, Zustand fortführen einschließlich Dauer, Symptom- und Aktivitätsauswahl, Symptom-Sortierung, Zeitfenster der Aktivitätsmatrix, Sortierungen der Belastungstabellen und der Matrix sowie die beiden Auswertungsreiter. Symptom- und Aktivitätsnamen werden sprachunabhängig gespeichert. Bereits vorhandene Einstellungen für die Zustandsfortführung bleiben kompatibel. KI-Abfragen werden durch diese Automatik nicht ausgelöst.

Das PEM-Zeitfenster steht unmittelbar bei der separaten PEM-Auswertung unterhalb der Symptom- und Aktivitätskarten. Der aufklappbare Tagesverlauf verwendet die Farben, Punktgrößen und Linien des vorhandenen Verlaufsgraphen. Drei Diagramme besitzen jeweils Werteskala, Hilfslinien und Datumsskala; Punkte zeigen dokumentierte Tageswerte und Linien verbinden nur direkt aufeinanderfolgende Tage innerhalb desselben Zeitraums. Fehlende Werte bleiben Lücken. Die Legende nennt Zeitraum und Farbe, Tooltips Datum, Kennzahl, Wert und bei PEM den Zähler/Nenner.

## Berechnung

- Allgemeinzustand und jedes beurteilte Symptom: zunächst Mittelwert je dokumentiertem Tag, dann gleich gewichteter Mittelwert dieser Tageswerte. Nicht beurteilte Symptome und historische unbeurteilte Standard-Nullen zählen nicht mit. Explizit dokumentierte Symptomfreiheit zählt als null.
- Aktivität: dokumentierte Minuten je Tag; abgeschlossene Aktivitäten werden an Mitternacht geteilt und zeitliche Überschneidungen vereinigt. Laufende und ungültige Intervalle werden ausgeschlossen. Tage ohne Aktivitätseintrag bleiben unbekannt. Ein Rückgang dokumentierter Minuten belegt daher keinen tatsächlichen Rückgang der Aktivität.
- Verzögerte PEM: wahlweise 24–48, 24–72 oder 0–24 Stunden nach Aktivitätsende. Untergrenze einschließlich, Obergrenze ausschließlich. Das Fenster muss vollständig verstrichen sein und mindestens einen Zustandseintrag enthalten. PEM erkannt oder Crash zählt als positive Beobachtung; nur vermutete PEM nicht. Ein positiver Zustand genügt, auch bei mehreren Folgeeinträgen. Bereits bestehende PEM wird nicht von neu aufgetretener PEM unterschieden.
- Zuordnung zum Tag des Aktivitätsendes. Die Auswertung zeigt den Tagesanteil positiver Aktivitäten unter Aktivitäten mit auswertbaren Folgeeinträgen sowie die gepoolten absoluten Zähler/Nenner je Zeitraum. Mittelwert der Tagesanteile und gepoolter Anteil sind unterschiedliche Größen. Folgeeinträge dürfen außerhalb des ausgewählten Zeitraums liegen. Derselbe Zustand kann mehreren Aktivitäten zugeordnet werden; die Beobachtungen sind daher nicht unabhängig.
- Differenz = Vergleich minus Ausgang, in Skalenpunkten, Minuten oder Prozentpunkten. Schwankungsbreite = Minimum bis Maximum der beobachteten Tageswerte; kein Vertrauensintervall. Datenabdeckung wird je Kennzahl als beobachtete Tage / Kalendertage angegeben. Unter sieben Zustandstagen erscheint ein Hinweis auf die geringe Datenbasis.

Der gemeinsame Verlauf hat drei getrennte Skalen (Allgemeinzustand, Aktivitätsminuten, PEM-Anteil) und eine gemeinsame Kalenderachse; Lücken werden nicht verbunden. Die aufklappbaren Tageswerte enthalten zusätzlich die einzelnen Symptome. Andere Maßnahmen, Phasen/Pausen, Einnahmeanzahl und eigene Notizen liefern Kontext; es gibt keine rechnerische Korrektur für Begleitumstände und keine automatische Einstufung als wirksam oder unwirksam.

## Prüfung

Berechnungstests ohne Zugriff auf die persönliche Datenbank:

```powershell
dotnet run --project tools/InterventionTests/InterventionTests.csproj -c Release
```

Der optionale UI-Test verwendet ausschließlich Beispieldaten in einer wegwerfbaren Windows Sandbox:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-TrayButtons.ps1 -Interventions
```

Er benötigt die Windows Sandbox CLI `wsb`. Der Runner verweigert diesen Modus außerhalb von `WDAGUtilityAccount`.
