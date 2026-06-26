<#
.SYNOPSIS
    Interner Helfer: veröffentlicht die Nav-CLI self-contained als Single-File nach deploy\Build Tools.

.DESCRIPTION
    Kein eigener n-Command (keine .FUNCTIONALITY) — wird von `n publish` (Invoke-Publish) NACH dem
    Solution-Build aufgerufen. Spiegelbild des LSP-/MCP-Publishs, nur mit anderem Ziel: Der
    Solution-Build (DeployFiles-Target) leert deploy\Build Tools und legt Task-DLL, Targets und
    Grammatik hinein; dieser Schritt ergänzt die self-contained nav.exe (trägt .NET-Runtime + Engine
    selbst, keine separate Runtime-Installation nötig). Damit entfällt der net472-Binding-Redirect-
    Konflikt der früheren framework-abhängigen nav.exe.

    Flags wie beim LSP-/MCP-Publish: PublishSingleFile + IncludeNativeLibrariesForSelfExtract → eine
    exe; EnableCompressionInSingleFile → kleinere exe; SatelliteResourceLanguages=en → keine
    lokalisierten Satellite-Ressourcen; DebugType=embedded → keine separate .pdb.

    WICHTIG (Reihenfolge): Muss NACH Invoke-Build laufen, weil das DeployFiles-Target
    deploy\Build Tools\**\* vorher löscht. Das Verzeichnis wird hier bewusst NICHT geleert —
    Task-DLL/Targets/Grammatik stammen aus dem Solution-Build und müssen erhalten bleiben.

.PARAMETER Configuration
    Build-Konfiguration. Default: Debug.
#>
function Publish-Cli {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $rid        = 'win-x64'
    $publishDir = Join-Path $root 'deploy\Build Tools'
    $project    = Join-Path $root 'Nav.Cli\Nav.Cli.csproj'
    if (-not (Test-Path $project)) { throw "CLI-Projekt nicht gefunden: '$project'." }

    & dotnet publish $project -c $Configuration -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en `
        -p:DebugType=embedded `
        -o $publishDir -v:m
    if ($LASTEXITCODE) { throw "CLI-Publish fehlgeschlagen (Exit $LASTEXITCODE)." }

    $exe = Join-Path $publishDir 'nav.exe'
    Write-Host ""
    Write-Host "Self-contained CLI veröffentlicht nach: $publishDir" -ForegroundColor Green
    Write-Host "Finale ausführbare Datei: $exe" -ForegroundColor DarkGray
}
