# PaceAtlas 0.19.8 – Raycast-Setup npm-Fehlerbehandlung

- `Invoke-Npm` startet npm über `System.Diagnostics.Process` und `cmd.exe`.
- stdout und stderr werden unabhängig und parallel eingelesen.
- npm-Warnungen auf stderr werden protokolliert, aber nicht als Fehler behandelt.
- Ausschließlich ein von null verschiedener npm-Exitcode führt zum Abbruch.
- Logdatei: `%LOCALAPPDATA%\PaceAtlas\Logs\Raycast-Setup.log`.
- Die automatische Registrierung in Raycast ist weiterhin nicht gelöst.
- Kein Windows-Installer-Test in dieser Umgebung durchgeführt.
