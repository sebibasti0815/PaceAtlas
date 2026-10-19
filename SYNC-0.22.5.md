# 0.22.5 – Eingehende Änderungen: sichere Vorprüfung

`Store.StageIncomingMutation` legt einen transaktionsgesicherten, metadatenbasierten Eingangskanal (`sync_inbox`) an. Ein SQLite-IMMEDIATE-Transaktionslock schützt die Revisionsprüfung gegen parallele Schreibvorgänge. Wiederholte Operation-IDs werden erkannt; Tombstones verhindern die Annahme einer Wiederherstellung als reguläres Create. Es werden keine Gesundheits-Payloads im Eingangskanal gespeichert.

**Wichtig:** `Accept` bedeutet hier nur, dass die Vorbedingungen zum Zeitpunkt der Prüfung erfüllt waren, **nicht**, dass die Änderung angewendet wurde. Der Eingangskanal ist kein fertiger Sync-Server. Es fehlen weiterhin authentifizierte Transportwege, validierte Payload-Mappings, atomare fachliche Anwendung, Gerätezuordnung und Konflikt-UI. Deshalb darf der Kanal noch nicht für produktive Fremdgeräte freigeschaltet werden.

Die neue Tabelle wird beim Start angelegt; vorhandene Gesundheitsdatensätze bleiben unverändert. Vor Datenbankschema-Updates ein Backup anlegen. Windows/.NET-Build und Laufzeittest stehen aus.
