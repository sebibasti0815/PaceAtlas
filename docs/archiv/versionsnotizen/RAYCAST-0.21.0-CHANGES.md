# PaceAtlas 0.21.0 – Raycast Command Center (erste Ausbaustufe)

- Neuer Raycast-Befehl „Schnellaktionen“ mit Verweisen auf die bisherigen Befehle, Tagesstatus, Pacing und Ruhe sowie kontextbezogenen laufenden Aktivitäten und den letzten fünf Einträgen.
- Ruhephasen können über IPC gestartet und beendet werden. Ende und Beginn sind editierbar und auf die lokale Uhrzeit vorbelegt. Die Daten werden als bestehende `Ruhe`-Einträge gespeichert.
- Neuer Tagesstatus: letzter dokumentierter Zustand, Anzahl und Zeit der Aktivitäten und Ruhephasen sowie offene Medikamente. Nicht erfasste Zeit wird nicht als Ruhe interpretiert.
- Pacing-Timer: IPC-Kommandos lesen und steuern die **existierende** WinUI-Timerinstanz via Dispatcher. Keine zweite Timerimplementierung.
- Kombinierte Schnellaktion „Aktivität beenden und Ruhe starten“ mit identischem Übergangszeitpunkt. **Noch nicht atomar**: Bei einem Schreibfehler nach Beenden der Aktivität kann die Ruhephase fehlen. Diese Aktion erst nach Test produktiv nutzen.
- Verlauf: Start und Ende stehen in einer Zeile; gleiches Enddatum wird weggelassen.
- Die weiteren Funktionen des genehmigten Ausbaus (Ernährung, Schmerz-Schnellerfassung, Lebensmittelprüfung, PEM-Hinweise, Symptomverlauf, Wiederholungen) sind für 0.21.1/0.21.2 vorgesehen und **nicht** Bestandteil dieser ZIP.

## Testschwerpunkte

1. Timerstatus und Timersteuerung aus Raycast mit gleichzeitig geöffneter WinUI prüfen.
2. Ruhephasen erstellen und beenden; anschließend in WinUI prüfen.
3. Tagesstatus auf Plausibilität prüfen, insbesondere laufende Intervalle.
4. Schnellaktionen und Verlauf öffnen.
5. Die kombinierte Aktion vorerst nur mit Testdaten ausführen.

Nicht unter Windows kompiliert oder zur Laufzeit getestet.
