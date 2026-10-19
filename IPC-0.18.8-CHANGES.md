# Raycast – Aktivitätenformular

- Vorlagenauswahl direkt im Formular „Aktivität erfassen“ ergänzt.
- Auswahl übernimmt Name, Intensität, Belastungsarten und Schutzmaßnahmen.
- Alle übernommenen Werte bleiben für den einzelnen Eintrag editierbar; die Vorlage wird nicht verändert.
- „Eigene Aktivität“ bleibt als Alternative verfügbar.
- Speicherung verwendet unverändert `activities.custom.create` und die vorhandene PaceAtlas-Geschäftslogik.

Hinweis: Das bestehende Datenmodell speichert keinen eigenständigen Vorlagenverweis im Aktivitätseintrag. Daher bleibt eine nachträgliche eindeutige Vorlagenzuordnung insbesondere bei geänderten Bezeichnungen derzeit nicht gewährleistet.
