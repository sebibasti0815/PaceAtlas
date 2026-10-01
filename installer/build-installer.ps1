param(
    [string] $InnoCompiler
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') { throw 'Das Setup muss unter Windows gebaut werden.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $root 'Directory.Build.props'
[xml] $props = Get-Content -LiteralPath $versionFile -Raw
$version = [string] $props.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Ungueltige Versionsnummer in $versionFile" }

$prerequisites = @(
    'windowsdesktop-runtime-10-x64.exe',
    'WindowsAppRuntimeInstall-x64.exe'
)
foreach ($name in $prerequisites) {
    $file = Join-Path $PSScriptRoot "prerequisites\$name"
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Fehlende Laufzeitdatei: $file (siehe installer\prerequisites\README.md)"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status -ne 'Valid' -or
        $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
        throw "Die Microsoft-Signatur konnte nicht verifiziert werden: $file ($($signature.Status))"
    }
}

if (-not $InnoCompiler) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $InnoCompiler = $found.Source }
    else {
        $InnoCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    }
}
if (-not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw 'Inno Setup 6 (ISCC.exe) nicht gefunden. -InnoCompiler mit vollständigem Pfad angeben.'
}

$project = Join-Path $root 'PaceAtlas.WinUI\PaceAtlas.WinUI.csproj'
$output = Join-Path $root 'artifacts\installer'
$publish = Join-Path $output ("publish-$version-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $publish | Out-Null
& dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:WindowsAppSDKSelfContained=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist fehlgeschlagen.' }
if (-not (Test-Path -LiteralPath (Join-Path $publish 'PaceAtlas.WinUI.exe'))) {
    throw 'Die veröffentlichte WinUI-Anwendung fehlt.'
}

$script = Join-Path $PSScriptRoot 'PaceAtlas.iss'
& $InnoCompiler "/DAppVersion=$version" "/DPublishDir=$publish" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup konnte das Setup nicht erstellen.' }
$setup = Join-Path $output "PaceAtlas-Setup-$version-win-x64.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw "Setup nicht gefunden: $setup" }
Write-Host "Fertig: $setup"
