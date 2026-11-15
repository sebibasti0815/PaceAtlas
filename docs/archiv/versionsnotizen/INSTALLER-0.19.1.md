# PaceAtlas 0.19.1 – Raycast-Installation im Benutzerprofil

Die Raycast-Quelldateien liegen weiterhin geschützt unter `{app}\Raycast`. Das Setup-Skript wird als interaktiver Benutzer ausgeführt und kopiert `package.json`, `tsconfig.json`, `src` und `assets` nach `%LOCALAPPDATA%\PaceAtlas\Raycast`. `npm install` (oder `npm ci`, wenn ein mitgeliefertes Lockfile existiert) und `npm run build` laufen **ausschließlich dort**.

Bei erneuter Einrichtung werden verwaltete Quelldateien aktualisiert; Raycast-Benutzereinstellungen und andere Erweiterungen werden nicht verändert. Die Registrierung über **Raycast → Import Extension** bleibt ein manueller Schritt; ob ein erneuter Import bei einem Update erforderlich ist, hängt von Raycast ab.

Zum Test: Installer bauen (`.\installer\build-installer.ps1`), Raycast-Komponente auswählen, Einrichtung ausführen, `%LOCALAPPDATA%\PaceAtlas\Raycast` auf `node_modules` und Build-Artefakte prüfen, anschließend aus diesem Ordner importieren. Der Installer wurde hier nicht unter Windows ausgeführt.

Hinweis: Die Startmenü-Verknüpfung wird von Inno Setup systemweit angelegt. Bei Mehrbenutzerinstallationen muss die Einrichtung im jeweiligen Benutzerkonto erfolgen. Ein Installationsupdate allein aktualisiert noch nicht automatisch jedes Benutzerprofil.
