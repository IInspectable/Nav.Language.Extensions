<#
.SYNOPSIS
    Erhöht die Major-Version: setzt das Tag v(X+1).0.0 auf HEAD.

.DESCRIPTION
    Die Version wird git-abgeleitet (siehe Get-ProductVersion): Major/Minor werden über Tags
    gesteuert, der Patch zählt automatisch die Commits seit dem letzten Tag. incmajor legt das
    nächste Major-Tag auf HEAD an — mit Clean-Tree-Check, Monotonie-Absicherung, Bestätigung und
    optionalem Push (siehe Set-VersionTag). Der Repo-Root wird zur Aufruf-Zeit aufgelöst.

.PARAMETER Push
    Das neue Tag direkt nach origin pushen.

.PARAMETER Force
    Bestätigung vor dem Taggen überspringen.

.FUNCTIONALITY
    incmajor
#>
function Invoke-IncreaseMajor {
    [CmdletBinding()]
    param(
        [switch] $Push,
        [switch] $Force
    )

    $root = Resolve-Root
    if (-not $root) { return }

    Set-VersionTag -Root $root -Part Major -Push:$Push -Force:$Force
}
