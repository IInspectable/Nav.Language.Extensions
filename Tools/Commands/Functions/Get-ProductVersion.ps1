<#
.SYNOPSIS
    Leitet die Produktversion aus git ab (einzige Quelle der Wahrheit — löst das frühere
    Version.props ab).

.DESCRIPTION
    Interner Helper (Verb-Noun ohne .FUNCTIONALITY → kein nav-Command). Die Version entsteht
    „on the fly" aus `git describe`:
      Kern (3-teilig)        = Major.Minor.(TagPatch + CommitsSeitTag)
      Informational          = Kern + "+" + Branch (SemVer-sanitisiert) + "." + KurzSHA

    Striktes 3-teiliges SemVer, weil vsce (VS-Code-VSIX), das VS-VSIX-Manifest und AssemblyVersion
    nichts anderes akzeptieren. Branch und SHA landen daher NUR im Informational-Feld, nie im Kern.

    Major/Minor werden über Tags gesteuert (`git tag vX.Y.0`, siehe Invoke-IncreaseMinor/Major),
    der Patch zählt automatisch die Commits seit dem letzten Tag. Ein getaggter Commit ist exakt
    die Tag-Version. Fehlt ein erreichbares vX.Y.Z-Tag (frischer/shallow Clone), greift der
    Fallback 0.0.<Commit-Anzahl> mit Warnung.

    Diese Berechnung ist die Autorität für `n build`/`n publish`: Invoke-Build reicht die Werte als
    `-p:ProductVersion=…` / `-p:ProductVersionInformational=…` an MSBuild durch (siehe auch das
    spiegelnde Fallback-Target in _build\Version.targets).

.PARAMETER Root
    Repo-/Worktree-Root. Default: via Resolve-Root.

.OUTPUTS
    PSCustomObject mit Version, Informational, Branch, Sha.
#>
function Get-ProductVersion {
    [CmdletBinding()]
    param(
        [string] $Root
    )

    $ErrorActionPreference = 'Stop'

    if (-not $Root) { $Root = Resolve-Root }
    if (-not $Root) { throw "Repo-Root nicht auflösbar." }

    # describe greift bewusst nur 3-teilige vX.Y.Z-Tags (schließt Alt-Tags wie v4.0 aus).
    $desc = (& git -C $Root describe --tags --long --match 'v[0-9]*.[0-9]*.[0-9]*' 2>$null)
    if ($LASTEXITCODE -eq 0 -and $desc -match '^v(\d+)\.(\d+)\.(\d+)-(\d+)-g([0-9A-Fa-f]+)$') {
        $major   = [int] $Matches[1]
        $minor   = [int] $Matches[2]
        $patch   = [int] $Matches[3] + [int] $Matches[4]
        $sha     = $Matches[5]
        $version = "$major.$minor.$patch"
    }
    else {
        $count = (& git -C $Root rev-list --count HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $count) { $count = '0' }
        $version = "0.0.$($count.ToString().Trim())"
        $sha     = ''
        Write-Warning "Kein vX.Y.Z-Tag erreichbar – Fallback-Produktversion $version."
    }

    $branch = (& git -C $Root rev-parse --abbrev-ref HEAD 2>$null)
    if (-not $branch) { $branch = '' }
    $branchSan = ($branch -replace '[^0-9A-Za-z-]', '-')

    $informational = if ($sha) { "$version+$branchSan.$sha" } else { $version }

    [pscustomobject]@{
        Version       = $version
        Informational = $informational
        Branch        = $branch
        Sha           = $sha
    }
}
