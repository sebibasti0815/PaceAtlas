# PaceAtlas 0.22.4 — Lokaler Sync-Lesekanal

- `ReadSyncJournal`: bounded, cursor-based, metadata-only journal pages; stable snapshot watermark.
- `ReadSyncRecordState`: current revision or tombstone lookup with table allowlist.
- No server, network transport, remote writes, or health-data export yet.
- Important: journal events do not include record payloads; a future secure authenticated transport must resolve data and apply mutations transactionally with deduplication and revision checks.
- Future work: transactional inbound apply, operation-id deduplication, conflict storage, deletion propagation and automated integration tests.
- Test on Windows with `dotnet build PaceAtlas.sln -c Release` before deployment.
