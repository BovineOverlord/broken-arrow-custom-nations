param([string]$GameDir = 'D:\Games\Broken Arrow')
$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath($GameDir)
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'BrokenArrow.exe'))) { throw 'BrokenArrow.exe was not found.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'MelonLoader\net6\MelonLoader.dll'))) { throw 'Install MelonLoader and launch the modded game once before installing this addon.' }
if (Get-Process BrokenArrow -ErrorAction SilentlyContinue) { throw 'Close Broken Arrow before installing.' }
$sourceDll = Join-Path $PSScriptRoot 'dist\BACustomNations.dll'
if (!(Test-Path -LiteralPath $sourceDll)) { throw 'The packaged DLL is missing. Build the source and copy its DLL to dist first.' }
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'packs\assets\zombies-flag.png'))) { throw 'The packaged zombie flag is missing.' }
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
