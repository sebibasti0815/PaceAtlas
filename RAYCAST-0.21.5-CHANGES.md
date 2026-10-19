# PaceAtlas 0.21.5 – Schnellaktionen und Schutzmaßnahmen

- Schnellaktionen alphabetisch nach sichtbarem Titel sortiert. Kontextbereiche bleiben gesondert.
- Neue Aktion „Schutzmaßnahmen ergänzen“ für laufende Aktivitäten.
- Schutzmaßnahmen werden über `activities.protection.add` zur bestehenden Auswahl hinzugefügt, ohne vorhandene Werte zu löschen.
- Eingaben werden serverseitig gegen die in PaceAtlas konfigurierten Schutzmaßnahmen validiert.
- Fehler in `package.json` korrigiert: `pacing-load` und `protection-recent` waren versehentlich unter `preferences` statt `commands` eingetragen.
- Versionsnummern synchronisiert.

**Nicht enthalten:** eigenständige Schutzmaßnahmen ohne Aktivitätsbezug, PEM-Risikoprognose, Wiederholung alter Einträge.

**Tests:** JSON-Schema und Quellstruktur geprüft; .NET-/Raycast-Windows-Build und Laufzeittest ausstehend.
