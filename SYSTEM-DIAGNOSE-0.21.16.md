# PaceAtlas 0.21.16 – Diagnosepaket und Logrotation

- IPC-Diagnoseprotokoll wird bei 2 MiB rotiert; drei ältere Generationen werden behalten.
- Neuer Button **Diagnosepaket erstellen** im Reiter System & Diagnose.
- Export nach Dokumente/PaceAtlas-Diagnose als ZIP.
- ZIP enthält nur technische Systeminformationen und bis zu 300 bereits geladene, auf bekannte Felder reduzierte IPC-Ereignisse. Keine Rohlogs, Gesundheitsdaten, Datenbank oder Konfigurationsdateien.
- Die Diagnoseoberfläche bleibt während des Exports bedienbar.

Hinweis: Es werden nur die aktuell in der Diagnoseansicht geladenen Ereignisse exportiert. Keine Vollständigkeitsgarantie für ältere Logs. Windows-Build und Laufzeittest stehen noch aus.
