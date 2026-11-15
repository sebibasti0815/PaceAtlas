# PaceAtlas 0.20.0 – Raycast-Setup

- Der Inno-Setup-Installer übergibt den tatsächlichen EXE-Pfad an `Setup-Raycast.ps1`.
- Die Einrichtung legt `%LOCALAPPDATA%\PaceAtlas\installation.json` als Fallback für den Raycast-Autostart an. Manuelle Raycast-Pfadeinstellungen haben Vorrang.
- `npm run build` wird ausgeführt und die von Raycast generierten `*.js`-Einstiegspunkte sowie `plugin.json` werden überprüft.
- Beim erstmaligen Import kann eine manuelle Registrierung in Raycast erforderlich sein. Wenn Artefakte fehlen, meldet der Installer dies ausdrücklich statt fälschlich Erfolg zu behaupten. Nach dem Import den Startmenüeintrag „Raycast-Integration einrichten“ erneut ausführen.
- Eine vollautomatische Registrierung ohne dokumentierte Raycast-Schnittstelle ist nicht implementiert.
- Windows-Installer und Raycast-Laufzeit müssen auf dem Zielsystem getestet werden.
