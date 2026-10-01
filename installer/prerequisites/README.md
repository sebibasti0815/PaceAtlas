# Laufzeitdateien für das Offline-Setup

Die zwei originalen, digital signierten Microsoft-Installer hier ablegen und exakt so benennen:

- `windowsdesktop-runtime-10-x64.exe`: .NET 10 Desktop Runtime x64 von https://dotnet.microsoft.com/download/dotnet/10.0 (Abschnitt Desktop Runtime, Windows x64).
- `WindowsAppRuntimeInstall-x64.exe`: Windows App SDK Runtime 1.8 x64 von https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads (Runtime-Installer für Version 1.8, x64).

Das Buildskript prüft beide Microsoft-Signaturen. Die Binärdateien werden nicht im Quellarchiv mitgeliefert. Beim Installieren werden beide Laufzeiten unbeaufsichtigt eingerichtet; vorhandene Installationen werden vom jeweiligen Microsoft-Installer behandelt. Für das Build sind außerdem .NET 10 SDK mit WinUI-Buildunterstützung und Inno Setup 6 nötig.
