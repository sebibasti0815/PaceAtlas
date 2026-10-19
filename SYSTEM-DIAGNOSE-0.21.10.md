# System & Diagnose – 0.21.10

Erste read-only WinUI-Diagnoseansicht für das bestehende IPC-Diagnoseprotokoll.

- Aktualisieren und Filter (Alle, Fehler, langsame Anfragen)
- Maximal 300 Zeilen aus den letzten 256 KiB; kein automatisches Polling
- Lesen mit FileShare.ReadWrite; keine Änderung an Pipe, Datenbank oder Gesundheitsdaten
- Noch kein vollständiges Mission Control Center.
- Windows-Build und UI-Laufzeittest stehen aus.
