# PaceAtlas 0.19.6 – Raycast-Setup-Diagnose

- `Setup-Raycast.ps1` protokolliert npm-Ausgaben, Exitcodes und PowerShell-Stacktraces unter `%LOCALAPPDATA%\PaceAtlas\Logs\Raycast-Setup.log`.
- Fehler bleiben im interaktiven Modus sichtbar; Exitcode 1 wird an den aufrufenden Prozess zurückgegeben.
- Der Inno-Setup-Eintrag verwendet kein `nowait` mehr; die Einrichtung wird weiterhin im ursprünglichen Benutzerkontext ausgeführt.
- Wiederholte Ausführung ist vorgesehen; Raycast-Einstellungen werden nicht absichtlich gelöscht.
- **Grenze:** Raycast bietet hier noch keinen nachgewiesenen unbeaufsichtigten Import-/Updateweg. `ray build` ist keine Garantie für die Registrierung. Import kann weiterhin nötig sein.
- **Test:** Installer bauen, Raycast-Setup starten und das Log prüfen. Windows-/Inno-Setup-Laufzeittest ist noch ausstehend.
