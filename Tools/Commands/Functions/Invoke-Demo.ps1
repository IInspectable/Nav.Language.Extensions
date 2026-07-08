<#
.SYNOPSIS
    Baut die NavV2NavigationDemo (Demos\NavV2NavigationDemo) mit Full-Framework-MSBuild.exe.

.DESCRIPTION
    Die Demo (unter `Demos/`) testet die Begin↔After-GoTo-Navigation des V2-Codegens. Ihr Codegen
    bezieht Task-DLL, Targets und die self-contained nav.exe aus `deploy\Build Tools` — dieses
    Verzeichnis füllt `nav publish`. Vor dem Build wird geprüft, ob die Build Tools vorhanden sind;
    fehlen sie, bricht der Command mit einer klaren Meldung ab (statt eines kryptischen MSBuild-Fehlers).

    Bewusst MSBuild.exe statt `dotnet build`: der Nav-Build-Task ist net472 und läuft im
    MSBuild-Prozess. Repo-/Worktree-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root), daher von
    jedem Ort im Repo aufrufbar.

    Wird zusätzlich von `nav publish` als Smoke-Test der publizierten Toolchain aufgerufen.

.PARAMETER Configuration
    Build-Konfiguration der Demo. Default: Debug.

.PARAMETER RemainingArgs
    Zusätzliche, unverändert an den MSBuild-Aufruf durchgereichte Argumente (z. B. -t:Rebuild).

.EXAMPLE
    nav demo
    Baut die Demo in Debug (setzt ein vorheriges `nav publish` voraus).

.FUNCTIONALITY
    demo
#>
function Invoke-Demo {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug',
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $RemainingArgs
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    # Guard: Build Tools müssen publiziert sein (nav.exe co-lokalisiert mit der Task-DLL).
    $navExe = Join-Path $root 'deploy\Build Tools\nav.exe'
    if (-not (Test-Path $navExe)) {
        Write-Host ""
        Write-Host "  Nav Build Tools nicht gefunden: '$navExe'." -ForegroundColor Red
        Write-Host "  Die Demo bezieht ihren Codegen aus deploy\Build Tools." -ForegroundColor Yellow
        Write-Host "  Bitte zuerst 'nav publish' ausführen." -ForegroundColor Yellow
        Write-Host ""
        throw "Build Tools fehlen — 'nav publish' zuerst ausführen."
    }

    $msbuild = Resolve-MsBuild
    if (-not $msbuild) { return }

    $project = Join-Path $root 'Demos\NavV2NavigationDemo\NavV2NavigationDemo.csproj'
    if (-not (Test-Path $project)) {
        throw "Demo-Projekt nicht gefunden: '$project'."
    }

    & $msbuild $project -t:Restore,Build -p:Configuration=$Configuration -v:minimal -nologo @RemainingArgs
    if ($LASTEXITCODE) { throw "Demo-Build fehlgeschlagen (Exit $LASTEXITCODE)." }

    Write-Host ""
    Write-Host "Demo gebaut: $project ($Configuration)" -ForegroundColor Green
}
