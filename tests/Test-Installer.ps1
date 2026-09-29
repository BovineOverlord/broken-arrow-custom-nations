#Requires -Version 5.1
param([string]$TestRoot = (Join-Path $env:TEMP ('BACustomNations-installer-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts\Find-BrokenArrow.ps1')
New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
$game = Join-Path $TestRoot 'Game with spaces'
New-Item -ItemType Directory -Path $game,(Join-Path $game 'Mods'),(Join-Path $game 'MelonLoader\net6') -Force | Out-Null
foreach ($relative in @('BrokenArrow.exe','Mods\BALocalSkirmish.dll','MelonLoader\net6\MelonLoader.dll')) {
    [IO.File]::WriteAllText((Join-Path $game $relative),'fixture')
}
function Require($Condition, [string]$Message) { if (-not $Condition) { throw $Message }; Write-Host ('PASS ' + $Message) }
$installer = Join-Path $repo 'install.ps1'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -GameDir $game -NonInteractive
Require ($LASTEXITCODE -eq 0) 'fresh install succeeds under Windows PowerShell 5.1'
$installed = Join-Path $game 'Mods\BACustomNations.dll'
Require ((Get-FileHash -LiteralPath $installed).Hash -eq (Get-FileHash -LiteralPath (Join-Path $repo 'dist\BACustomNations.dll')).Hash) 'precompiled DLL copied without a build'
$json = Join-Path $game 'UserData\BACustomNations\packs\zombies.json'
$flag = Join-Path $game 'UserData\BACustomNations\packs\assets\zombies-flag.png'
Require ((Test-Path -LiteralPath $json) -and (Test-Path -LiteralPath $flag)) 'default pack and flag installed'
[IO.File]::WriteAllText($json,'{"user-edit":"preserve"}')
[IO.File]::WriteAllText($flag,'user flag fixture')
$jsonHash=(Get-FileHash -LiteralPath $json).Hash
$flagHash=(Get-FileHash -LiteralPath $flag).Hash
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -GameDir $game -NonInteractive
Require ($LASTEXITCODE -eq 0) 'repeat install succeeds'
Require ((Get-FileHash -LiteralPath $json).Hash -eq $jsonHash) 'custom JSON preserved'
Require ((Get-FileHash -LiteralPath $flag).Hash -eq $flagHash) 'custom flag preserved'
Require (@(Get-ChildItem -LiteralPath (Join-Path $game 'UserData\BACustomNations\backups') -Filter '*.dll').Count -eq 1) 'previous DLL backed up'
Require ((Find-BrokenArrow -ExplicitPath ('"'+$game+'"') -PackageRoot $repo -NonInteractive) -eq $game) 'quoted explicit path with spaces resolves'
Require (-not (Test-BrokenArrowFolder 'Z:\nonexistent-bacn-test')) 'missing drive is tolerated'
$rejected = $false
try { Find-BrokenArrow -ExplicitPath (Join-Path $TestRoot 'missing') -PackageRoot $repo -NonInteractive | Out-Null } catch { $rejected = $true }
Require $rejected 'invalid explicit folder is rejected instead of installing elsewhere'
$steam = Join-Path $TestRoot 'Steam'
$library = Join-Path $TestRoot 'Extra library'
New-Item -ItemType Directory -Path (Join-Path $steam 'steamapps'),(Join-Path $library 'steamapps') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $steam 'steamapps\libraryfolders.vdf'),('"path" "'+$library.Replace('\','\\')+'"'))
[IO.File]::WriteAllText((Join-Path $library 'steamapps\appmanifest_1604270.acf'),'"installdir" "Custom Game Folder"')
$candidates=@(Get-SteamGameCandidates @($steam,'Z:\nonexistent-bacn-test'))
Require ($candidates -contains (Join-Path $library 'steamapps\common\Custom Game Folder')) 'Steam library and manifest detection respects alternate install folder'
$missingDependency = Join-Path $TestRoot 'Missing Local Skirmish'
New-Item -ItemType Directory -Path (Join-Path $missingDependency 'MelonLoader\net6') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $missingDependency 'BrokenArrow.exe'),'fixture')
[IO.File]::WriteAllText((Join-Path $missingDependency 'MelonLoader\net6\MelonLoader.dll'),'fixture')
$ErrorActionPreference = 'Continue' # Windows PowerShell wraps redirected native stderr as error records.
$rejection = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -GameDir $missingDependency -NonInteractive 2>&1
$ErrorActionPreference = 'Stop'
Require ($LASTEXITCODE -ne 0) 'missing Local Skirmish rejects installation'
Require (($rejection | Out-String) -match 'Local Skirmish is required') 'dependency failure gives actionable instructions'
Require (-not (Test-Path -LiteralPath (Join-Path $missingDependency 'UserData'))) 'dependency failure writes no addon files'
$batch = Join-Path $repo 'install.bat'
# Redirect standard input so the wrapper pause does not wait for a test operator.
& $env:ComSpec /d /c ('call "' + $batch + '" -GameDir "' + $game + '" -NonInteractive <NUL')
Require ($LASTEXITCODE -eq 0) 'batch wrapper forwards a game path with spaces and returns success'
$ErrorActionPreference = 'Continue'
& $env:ComSpec /d /c ('call "' + $batch + '" -GameDir "' + (Join-Path $TestRoot 'missing') + '" -NonInteractive <NUL') 2>&1 | Out-String | Write-Host
$ErrorActionPreference = 'Stop'
Require ($LASTEXITCODE -ne 0) 'batch wrapper preserves failure exit code'
Write-Host ('Installer fixtures kept at: ' + $TestRoot)
