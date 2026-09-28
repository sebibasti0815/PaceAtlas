# PaceAtlas: WinUI-3-Vergleichsprototyp

Das ZIP enthält drei nebeneinanderliegende Projekte:

- `PaceAtlas/` – bisherige WinForms-Anwendung, weiterhin mit den echten Daten.
- `PaceAtlas.Core/` – gemeinsame Modelle und SQLite-Datenzugriff für beide Oberflächen, ohne UI-Abhängigkeit.
- `PaceAtlas.WinUIPrototype/` – eigenständiger WinUI-3-Prototyp für Eingabe und Fenstergrößenvergleich.

**Alle drei Ordner zusammen in denselben übergeordneten Ordner entpacken.** Die Projektverweise setzen voraus, dass `PaceAtlas/` und `PaceAtlas.Core/` direkt nebeneinander liegen. Der WinUI-Timer bindet die Tonsignale und das Symbol aus der WinForms-Projektmappe ein. Die mitgelieferte `PaceAtlas.sln` im übergeordneten Ordner öffnen und das gewünschte Startprojekt auswählen. Wenn nur `PaceAtlas/` in ein bestehendes Repository kopiert wird, fehlt `PaceAtlas.Core/` und NuGet meldet NU1105.

## Unter Windows starten

Visual Studio 2026 mit der Workload „WinUI application development“, .NET 10 SDK und Windows SDK 10.0.26100 oder neuer verwenden. Öffne `PaceAtlas.sln` und wähle „Projektmappe erstellen“: In `Debug|x64` und `Release|x64` werden Core und der WinUI-Prototyp gebaut. Das WinForms-Projekt ist nicht Teil der Solution; einige seiner Quelldateien und Ressourcen werden vom WinUI-Projekt weiterhin direkt eingebunden. Für F5 `PaceAtlas.WinUIPrototype` als Startprojekt festlegen. Alternativ im entpackten Hauptordner:

```powershell
dotnet run --project .\PaceAtlas.WinUIPrototype\PaceAtlas.WinUIPrototype.csproj
```

Der Prototyp ist eine **nicht paketierte, frameworkabhängige WinUI-3-App**. Der erste Build lädt die Windows App SDK-Abhängigkeiten über NuGet. Auf dem Zielrechner müssen die passende .NET-10-Laufzeit und die Windows App SDK Runtime 1.8 (x64) installiert sein; das Windows App SDK wird bei nicht paketierten Apps durch `WindowsPackageType=None` beim Start automatisch initialisiert. Anders als beim bisherigen eigenständigen Build liegen dessen Laufzeitdateien nicht mehr in jedem Ausgabeordner. Ein bereits vorhandener Ausgabeordner kann noch Dateien des eigenständigen Builds enthalten; für einen Größenvergleich einen frischen Ausgabeordner verwenden. Das Hauptfenster enthält dieselben Zustandseingaben wie PaceAtlas und eine darunterliegende Liste zum Vergleich beim Ziehen des Fensters.

Das WinUI-Projekt ist für Windows x64 konfiguriert.

## Windows-Setup erstellen

Unter Windows Inno Setup 6 und die oben genannte .NET/WinUI-Buildumgebung installieren. Die beiden signierten Microsoft-Laufzeitinstaller gemäß `installer/prerequisites/README.md` ablegen. In Visual Studio die Solution-Konfiguration **Installer|x64** wählen und „Projektmappe erstellen“ ausführen. Sie baut WinUI und Core und ruft anschließend `installer/build-installer.ps1` auf. Debug|x64 und Release|x64 erstellen weiterhin ausschließlich die Anwendung. Alternativ im Hauptordner ausführen:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Das Skript veröffentlicht die WinUI-Anwendung als frameworkabhängige x64-Version, prüft die Signaturen der Laufzeitinstaller und erzeugt `artifacts\installer\PaceAtlas-Setup-<Version>-win-x64.exe`. Das Setup installiert .NET Desktop Runtime 10 und Windows App SDK Runtime 1.8, erstellt einen Startmenüeintrag und bietet optional eine Desktopverknüpfung. Installation und Deinstallation erfordern Administratorrechte. Eine Deinstallation entfernt die Programmdateien und Verknüpfungen, aber keine persönlichen Daten unter `%LOCALAPPDATA%\PaceAtlas` oder Einstellungen unter `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype`. Beim Wechsel auf einen anderen Rechner die Daten gesondert übertragen.

### Updates anbieten

Das Repository `https://github.com/sebibasti0815/PaceAtlas` ist in der Anwendung fest hinterlegt. Über den Info-Dialog (App-Symbol links oben oder Tray → Info) lässt sich sofort prüfen. Außerdem prüft die Anwendung beim Start höchstens einmal täglich; der letzte Prüfzeitpunkt liegt unter `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype\update-settings.json`. Die Prüfung ruft die öffentlichen GitHub-Releases ab und wählt aus den letzten 100 veröffentlichten, regulären Releases das höchste Versions-Tag, statt sich auf GitHubs zeitlich neueste Release zu verlassen. Das Release-Tag muss die Versionsnummer tragen, etwa `v0.3.2` oder `0.3.2`. Hänge die vom Buildskript erzeugte Datei `PaceAtlas-Setup-<Version>-win-x64.exe` als Release-Asset an; bei abweichendem Dateinamen öffnet die Anwendung stattdessen die Release-Seite. Bei einer neueren Version fragt sie vor dem Öffnen im Standardbrowser. Installation und Datenübernahme geschehen nicht automatisch. Die Prüfung benötigt für öffentliche Releases keine GitHub-Anmeldung.

Falls das Fenster beim Start nicht erscheint, zeigt die Anwendung nun erkannte Startfehler in einem eigenen Dialog an und schreibt sie zusätzlich nach `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype\startup-error.log`.

Der Prototyp verwendet Windows App SDK 1.8.12. In dieser Wartungsreihe wurde ein Fehler beim Mausrad-Scrollen in aktiven WinUI-Fenstern behoben, der bei deaktivierter Windows-Option „Inaktive Fenster beim Daraufzeigen scrollen“ auftrat. Weil der Prototyp zuvor mit einer neueren Paketreihe betroffen war, testen wir damit, ob das Mausrad und die schnelle Größenänderung zusammen funktionieren.

Die anfängliche Fenstergröße berücksichtigt die DPI-Skalierung des Bildschirms und bleibt innerhalb seiner verfügbaren Arbeitsfläche. Bitte das Mausrad ausdrücklich auch auf dem hochskalierten 4K-Bildschirm testen: Auf dem anderen Bildschirm funktionierte es bereits, daher ist die Skalierung ein wichtiger Hinweis zur Ursache.

Das WinUI-Programm deklariert über `app.manifest` ausdrücklich DPI-Bewusstsein pro Monitor (PerMonitorV2). Dadurch soll Windows die Oberfläche beim Wechsel zwischen unterschiedlich skalierten Bildschirmen in der jeweils passenden Auflösung zeichnen, statt ein fertiges Bild weich zu vergrößern.

WinForms und WinUI lesen und ändern jetzt dieselbe Datenbank unter `%LOCALAPPDATA%\PaceAtlas\paceatlas.db`. Der gemeinsame `Store` liegt in `PaceAtlas.Core`. Zustände, Zeiträume, Maßnahmen, Medikamentenplan, Tagesprotokoll und Vorrat werden über die vorhandenen SQLite-Methoden gespeichert. Vorhandene Testdateien unter `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype` werden nicht importiert oder gelöscht. Dort werden nur die WinUI-Spaltenbreiten in `column-widths.json` abgelegt. Die Datenbank vor dem ersten Test mit echten Einträgen sichern.

Planeinträge besitzen einen Beginn und ein optionales Ende („Läuft noch“). Neue Einträge beginnen standardmäßig heute; ein früher beendetes Präparat kann als neuer Planeintrag erneut aufgenommen werden. Das Tagesprotokoll zeigt einen Plan nur für Tage innerhalb seines Zeitraums, zusätzlich bereits dokumentierte Einnahmen. Bestehende Planeinträge erhalten bei der Migration den Beginn „bisher“, damit frühere Tage weiterhin lesbar bleiben. Der geschätzte Wochenbedarf berücksichtigt nur heute laufende Pläne.

Alle Tabellen haben klickbare Spaltenköpfe zum Sortieren. Die weißen Griffe an den Spaltenrändern erlauben das Verstellen der Breite; nach dem Loslassen werden die Breiten gespeichert. Tabellenzeilen sind platzsparend und die Speicheraktionen türkis markiert. Das Kontextmenü der gespeicherten Einträge bietet Bearbeiten, Löschen, Beenden eines laufenden Zeitraums, CSV-Export sowie Backup und Wiederherstellung. Beim Bearbeiten einer Einnahme wird das Tagesprotokoll für den betreffenden Tag geöffnet. Die Auswahllisten für Maßnahmen, Aktivitäten und Ruheformen werden über Dialoge gepflegt und stehen damit auch in WinForms zur Verfügung.

Der Pacing-Timer verwendet in beiden Oberflächen dieselben Intervalle aus `%LOCALAPPDATA%\PaceAtlas\pacing-timer.json`: oben rechts stehen Aktiv- und Pausendauer, Start/Stopp, Pause und Restzeit. WinUI setzt Tray-Symbol und bildschirmfüllende Pausenfenster mit Win32 und WinUI um, ohne WinForms in den XAML-Build einzubinden. Beim automatischen Beginn einer Pause erscheinen die Overlays auf allen Monitoren; akustische Signale und die Meldung am Pausenende entsprechen WinForms. Wird das Hauptfenster minimiert, verschwindet es aus der Taskleiste und bleibt über das Tray-Symbol erreichbar; ein Doppelklick oder „Pace Atlas öffnen“ holt es zurück. Falls das Tray-Symbol nicht erstellt werden konnte, bleibt das minimierte Fenster stattdessen in der Taskleiste erreichbar. Das Tray-Menü bietet Öffnen, Pacing starten, Pause beginnen/beenden, Pacing stoppen, Start im Tray und Beenden. Die Start-im-Tray-Einstellung der WinUI-Oberfläche liegt gesondert unter `%LOCALAPPDATA%\PaceAtlas.WinUIPrototype\tray-settings.json`, damit andere WinForms-Fenstereinstellungen nicht überschrieben werden. Bei laufendem Timer verlangt das Schließen des Fensters eine Bestätigung.

KI und Auswertungsdiagramme gehören noch nicht zum WinUI-Prototyp.

Für Zeiträume werden Datum und Uhrzeit platzsparend als `TT.MM.JJJJ` und `HH:mm` eingegeben und vor dem Speichern geprüft. Die eingebauten WinUI-Picker lassen sich nicht ohne eigene Vorlagen zuverlässig in ihren einzelnen Spalten schmaler machen; die vorherige feste Gesamtbreite schnitt deshalb Werte ab. Notizen und Speichern-Schaltflächen bleiben nun in beiden Eingabe-Tabs am unteren Rand sichtbar.

Die bisherige WinForms-App wird weiterhin über `PaceAtlas/PaceAtlas.csproj` gestartet. Sie benutzt jetzt für neu erfasste Zustände dieselbe `ConditionEntryFactory` aus `PaceAtlas.Core`; das JSON-Format bestehender Einträge wurde beibehalten. Auch die textliche Einordnung und das Zusammenführen überlappender Schlafintervalle liegen in dieser gemeinsamen, UI-unabhängigen Bibliothek.

## Versionsnummer

Alle Projekte beziehen ihre gemeinsame Versionsnummer aus `Directory.Build.props`. Nach jedem abgeschlossenen Änderungsschritt `python3 bump-version.py` ausführen und anschließend das ZIP neu erstellen. Die dritte Stelle läuft von 0 bis 10; nach `0.1.10` folgt `0.2.0`. Die erste Stelle wird ausschließlich auf ausdrücklichen Wunsch geändert. Der Info-Dialog zeigt die Versionsnummer der gebauten Anwendung an.

Die WinUI-Oberfläche zeigt für den Allgemeinzustand und die Symptome gefüllte Kreise, für die Intensität von Aktivität und Ruhe Balken. In gespeicherten Einträgen erscheinen zusätzlich farbige Marker für PEM, Crash und noch laufende Zeiträume. Der Intensitätswert steht dort nur noch im Tooltip und im zugänglichen Namen; die Bezeichnung des Allgemeinzustands bleibt sichtbar. Eine kompakte Übersicht oberhalb der Tabelle zeigt den letzten Zustand des Tages, laufende Zeiträume und die nächste geplante Einnahme. Sie wird bei Änderungen und minütlich aktualisiert.

Im Einnahmeplan ist die Form eine feste Auswahl mit Symbol und Text (Kapsel, Tablette, mg, ml, Spray, Pflaster, Gel). Bestehende Einträge mit früher frei eingegebenen Formen bleiben beim Bearbeiten erhalten. Einnahmeplan, Tagesprotokoll und Vorratstabelle zeigen die Form als Symbol mit Tooltip. Das Tagesprotokoll zeigt Einnahmestatus als ○ offen, ✓ genommen und ✕ ausgelassen; auch gespeicherte Einnahmen verwenden diese Symbole. Symptom-Dropdowns haben eine feste Breite, und das Raster wählt bei schmaleren Fensterbreiten weniger Spalten.

Die Tabellenüberschriften der vier Listen verwenden denselben linken Ursprung und auf die Tabellenwerte abgestimmte Innenabstände.

Im Einnahmeplan trennt eine feste, leicht hinterlegte Überschrift „Einnahme bearbeiten“ die gespeicherte Tabelle vom darunter unabhängig scrollenden Editor. Die Überschrift bleibt sichtbar, auch wenn die Felder im Editor gescrollt werden.
# Heatmap der Auswertung

Im Tab **Auswertung → Heatmap** zeigt jede Spalte einen Tag und jede Zeile eine Stunde. Blaue Felder markieren erfasste Aktivitäten nach ihrer Intensität; bei Überschneidungen gilt die höchste Intensität. Farbige Punkte zeigen den dokumentierten Allgemeinzustand zur Uhrzeit des Eintrags; orange und rote Ränder kennzeichnen PEM und Crash. Ein Tooltip zeigt Uhrzeit und Intensität. Weiße Felder bedeuten, dass dort keine Aktivität eingetragen ist, nicht dass ein guter Zustand gemessen wurde. Bei langen Auswertungszeiträumen zeigt die Heatmap die letzten 42 Tage und nennt die Begrenzung oberhalb der Grafik. Zeitliche Nähe erlaubt keinen Rückschluss auf eine Ursache.

Die zweite Ansicht **Auswertung → Heatmap · Wochendurchschnitt** gruppiert alle Einträge des gewählten Auswertungszeitraums nach Wochentag und Stunde. Die Hintergrundfarbe bildet die mittlere Intensität dokumentierter Aktivitäten ab; ein Punkt steht für den mittleren Allgemeinzustand. Zahlen im Feld geben die Anzahl der PEM- und Crash-Markierungen an (in dieser Reihenfolge); der Tooltip nennt außerdem die Zahl der Zustandsangaben und Aktivitäten, damit einzelne Einträge nicht wie ein häufiges Muster erscheinen. Verlauf und beide Heatmaps nutzen die verfügbare Höhe und Breite des Fensters. Kennzahlen zu Schlaf und Einträgen stehen über den vier Subtabs **Verlauf**, **Heatmap · Tage**, **Heatmap · Wochendurchschnitt** und **Automatische Einordnung**. Die Erklärung zur Verlaufslinie steht ausschließlich im Tab **Verlauf**. Unter Automatische Einordnung bleiben die Ansichten **Lokal** und **KI** einschließlich der Analyse- und Verbindungsschaltflächen erhalten.

Die Kennzahlen erscheinen direkt neben der Zeitraumwahl, sodass die Subtabs mehr Höhe erhalten. Die Tages-Heatmap dehnt wenige Tage auf die verfügbare Breite aus und scrollt bei vielen Tagen horizontal, ohne die Spalten unter ihre Mindestbreite zu verkleinern. Für längere Zeiträume zeigt sie höchstens die letzten 42 Tage und nennt den ausgelassenen Datumsbereich ausdrücklich. Die Wochenansicht berücksichtigt dagegen den ganzen gewählten Zeitraum; der Verlauf kennzeichnet, wenn die erste und letzte vorhandene Zustandsangabe nicht den gesamten gewählten Zeitraum abdecken.

Beide Heatmaps zeichnen die Stunden ohne einzelne Zellrahmen, Zwischenräume oder horizontale Hilfslinien. Dezente Trennlinien zwischen Tagen bleiben als Orientierung erhalten. Fortgeführte Zustände werden in der Tagesansicht pro Tag als zusammenhängende Fläche gezeichnet, sodass auch ihre Überlagerung keine Stundenstreifen erzeugt.

Über **Zustand fortführen** lässt sich der letzte dokumentierte Allgemeinzustand für höchstens 24 oder 48 Stunden bis zur nächsten Zustandsangabe annehmen. Die Option ist zunächst ausgeschaltet und wird mit der gewählten Dauer lokal gespeichert. Im Verlauf stehen gestrichelte Stufen für diese Annahme (mit eigener Legende), in beiden Heatmaps blasse Flächen mit schmalem Farbstreifen; Punkte zeigen ausschließlich tatsächliche Zustandsangaben. Nach Ablauf der Grenze bleibt der Zustand unbekannt. PEM und Crash werden nicht fortgeführt. Der Wochendurchschnitt mittelt dokumentierte Zustände und angenommene Stunden getrennt; der Tooltip nennt beide Stichproben. Überlappende Aktivitäten verwenden je Tag und Stunde die höchste Intensität, anschließend werden die entsprechenden Wochentage gemittelt. Mehrere Montage im gewählten Zeitraum können daher weiterhin anders aussehen als ein einzelner Montag in der Tagesansicht. Die gemeinsamen Kennzahlen zählen ausschließlich erfasste Angaben. Bei aktivierter Fortführung erhält die KI den Auftrag, diese Grenze anzuwenden und angenommene Zustände als solche zu benennen; die zur Übertragung überprüfbaren JSON-Daten enthalten weiterhin nur die Originaleinträge. Eine Änderung der Fortführungsoption markiert eine zuvor gespeicherte KI-Auswertung als veraltet.
