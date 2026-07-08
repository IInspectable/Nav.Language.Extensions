<#
.SYNOPSIS
    Legt das nächste Major-/Minor-Version-Tag (vX.Y.0) auf HEAD an.

.DESCRIPTION
    Interner Helper (Verb-Noun ohne .FUNCTIONALITY → kein nav-Command); genutzt von
    Invoke-IncreaseMinor/Major. Seit der Umstellung auf git-abgeleitete Versionen
    (siehe Get-ProductVersion) werden Major/Minor über Tags gesteuert, der Patch zählt
    automatisch die Commits seit dem letzten Tag — ein eigenes „incbuild" gibt es nicht mehr.

    Ablauf:
      1. Working-Tree muss sauber sein — das Tag soll den aktuellen *committeten* Stand pinnen.
      2. Letztes 3-teiliges Version-Tag lesen (Anker für Major/Minor).
      3. Nächste Version berechnen: Major → (X+1).0.0, Minor → X.(Y+1).0.
      4. Absicherung: neue Version strikt größer als das letzte Tag UND Tag noch nicht vorhanden.
      5. Bestätigung abwarten (außer -Force), dann annotiertes Tag setzen.
      6. Optional nach origin pushen (-Push, oder Rückfrage).

.PARAMETER Part
    'Major' oder 'Minor'.

.PARAMETER Root
    Repo-/Worktree-Root. Default: via Resolve-Root.

.PARAMETER Push
    Das Tag ohne separate Rückfrage direkt nach origin pushen.

.PARAMETER Force
    Die Bestätigung vor dem Taggen überspringen (pusht NICHT automatisch — Push bleibt -Push).
#>
function Set-VersionTag {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Major', 'Minor')]
        [string] $Part,
        [string] $Root,
        [switch] $Push,
        [switch] $Force
    )

    $ErrorActionPreference = 'Stop'

    if (-not $Root) { $Root = Resolve-Root }
    if (-not $Root) { return }

    # 1) Working-Tree muss sauber sein — sonst bezöge sich das Tag auf einen Stand, der so nicht
    #    committet ist.
    if (& git -C $Root status --porcelain) {
        Write-Host "Working-Tree ist nicht sauber — bitte erst committen oder stashen. Kein Tag gesetzt." -ForegroundColor Red
        & git -C $Root status --short
        return
    }

    # 2) Letztes 3-teiliges Version-Tag als Anker.
    $lastTag = (& git -C $Root describe --tags --abbrev=0 --match 'v[0-9]*.[0-9]*.[0-9]*' 2>$null)
    if ($LASTEXITCODE -eq 0 -and $lastTag) {
        $old = [Version] ($lastTag.TrimStart('v'))
    }
    else {
        $old = [Version] '0.0.0'
        Write-Host "Kein vX.Y.Z-Tag gefunden — beginne bei $old." -ForegroundColor Yellow
    }

    # 3) Nächste Version.
    if ($Part -eq 'Major') {
        $new = [Version]::new($old.Major + 1, 0, 0)
    }
    else {
        $new = [Version]::new($old.Major, $old.Minor + 1, 0)
    }
    $newTag = "v$new"

    # 4) Absicherung: strikt steigend, Tag noch nicht vorhanden.
    if ($new -le $old) {
        throw "Neue Version $new ist nicht größer als das letzte Tag v$old — abgebrochen."
    }
    if (& git -C $Root tag --list $newTag) {
        throw "Tag $newTag existiert bereits — abgebrochen."
    }

    $head = (& git -C $Root rev-parse --short HEAD)
    Write-Host ""
    Write-Host ("  Letztes Tag : v{0}" -f $old) -ForegroundColor DarkGray
    Write-Host ("  Neues Tag   : {0}   (auf HEAD {1})" -f $newTag, $head) -ForegroundColor Cyan

    # 5) Bestätigung (außer -Force).
    if (-not $Force) {
        $answer = Read-Host "Tag $newTag jetzt anlegen? (j/N)"
        if ($answer -notin @('j', 'J', 'ja', 'Ja', 'y', 'Y', 'yes')) {
            Write-Host "Abgebrochen — kein Tag gesetzt." -ForegroundColor Yellow
            return
        }
    }

    & git -C $Root tag -a $newTag -m $newTag
    if ($LASTEXITCODE) { throw "git tag fehlgeschlagen (Exit $LASTEXITCODE)." }
    Write-Host "Tag $newTag angelegt." -ForegroundColor Green

    # 6) Push (outward-facing → nur auf ausdrücklichen Wunsch). -Push pusht direkt; sonst Rückfrage
    #    (entfällt bei -Force, dann muss -Push explizit gesetzt sein).
    $doPush = [bool] $Push
    if (-not $Push -and -not $Force) {
        $answer = Read-Host "Tag $newTag nach origin pushen? (j/N)"
        $doPush = $answer -in @('j', 'J', 'ja', 'Ja', 'y', 'Y', 'yes')
    }
    if ($doPush) {
        & git -C $Root push origin $newTag
        if ($LASTEXITCODE) { throw "git push fehlgeschlagen (Exit $LASTEXITCODE)." }
        Write-Host "Tag $newTag nach origin gepusht." -ForegroundColor Green
    }
    else {
        Write-Host "Nicht gepusht. Bei Bedarf: git push origin $newTag" -ForegroundColor DarkGray
    }
}
