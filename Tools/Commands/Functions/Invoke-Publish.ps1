<#
.SYNOPSIS
    Baut die Solution (Debug) und stellt alle Deliverables unter deploy\ bereit: Build Tools,
    VS-Code-Extension (mit eingebettetem LSP) und MCP-Server als Single-File.

.DESCRIPTION
    Der eine Publish-Einstiegspunkt. Ablauf (durchgängig Debug):
      1. Solution in Debug bauen (Invoke-Build → MSBuild.exe). Die DeployFiles-Targets füllen dabei
         deploy\Build Tools (Build-Task-DLL + Targets) und deploy\Vsix (VS-2026-Extension)
         automatisch. **Bewusst Debug:** die Release-Config der Solution ist unvollständig (Nav.Cli
         ohne NDESK_OPTIONS-Define, VS-Extension ohne AllowUnsafeBlocks) und baut nicht durch — die
         Build Tools werden wie bei `nav deploy` aus dem Debug-Build geliefert.
      1b. CLI (nav.exe) self-contained als Single-File nach deploy\Build Tools ergänzen (Publish-Cli).
          Muss nach dem Solution-Build laufen, da DeployFiles das Verzeichnis vorher leert.
      1c. EBNF-Grammatik (NavGrammar.ebnf) nach deploy\Build Tools exportieren (Export-Grammar, ruft
          die nav.exe aus 1b mit `grammar` auf). Muss nach Publish-Cli laufen.
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

    # 0) deploy\ komplett leeren — garantiert einen sauberen Stand ohne Altlasten aus früheren
    #    Publishes (die Einzel-Targets leeren nur ihr jeweiliges Unterverzeichnis).
    $deploy = Join-Path $root 'deploy'
    if (Test-Path $deploy) {
        Write-Host "Leere deploy\ ..." -ForegroundColor DarkGray
        Remove-Item -Path $deploy -Recurse -Force
    }

    # 1) Solution bauen — füllt deploy\Build Tools (Task-DLL/Targets/Grammatik) und deploy\Vsix
    #    per Post-Build-Targets. ACHTUNG: leert deploy\Build Tools vorher.
    Invoke-Build -Configuration $Configuration

    # 1b) CLI self-contained als Single-File → ergänzt nav.exe in deploy\Build Tools.
    #     Muss NACH Invoke-Build laufen (DeployFiles leert das Verzeichnis vorher).
    Publish-Cli -Configuration $Configuration

    # 1c) EBNF-Grammatik (NavGrammar.ebnf) → deploy\Build Tools. Muss NACH Publish-Cli laufen
    #     (nutzt die soeben dorthin gelegte nav.exe), derselbe Zielort wie früher die .g4.
    Export-Grammar -Configuration $Configuration

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
