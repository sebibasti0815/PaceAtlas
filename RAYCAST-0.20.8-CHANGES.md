# PaceAtlas 0.20.8 – Raycast

- Laufende Aktivitäten: Beim Beenden erscheint ein Formular mit editierbarer Endzeit (HH:mm), vorbelegt beim Öffnen mit der aktuellen lokalen Uhrzeit. Notizen lassen sich ergänzen.
- Die API `activities.stop` akzeptiert optional `end` im Format `yyyy-MM-ddTHH:mm`; ohne Angabe bleibt der bisherige Jetzt-Zeitpunkt erhalten. Start-/Endzeit-Reihenfolge und Zukunftszeiten werden serverseitig geprüft.
- Verlauf: Beim ersten Laden wird der neueste Eintrag ausgewählt. Nachladen erhält die aktuelle Auswahl, Filterwechsel wählt bei Bedarf den ersten sichtbaren Eintrag.

Die Änderungen wurden statisch geprüft; ein Windows-/Raycast-Laufzeittest ist noch erforderlich.
