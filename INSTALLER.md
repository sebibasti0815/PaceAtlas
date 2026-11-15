# INSTALLER – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="installer-0-20-3"></a>

## Version 0.20.3

# PaceAtlas 0.20.3 – Raycast-Setup

- Korrigiert die Artefaktprüfung: `Get-MissingRaycastArtifacts` gibt die Namen einzeln zurück, statt ein verschachteltes `System.Object[]`-Array zu erzeugen.
- Bei fehlenden Artefakten nennt das Protokoll die konkreten Dateien.
- Zentrale Anwendungs-, Installer- und Raycast-Version auf 0.20.3 vereinheitlicht.
- npm-Warnungen bleiben nicht-fatal, sofern der Exitcode 0 ist.

Build: `./installer/build-installer.ps1`. Test auf Windows erforderlich.

<a id="installer-0-20-2"></a>

## Version 0.20.2

# 0.20.2 – Raycast setup order and repair

On first installation the setup waits for the user to import the extension, then builds it. On upgrades, existing registration is preserved and built. After building, the four entry-point JavaScript files and plugin.json are validated. The Start Menu repair command repeats the build if a later import has removed the files. No unsupported manipulation of Raycast internal files. Windows installer execution has not been verified in this environment.

<a id="installer-0-20-1"></a>

## Version 0.20.1

# PaceAtlas 0.20.1 – Raycast-Autostart-Pfaderkennung

- Die Erweiterung prüft installation.json über LOCALAPPDATA, USERPROFILE, APPDATA und os.homedir().
- UTF-8-BOM wird beim Lesen akzeptiert.
- Standardpfade werden auch dann geprüft, wenn Raycast ProgramFiles-Umgebungsvariablen nicht bereitstellt.
- Bei fehlender EXE enthält die Fehlermeldung die geprüften Pfade.
- Benutzerdefinierte Pfade haben unverändert Vorrang.

Windows-/Raycast-Laufzeittest steht noch aus. Das Paket verändert weder die SQLite-Daten noch den IPC-Server.

<a id="installer-0-20-0"></a>

## Version 0.20.0

# PaceAtlas 0.20.0 – Raycast-Setup

- Der Inno-Setup-Installer übergibt den tatsächlichen EXE-Pfad an `Setup-Raycast.ps1`.
- Die Einrichtung legt `%LOCALAPPDATA%\PaceAtlas\installation.json` als Fallback für den Raycast-Autostart an. Manuelle Raycast-Pfadeinstellungen haben Vorrang.
- `npm run build` wird ausgeführt und die von Raycast generierten `*.js`-Einstiegspunkte sowie `plugin.json` werden überprüft.
- Beim erstmaligen Import kann eine manuelle Registrierung in Raycast erforderlich sein. Wenn Artefakte fehlen, meldet der Installer dies ausdrücklich statt fälschlich Erfolg zu behaupten. Nach dem Import den Startmenüeintrag „Raycast-Integration einrichten“ erneut ausführen.
- Eine vollautomatische Registrierung ohne dokumentierte Raycast-Schnittstelle ist nicht implementiert.
- Windows-Installer und Raycast-Laufzeit müssen auf dem Zielsystem getestet werden.

<a id="installer-0-19-8"></a>

## Version 0.19.8

# PaceAtlas 0.19.8 – Raycast-Setup npm-Fehlerbehandlung

- `Invoke-Npm` startet npm über `System.Diagnostics.Process` und `cmd.exe`.
- stdout und stderr werden unabhängig und parallel eingelesen.
- npm-Warnungen auf stderr werden protokolliert, aber nicht als Fehler behandelt.
- Ausschließlich ein von null verschiedener npm-Exitcode führt zum Abbruch.
- Logdatei: `%LOCALAPPDATA%\PaceAtlas\Logs\Raycast-Setup.log`.
- Die automatische Registrierung in Raycast ist weiterhin nicht gelöst.
- Kein Windows-Installer-Test in dieser Umgebung durchgeführt.

<a id="installer-0-19-7"></a>

## Version 0.19.7

Fix: SourceDirectory wird nach param() aus PSScriptRoot gesetzt und vor Resolve-Path validiert. Raycast-Registrierung ohne Entwicklungsmodus bleibt offen.

<a id="installer-0-19-6"></a>

## Version 0.19.6

# PaceAtlas 0.19.6 – Raycast-Setup-Diagnose

- `Setup-Raycast.ps1` protokolliert npm-Ausgaben, Exitcodes und PowerShell-Stacktraces unter `%LOCALAPPDATA%\PaceAtlas\Logs\Raycast-Setup.log`.
- Fehler bleiben im interaktiven Modus sichtbar; Exitcode 1 wird an den aufrufenden Prozess zurückgegeben.
- Der Inno-Setup-Eintrag verwendet kein `nowait` mehr; die Einrichtung wird weiterhin im ursprünglichen Benutzerkontext ausgeführt.
- Wiederholte Ausführung ist vorgesehen; Raycast-Einstellungen werden nicht absichtlich gelöscht.
- **Grenze:** Raycast bietet hier noch keinen nachgewiesenen unbeaufsichtigten Import-/Updateweg. `ray build` ist keine Garantie für die Registrierung. Import kann weiterhin nötig sein.
- **Test:** Installer bauen, Raycast-Setup starten und das Log prüfen. Windows-/Inno-Setup-Laufzeittest ist noch ausstehend.

<a id="installer-0-19-2"></a>

## Version 0.19.2

# PaceAtlas 0.19.2 – WinUI Publish-Korrektur

Der Installer-Build führt einen Release-Build und danach ein `dotnet publish --no-build` mit identischer Runtime aus. Er kopiert `App.xbf`, `MainWindow.xbf` und `PaceAtlas.WinUI.pri` aus **demselben Release-Build** in `artifacts/publish` und prüft deren SHA-256-Hashes. Bei fehlenden Ressourcen wird kein Installer erstellt. Vor jedem Publish wird der Zielordner bereinigt.

Auf Windows ausführen: `./installer/build-installer.ps1`. Anschließend die EXE aus `artifacts/publish` **vor** der Installation starten und prüfen, ob die WinUI-Oberfläche erscheint. Danach den Installer testen.

**Raycast:** `npm run build` erstellt die Erweiterung, ist aber kein Nachweis einer funktionierenden Raycast-Registrierung. Der zuvor beobachtete Fehler „Missing executable“ ist mit diesem Update **noch nicht nachweislich behoben**. Bis zur Klärung ist `npm run dev` ein Entwicklungstest, keine Lösung für eine dauerhafte Installation. Die Einrichtung im Benutzerprofil bleibt erhalten; kein `npm install` unter Program Files.

Ein Windows-Build und der Raycast-Laufzeittest wurden hier nicht durchgeführt.

<a id="installer-0-19-1"></a>

## Version 0.19.1

# PaceAtlas 0.19.1 – Raycast-Installation im Benutzerprofil

Die Raycast-Quelldateien liegen weiterhin geschützt unter `{app}\Raycast`. Das Setup-Skript wird als interaktiver Benutzer ausgeführt und kopiert `package.json`, `tsconfig.json`, `src` und `assets` nach `%LOCALAPPDATA%\PaceAtlas\Raycast`. `npm install` (oder `npm ci`, wenn ein mitgeliefertes Lockfile existiert) und `npm run build` laufen **ausschließlich dort**.

Bei erneuter Einrichtung werden verwaltete Quelldateien aktualisiert; Raycast-Benutzereinstellungen und andere Erweiterungen werden nicht verändert. Die Registrierung über **Raycast → Import Extension** bleibt ein manueller Schritt; ob ein erneuter Import bei einem Update erforderlich ist, hängt von Raycast ab.

Zum Test: Installer bauen (`.\installer\build-installer.ps1`), Raycast-Komponente auswählen, Einrichtung ausführen, `%LOCALAPPDATA%\PaceAtlas\Raycast` auf `node_modules` und Build-Artefakte prüfen, anschließend aus diesem Ordner importieren. Der Installer wurde hier nicht unter Windows ausgeführt.

Hinweis: Die Startmenü-Verknüpfung wird von Inno Setup systemweit angelegt. Bei Mehrbenutzerinstallationen muss die Einrichtung im jeweiligen Benutzerkonto erfolgen. Ein Installationsupdate allein aktualisiert noch nicht automatisch jedes Benutzerprofil.

<a id="installer-0-19-0"></a>

## Version 0.19.0

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
