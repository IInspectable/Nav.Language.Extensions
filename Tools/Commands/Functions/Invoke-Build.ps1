<#
.SYNOPSIS
    Baut die Solution per Full-Framework-MSBuild (Restore + Build).

.DESCRIPTION
    Ruft `MSBuild.exe Nav.Language.Extensions.slnx -t:restore -m` und anschließend den
    eigentlichen Build (`-p:Configuration=<Configuration> -v:n -m`). Der Repo-/Worktree-Root
    wird zur Aufruf-Zeit aufgelöst (Resolve-Root), daher von jedem Ort im Repo aufrufbar.

    Bewusst MSBuild.exe statt `dotnet build` (CodeTaskFactory in der Engine, siehe
    Resolve-MsBuild).

.PARAMETER Configuration
    Build-Konfiguration. Default: Debug.

.PARAMETER RemainingArgs
    Zusätzliche, unverändert an den Build-Aufruf durchgereichte MSBuild-Argumente.

.EXAMPLE
    n build
    Baut die gesamte Solution in Debug.

.EXAMPLE
    n build -Configuration Release
    Baut die gesamte Solution in Release.

.FUNCTIONALITY
    build
#>
function Invoke-Build {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug',
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $RemainingArgs
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $msbuild = Resolve-MsBuild
    if (-not $msbuild) { return }

    $solution = Join-Path $root 'Nav.Language.Extensions.slnx'
    if (-not (Test-Path $solution)) {
        throw "Solution nicht gefunden: '$solution'."
    }

    & $msbuild $solution -t:restore -m
    if ($LASTEXITCODE) { throw "Restore fehlgeschlagen (Exit $LASTEXITCODE)." }

    & $msbuild $solution -p:Configuration=$Configuration -v:n -m @RemainingArgs
    if ($LASTEXITCODE) { throw "Build fehlgeschlagen (Exit $LASTEXITCODE)." }
}
