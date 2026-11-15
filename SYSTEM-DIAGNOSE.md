# SYSTEM-DIAGNOSE – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="system-diagnose-0-21-16"></a>

## Version 0.21.16

# PaceAtlas 0.21.16 – Diagnosepaket und Logrotation

- IPC-Diagnoseprotokoll wird bei 2 MiB rotiert; drei ältere Generationen werden behalten.
- Neuer Button **Diagnosepaket erstellen** im Reiter System & Diagnose.
- Export nach Dokumente/PaceAtlas-Diagnose als ZIP.
- ZIP enthält nur technische Systeminformationen und bis zu 300 bereits geladene, auf bekannte Felder reduzierte IPC-Ereignisse. Keine Rohlogs, Gesundheitsdaten, Datenbank oder Konfigurationsdateien.
- Die Diagnoseoberfläche bleibt während des Exports bedienbar.

Hinweis: Es werden nur die aktuell in der Diagnoseansicht geladenen Ereignisse exportiert. Keine Vollständigkeitsgarantie für ältere Logs. Windows-Build und Laufzeittest stehen noch aus.

<a id="system-diagnose-0-21-15"></a>

## Version 0.21.15

# PaceAtlas 0.21.15 – Systemstatus und Datenbankwartung

- Neue Schaltfläche **Datenbank komprimieren** neben **Datenbank prüfen**.
- Bestätigung, Prüfung freien Speicherplatzes, konsistente SQLite-Online-Sicherung mittels `BackupDatabase`, danach `VACUUM` und `PRAGMA quick_check`.
- Laufzeit und Größen vorher/nachher werden angezeigt; Sicherheitskopie verbleibt neben der Datenbank.
- Neue Schaltfläche **Systemstatus**: Prozess-ID, RAM, Datenbankdatei, Raycast-Build-Dateien und IPC-Diagnosemodus.

## Wichtige Einschränkung

Die Wartung ist noch **nicht** gegen parallele Schreibzugriffe sämtlicher PaceAtlas-Komponenten zentral gesperrt. SQLite serialisiert Schreibtransaktionen; unter Last kann `VACUUM` mit `SQLITE_BUSY` fehlschlagen. Bitte die Wartung nur durchführen, wenn keine Erfassungen laufen, und vorher zusätzlich die reguläre Datensicherung prüfen. Die Sicherung wird nicht automatisch gelöscht. Die Systemstatusanzeige prüft lokale Artefakte, nicht die tatsächliche IPC-Erreichbarkeit.

Windows-Build und Laufzeittest ausstehend.

<a id="system-diagnose-0-21-13"></a>

## Version 0.21.13

# PaceAtlas 0.21.13 – Diagnose-Ereignistabelle

Die IPC-Diagnose zeigt nun eine strukturierte, begrenzte Ereignistabelle statt Rohtext. Filter für Fehler und langsame Anfragen, Suche nach Methode/Korrelations-ID und Sortierung nach Zeit, Dauer oder Methode. Live-Modus, Pause und 15-Minuten-Diagnose bleiben erhalten.

Hinweis: Kennzahlen beziehen sich auf die letzten 256 KiB Logdaten, nicht auf alle Anfragen. Ein Windows-Build und ein UI-Laufzeittest sind noch erforderlich.

<a id="system-diagnose-0-21-12"></a>

## Version 0.21.12

# PaceAtlas 0.21.12 – Diagnoseausbau

- IPC-Anfragen erhalten eine zufällige Korrelations-ID im Diagnoseprotokoll.
- Keine Protokollierung von Parametern, Gesundheitsdaten oder JSON-RPC-Nutzdaten.
- Statistik über das begrenzte eingelesene Protokollfenster: Fehler, langsame Anfragen, Mittelwert, P95.
- Die Statistik bezieht sich nur auf protokollierte Anfragen, nicht auf alle IPC-Aufrufe.
- Bestehende Live-Aktualisierung und Pausenfunktion bleiben erhalten.
- Build und Laufzeittest unter Windows stehen noch aus.

<a id="system-diagnose-0-21-11"></a>

## Version 0.21.11

# PaceAtlas 0.21.11 – Live-Diagnose

- IPC-Diagnoseansicht aktualisiert sich alle 2 Sekunden, solange sie sichtbar und nicht pausiert ist.
- Pause/Fortsetzen hält nur die Anzeige an, nicht die Aufzeichnung.
- Schalter aktiviert ausführliches IPC-Logging für maximal 15 Minuten, danach automatische Rückkehr zum Normalmodus.
- Logging erfasst nur Methodenkennung, Dauer und Erfolg, keine Parameter oder Gesundheitsdaten.
- Die Umgebungsvariable PACEATLAS_IPC_DIAGNOSTICS=1 bleibt als expliziter Override bestehen.
- Bestehende Protokollbegrenzung und Filter bleiben erhalten.
- Nicht unter Windows kompiliert/getestet.

<a id="system-diagnose-0-21-10"></a>

## Version 0.21.10

# System & Diagnose – 0.21.10

Erste read-only WinUI-Diagnoseansicht für das bestehende IPC-Diagnoseprotokoll.

- Aktualisieren und Filter (Alle, Fehler, langsame Anfragen)
- Maximal 300 Zeilen aus den letzten 256 KiB; kein automatisches Polling
- Lesen mit FileShare.ReadWrite; keine Änderung an Pipe, Datenbank oder Gesundheitsdaten
- Noch kein vollständiges Mission Control Center.
- Windows-Build und UI-Laufzeittest stehen aus.

<a id="system-diagnose-0-21-9"></a>

## Version 0.21.9

# PaceAtlas 0.21.9 – IPC-Diagnosegrundlage

Die IPC-Schicht protokolliert fehlgeschlagene und langsame (ab 500 ms) Anfragen in `%LOCALAPPDATA%\PaceAtlas\Logs\ipc-diagnostics.log`.

Nur Methode, UTC-Zeit, Status und Dauer werden geschrieben. Keine Parameter, Antworten oder Gesundheitsdaten.

Für eine zeitweilige vollständige Zeitmessung `PACEATLAS_IPC_DIAGNOSTICS=1` in der Prozessumgebung der WinUI-Anwendung setzen und PaceAtlas neu starten. Ohne diese Variable werden erfolgreiche schnelle Anfragen nicht protokolliert.

Dies ist nur die technische Grundlage für einen späteren System-&-Diagnose-Bereich, **noch keine WinUI-Oberfläche**. Ein Windows-Build und ein Laufzeittest stehen aus.

Offen: Logrotation, Korrelations-IDs, zentraler Diagnoseviewer, Datenbank- und Sync-Telemetrie.
