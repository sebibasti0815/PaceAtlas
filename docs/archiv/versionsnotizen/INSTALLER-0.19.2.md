# PaceAtlas 0.19.2 – WinUI Publish-Korrektur

Der Installer-Build führt einen Release-Build und danach ein `dotnet publish --no-build` mit identischer Runtime aus. Er kopiert `App.xbf`, `MainWindow.xbf` und `PaceAtlas.WinUI.pri` aus **demselben Release-Build** in `artifacts/publish` und prüft deren SHA-256-Hashes. Bei fehlenden Ressourcen wird kein Installer erstellt. Vor jedem Publish wird der Zielordner bereinigt.

Auf Windows ausführen: `./installer/build-installer.ps1`. Anschließend die EXE aus `artifacts/publish` **vor** der Installation starten und prüfen, ob die WinUI-Oberfläche erscheint. Danach den Installer testen.

**Raycast:** `npm run build` erstellt die Erweiterung, ist aber kein Nachweis einer funktionierenden Raycast-Registrierung. Der zuvor beobachtete Fehler „Missing executable“ ist mit diesem Update **noch nicht nachweislich behoben**. Bis zur Klärung ist `npm run dev` ein Entwicklungstest, keine Lösung für eine dauerhafte Installation. Die Einrichtung im Benutzerprofil bleibt erhalten; kein `npm install` unter Program Files.

Ein Windows-Build und der Raycast-Laufzeittest wurden hier nicht durchgeführt.
