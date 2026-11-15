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
