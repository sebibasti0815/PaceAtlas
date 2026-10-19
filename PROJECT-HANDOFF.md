# PaceAtlas – gemeinsamer Projektstand und Übergabe

## Basis
Die hier enthaltene Solution ist die zuletzt bereitgestellte Version 0.21.7. Sie wurde in dieser Umgebung nicht unter Windows gebaut oder getestet. Dies ist eine **Projektübergabe**, keine Implementierung der noch offenen 0.21.8-Funktionen.

## Leitmotive
- PaceAtlas soll dem Anwender Energie sparen, nicht Energie kosten.
- Komplexität gehört in die Architektur, nicht in die Bedienung.
- Gesundheitsdaten und technische Diagnoseinformationen bleiben in der normalen Benutzeroberfläche strikt getrennt.

## Produktrollen
- WinUI: vollständiges Konfigurations-, Analyse- und Diagnoseinterface ("Master of Disaster").
- Raycast: reizarmer Schnellzugriff für Erfassung und kurze Abfragen.
- Web/PWA und Smartphone: offlinefähige, einfache Alltagsclients.
- Linux/.NET-Server (Synology, Reverse Proxy): zentrale Synchronisierung, Authentifizierung und Geräteverwaltung, kein Zwang zu Onlinebetrieb.

## Noch offene unmittelbare Arbeiten (0.21.8)
1. Schmerz-Schnellerfassung nicht als Freitext-Schnellnotiz speichern, sondern über das bestehende Zustandsschema: Allgemeinzustand, PEM, Crash, Schmerzorte, optionale Notiz; Puls nicht angegeben; sämtliche nicht abgefragten Symptome explizit `-1` = nicht beurteilt. Vor Änderung die tatsächliche Persistenz und Validierung von Schmerzorten prüfen.
2. Einheitliche Erfassungsmetadaten `CaptureMode` (Full/Quick) und `CaptureSource` (WinUI/Raycast/Api/Web/Android/iOS) für Zustände und Aktivitäten. Bestehende Datensätze nach Nutzerentscheidung einmalig als WinUI/Full migrieren; Migration idempotent, bereits gesetzte Metadaten nicht überschreiben. Diese Zuordnung ist eine Konvention, kein historischer Nachweis.
3. Tests für strukturierte Speicherung, alte Einträge, Auswertungen und migrationssichere Updates.

## Weiterer Funktions- und Technik-Backlog
- PEM-Hinweise und Schmerzbewertung auf fachlich korrekte Auswertung prüfen.
- Eindeutige Vorlagen-ID für Aktivitäten statt heuristischer Namensauflösung.
- Unterschiedliche Intensitätsskalen konsolidieren.
- Schnellnotizen im WinUI-Verlauf sichtbar machen.
- Kombinierte Aktion Aktivität beenden + Ruhe starten transaktionssicher machen.
- Optional vollständige Mahlzeitenerfassung mit mehreren Zutaten und Mengen; Ein-Lebensmittel-Variante ausdrücklich verworfen.
- System & Diagnose: strukturierte Logs, Korrelations-IDs, IPC/API-Latenzen, Sync-Status, Konflikte, Integrität, Backup, Diagnosepaket ohne Gesundheits-/Zugangsdaten.

## Synchronisierung – verbindliche Invarianten
- Neue Offline-Einträge dürfen unabhängig vom Alter des lokalen Bestands erzeugt werden; clientseitige UUID und idempotente Übertragung.
- Keine ungeprüfte Überschreibung älterer Revisionen und keine Wiederbelebung gelöschter Datensätze.
- Tombstones, Revisions-/Versionsprüfung, Änderungsjournal und explizite Konfliktbehandlung; niemals pauschal "last write wins" für Gesundheitsdaten.
- Konflikte ohne Unterbrechung der Erfassung sicher zurückstellen; lokale Speicherung muss auch bei Netzwerkausfall funktionieren.
- Sync-Server ist Koordinator, nicht notwendige Laufzeitvoraussetzung für lokale Erfassung.
- Pro Benutzer strikt getrennte persönliche Daten; keine Kliniksoftware oder gemeinsamen Patientendatensätze.

## Übergabe und Tests
Die Windows-Anwendung und Raycast-Erweiterung sind vom Nutzer bis Version 0.21.6 in weiten Teilen erfolgreich getestet worden; 0.21.7 ist eine bereitgestellte, aber hier nicht bestätigte Entwicklungsbasis. Vor der nächsten ZIP zuerst den tatsächlich verwendeten Quellstand und die Änderungen verifizieren; keine neue Versionsnummer ohne Quellcodeänderung und keine Erfolgsbehauptung ohne Test.
