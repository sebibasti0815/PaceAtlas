[CmdletBinding()]
param([switch]$KeepSandbox, [switch]$Interventions)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = Join-Path $root 'artifacts\tests'
$appOutput = Join-Path $testRoot 'runtime\app'
$runnerOutput = Join-Path $testRoot 'runtime\runner'
$resultOutput = Join-Path $testRoot ('results\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $appOutput,$runnerOutput,$resultOutput | Out-Null
Push-Location $root
try {
    & dotnet publish PaceAtlas.WinUI/PaceAtlas.WinUI.csproj -c Debug -p:Platform=x64 -p:WindowsAppSDKSelfContained=true --self-contained true -o $appOutput -v:quiet
    if ($LASTEXITCODE -ne 0) { throw 'PaceAtlas-Testbuild fehlgeschlagen.' }
    $build = Join-Path $root 'PaceAtlas.WinUI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64'
    foreach ($name in @('App.xbf','MainWindow.xbf','PaceAtlas.WinUI.pri')) {
        Copy-Item -LiteralPath (Join-Path $build $name) -Destination $appOutput -Force
    }
    & dotnet publish tools/TrayUiTests/TrayUiTests.csproj -c Release -r win-x64 --self-contained true -o $runnerOutput -v:quiet
    if ($LASTEXITCODE -ne 0) { throw 'UI-Testrunner-Build fehlgeschlagen.' }
    if (!(Get-Command wsb -ErrorAction SilentlyContinue)) {
        throw 'Windows Sandbox CLI (wsb) ist nicht verfügbar. Testbuild erstellt, aber kein Oberflächentest ausgeführt.'
    }
    $sandbox = (& wsb start --raw | ConvertFrom-Json).Id
    if (!$sandbox) { throw 'Windows Sandbox konnte nicht gestartet werden.' }
    $sandbox | Set-Content (Join-Path $resultOutput 'sandbox-id.txt')
    & wsb share --id $sandbox -f $testRoot -s 'C:\PaceAtlasTest' --allow-write
    if ($LASTEXITCODE -ne 0) { throw 'Testordner konnte nicht freigegeben werden.' }
    & wsb connect --id $sandbox
    $relativeResults = 'results\' + (Split-Path $resultOutput -Leaf)
    $mode = if ($Interventions) { '--interventions ' } else { '' }
    $expected = if ($Interventions) { 'PASS interventions' } else { 'PASS 8/8' }
    $command = "C:\PaceAtlasTest\runtime\runner\TrayUiTests.exe ${mode}C:\PaceAtlasTest\runtime\app\PaceAtlas.WinUI.exe C:\PaceAtlasTest\$relativeResults"
    for ($attempt=0; $attempt -lt 12; $attempt++) {
        & wsb exec --id $sandbox -r ExistingLogin -c $command
        if (Test-Path (Join-Path $resultOutput 'status.txt')) { break }
        Start-Sleep -Seconds 5
    }
    $statusPath = Join-Path $resultOutput 'status.txt'
    if (!(Test-Path $statusPath)) { throw 'Die interaktive Testsitzung ist nicht bereit.' }
    $status = Get-Content $statusPath -Raw
    Write-Host "Ergebnis: $status"
    Write-Host "Protokoll und Screenshots: $resultOutput"
    if (!$KeepSandbox -and $status.Trim() -eq $expected) {
        & wsb stop --id $sandbox
        if ($LASTEXITCODE -eq 0) {
            $runtime = Join-Path $testRoot 'runtime'
            $resolvedRuntime = (Resolve-Path -LiteralPath $runtime).Path
            $resolvedRoot = (Resolve-Path -LiteralPath $testRoot).Path.TrimEnd('\')
            if ([IO.Path]::GetDirectoryName($resolvedRuntime) -ne $resolvedRoot -or
                [IO.Path]::GetFileName($resolvedRuntime) -ne 'runtime') { throw 'Unerwarteter Laufzeitpfad.' }
            # Only our regenerated test runtime, after its own Sandbox has stopped.
            Remove-Item -LiteralPath $resolvedRuntime -Recurse -Force
        }
    }
    if ($status.Trim() -ne $expected) { throw 'UI-Test fehlgeschlagen; Sandbox zur Diagnose geöffnet gelassen.' }
} finally { Pop-Location }
