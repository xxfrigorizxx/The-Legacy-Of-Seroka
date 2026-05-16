param(
    [string]$GodotExePath = "C:\Users\xxfri\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe",
    [string]$Version = "",
    [string]$BuildId = "",
    [string]$Channel = "alpha",
    [string]$BucketName = "Serok-alpha-game",
    [string]$BaseUrl = "https://f006.backblazeb2.com/file/Serok-alpha-game",
    [string]$B2ExePath = "C:\Users\xxfri\AppData\Local\Python\pythoncore-3.14-64\Scripts\b2.exe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Step([string]$message) {
    Write-Host ""
    Write-Host "==== $message ====" -ForegroundColor Cyan
}

function Run-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    Step $Label
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Label a echoue avec le code $LASTEXITCODE."
    }
}

function Assert-FileWritable([string]$path) {
    if (-not (Test-Path $path)) {
        return
    }
    try {
        $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
    } catch {
        throw "Fichier verrouille/inaccessible: $path"
    }
}

function Get-Sha256OrThrow([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Fichier introuvable pour hash: $path"
    }
    return (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$payloadDir = Join-Path $repoRoot "artifacts\release_payload_alpha"
$manifestPath = Join-Path $repoRoot "Distribution\manifest.alpha.json"
$gameExePath = Join-Path $repoRoot "SEROKAFrozenLegacy.exe"
$gamePckPath = Join-Path $repoRoot "SEROKAFrozenLegacy.pck"
$dotnetDataDirName = "data_Zero-K - Frozen Legacy_windows_x86_64"
$dotnetDataDir = Join-Path $repoRoot $dotnetDataDirName
$payloadDataDir = Join-Path $payloadDir $dotnetDataDirName
$debugBinDirCandidates = @(
    (Join-Path $repoRoot ".godot\mono\temp\bin\Debug\win-x64"),
    (Join-Path $repoRoot ".godot\mono\temp\bin\Debug")
)
$debugDllName = "Zero-K - Frozen Legacy.dll"
$debugDirInfos = @()
foreach ($candidate in $debugBinDirCandidates) {
    $candidateDll = Join-Path $candidate $debugDllName
    if (Test-Path $candidateDll) {
        $fi = Get-Item $candidateDll
        $debugDirInfos += [PSCustomObject]@{
            Dir = $candidate
            LastWriteTimeUtc = $fi.LastWriteTimeUtc
        }
    }
}
if ($debugDirInfos.Count -gt 0) {
    $debugBinDir = ($debugDirInfos | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).Dir
} else {
    $debugBinDir = $null
}
if ([string]::IsNullOrWhiteSpace($debugBinDir)) {
    throw "Sortie C# debug introuvable. Dossiers testes: $($debugBinDirCandidates -join ', ')"
}
$debugDllPath = Join-Path $debugBinDir $debugDllName
$debugPdbPath = Join-Path $debugBinDir "Zero-K - Frozen Legacy.pdb"
$debugDepsPath = Join-Path $debugBinDir "Zero-K - Frozen Legacy.deps.json"
$debugRuntimeConfigPath = Join-Path $debugBinDir "Zero-K - Frozen Legacy.runtimeconfig.json"

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version obligatoire. Exemple: -Version 0.1.0-alpha.16"
}
if ($Version -notmatch '^\d+\.\d+\.\d+-alpha\.\d+$') {
    throw "Format de version invalide: $Version (attendu ex: 0.1.0-alpha.16)"
}
if ([string]::IsNullOrWhiteSpace($BuildId)) {
    $BuildId = Get-Date -Format "yyyy.MM.dd.HHmm"
}
if (-not (Test-Path $GodotExePath)) {
    throw "Godot introuvable: $GodotExePath"
}
if (-not (Test-Path $B2ExePath)) {
    throw "CLI B2 introuvable: $B2ExePath"
}

Run-CheckedCommand -Label "Build C# (Godot --build-solutions)" -Action {
    & $GodotExePath --headless --path $repoRoot --build-solutions --quit
}

Run-CheckedCommand -Label "Export release Godot" -Action {
    & $GodotExePath --headless --path $repoRoot --export-release "Windows Desktop" $gameExePath
}

if (-not (Test-Path $gameExePath)) { throw "Export manquant: $gameExePath" }
if (-not (Test-Path $gamePckPath)) { throw "Export manquant: $gamePckPath" }
if (-not (Test-Path $dotnetDataDir)) { throw "Export data manquant: $dotnetDataDir" }
if (-not (Test-Path $debugDllPath)) { throw "DLL debug introuvable: $debugDllPath" }
if (-not (Test-Path $debugPdbPath)) { throw "PDB debug introuvable: $debugPdbPath" }
if (-not (Test-Path $debugDepsPath)) { throw "deps debug introuvable: $debugDepsPath" }
if (-not (Test-Path $debugRuntimeConfigPath)) { throw "runtimeconfig debug introuvable: $debugRuntimeConfigPath" }

Step "Preflight verrous fichiers"
Assert-FileWritable $gameExePath
Assert-FileWritable $gamePckPath
Assert-FileWritable (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.dll")
Assert-FileWritable (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.pdb")

Step "Alignement runtime C# (debug -> data)"
Copy-Item -Force $debugDllPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.dll")
Copy-Item -Force $debugPdbPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.pdb")
Copy-Item -Force $debugDepsPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.deps.json")
Copy-Item -Force $debugRuntimeConfigPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.runtimeconfig.json")

Step "Synchronisation payload local"
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
Copy-Item -Force $gameExePath (Join-Path $payloadDir "SEROKAFrozenLegacy.exe")
Copy-Item -Force $gamePckPath (Join-Path $payloadDir "SEROKAFrozenLegacy.pck")
if (Test-Path $payloadDataDir) {
    Remove-Item -Recurse -Force $payloadDataDir
}
Copy-Item -Recurse -Force $dotnetDataDir $payloadDataDir

Run-CheckedCommand -Label "Generation manifest launcher" -Action {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "Distribution\New-LauncherManifest.ps1") `
        -GameDirectory $payloadDir `
        -OutputManifestPath $manifestPath `
        -Version $Version `
        -BuildId $BuildId `
        -Channel $Channel `
        -EntryExecutable "SEROKAFrozenLegacy.exe" `
        -BaseUrl $BaseUrl
}

Step "Validation manifest"
$manifestObj = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
if ($manifestObj.version -ne $Version) {
    throw "Version manifest incoherente: $($manifestObj.version) (attendu $Version)"
}
if ($manifestObj.buildId -ne $BuildId) {
    throw "BuildId manifest incoherent: $($manifestObj.buildId) (attendu $BuildId)"
}
if ($manifestObj.entryExecutable -ne "SEROKAFrozenLegacy.exe") {
    throw "entryExecutable invalide dans manifest: $($manifestObj.entryExecutable)"
}

Run-CheckedCommand -Label "Upload payload vers Backblaze" -Action {
    & $B2ExePath sync --replace-newer $payloadDir "b2://$BucketName/$Version"
}

Run-CheckedCommand -Label "Upload manifest racine" -Action {
    & $B2ExePath file upload $BucketName $manifestPath "manifest.alpha.json" | Out-Host
}

Step "Empreintes runtime"
$hashExe = Get-Sha256OrThrow (Join-Path $payloadDir "SEROKAFrozenLegacy.exe")
$hashPck = Get-Sha256OrThrow (Join-Path $payloadDir "SEROKAFrozenLegacy.pck")
$hashDll = Get-Sha256OrThrow (Join-Path $payloadDataDir "Zero-K - Frozen Legacy.dll")
Write-Host "SHA256 EXE: $hashExe" -ForegroundColor DarkGreen
Write-Host "SHA256 PCK: $hashPck" -ForegroundColor DarkGreen
Write-Host "SHA256 DLL: $hashDll" -ForegroundColor DarkGreen

Step "Termine"
Write-Host "Version publiee: $Version" -ForegroundColor Green
Write-Host "BuildId: $BuildId" -ForegroundColor Green
Write-Host "Manifest: $BaseUrl/manifest.alpha.json" -ForegroundColor Green
