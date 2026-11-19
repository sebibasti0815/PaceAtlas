# REMINDER – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

## Version 0.22.7.16

- Die Erinnerung verwendet eine eigene native Windows-Eingabefläche mit echten Windows-Buttons. Sie benötigt keinen zuvor durch Tab initialisierten WinUI-Fokus mehr.
- Anzeige mit WS_EX_NOACTIVATE und SW_SHOWNOACTIVATE; auch Mausklicks aktivieren die Erinnerung nicht. Nur der ausdrücklich gewählte Befehl „Zustand erfassen“ öffnet die Hauptanwendung.
- Hover und Zeichnen benutzen dieselben nativen Buttonrechtecke. „Später“ lässt Vordergrundfenster und Tastaturfokus unverändert.
- Regression: beide Buttons jeweils zuerst nach frischem Prozessstart in beiden Tray-Szenarien, anschließend Wiederholung; keine Tab-Eingabe. Acht echte Hover-/Klickprüfungen plus Prüfung des unveränderten Vordergrund- und Tastaturfokus.
- Testbilder werden auf die jeweilige Anwendung begrenzt; keine riesigen Vollbildkopien mehr.

## Version 0.22.7.14

- Erinnerungsfenster mit regulärem Windows-Rahmen und konsistenter Clientfläche; clientbezogene Größe und Position nach dem Laden des WinUI-Inhalts erneut abgestimmt.
- Keine manuelle Vermessung einer noch nicht verbundenen XAML-Struktur mehr.
- Anzeigedauer auf zwei Minuten verlängert; während die Maus im Erinnerungsinhalt steht, pausiert das automatische Ausblenden.
- Tray-Menü „Zustandserinnerung → Jetzt anzeigen“ für unmittelbar wiederholbare Prüfungen.
- Pointer-Diagnose erfasst auch bereits von WinUI behandelte Ereignisse.
- Regressionstest lokalisiert die sichtbare Buttonfläche anhand von Bildpixeln und prüft dort zusätzlich das echte PointerEntered-Ereignis vor dem Klick. Diagnoseprotokoll nur bei ausdrücklich aktivierter lokaler Markierungsdatei.

## Version 0.22.7.11

- Wiederholbarer Windows-Sandbox-Test für beide Erinnerungsbuttons in beiden Tray-Szenarien, mit echten Mausklicks, Zustandsprüfungen und Screenshots.
- Alle vier Prüfungen erfolgreich, einschließlich Schließen-Dialog „Ins Tray“ und ohne künstliche Aktivierung vor dem Klick.
- Zusätzlicher Host-Test: „Später“ reagierte in der laufenden Installation 0.22.7.8. Der gemeldete Ausfall ist weiterhin nicht reproduziert; keine neue funktionale Buttonkorrektur behauptet.
- Aufruf und Grenzen: `tools/TrayUiTests/README.md` und `tools/TrayUiTests/RESULTS.md`.

## Version 0.22.7.8

- Das Erinnerungsfenster wird über WinUI aktiviert, damit die Buttons auch nach einem direkten Start im Tray Eingaben verarbeiten können.
- Nur das Erinnerungsfenster erhält den Fokus. „Später“ blendet es aus; „Zustand erfassen“ öffnet das Hauptfenster zur Eingabe.
- Die frühere direkte HWND-Anzeige wurde entfernt. Praktischer Klicktest nach Tray-Start steht aus.

<a id="reminder-0-22-7-2"></a>

## Version 0.22.7.2

# PaceAtlas 0.22.7.2 – Zustandserinnerung

- Vollständig deckender Dialoghintergrund ohne seitlichen Akzentstreifen.
- Flexible Schaltflächenbreiten und größere vertikale Reserve gegen Abschneiden bei gemischter DPI-Skalierung.
- Dialoghöhe aus gemessenem Inhalt zuzüglich Sicherheitsabstand.
- Verhalten der Erinnerung und Tray-Wiederherstellung unverändert.

Hinweis: Windows-/WinUI-Laufzeittest erforderlich.
