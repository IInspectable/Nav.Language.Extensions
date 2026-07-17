<#
.SYNOPSIS
    Liest die git-abgeleitete Produktversion aus dem MSBuild-Target ComputeGitVersion.

.DESCRIPTION
    Interner Helper (Verb-Noun ohne .FUNCTIONALITY → kein nav-Command). Die Version wird an genau
    EINER Stelle berechnet: im MSBuild-Target `ComputeGitVersion` (`Build\Version.targets`), das
    über `Directory.Build.props` in jedem Projekt/Build-Pfad aktiv ist und die Werte aus
    `git describe` ableitet. Diese Funktion RECHNET NICHTS selbst — sie liest die dort berechneten
    Properties per `dotnet msbuild … -getProperty` (MSBuild 17.8+/.NET-SDK). Dadurch gibt es keinen
    zweiten Versions-Parser in PowerShell und kein `-p`-Durchreichen mehr; der Target ist die
    einzige Autorität.

    Verwender brauchen die Version nur lesend (vsce-Dateiname/Paket-Version in Publish-VsCode,
    VSIX-Dateiname in Install-Extension, Anzeige in Invoke-Build) — das eigentliche Stempeln von
    Binaries und VSIX-Manifest macht der Build selbst.

.PARAMETER Root
    Repo-/Worktree-Root. Default: via Resolve-Root.

.OUTPUTS
    PSCustomObject mit Version und Informational.
#>
function Get-ProductVersion {
    [CmdletBinding()]
    param(
        [string] $Root
    )

    $ErrorActionPreference = 'Stop'

    if (-not $Root) { $Root = Resolve-Root }
    if (-not $Root) { throw "Repo-Root nicht auflösbar." }

    $targets = Join-Path $Root 'Build\Version.targets'
    if (-not (Test-Path $targets)) { throw "Version.targets nicht gefunden: '$targets'." }

    # Werte aus dem Target lesen. -getProperty liefert bei mehreren Properties JSON; die git-Aufrufe
    # im Target schreiben ihre Ausgabe in Properties (nicht nach stdout), sodass stdout reines JSON
    # bleibt. stderr verwerfen (mögliche Fallback-Warnung beeinflusst die JSON-Ausgabe nicht).
    $json = & dotnet msbuild $targets -t:ComputeGitVersion -nologo `
        -getProperty:ProductVersion -getProperty:ProductVersionInformational 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $json) {
        throw "Versionsermittlung via MSBuild fehlgeschlagen (Exit $LASTEXITCODE)."
    }

    $props = ($json | ConvertFrom-Json).Properties

    [pscustomobject]@{
        Version       = $props.ProductVersion
        Informational = $props.ProductVersionInformational
    }
}
