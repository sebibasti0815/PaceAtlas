# PaceAtlas 0.22.2 – Lokale Identitäten für spätere Synchronisierung

- Relevante lokale Tabellen (`entries`, `medication_plan`, `medication_products`, `stock_movements`, `foods`, `food_rules`, `meals`, `ai_analyses`) erhalten `sync_uuid` und `sync_revision`.
- Vor der ersten Schemaerweiterung wird eine konsistente SQLite-Sicherung unter `paceatlas.db.pre-sync-identity.bak` erstellt. Bereits vorhandene Sicherungen werden nicht überschrieben.
- Vorhandene Datensätze erhalten einmalig UUIDs; numerische Primärschlüssel bleiben erhalten. Eindeutige Indizes verhindern doppelte UUIDs pro Tabelle.
- Neue Datensätze bekommen auch bei älteren Einfügepfaden automatisch eine UUID durch SQLite-Trigger.
- Die Migration ist wiederholbar und läuft für die Identitätsspalten in einer Transaktion.

## Noch nicht implementiert

`sync_revision` wird initial auf 1 gesetzt, aber **noch nicht bei Änderungen fortgeschrieben**. Löschungen werden noch physisch durchgeführt; Tombstones, Änderungsjournal, Konfliktprüfung beim Schreiben und eine Server-Synchronisierung folgen erst später. Die Datenbank ist daher **noch nicht synchronisationsfähig**. Externe Referenzkataloge (`bls_foods`, `off_foods`, `gi_references`) werden nicht individuell synchronisiert.

## Testhinweise

Vor dem Upgrade zusätzlich ein eigenes Backup anfertigen. Nach der Installation Start, Bestandsdaten, Neu-Erfassung, wiederholten Start und die neue Backup-Datei prüfen. Ein Windows-.NET-Build war in der Erstellungsumgebung nicht möglich.
