# PaceAtlas 0.21.8 – Strukturierte Schmerz-Schnellerfassung

- Schmerz-Schnellerfassung speichert reguläre Zustandseinträge über `status.create`, nicht als Freitext-Schnellnotiz.
- Allgemeinzustand, PEM, Crash, Schmerzorte (Mehrfachauswahl), Notizen; kein Puls und keine Symptombewertung. Symptome fehlen im Dictionary und werden durch `SymptomSeverity` als `-1` (nicht beurteilt) interpretiert.
- Eintragsmetadaten als separate SQLite-Spalten `capture_source` und `capture_mode`, idempotente Migration bestehender Einträge auf `WinUI`/`Full` (bewusste historische Konvention).
- Neue Raycast-Zustände: `Raycast`/`Full` oder `Raycast`/`Quick`; neue Raycast-Aktivitäten: `Raycast`/`Quick`.
- Die bestehenden `Entry`-Lese-/Speicherwege berücksichtigen die neuen Spalten, auch bei Änderungen.
- Noch offen: Metadaten für alle übrigen IPC-Schreibwege und eine spätere geräteübergreifende Synchronisierung; kein Sync-Schema eingeführt.
- Die derzeitige Schmerz-Datenstruktur speichert Schmerzorte ohne individuelle Schmerzstärken. Hier werden **keine** neuen Stärke-Werte erfunden.
- Nicht unter Windows gebaut oder ausgeführt. Vor Installation Datenbank sichern.
