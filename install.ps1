#Requires -Version 5.1
param([string]$GameDir, [switch]$NonInteractive)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts\Find-BrokenArrow.ps1')
Write-Host 'Custom Nations installer (precompiled mod; no compilation required)'
$gameRoot = Find-BrokenArrow -ExplicitPath $GameDir -PackageRoot $PSScriptRoot -NonInteractive:$NonInteractive
Write-Host ('Game folder: ' + $gameRoot)
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'MelonLoader\net6\MelonLoader.dll'))) { throw 'Install Local Skirmish first from https://github.com/BovineOverlord/broken-arrow-local-skirmish and launch the modded game once. Then run install.bat again.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'Mods\BALocalSkirmish.dll'))) { throw 'Local Skirmish is required for offline battles. Install it from https://github.com/BovineOverlord/broken-arrow-local-skirmish, then run install.bat again.' }
if (Get-Process BrokenArrow -ErrorAction SilentlyContinue) { throw 'Close Broken Arrow before installing.' }
$sourceDll = Join-Path $PSScriptRoot 'dist\BACustomNations.dll'
if (!(Test-Path -LiteralPath $sourceDll)) { throw 'The precompiled DLL is missing. Download the complete repository ZIP from GitHub and choose Extract All before running install.bat. No compilation is needed.' }
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'packs\assets\zombies-flag.png'))) { throw 'The packaged zombie flag is missing. Extract the complete GitHub ZIP before installing.' }
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'packs\zombies.json'))) { throw 'The packaged zombie definition is missing. Extract the complete GitHub ZIP before installing.' }
$modsDir = Join-Path $gameRoot 'Mods'
$packDir = Join-Path $gameRoot 'UserData\BACustomNations\packs'
New-Item -ItemType Directory -Path $modsDir,$packDir -Force | Out-Null
$destinationDll = Join-Path $modsDir 'BACustomNations.dll'
if (Test-Path -LiteralPath $destinationDll) {
    $backupDir = Join-Path $gameRoot 'UserData\BACustomNations\backups'
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $destinationDll -Destination (Join-Path $backupDir ('BACustomNations-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.dll'))
}
Copy-Item -LiteralPath $sourceDll -Destination $destinationDll -Force
$destinationPack = Join-Path $packDir 'zombies.json'
if (!(Test-Path -LiteralPath $destinationPack)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packs\zombies.json') -Destination $destinationPack
}
$assetDir = Join-Path $packDir 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$flagDestination = Join-Path $assetDir 'zombies-flag.png'
if (!(Test-Path -LiteralPath $flagDestination)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packs\assets\zombies-flag.png') -Destination $flagDestination
}
Write-Host 'Custom Nations 0.1.1 installed. Launch with your existing offline modded shortcut.'
Write-Host 'Existing nation definitions and flag edits were preserved. See docs\CREATING-A-NATION.md for the modder guide.'
