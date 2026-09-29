# Pace Atlas für Windows (WinUI 3)

Die Projektmappe `PaceAtlas.sln` enthält die Windows-Oberfläche `PaceAtlas.WinUI` und `PaceAtlas.Core`. Der ältere WinForms-Quellcode unter `PaceAtlas/` liefert weiterhin gemeinsam verwendete Ressourcen und Übersetzungen. Deshalb müssen alle drei Projektordner nebeneinander bleiben.

## Starten

Visual Studio mit WinUI-Workload, .NET 10 SDK und Windows SDK 10.0.26100 oder neuer verwenden. `PaceAtlas.sln` öffnen und `PaceAtlas.WinUI` als Startprojekt auswählen. Alternativ:

```powershell
dotnet run --project .\PaceAtlas.WinUI\PaceAtlas.WinUI.csproj
```

Die Anwendung ist für Windows x64 ausgelegt und benötigt .NET Desktop Runtime 10 und Windows App SDK Runtime 1.8. Beide Oberflächen verwenden die Datenbank `%LOCALAPPDATA%\PaceAtlas\paceatlas.db`.

## Installer

Inno Setup 6 und die signierten Laufzeitinstaller aus `installer/prerequisites/README.md` bereitstellen. In Visual Studio `Installer|x64` bauen oder ausführen:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Das Setup erscheint unter `artifacts\installer\PaceAtlas-Setup-<Version>-win-x64.exe`. Die bestehende Installer-Kennung bleibt erhalten. Ein Upgrade entfernt die alte EXE `PaceAtlas.WinUIPrototype.exe`, legt `PaceAtlas.WinUI.exe` an und aktualisiert die Verknüpfungen.

## Einstellungen und Daten

Die WinUI-Einstellungen liegen nun unter `%LOCALAPPDATA%\PaceAtlas.WinUI`. Beim ersten Start werden vorhandene Sprache, Fensterposition, Tab-Reihenfolge, Spaltenbreiten, Filter, Tray- und Update-Einstellungen aus `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype` übernommen, sofern am neuen Ort noch keine entsprechende Datei existiert. Der alte Einstellungsordner bleibt als Rückfallmöglichkeit erhalten. Die gemeinsame Datenbank wird dabei nicht verschoben.

Der Info-Dialog und die automatische tägliche Prüfung lesen die veröffentlichten Releases von `https://github.com/sebibasti0815/PaceAtlas`. Ein höheres reguläres Versions-Tag kann ein Update anbieten; Installation und Datenübernahme erfolgen nicht automatisch.
