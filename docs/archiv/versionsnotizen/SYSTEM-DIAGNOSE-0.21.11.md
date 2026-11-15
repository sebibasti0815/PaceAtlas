# PaceAtlas 0.21.11 – Live-Diagnose

- IPC-Diagnoseansicht aktualisiert sich alle 2 Sekunden, solange sie sichtbar und nicht pausiert ist.
- Pause/Fortsetzen hält nur die Anzeige an, nicht die Aufzeichnung.
- Schalter aktiviert ausführliches IPC-Logging für maximal 15 Minuten, danach automatische Rückkehr zum Normalmodus.
- Logging erfasst nur Methodenkennung, Dauer und Erfolg, keine Parameter oder Gesundheitsdaten.
- Die Umgebungsvariable PACEATLAS_IPC_DIAGNOSTICS=1 bleibt als expliziter Override bestehen.
- Bestehende Protokollbegrenzung und Filter bleiben erhalten.
- Nicht unter Windows kompiliert/getestet.
