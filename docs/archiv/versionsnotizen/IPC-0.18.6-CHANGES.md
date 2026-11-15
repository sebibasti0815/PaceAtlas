# PaceAtlas 0.18.6 – IPC-Stabilisierung

- Die ACL der Named Pipe gewährt dem angemeldeten Windows-Benutzer nun ausdrücklich `CreateNewInstance` zusätzlich zu `ReadWrite`. Unter Windows benötigen weitere Serverinstanzen dieses Recht.
- Parallele Fehlerprotokollierungen werden über ein gemeinsames Lock serialisiert.
- Bei dauerhaftem `UnauthorizedAccessException` wird der betroffene Listener beendet, statt eine Endlosschleife zu erzeugen. Die Ausnahme wird einmal protokolliert.
- Vier Listener und maximal 32 Clients bleiben erhalten; Raycast und das JSON-RPC-Protokoll wurden nicht verändert.

## Test

PaceAtlas vollständig beenden (auch im Tray), neu bauen und starten. In Visual Studio prüfen, ob im Leerlauf weitere `UnauthorizedAccessException` auftreten. Danach Raycast-Aktivitäten und Medikamentenliste mehrfach öffnen.

**Hinweis:** Unter Windows nicht kompiliert oder zur Laufzeit getestet. Wenn die Fehlermeldungen weiter auftreten, bitte den vollständigen Stacktrace aus `%LOCALAPPDATA%\PaceAtlas\ipc-error.log` bereitstellen.
