# PaceAtlas 0.20.3 – Raycast-Setup

- Korrigiert die Artefaktprüfung: `Get-MissingRaycastArtifacts` gibt die Namen einzeln zurück, statt ein verschachteltes `System.Object[]`-Array zu erzeugen.
- Bei fehlenden Artefakten nennt das Protokoll die konkreten Dateien.
- Zentrale Anwendungs-, Installer- und Raycast-Version auf 0.20.3 vereinheitlicht.
- npm-Warnungen bleiben nicht-fatal, sofern der Exitcode 0 ist.

Build: `./installer/build-installer.ps1`. Test auf Windows erforderlich.
