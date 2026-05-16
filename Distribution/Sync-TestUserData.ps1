param(
    [string]$ProjectName = "SEROKA",
    [switch]$SkipBackup,
    [switch]$KeepSaves
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Step([string]$Message) {
    Write-Host ""
    Write-Host "==== $Message ====" -ForegroundColor Cyan
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$userDataRoot = Join-Path $env:APPDATA ("Godot\app_userdata\" + $ProjectName)

if (-not (Test-Path $userDataRoot)) {
    throw "Dossier user:// introuvable pour le projet '$ProjectName': $userDataRoot"
}

Step "Cible user://"
Write-Host $userDataRoot

if (-not $SkipBackup) {
    $backupRoot = Join-Path $repoRoot ("Distribution\backups\userdata_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
    Step "Backup user://"
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    Copy-Item -Recurse -Force $userDataRoot (Join-Path $backupRoot $ProjectName)
    Write-Host "Backup cree: $backupRoot"
}

Step "Reset options/runtime cache"
$fichiersASupprimer = @(
    "options_graphics.cfg",
    "last_played_world.txt"
)
foreach ($f in $fichiersASupprimer) {
    $path = Join-Path $userDataRoot $f
    if (Test-Path $path) {
        Remove-Item -Force $path
        Write-Host "Supprime: $path"
    }
}

$dossiersASupprimer = @(
    "shader_cache",
    "vulkan",
    "logs",
    "objectdb_snapshots",
    "chunks"
)
foreach ($d in $dossiersASupprimer) {
    $path = Join-Path $userDataRoot $d
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path
        Write-Host "Supprime: $path"
    }
}

if (-not $KeepSaves) {
    Step "Reset sauvegardes de test"
    $savePath = Join-Path $userDataRoot "saves"
    if (Test-Path $savePath) {
        Remove-Item -Recurse -Force $savePath
        Write-Host "Supprime: $savePath"
    }
}
else {
    Write-Host "Saves conservees (-KeepSaves)."
}

Step "Termine"
Write-Host "user:// reinitialise pour comparaison editor/launcher." -ForegroundColor Green
