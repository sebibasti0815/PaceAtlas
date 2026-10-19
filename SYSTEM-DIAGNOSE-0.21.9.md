# PaceAtlas 0.21.9 – IPC-Diagnosegrundlage

Die IPC-Schicht protokolliert fehlgeschlagene und langsame (ab 500 ms) Anfragen in `%LOCALAPPDATA%\PaceAtlas\Logs\ipc-diagnostics.log`.

Nur Methode, UTC-Zeit, Status und Dauer werden geschrieben. Keine Parameter, Antworten oder Gesundheitsdaten.

Für eine zeitweilige vollständige Zeitmessung `PACEATLAS_IPC_DIAGNOSTICS=1` in der Prozessumgebung der WinUI-Anwendung setzen und PaceAtlas neu starten. Ohne diese Variable werden erfolgreiche schnelle Anfragen nicht protokolliert.

Dies ist nur die technische Grundlage für einen späteren System-&-Diagnose-Bereich, **noch keine WinUI-Oberfläche**. Ein Windows-Build und ein Laufzeittest stehen aus.

Offen: Logrotation, Korrelations-IDs, zentraler Diagnoseviewer, Datenbank- und Sync-Telemetrie.
