<#
.SYNOPSIS
    Veröffentlicht den Nav-LSP-Server self-contained nach deploy\lsp.

.DESCRIPTION
    Erzeugt deploy\lsp\nav.lsp.exe samt gebündelter .NET-Runtime (keine separate
    Runtime-Installation nötig). Läuft bewusst über die Full-Framework-MSBuild.exe (wie
    Invoke-Build): die Engine nutzt in Nav.Language\CustomBuild.targets die CodeTaskFactory,
    die `dotnet publish` nicht kennt.

    Flags: PublishSingleFile + IncludeNativeLibrariesForSelfExtract → alles in eine exe;
    EnableCompressionInSingleFile → kleinere exe; SatelliteResourceLanguages=en → keine
    lokalisierten Satellite-Ressourcen; DebugType=embedded → keine separate .pdb.

.PARAMETER Configuration
    Build-Konfiguration. Default: Release.

.FUNCTIONALITY
    publishlsp
#>
function Publish-Lsp {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Release'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $msbuild = Resolve-MsBuild
    if (-not $msbuild) { return }

    $rid = 'win-x64'
    $publishDir = Join-Path $root 'deploy\lsp'
    $project = Join-Path $root 'Nav.Language.Lsp\Nav.Language.Lsp.csproj'
    if (-not (Test-Path $project)) { throw "LSP-Projekt nicht gefunden: '$project'." }

    # Zielverzeichnis vorher leeren — der self-contained Publish räumt Altbestand nicht selbst auf.
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    & $msbuild $project -restore -t:Publish `
        -p:Configuration=$Configuration -p:RuntimeIdentifier=$rid -p:SelfContained=true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en `
        -p:DebugType=embedded `
        "-p:PublishDir=$publishDir/" -v:m -m
    if ($LASTEXITCODE) { throw "Publish fehlgeschlagen (Exit $LASTEXITCODE)." }

    $exe = Join-Path $publishDir 'nav.lsp.exe'
    Write-Host ""
    Write-Host "Self-contained LSP-Server veröffentlicht nach: $publishDir" -ForegroundColor Green
    Write-Host "Finale ausführbare Datei: $exe" -ForegroundColor DarkGray
}
