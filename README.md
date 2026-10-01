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
# BLS-Offlinekatalog

Der eingebettete Katalog `PaceAtlas.Core/Bls4Catalog.tsv.gz` wurde aus dem
Bundeslebensmittelschlüssel 4.0 erstellt: Max Rubner-Institut (2025),
*Bundeslebensmittelschlüssel (BLS), Version 4.0 — Deutsche Nährstoffdatenbank*,
Karlsruhe, DOI: https://doi.org/10.25826/Data20251217-134202-0.
Lizenz: Creative Commons Namensnennung 4.0 International (CC BY 4.0),
https://creativecommons.org/licenses/by/4.0/.
Die ausführlichen Quellen- und Lizenzangaben stehen in `THIRD-PARTY-NOTICES.md`
und im Infodialog der Anwendung.
Originaldaten: https://blsdb.de/download.
Die Datei enthält alle 7.140 Lebensmittel sowie Nährstoffwerte und deren
Herkunftsangaben. Zur Reproduktion dient `tools/build-bls-catalog.py` mit der
offiziellen Excel-Datei als Eingabe. Persönliche Angaben liegen getrennt in
`foods`; der BLS-Bestand wird auf einer neuen Installation offline eingespielt.

Kleinere Projektarchive dürfen `Bls4Catalog.tsv.gz` auslassen. Die Anwendung
funktioniert damit auf einem bestehenden Benutzerprofil mit bereits importiertem
BLS-Katalog weiter. Für eine neue Installation muss einmal das vollständige
Projektpaket mit der Datendatei gebaut und gestartet werden. Im Lebensmittel-Tab
prüft „BLS-Aktualisierung prüfen“ die offizielle Download-Datei. Zusätzlich
geschieht dies beim Start höchstens einmal täglich. Ein geänderter Download wird
gemeldet, aber nicht ungeprüft in die lokale Datenbank importiert.

## Open Food Facts importieren

Die offizielle, tabulatorgetrennte CSV-Exportdatei (auch als `.gz`) steht unter
https://world.openfoodfacts.org/data zur Verfügung. Im Register **Ernährung →
Lebensmittel** auf **Open Food Facts importieren** klicken und diese Datei
auswählen. Der vollständige Export ist groß; Einlesen und der erste Aufbau der
Lebensmittelliste können entsprechend dauern. Produkte mit deutschem Namen,
Kohlenhydratwert und Bezug zu Deutschland werden in einer eigenen SQLite-Tabelle
gespeichert. Dafür wird `product_name_de` genutzt oder bei Hauptsprache `de`
der allgemeine Produktname. Falls die CSV keine Sprachangabe enthält, wird
für Produkte mit Deutschlandbezug der vorhandene Produktname übernommen;
dieser kann im Einzelfall fremdsprachig sein. Vor dem Speichern werden HTML-Codes
im Namen dekodiert, führende Nummern in Klammern und störende führende Zeichen
entfernt, Packungsgewichte und Preisangaben gestrichen sowie Leerzeichen
vereinheitlicht. Die ursprüngliche Groß- und Kleinschreibung von Produkt- und
Markennamen bleibt erhalten. Prozentangaben wie „5 % Fett“ bleiben erhalten. Der Strichcode bleibt in
der Quellenangabe statt im Namen. Offensichtliche Doppelungen mit gleicher
normalisierter angezeigter Bezeichnung werden zusammengefasst, auch wenn
Nährwerte oder Strichcodes abweichen. Der Datensatz mit mehr vorhandenen
Nährwertfeldern wird bevorzugt; bei Gleichstand bleibt der zuerst gelesene.
Die Werte verschiedener Produkte werden dabei nicht vermischt. Eine automatische
Übersetzung anderer Sprachen findet nicht statt. Der Import kann mit einem
neuen Export wiederholt werden und ersetzt dabei den gesamten vorherigen
OFF-Import innerhalb einer Transaktion. Persönliche Ergänzungen bleiben
erhalten. Die Exportdatei wird nicht mit dem
Projektarchiv ausgeliefert; auf einem neuen Benutzerprofil muss sie erneut
importiert werden. Die Suchfunktion ignoriert Akzente und Interpunktion,
findet Wörter unabhängig von ihrer Reihenfolge und toleriert bei längeren
Suchwörtern einen Schreibfehler. Die Quellen- und Lizenzangaben stehen im
Infodialog und in `LICENSE-OPEN-FOOD-FACTS.md`.

Während des Imports zeigt die Oberfläche die Zahl der geprüften Zeilen und
einen Abbruchknopf. Ein Abbruch verwirft die laufende Transaktion; der bisherige
Katalog bleibt erhalten. Beim Schließen während des Imports fragt die Anwendung
nach und wartet vor dem Beenden auf diesen kontrollierten Abbruch.

# Pace Atlas for Windows (WinUI 3)

The project solution `PaceAtlas.sln` contains the Windows user interface `PaceAtlas.WinUI` and `PaceAtlas.Core`. The older WinForms source code in `PaceAtlas/` still provides shared resources and translations. Therefore, all three project folders must remain side by side.

## Getting Started

Use Visual Studio with the WinUI workload, the .NET 10 SDK, and the Windows SDK 10.0.26100 or later. Open `PaceAtlas.sln` and select `PaceAtlas.WinUI` as the startup project. Alternatively:

```powershell
dotnet run --project .\PaceAtlas.WinUI\PaceAtlas.WinUI.csproj
```

The application is designed for Windows x64 and requires .NET Desktop Runtime 10 and Windows App SDK Runtime 1.8. Both interfaces use the database `%LOCALAPPDATA%\PaceAtlas\paceatlas.db`.

## Installer

Provide Inno Setup 6 and the signed runtime installers from `installer/prerequisites/README.md`. In Visual Studio, build or run `Installer|x64`:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

The setup file appears at `artifacts\installer\PaceAtlas-Setup-<Version>-win-x64.exe`. The existing installer ID is retained. An upgrade removes the old EXE `PaceAtlas.WinUIPrototype.exe`, creates `PaceAtlas.WinUI.exe`, and updates the shortcuts.

## Settings and Data

The WinUI settings are now located at `%LOCALAPPDATA%\PaceAtlas.WinUI`. Upon first launch, the existing language, window position, tab order, column widths, filters, system tray, and update settings are imported from `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype`, provided that no corresponding file exists yet at the new location. The old settings folder is retained as a fallback option. The shared database is not moved.

The info dialog and the automatic daily check retrieve the published releases from `https://github.com/sebibasti0815/PaceAtlas`. A higher regular version tag may indicate an update is available; installation and data migration do not occur automatically.
# BLS Offline Catalog

The embedded catalog `PaceAtlas.Core/Bls4Catalog.tsv.gz` was created from the
Federal Food Code 4.0: Max Rubner Institute (2025),
*Federal Food Code (BLS), Version 4.0 — German Nutrient Database*,
Karlsruhe, DOI: https://doi.org/10.25826/Data20251217-134202-0.
License: Creative Commons Attribution 4.0 International (CC BY 4.0),
https://creativecommons.org/licenses/by/4.0/.
Detailed source and license information can be found in `THIRD-PARTY-NOTICES.md`
and in the application’s information dialog.
Original data: https://blsdb.de/download.
The file contains all 7,140 foods as well as nutrient values and their
origin information. To reproduce the results, use `tools/build-bls-catalog.py` with the
official Excel file as input. Personal data is stored separately in
`foods`; the BLS database is imported offline during a new installation.

Smaller project archives may omit `Bls4Catalog.tsv.gz`. The application
will then continue to function on an existing user profile with the
BLS catalog already imported. For a new installation, the complete
project package must be built and started once using the data file. In the Food tab,
“Check for BLS Update” verifies the official download file. Additionally,
this check occurs at startup and no more than once a day. A modified download is
reported but not imported into the local database without verification.

## Importing Open Food Facts

The official, tab-delimited CSV export file (also available as `.gz`) is available at
https://world.openfoodfacts.org/data. In the **Nutrition →
Food**, click **Import to Open Food Facts** and select this file
. The complete export is large; importing and the initial generation of the
food list may take some time. Products with German names,
carbohydrate values, and a connection to Germany are stored in a separate SQLite table
. For this, `product_name_de` is used, or, if the primary language is `de`,
the general product name. If the CSV file does not contain a language specification,
the existing product name is used for products related to Germany;
in some cases, this may be in a foreign language. Before saving, HTML codes
in the name are decoded, leading numbers in parentheses and disruptive leading characters
are removed, package weights and price information are deleted, and spaces
are standardized. The original capitalization of product and
brand names is retained. Percentages such as “5% fat” are retained. The barcode remains in
the source reference rather than in the name. Obvious duplicates with the same
normalized displayed name are combined, even if
nutritional values or barcodes differ. The data record with more existing
nutritional value fields is given priority; in the event of a tie, the one read first is retained.
The values of different products are not mixed together. Automatic
translation of other languages does not occur. The import can be repeated with a
new export, which replaces the entire previous
OFF import within a single transaction. Personal additions are
retained. The export file is not included with the
project archive; it must be re-imported
onto a new user profile. The search function ignores accents and punctuation,
finds words regardless of their order, and tolerates a typo in longer
search terms. The source and license information can be found in the
info dialog and in `LICENSE-OPEN-FOOD-FACTS.md`.

During import, the interface displays the number of lines checked and
a cancel button. Canceling discards the current transaction; the existing
catalog is preserved. If you close the application during import, it prompts
you and waits for this controlled cancellation before exiting.

