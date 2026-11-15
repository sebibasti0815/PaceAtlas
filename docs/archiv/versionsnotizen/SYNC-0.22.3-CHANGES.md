# PaceAtlas 0.22.3 – lokales Änderungsjournal

- Ergänzt `sync_change_journal` (append-only Metadaten: Entität, UUID, Revision, Aktion, UTC-Zeitpunkt).
- Ergänzt `sync_tombstones` für Löschungen. Ein Löschereignis erhält die nächsthöhere Revision.
- SQLite-Trigger verfolgen neue Datensätze, Änderungen und Löschungen für die acht in `SyncIdentityTables` aufgeführten Tabellen.
- Bei jedem regulären UPDATE fachlicher Spalten wird `sync_revision` erhöht. Änderungen an `sync_uuid`/`sync_revision` allein gelten nicht als fachliche Änderungen.
- Bestandsdaten der vorangegangenen UUID-Migration werden **nicht** nachträglich als neue Ereignisse ins Journal geschrieben.
- Keine aktive Synchronisierung, keine automatische Konfliktauflösung, keine Netzwerkkommunikation. Die Tombstones werden nicht automatisch gelöscht.
- Diese Version führt beim Start Schemaänderungen durch. Vorher eine eigene Sicherung der Datenbank anlegen.

## Prüfung

Die SQL-Triggerlogik wurde mit SQLite für die Folge CREATE → UPDATE → DELETE geprüft (Revisionen 1, 2, 3 und Tombstone 3). Ein vollständiger Windows/.NET-Build sowie Tests mit der produktiven Datenbank waren hier nicht möglich. Bitte zuerst mit einer Datenbankkopie testen.

## Offene Architekturarbeit

Das Journal erfasst bislang nur Metadaten. Für einen sicheren späteren Upload müssen Datensatz-Snapshots, Transaktionsgrenzen, Client-Identitäten und ein serverseitiges Revisionsprotokoll festgelegt werden. Ein Journal-Eintrag allein darf niemals als Freigabe zum Überschreiben eines Serverdatensatzes gelten.
