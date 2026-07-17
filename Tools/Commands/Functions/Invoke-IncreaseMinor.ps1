<#
.SYNOPSIS
    Erhöht die Minor-Version: setzt das Tag vX.(Y+1).0 auf HEAD.

.DESCRIPTION
    Die Version wird git-abgeleitet (siehe Get-ProductVersion): Major/Minor werden über Tags
    gesteuert, der Patch zählt automatisch die Commits seit dem letzten Tag. incminor legt das
    nächste Minor-Tag auf HEAD an — mit Clean-Tree-Check, Monotonie-Absicherung, Bestätigung und
    optionalem Push (siehe Set-VersionTag). Der Repo-Root wird zur Aufruf-Zeit aufgelöst.

.PARAMETER Push
    Das neue Tag direkt nach origin pushen.

.PARAMETER Force
    Bestätigung vor dem Taggen überspringen.

.FUNCTIONALITY
    incminor
#>
function Invoke-IncreaseMinor {
    [CmdletBinding()]
    param(
        [switch] $Push,
        [switch] $Force
    )

    $root = Resolve-Root
    if (-not $root) { return }

    Set-VersionTag -Root $root -Part Minor -Push:$Push -Force:$Force
}
