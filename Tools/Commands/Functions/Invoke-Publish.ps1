<#
.SYNOPSIS
    Baut die Solution (Debug) und stellt alle Deliverables unter deploy\ bereit: Build Tools,
    VS-Code-Extension (mit eingebettetem LSP) und MCP-Server als Single-File.

.DESCRIPTION
    Der eine Publish-Einstiegspunkt. Ablauf (durchgängig Debug):
      1. Solution in Debug bauen (Invoke-Build → MSBuild.exe). Die DeployFiles-Targets füllen dabei
         deploy\Build Tools (CLI nav.exe + Build-Tasks + Grammatiken) und deploy\Vsix
         (VS-2026-Extension) automatisch. **Bewusst Debug:** die Release-Config der Solution ist
         unvollständig (Nav.Cli ohne NDESK_OPTIONS-Define, VS-Extension ohne AllowUnsafeBlocks) und
         baut nicht durch — die Build Tools werden wie bei `n deploy` aus dem Debug-Build geliefert.
      2. VS-Code-Extension publizieren (Publish-VsCode): LSP self-contained einbetten + VSIX nach
         deploy\vscode.
      3. MCP-Server self-contained als Single-File nach deploy\mcp publizieren (Publish-Mcp).

    Ergebnis unter deploy\: Build Tools, vscode\nav-language-<Version>-win32-x64.vsix, mcp\nav.mcp.exe.

.PARAMETER Configuration
    Build-/Publish-Konfiguration. Default: Debug (projektweit wird ausschließlich Debug gebaut —
    die Release-Config der Solution ist unvollständig).

.FUNCTIONALITY
    publish
#>
function Invoke-Publish {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    # 1) Solution bauen — füllt deploy\Build Tools und deploy\Vsix per Post-Build-Targets.
    Invoke-Build -Configuration $Configuration

    # 2) VS-Code-Extension (mit eingebettetem LSP) → deploy\vscode.
    Publish-VsCode -Configuration $Configuration

    # 3) MCP-Server als Single-File → deploy\mcp.
    Publish-Mcp -Configuration $Configuration

    $buildTools = Join-Path $root 'deploy\Build Tools'
    $vscodeDir  = Join-Path $root 'deploy\vscode'
    $mcpExe     = Join-Path $root 'deploy\mcp\nav.mcp.exe'

    Write-Host ""
    Write-Host "Publish abgeschlossen — Deliverables unter deploy\:" -ForegroundColor Green
    Write-Host "  Build Tools : $buildTools" -ForegroundColor DarkGray
    Write-Host "  VS Code     : $vscodeDir" -ForegroundColor DarkGray
    Write-Host "  MCP         : $mcpExe" -ForegroundColor DarkGray
}
