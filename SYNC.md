# SYNC – Versionshistorie

Die neuesten Versionen stehen zuerst. Die bisherigen Änderungsnotizen sind vollständig übernommen.

<a id="sync-0-22-7"></a>

## Version 0.22.7

# PaceAtlas 0.22.7 – Lokale atomare Anwendung von Synchronisationsänderungen

- `Store.ApplyIncomingEntryMutation(SyncMutation)` ist eine explizite, bislang nicht von einem Netzwerkendpunkt aufgerufene Core-Methode.
- Unterstützt ausschließlich `entries` (Zustände, Aktivitäten, Ruhe und Schnellnotizen), nicht Medikamente/Ernährung/andere Tabellen.
- `SyncEntryPayload` wird als JSON im `SyncMutation.Payload` übergeben (Kind, Start, End, Data, Note).
- Prüfung des aktuellen Revisionsstands und der Tombstones erfolgt innerhalb einer SQLite-IMMEDIATE-Transaktion.
- Erstellen/Ändern/Löschen und Idempotenzbeleg in `sync_applied_operations` werden atomar committed; bei Fehler erfolgt Rollback.
- Vorhandene SQLite-Trigger pflegen Revisionen, Änderungsjournal und Tombstones. Bestehende `StageIncomingMutation`-Belege sind **keine** Commit-Belege.
- Identische Operation-IDs werden als `AlreadyApplied` quittiert, selbst wenn die Anfrage erneut eintrifft.
- Konflikte werden nicht automatisch überschrieben. Es gibt weiterhin **keine** aktive Netzwerk-Synchronisierung.
- Für einen späteren Server sind Authentifizierung, Autorisierung pro Benutzer, Eingabegrenzen und echte Integrationstests erforderlich.
- WICHTIG: Dies ist ein technischer Zwischenstand. Vor dem Upgrade die SQLite-Datenbank sichern. Windows/.NET-Build und Tests mit produktiven Daten stehen aus.

<a id="sync-0-22-5"></a>

## Version 0.22.5

# 0.22.5 – Eingehende Änderungen: sichere Vorprüfung

`Store.StageIncomingMutation` legt einen transaktionsgesicherten, metadatenbasierten Eingangskanal (`sync_inbox`) an. Ein SQLite-IMMEDIATE-Transaktionslock schützt die Revisionsprüfung gegen parallele Schreibvorgänge. Wiederholte Operation-IDs werden erkannt; Tombstones verhindern die Annahme einer Wiederherstellung als reguläres Create. Es werden keine Gesundheits-Payloads im Eingangskanal gespeichert.

**Wichtig:** `Accept` bedeutet hier nur, dass die Vorbedingungen zum Zeitpunkt der Prüfung erfüllt waren, **nicht**, dass die Änderung angewendet wurde. Der Eingangskanal ist kein fertiger Sync-Server. Es fehlen weiterhin authentifizierte Transportwege, validierte Payload-Mappings, atomare fachliche Anwendung, Gerätezuordnung und Konflikt-UI. Deshalb darf der Kanal noch nicht für produktive Fremdgeräte freigeschaltet werden.

Die neue Tabelle wird beim Start angelegt; vorhandene Gesundheitsdatensätze bleiben unverändert. Vor Datenbankschema-Updates ein Backup anlegen. Windows/.NET-Build und Laufzeittest stehen aus.

<a id="sync-0-22-4"></a>

## Version 0.22.4

# PaceAtlas 0.22.4 — Lokaler Sync-Lesekanal

- `ReadSyncJournal`: bounded, cursor-based, metadata-only journal pages; stable snapshot watermark.
- `ReadSyncRecordState`: current revision or tombstone lookup with table allowlist.
- No server, network transport, remote writes, or health-data export yet.
- Important: journal events do not include record payloads; a future secure authenticated transport must resolve data and apply mutations transactionally with deduplication and revision checks.
- Future work: transactional inbound apply, operation-id deduplication, conflict storage, deletion propagation and automated integration tests.
- Test on Windows with `dotnet build PaceAtlas.sln -c Release` before deployment.

<a id="sync-0-22-3-changes"></a>

## Version 0.22.3 – CHANGES

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

<a id="sync-0-22-2"></a>

## Version 0.22.2

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

<a id="sync-0-22-1"></a>

## Version 0.22.1

# PaceAtlas 0.22.1 – Synchronisationsverträge (Vorbereitung)

Diese Stufe enthält **keine aktive Synchronisierung** und **keine Datenbankmigration**. Sie legt bewusst konservative, transportneutrale Vorbedingungen im Namespace `PaceAtlas.Sync` fest.

- `Create` funktioniert auch bei stark veraltetem lokalem Datenbestand, sofern die Datensatz-UUID neu ist.
- `Update` und `Delete` erfordern die exakt aktuelle Revision; veraltete Änderungen werden nicht automatisch übernommen.
- Ein Tombstone blockiert Wiederherstellung durch `Create` oder `Update` mit derselben UUID.
- `OperationId` dient als Schlüssel für serverseitige idempotente Wiederholungen. Der Server muss dessen Prüfung und die Mutation **in einer Transaktion** durchführen.
- Konflikte müssen die Erfassung neuer Daten **nicht blockieren**; die spätere Benutzeroberfläche kann sie zur späteren Bearbeitung sicher zurückstellen.

## Noch erforderlich vor produktiver Synchronisierung

1. Bestandsdaten einmalig mit stabilen UUIDs ausstatten und eine idempotente Migration testen.
2. Tombstones und monotone Revisionen dauerhaft persistieren; Löschmarkierungen nicht ohne Resync-Protokoll entsorgen.
3. Transaktionale Operation-ID-Deduplizierung, Benutzertrennung und Geräteauthentifizierung auf dem Server implementieren.
4. Lokales Änderungsjournal/Outbox, Cursor-basierter Pull, Retry und Offline-Konfliktablage.
5. Tests für Monate offline befindliche Geräte, Löschen/Ändern auf mehreren Geräten, Wiederholungsrequests und Ausfälle mitten im Commit.
6. Auf Synology Linux containerisierten ASP.NET-Core-Dienst über den bestehenden Reverse Proxy sicher bereitstellen.

Die bisherigen IPC- und WinUI-Funktionen werden durch diese Vorbereitungsstufe nicht verändert.
