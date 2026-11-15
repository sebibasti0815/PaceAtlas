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
