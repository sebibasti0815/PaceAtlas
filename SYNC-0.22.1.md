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
