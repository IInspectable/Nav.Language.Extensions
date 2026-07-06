<#
.SYNOPSIS
    Publiziert das Repo (nav publish) und kopiert die Build Tools in ein anzugebendes Zielverzeichnis.

.DESCRIPTION
    Ruft zuerst Invoke-Publish (voller Publish-Lauf: Solution bauen + alle Deliverables unter
    `deploy\` bereitstellen) und kopiert anschließend den Inhalt von `deploy\Build Tools` in das
    per -Target angegebene Zielverzeichnis. Der Repo-Root wird zur Aufruf-Zeit aufgelöst
    (Resolve-Root).

.PARAMETER Target
    Zielverzeichnis für die Build Tools. Pflichtangabe (kein Default).

.PARAMETER Configuration
    Build-Konfiguration. Default: Debug.

.FUNCTIONALITY
    deploy
#>
function Invoke-Deploy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Target,
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    Invoke-Publish -Configuration $Configuration

    $source = Join-Path $root 'deploy\Build Tools'
    if (-not (Test-Path $source)) { throw "Quelle nicht gefunden: '$source'." }

    if (-not (Test-Path $Target)) { New-Item -ItemType Directory -Path $Target -Force | Out-Null }
    Copy-Item -Path (Join-Path $source '*') -Destination $Target -Recurse -Force

    Write-Host ""
    Write-Host "Deployt nach: $Target" -ForegroundColor Green
    Write-Host "Quelle: $source" -ForegroundColor DarkGray
}
