# QUICK-NOTES – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="quick-notes-0-22-6"></a>

## Version 0.22.6

# PaceAtlas 0.22.6 – Schnellnotizen in WinUI

- Schnellnotizen (`Entry.Kind == "Notiz"`) erscheinen im gemeinsamen WinUI-Verlauf mit Zeitstempel und Notiztext.
- Doppelklick oder Bearbeiten öffnet einen mehrzeiligen Editor. Leere Notizen werden nicht gespeichert.
- Der bestehende Löschdialog gilt auch für Schnellnotizen.
- Speicherung erfolgt über den bestehenden `Store.Save`-Pfad; vorhandene Sync-UUIDs, Revisionen, Trigger und Tombstones werden nicht umgangen.
- Keine neue Tabelle und keine Migration erforderlich; bereits gespeicherte Schnellnotizen werden sichtbar.
- Die Versionsangaben wurden auf 0.22.6 angepasst.

Prüfung unter Windows erforderlich: Verlauf/Filter, Bearbeiten/Löschen, Raycast-Erstellung und Journal-Trigger. Der Windows-Build wurde in dieser Umgebung nicht ausgeführt.
