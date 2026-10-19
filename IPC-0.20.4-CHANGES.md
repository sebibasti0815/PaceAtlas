# 0.20.4 – Aktivitätsnamen in Raycast

- `activities.active.list` gibt jetzt `name` zusätzlich zu den bestehenden Feldern zurück.
- Die WinUI speichert bislang keinen Vorlagenverweis in Aktivitätseinträgen. Deshalb wird eine Vorlage nur bei eindeutiger Übereinstimmung von Intensität, Belastungsarten und Schutzmaßnahmen angezeigt. Bei mehrdeutigen Einträgen werden die Belastungsarten verwendet, statt einen falschen Vorlagennamen zu behaupten.
- Raycast zeigt den vom Server gelieferten Namen und die Startzeit als HH:mm.
- Die Datenbank und bestehende Einträge werden nicht verändert.
- Für eine garantierte Vorlagenzuordnung bei zukünftigen WinUI-Einträgen wäre eine persistierte Template-ID nötig.
