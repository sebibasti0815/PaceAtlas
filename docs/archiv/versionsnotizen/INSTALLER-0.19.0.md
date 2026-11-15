# PaceAtlas 0.19.0 – Installer und Raycast

## Bauen

Auf Windows mit .NET 10 SDK, Windows App SDK Build-Unterstützung und Inno Setup 6:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Das Ergebnis liegt unter `artifacts\installer`. Die Konfiguration `Installer` im WinUI-Projekt verweist bereits auf das Buildskript.

## Installation und Update

Der Inno-Setup-Installer hat eine feste AppId. Ein erneuter Lauf installiert über die vorhandene Installation; `UsePreviousAppDir` erhält das Installationsverzeichnis. Benutzerdateien unter `%LOCALAPPDATA%\PaceAtlas` werden nicht angefasst. Laufende Prozesse müssen ggf. geschlossen werden.

Die optionale Komponente „Raycast-Integration“ installiert die TypeScript-Quellen in `{app}\Raycast`. Nach der Installation wird im Benutzerkontext ein Setup-Assistent angeboten, der `npm install` und `npm run build` ausführt und den Import in Raycast erklärt. **Ein vollständig stiller Raycast-Import ist nicht implementiert**, weil keine stabile öffentliche Windows-Registrierungs-API vorausgesetzt werden kann. Raycast: „Import Extension“ auswählen und `{app}\Raycast` angeben. Bei Updates ggf. erneut importieren. Über die Startmenüverknüpfung „Raycast-Integration einrichten“ lässt sich dies wiederholen.

Voraussetzungen für den optionalen Raycast-Build: Node.js/npm, Raycast und Netzwerkzugriff für npm. Die eigentliche PaceAtlas-Anwendung benötigt Node.js nicht.

## Sicherheit und Grenzen

Die Installer-Dateien werden nicht direkt in Raycasts interne Profildatenbank kopiert. Raycast-Einstellungen bleiben bei PaceAtlas-Updates erhalten, soweit Raycast den importierten Extension-Identifier weiterverwendet. Das Installationspaket wurde hier nicht unter Windows kompiliert oder installiert. Der PKCS-Pin auf 10.0.13 sollte mit `dotnet list package --include-transitive --vulnerable` überprüft werden.
