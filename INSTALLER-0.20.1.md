# PaceAtlas 0.20.1 – Raycast-Autostart-Pfaderkennung

- Die Erweiterung prüft installation.json über LOCALAPPDATA, USERPROFILE, APPDATA und os.homedir().
- UTF-8-BOM wird beim Lesen akzeptiert.
- Standardpfade werden auch dann geprüft, wenn Raycast ProgramFiles-Umgebungsvariablen nicht bereitstellt.
- Bei fehlender EXE enthält die Fehlermeldung die geprüften Pfade.
- Benutzerdefinierte Pfade haben unverändert Vorrang.

Windows-/Raycast-Laufzeittest steht noch aus. Das Paket verändert weder die SQLite-Daten noch den IPC-Server.
