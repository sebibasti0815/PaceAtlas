# CONSOLIDATION – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="consolidation-0-22-0"></a>

## Version 0.22.0

# PaceAtlas 0.22.0 – Konsolidierung (erste Etappe)

## Enthalten
- Gemeinsame Versionsnummer in `Directory.Build.props`, Inno Setup und Raycast `package.json`.
- Diagnose-Ereignistabelle: Zeilen-UI wird nur für neue Ereignisse erzeugt; bestehende Zeilen werden wiederverwendet.
- Doppelte identische Logzeilen führen nicht mehr zu einer `ToDictionary`-Exception.

## Noch offen / nicht als erledigt deklarieren
- Windows-/WinUI-Laufzeittest, .NET-Build, Raycast-Build und Installer-Test.
- Belastungstest bei Logrotation und langen Live-Sessions.
- Vollständige Datenmodell-/IPC-Konsolidierung und Sync-Architektur.
- Vollständige Ende-zu-Ende-Diagnose einschließlich Datenbank- und Servertelemetrie.

## Testplan
1. `dotnet build PaceAtlas.sln -c Release`
2. Im Reiter System & Diagnose den 15-Minuten-Modus aktivieren und wiederholt Raycast-Befehle ausführen.
3. Selektion, Sortierung und Scrollposition bei neuen Einträgen beobachten.
4. `npm run build` im Ordner PaceAtlas.Raycast ausführen.
5. `installer/build-installer.ps1` ausführen und installierte Version prüfen.
