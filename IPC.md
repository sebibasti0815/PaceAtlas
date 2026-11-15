# IPC – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="ipc-0-20-4-changes"></a>

## Version 0.20.4 – CHANGES

# 0.20.4 – Aktivitätsnamen in Raycast

- `activities.active.list` gibt jetzt `name` zusätzlich zu den bestehenden Feldern zurück.
- Die WinUI speichert bislang keinen Vorlagenverweis in Aktivitätseinträgen. Deshalb wird eine Vorlage nur bei eindeutiger Übereinstimmung von Intensität, Belastungsarten und Schutzmaßnahmen angezeigt. Bei mehrdeutigen Einträgen werden die Belastungsarten verwendet, statt einen falschen Vorlagennamen zu behaupten.
- Raycast zeigt den vom Server gelieferten Namen und die Startzeit als HH:mm.
- Die Datenbank und bestehende Einträge werden nicht verändert.
- Für eine garantierte Vorlagenzuordnung bei zukünftigen WinUI-Einträgen wäre eine persistierte Template-ID nötig.

<a id="ipc-0-18-9-changes"></a>

## Version 0.18.9 – CHANGES

# PaceAtlas 0.18.9 – Raycast Autostart & UI

- Raycast-Erweiterung: optionaler, jederzeit änderbarer Anwendungspfad in den Extension-Einstellungen.
- Ohne Pfadangabe: Suche in üblichen Installationsverzeichnissen und Windows-Uninstall-Registrierung.
- Beim ersten ENOENT/ECONNREFUSED wird PaceAtlas mit `--tray` gestartet; bis zu 20 Sekunden auf die Pipe warten.
- WinUI: `--tray` unterdrückt Splash und initiale Fensteraktivierung; Single-Instance-Start mit `--tray` aktiviert keine bereits laufende Instanz.
- Alphabetische, deutschsprachige Sortierung der dynamisch geladenen Vorlagen, Belastungsarten, Schutzmaßnahmen, Symptome, Schmerzorte, Maßnahmen und Gründe.
- Status-Icon für Symptome: rotes Kreuz. Aktivitäten und Medikamente: Status-Icons. Intensität in der Vorlagenliste als Klartext.

## Testhinweise
1. PaceAtlas beenden, Raycast öffnen, Befehl ausführen. Erwartet: PaceAtlas startet ohne sichtbares Hauptfenster und der Befehl lädt.
2. Tray-Icon öffnen; Hauptfenster muss erscheinen.
3. Anwendung erneut beenden und in den Raycast-Erweiterungseinstellungen einen benutzerdefinierten EXE-Pfad setzen; erneut testen.
4. Parallele Raycast-Befehle und bereits laufende Anwendung prüfen.

Nicht unter Windows kompiliert oder mit Raycast getestet. Je nach Installer-Layout kann der Pfad manuell angegeben werden müssen.

<a id="ipc-0-18-8-changes"></a>

## Version 0.18.8 – CHANGES

# Raycast – Aktivitätenformular

- Vorlagenauswahl direkt im Formular „Aktivität erfassen“ ergänzt.
- Auswahl übernimmt Name, Intensität, Belastungsarten und Schutzmaßnahmen.
- Alle übernommenen Werte bleiben für den einzelnen Eintrag editierbar; die Vorlage wird nicht verändert.
- „Eigene Aktivität“ bleibt als Alternative verfügbar.
- Speicherung verwendet unverändert `activities.custom.create` und die vorhandene PaceAtlas-Geschäftslogik.

Hinweis: Das bestehende Datenmodell speichert keinen eigenständigen Vorlagenverweis im Aktivitätseintrag. Daher bleibt eine nachträgliche eindeutige Vorlagenzuordnung insbesondere bei geänderten Bezeichnungen derzeit nicht gewährleistet.

<a id="ipc-0-18-7-changes"></a>

## Version 0.18.7 – CHANGES

# PaceAtlas 0.18.7

- Raycast: Notizen für eigene Aktivitäten, Vorlagenstart und Aktivitätsende.
- Raycast: Sofortmaßnahme erfassen (Auswahl, Mehrfachgründe, Notiz; Datum/Uhrzeit automatisch).
- IPC: measures.options.list und measures.create, Benachrichtigung für Maßnahmen.

Hinweis: Kein Windows-Build in dieser Umgebung durchgeführt.

<a id="ipc-0-18-6-changes"></a>

## Version 0.18.6 – CHANGES

# PaceAtlas 0.18.6 – IPC-Stabilisierung

- Die ACL der Named Pipe gewährt dem angemeldeten Windows-Benutzer nun ausdrücklich `CreateNewInstance` zusätzlich zu `ReadWrite`. Unter Windows benötigen weitere Serverinstanzen dieses Recht.
- Parallele Fehlerprotokollierungen werden über ein gemeinsames Lock serialisiert.
- Bei dauerhaftem `UnauthorizedAccessException` wird der betroffene Listener beendet, statt eine Endlosschleife zu erzeugen. Die Ausnahme wird einmal protokolliert.
- Vier Listener und maximal 32 Clients bleiben erhalten; Raycast und das JSON-RPC-Protokoll wurden nicht verändert.

## Test

PaceAtlas vollständig beenden (auch im Tray), neu bauen und starten. In Visual Studio prüfen, ob im Leerlauf weitere `UnauthorizedAccessException` auftreten. Danach Raycast-Aktivitäten und Medikamentenliste mehrfach öffnen.

**Hinweis:** Unter Windows nicht kompiliert oder zur Laufzeit getestet. Wenn die Fehlermeldungen weiter auftreten, bitte den vollständigen Stacktrace aus `%LOCALAPPDATA%\PaceAtlas\ipc-error.log` bereitstellen.
