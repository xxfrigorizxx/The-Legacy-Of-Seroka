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

function Resolve-ReleaseBinDir([string]$repoRoot) {
    $releaseBinDirCandidates = @(
        (Join-Path $repoRoot ".godot\mono\temp\bin\Release\win-x64"),
        (Join-Path $repoRoot ".godot\mono\temp\bin\Release")
    )
    $releaseDllName = "Zero-K - Frozen Legacy.dll"
    $releaseDirInfos = @()
    foreach ($candidate in $releaseBinDirCandidates) {
        $candidateDll = Join-Path $candidate $releaseDllName
        if (Test-Path $candidateDll) {
            $fi = Get-Item $candidateDll
            $releaseDirInfos += [PSCustomObject]@{
                Dir = $candidate
                LastWriteTimeUtc = $fi.LastWriteTimeUtc
            }
        }
    }
    if ($releaseDirInfos.Count -eq 0) {
        throw "Sortie C# Release introuvable. Dossiers testes: $($releaseBinDirCandidates -join ', ')"
    }
    return ($releaseDirInfos | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).Dir
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$csprojPath = Join-Path $repoRoot "Zero-K - Frozen Legacy.csproj"
$payloadDir = Join-Path $repoRoot "artifacts\release_payload_alpha"
$manifestPath = Join-Path $repoRoot "Distribution\manifest.alpha.json"
$gameExePath = Join-Path $repoRoot "SEROKAFrozenLegacy.exe"
$gamePckPath = Join-Path $repoRoot "SEROKAFrozenLegacy.pck"
$dotnetDataDirName = "data_Zero-K - Frozen Legacy_windows_x86_64"
$dotnetDataDir = Join-Path $repoRoot $dotnetDataDirName
$payloadDataDir = Join-Path $payloadDir $dotnetDataDirName

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

Run-CheckedCommand -Label "Build C# Release (dotnet build -c Release)" -Action {
    & dotnet build $csprojPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        & dotnet restore $csprojPath
        & dotnet build $csprojPath -c Release
    }
}

$releaseBinDir = Resolve-ReleaseBinDir $repoRoot
$releaseDllPath = Join-Path $releaseBinDir "Zero-K - Frozen Legacy.dll"
$releaseDepsPath = Join-Path $releaseBinDir "Zero-K - Frozen Legacy.deps.json"
$releaseRuntimeConfigPath = Join-Path $releaseBinDir "Zero-K - Frozen Legacy.runtimeconfig.json"

if (-not (Test-Path $releaseDllPath)) { throw "DLL Release introuvable: $releaseDllPath" }
if (-not (Test-Path $releaseDepsPath)) { throw "deps Release introuvable: $releaseDepsPath" }
if (-not (Test-Path $releaseRuntimeConfigPath)) { throw "runtimeconfig Release introuvable: $releaseRuntimeConfigPath" }

Run-CheckedCommand -Label "Export release Godot" -Action {
    & $GodotExePath --headless --path $repoRoot --export-release "Windows Desktop" $gameExePath
}

if (-not (Test-Path $gameExePath)) { throw "Export manquant: $gameExePath" }
if (-not (Test-Path $gamePckPath)) { throw "Export manquant: $gamePckPath" }
if (-not (Test-Path $dotnetDataDir)) { throw "Export data manquant: $dotnetDataDir" }

Step "Preflight verrous fichiers"
Assert-FileWritable $gameExePath
Assert-FileWritable $gamePckPath
Assert-FileWritable (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.dll")

Step "Alignement runtime C# (Release -> data, sans symboles debug)"
Copy-Item -Force $releaseDllPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.dll")
Copy-Item -Force $releaseDepsPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.deps.json")
Copy-Item -Force $releaseRuntimeConfigPath (Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.runtimeconfig.json")
$pdbInData = Join-Path $dotnetDataDir "Zero-K - Frozen Legacy.pdb"
if (Test-Path $pdbInData) {
    Remove-Item -Force $pdbInData
}

Step "Synchronisation payload local"
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
Copy-Item -Force $gameExePath (Join-Path $payloadDir "SEROKAFrozenLegacy.exe")
Copy-Item -Force $gamePckPath (Join-Path $payloadDir "SEROKAFrozenLegacy.pck")
if (Test-Path $payloadDataDir) {
    Remove-Item -Recurse -Force $payloadDataDir
}
Copy-Item -Recurse -Force $dotnetDataDir $payloadDataDir
Get-ChildItem -Path $payloadDataDir -Filter "*.pdb" -Recurse -File | ForEach-Object {
    Remove-Item -Force $_.FullName
}

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
$pdbInManifest = @($manifestObj.files | Where-Object { $_.path -like "*.pdb" })
if ($pdbInManifest.Count -gt 0) {
    throw "Le manifest ne doit pas contenir de fichiers .pdb (joueurs): $($pdbInManifest.path -join ', ')"
}

Run-CheckedCommand -Label "Upload payload vers Backblaze (sync complet)" -Action {
    # compare-versions none : re-uploade tous les fichiers du payload, pas seulement les plus recents.
    & $B2ExePath sync --compare-versions none $payloadDir "b2://$BucketName/$Version"
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
Write-Host "SHA256 DLL (Release): $hashDll" -ForegroundColor DarkGreen
Write-Host "Configuration C#: Release (pas de PDB dans le payload)" -ForegroundColor DarkGreen

Step "Termine"
Write-Host "Version publiee: $Version" -ForegroundColor Green
Write-Host "BuildId: $BuildId" -ForegroundColor Green
Write-Host "Manifest: $BaseUrl/manifest.alpha.json" -ForegroundColor Green
