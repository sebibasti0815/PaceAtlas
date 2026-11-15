# PaceAtlas 0.21.15 – Systemstatus und Datenbankwartung

- Neue Schaltfläche **Datenbank komprimieren** neben **Datenbank prüfen**.
- Bestätigung, Prüfung freien Speicherplatzes, konsistente SQLite-Online-Sicherung mittels `BackupDatabase`, danach `VACUUM` und `PRAGMA quick_check`.
- Laufzeit und Größen vorher/nachher werden angezeigt; Sicherheitskopie verbleibt neben der Datenbank.
- Neue Schaltfläche **Systemstatus**: Prozess-ID, RAM, Datenbankdatei, Raycast-Build-Dateien und IPC-Diagnosemodus.

## Wichtige Einschränkung

Die Wartung ist noch **nicht** gegen parallele Schreibzugriffe sämtlicher PaceAtlas-Komponenten zentral gesperrt. SQLite serialisiert Schreibtransaktionen; unter Last kann `VACUUM` mit `SQLITE_BUSY` fehlschlagen. Bitte die Wartung nur durchführen, wenn keine Erfassungen laufen, und vorher zusätzlich die reguläre Datensicherung prüfen. Die Sicherung wird nicht automatisch gelöscht. Die Systemstatusanzeige prüft lokale Artefakte, nicht die tatsächliche IPC-Erreichbarkeit.

Windows-Build und Laufzeittest ausstehend.
