# Praktische Tests der passiven Erinnerung

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-TrayButtons.ps1`

Voraussetzung: aktivierte Windows Sandbox und .NET SDK 10 oder neuer. Der Befehl
baut eine eigenständige Testausgabe und startet eine frische Windows Sandbox.
Nur `artifacts/tests` wird geteilt; persönliche PaceAtlas-Daten werden nicht kopiert.
Der Standardmodus verweigert den Start außerhalb von `WDAGUtilityAccount`.

## Geprüfte Fälle

Beide Buttons sind jeweils zuerst nach einem frisch gestarteten Prozess zu prüfen,
jeweils bei direktem Tray-Start und bei normalem Öffnen mit anschließendem
Schließen über den Dialog „Ins Tray“. Danach wird der jeweils andere Button am
wiederverwendeten Erinnerungsfenster geprüft: acht Klicks insgesamt, ohne Tab.
Der Menüeintrag „Zustandserinnerung → Jetzt anzeigen“ löst die Erinnerung sofort aus.

Die gezeichnete Buttonfläche wird aus Bildschirm-Pixeln ermittelt. SendInput bewegt
die Maus zur sichtbaren Mitte; das erwartete echte PointerEntered-Ereignis muss
vor dem Klick eintreffen. Erst danach werden Bedienungshilfe-Daten abgeglichen und
ein tatsächlicher Mausklick gesendet. Alle Koordinaten sind physische Bildschirmkoordinaten.

Die Erinnerung muss WS_EX_NOACTIVATE verwenden. Erstellen und Anzeigen dürfen
weder Vordergrundfenster noch Tastaturfokus verändern. „Später“ muss beide erhalten;
„Zustand erfassen“ darf nach dem ausdrücklichen Klick die Hauptanwendung aktivieren.
Keine Prüfung sendet Tab oder speichert einen Zustandseintrag.

## Ausgaben und Aufräumen

Protokolle und auf die App begrenzte Bilder liegen unter
`artifacts/tests/results/<Zeitstempel>`. Erfolgreiche Läufe beenden ihre eigene Sandbox
und löschen erzeugte Programmkopien aus `artifacts/tests/runtime`. Fehlgeschlagene
Läufe bleiben zur Diagnose geöffnet. `-KeepSandbox` erhält auch erfolgreiche Läufe.

`reminder-input-diagnostics.enabled` im WinUI-Einstellungsordner aktiviert ausschließlich
Zeitstempel und Ereignisnamen in `reminder-input.log`; keine Eintragsdaten.
Der Sandbox-Test verwaltet die Datei selbst.

## Explizite Host-Prüfung

`TrayUiTests.exe --host-cold-suite <installierte EXE> <Ergebnisordner>` setzt voraus,
dass die bestehende App zuvor regulär beendet wurde und kein Pacing-Timer läuft.
Der Modus startet nur die ausgewählte Installation, prüft dieselben acht Fälle und
stellt Start- und tägliche Schließen-Einstellungen bytegenau wieder her. Er speichert
keine Einträge und verändert das Erinnerungsintervall nicht. Einstellungsbackups und
Ergebnisse werden im angegebenen Ordner gesichert. Es wird kein Host-Prozess erzwungen beendet.
