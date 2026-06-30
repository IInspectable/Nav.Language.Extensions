<#
.SYNOPSIS
    Interner Helfer: veröffentlicht den Nav-MCP-Server self-contained als Single-File nach deploy\mcp.

.DESCRIPTION
    Kein eigener nav-Command (keine .FUNCTIONALITY) — wird von `nav publish` (Invoke-Publish) aufgerufen.
    Spiegelbild des LSP-Publishs: erzeugt deploy\mcp\nav.mcp.exe samt gebündelter .NET-Runtime
    (keine separate Runtime-Installation nötig). Läuft über `dotnet publish`.

    Flags: PublishSingleFile + IncludeNativeLibrariesForSelfExtract → alles in eine exe;
    EnableCompressionInSingleFile → kleinere exe; SatelliteResourceLanguages=en → keine
    lokalisierten Satellite-Ressourcen; DebugType=embedded → keine separate .pdb.

.PARAMETER Configuration
    Build-Konfiguration. Default: Debug.
#>
function Publish-Mcp {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $rid = 'win-x64'
    $publishDir = Join-Path $root 'deploy\mcp'
    $project = Join-Path $root 'Nav.Language.Mcp\Nav.Language.Mcp.csproj'
    if (-not (Test-Path $project)) { throw "MCP-Projekt nicht gefunden: '$project'." }

    # Zielverzeichnis vorher leeren — der self-contained Publish räumt Altbestand nicht selbst auf.
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    # Die Version berechnet der Build selbst im MSBuild-Target ComputeGitVersion (einzige Autorität);
    # hier wird bewusst nichts durchgereicht.
    & dotnet publish $project -c $Configuration -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en `
        -p:DebugType=embedded `
        -o $publishDir -v:m
    if ($LASTEXITCODE) { throw "Publish fehlgeschlagen (Exit $LASTEXITCODE)." }

    $exe = Join-Path $publishDir 'nav.mcp.exe'
    Write-Host ""
    Write-Host "Self-contained MCP-Server veröffentlicht nach: $publishDir" -ForegroundColor Green
    Write-Host "Finale ausführbare Datei: $exe" -ForegroundColor DarkGray
}
