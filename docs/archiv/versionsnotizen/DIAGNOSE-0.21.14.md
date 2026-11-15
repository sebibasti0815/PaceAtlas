# Diagnose 0.21.14

- Inkrementelle Aktualisierung der IPC-Ereignisliste: bestehende ListViewItems werden weiterverwendet; bei identischem Log-Snapshot kein Neurendern.
- Korrelations-ID-Spalte 300 px in Kopf und Zeilen.
- Neue read-only Datenbankdiagnose: SQLite quick_check, Dateigröße, PRAGMA user_version und Prüfzeit. Manuell per Schaltfläche, keine Änderungen an Daten.
- Hinweis: Windows-/WinUI-Build in dieser Umgebung nicht ausführbar; Laufzeittest ausstehend.
