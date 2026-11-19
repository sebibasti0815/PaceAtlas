# Testergebnisse – 15. November 2026

Produktstand: **0.22.7.16**, auf dem Host installiert.

Der Benutzer konnte die früheren WinUI-Erinnerungsbuttons erst nach einmaligem Tab
pro Prozess bedienen. Die Erinnerung besitzt jetzt eine unabhängige native
Windows-Eingabefläche. Ein automatisches Aktivieren oder Fokussieren des Fensters
ist dafür nicht erforderlich und wird nicht ausgeführt.

| Umgebung | Frische Prozesse | Hover/Klicks ohne Tab | Fokus beim Anzeigen/Später |
| --- | ---: | ---: | --- |
| Windows Sandbox | 4 | 8/8 bestanden | unverändert |
| Installierte Host-App, drei Monitore | 4 | 8/8 bestanden | unverändert |

Jeder Button war in beiden Tray-Szenarien einmal der erste Button nach Neustart.
Anschließend wurde jeweils der andere Button am wiederverwendeten Fenster geprüft.
Die Position wurde anhand der tatsächlich gezeichneten Fläche bestimmt und das
entsprechende PointerEntered-Ereignis vor dem echten Mausklick nachgewiesen.
„Zustand erfassen“ öffnete den richtigen Reiter. „Später“ beließ das Hauptfenster im Tray.
Die Start- und Schließen-Einstellungen des Hosts wurden anschließend wiederhergestellt;
das Erinnerungsintervall wurde nicht verändert, kein Zustandseintrag gespeichert.

Die passive Anzeige wurde über WS_EX_NOACTIVATE und über Vorher-/Nachher-Abgleich
von GetForegroundWindow sowie GetGUIThreadInfo.hwndFocus geprüft. Nur ein ausdrücklich
ausgelöster Record-Klick darf die Hauptanwendung aktivieren.

Protokolle:
- `artifacts/tests/results/20261115-143925/test.log` (Sandbox)
- `artifacts/tests/results/host-native-cold/test.log` (Host)
- `artifacts/tests/results/host-native-cold/pointer-host.log` (Ereignisse)
