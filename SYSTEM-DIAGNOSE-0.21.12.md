# PaceAtlas 0.21.12 – Diagnoseausbau

- IPC-Anfragen erhalten eine zufällige Korrelations-ID im Diagnoseprotokoll.
- Keine Protokollierung von Parametern, Gesundheitsdaten oder JSON-RPC-Nutzdaten.
- Statistik über das begrenzte eingelesene Protokollfenster: Fehler, langsame Anfragen, Mittelwert, P95.
- Die Statistik bezieht sich nur auf protokollierte Anfragen, nicht auf alle IPC-Aufrufe.
- Bestehende Live-Aktualisierung und Pausenfunktion bleiben erhalten.
- Build und Laufzeittest unter Windows stehen noch aus.
