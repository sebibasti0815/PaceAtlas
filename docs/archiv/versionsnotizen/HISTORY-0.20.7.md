# PaceAtlas 0.20.7 – Verlauf

- Verlaufseinträge liefern über `history.get` interpretierte, lokalisierte Felder statt Roh-JSON.
- Aktuelle WinUI-Sprache aus `%LOCALAPPDATA%/PaceAtlas.WinUI/language.txt` (de/en).
- Nicht beurteilte Symptome werden ausgeblendet; explizit als `keine` beurteilte bleiben sichtbar.
- Technische Metadaten (SymptomValuesVersion etc.) erscheinen nicht mehr.
- Intensität und Erholung werden als Klartexte dargestellt.
- Die Raycast-Detailansicht ist weiterhin schreibgeschützt.
- Keine Datenbankmigration.

Hinweis: Die Aktivitätsintensität ist im Bestand nicht vollständig eindeutig skaliert (WinUI und Raycast verwenden teils unterschiedliche numerische Werte). Für bestehende Raycast-Einträge bleibt die bisherige 0–3-Darstellung erhalten. Eine Vereinheitlichung des gespeicherten Formats sollte gesondert erfolgen.
