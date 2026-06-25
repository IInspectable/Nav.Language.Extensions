<#
.SYNOPSIS
    Baut das Repo und kopiert die Build Tools in das XTplus-Zielverzeichnis.

.DESCRIPTION
    Ruft zuerst Invoke-Build (gesamte Solution) und kopiert anschließend den Inhalt von
    `deploy\Build Tools` in das Zielverzeichnis (Default: das bisherige XTplus-Nav-Skript-
    Verzeichnis). Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root).

.PARAMETER Target
    Zielverzeichnis für die Build Tools. Default: C:\ws\XTplus\z_Nav3\build\Script\Nav.

.PARAMETER Configuration
    Build-Konfiguration. Default: Debug.

.FUNCTIONALITY
    deploy
#>
function Invoke-Deploy {
    [CmdletBinding()]
    param(
        [string] $Target = 'C:\ws\XTplus\z_Nav3\build\Script\Nav',
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    Invoke-Build -Configuration $Configuration

    $source = Join-Path $root 'deploy\Build Tools'
    if (-not (Test-Path $source)) { throw "Quelle nicht gefunden: '$source'." }

    if (-not (Test-Path $Target)) { New-Item -ItemType Directory -Path $Target -Force | Out-Null }
    Copy-Item -Path (Join-Path $source '*') -Destination $Target -Recurse -Force

    Write-Host ""
    Write-Host "Deployt nach: $Target" -ForegroundColor Green
    Write-Host "Quelle: $source" -ForegroundColor DarkGray
}
