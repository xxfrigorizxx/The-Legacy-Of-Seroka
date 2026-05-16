param(
    [string]$GodotExePath = "C:\Users\xxfri\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe",
    [switch]$SkipBuild,
    [switch]$ForceKillGodot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Step([string]$Message) {
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$cacheMonoTemp = Join-Path $repoRoot ".godot\mono\temp"
$cacheExported = Join-Path $repoRoot ".godot\exported"

Step "Verification des processus"
$godotProcesses = @("Godot_v4.6.1-stable_mono_win64", "Godot_v4.6.1-stable_mono_win64_console", "godot")
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $godotProcesses -contains $_.ProcessName })
if ($running.Count -gt 0) {
    if (-not $ForceKillGodot) {
        throw "Godot est en cours d'execution. Ferme l'editeur puis relance, ou utilise -ForceKillGodot."
    }
    foreach ($p in $running) {
        Write-Host "Arret du processus: $($p.ProcessName) (PID $($p.Id))"
        Stop-Process -Id $p.Id -Force
    }
}

Step "Nettoyage cache editeur"
if (Test-Path $cacheMonoTemp) {
    Remove-Item -Recurse -Force $cacheMonoTemp
    Write-Host "Supprime: $cacheMonoTemp"
} else {
    Write-Host "Absent: $cacheMonoTemp"
}

if (Test-Path $cacheExported) {
    Remove-Item -Recurse -Force $cacheExported
    Write-Host "Supprime: $cacheExported"
} else {
    Write-Host "Absent: $cacheExported"
}

if ($SkipBuild) {
    Step "Termine (sans rebuild)"
    Write-Host "Cache nettoye. Ouvre Godot puis lance un Build C#."
    exit 0
}

if (-not (Test-Path $GodotExePath)) {
    throw "Executable Godot introuvable: $GodotExePath"
}

Step "Rebuild C# via Godot --build-solutions"
& $GodotExePath --headless --path $repoRoot --build-solutions --quit

Step "Termine"
Write-Host "Etat editeur reinitialise et C# reconstruit." -ForegroundColor Green
Write-Host "Tu peux relancer Godot et tester Play." -ForegroundColor Green
