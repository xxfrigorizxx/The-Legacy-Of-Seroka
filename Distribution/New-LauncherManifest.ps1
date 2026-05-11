param(
    [Parameter(Mandatory = $true)]
    [string]$GameDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$BuildId,

    [string]$Channel = "alpha",
    [string]$EntryExecutable = "SEROKAFrozenLegacy.exe",
    [string]$BaseUrl = "https://cdn.seroka.example/alpha",
    [switch]$UseRelativeLocalUrls
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $GameDirectory)) {
    throw "GameDirectory introuvable: $GameDirectory"
}

$gameDirFull = (Resolve-Path $GameDirectory).Path
$manifestDir = Split-Path -Parent $OutputManifestPath
if (-not (Test-Path $manifestDir)) {
    New-Item -ItemType Directory -Path $manifestDir | Out-Null
}

$files = @()
Get-ChildItem -Path $gameDirFull -Recurse -File | ForEach-Object {
    $fullPath = $_.FullName
    $baseUri = [System.Uri]::new(($gameDirFull.TrimEnd('\') + '\'))
    $fileUri = [System.Uri]::new($fullPath)
    $relativePath = [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fileUri).ToString())
    $hash = (Get-FileHash -Path $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $url = if ($UseRelativeLocalUrls) {
        "../payload/$relativePath"
    } else {
        "$BaseUrl/$Version/$relativePath"
    }

    $files += [ordered]@{
        path = $relativePath
        size = [int64]$_.Length
        sha256 = $hash
        url = $url
        required = $true
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    channel = $Channel
    version = $Version
    buildId = $BuildId
    publishedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    entryExecutable = $EntryExecutable
    notes = "Genere automatiquement."
    files = $files
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputManifestPath -Encoding UTF8
Write-Host "Manifest ecrit: $OutputManifestPath"
