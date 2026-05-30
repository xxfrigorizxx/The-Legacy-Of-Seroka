param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][int]$Start,        # 1-based inclusive
    [Parameter(Mandatory=$true)][int]$End,          # 1-based inclusive
    [Parameter(Mandatory=$true)][string]$Dest,
    [Parameter(Mandatory=$true)][string]$FirstAnchor,
    [Parameter(Mandatory=$true)][string]$LastAnchor,
    [string]$Mode = "create",                        # create | append
    [string]$Header = ""
)

$ErrorActionPreference = "Stop"

# --- Read source preserving content (UTF8, BOM auto-stripped on read) ---
$raw = [System.IO.File]::ReadAllText($Source)
$hadFinalNewline = $raw.EndsWith("`n")
$lines = [System.Collections.Generic.List[string]]::new()
$lines.AddRange([string[]]($raw -split "`n"))
if ($hadFinalNewline) { $lines.RemoveAt($lines.Count - 1) }  # drop trailing empty element

$s = $Start - 1
$e = $End - 1
if ($s -lt 0 -or $e -ge $lines.Count -or $s -gt $e) {
    throw "Range out of bounds: Start=$Start End=$End TotalLines=$($lines.Count)"
}

# --- Strict anchor assertions (abort if mismatch, no write) ---
if ($lines[$s] -ne $FirstAnchor) {
    throw "FIRST anchor mismatch at line $Start.`nExpected: [$FirstAnchor]`nActual:   [$($lines[$s])]"
}
if ($lines[$e] -ne $LastAnchor) {
    throw "LAST anchor mismatch at line $End.`nExpected: [$LastAnchor]`nActual:   [$($lines[$e])]"
}

# --- Extract block ---
$block = New-Object System.Collections.Generic.List[string]
for ($i = $s; $i -le $e; $i++) { $block.Add($lines[$i]) }

$enc = New-Object System.Text.UTF8Encoding($true)  # emit BOM
$nl = "`n"

$destDir = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Dest))
if ($destDir -and -not (Test-Path -LiteralPath $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

# --- Write / append destination ---
if ($Mode -eq "create") {
    $destLines = New-Object System.Collections.Generic.List[string]
    if ($Header -ne "") {
        $destLines.AddRange([string[]]($Header -split "`n"))
    }
    $destLines.AddRange($block)
    $destLines.Add("}")
    $destText = ($destLines -join $nl) + $nl
    [System.IO.File]::WriteAllText($Dest, $destText, $enc)
}
elseif ($Mode -eq "append") {
    if (-not (Test-Path -LiteralPath $Dest)) { throw "Append mode but Dest does not exist: $Dest" }
    $destRaw = [System.IO.File]::ReadAllText($Dest)
    $destAll = [System.Collections.Generic.List[string]]::new()
    $destAll.AddRange([string[]]($destRaw -split "`n"))
    # find last non-empty line; must be closing brace
    $lastIdx = $destAll.Count - 1
    while ($lastIdx -ge 0 -and $destAll[$lastIdx].Trim() -eq "") { $lastIdx-- }
    if ($lastIdx -lt 0 -or $destAll[$lastIdx].Trim() -ne "}") { throw "Dest does not end with closing brace" }
    $newDest = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $lastIdx; $i++) { $newDest.Add($destAll[$i]) }
    $newDest.Add("")
    $newDest.AddRange($block)
    $newDest.Add("}")
    $destText = ($newDest -join $nl) + $nl
    [System.IO.File]::WriteAllText($Dest, $destText, $enc)
}
else { throw "Unknown Mode: $Mode" }

# --- Remove block from source ---
$remaining = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $s; $i++) { $remaining.Add($lines[$i]) }
for ($i = $e + 1; $i -lt $lines.Count; $i++) { $remaining.Add($lines[$i]) }
$srcText = ($remaining -join $nl)
if ($hadFinalNewline) { $srcText += $nl }
[System.IO.File]::WriteAllText($Source, $srcText, $enc)

Write-Output "OK: moved lines $Start..$End ($($block.Count) lines) -> $Dest (mode=$Mode). Source now $($remaining.Count) lines."
