<#
.SYNOPSIS
    Pinnt die aktuelle git-abgeleitete Version als `v<Version>`-Tag auf HEAD und pusht sie
    (löst auf der CI einen Release-Build aus).

.DESCRIPTION
    Im Gegensatz zu incminor/incmajor (die Major/Minor *erhöhen* und den Patch auf 0 setzen)
    schreibt `release` die **aktuelle** Version — `Major.Minor.(TagPatch + Commits seit Tag)`,
    berechnet vom MSBuild-Target `ComputeGitVersion` und gelesen über Get-ProductVersion —
    genau so als Tag heraus und pusht ihn nach origin. Der Tag-Push triggert den GitHub-Actions-
    Workflow, dessen Release-Schritte nur bei `refs/tags/v*` laufen → automatischer Release-Build
    inkl. veröffentlichter Deliverables.

    Der Tag ist versions-stabil: liegt er erst einmal auf HEAD, liefert `git describe --long`
    dort `v<Version>-0-g<SHA>` → dieselbe Version wie zuvor (Höhe 0, TagPatch = voller Patch).

    Ablauf:
      1. Working-Tree muss sauber sein — der Tag pinnt den aktuellen *committeten* Stand.
      2. Aktuelle Version über Get-ProductVersion lesen (keine eigene Rechnung).
      3. Absicherung: echte, tag-abgeleitete Version (kein 0.0.<count>-Fallback), Format vX.Y.Z,
         Tag noch nicht vorhanden (sonst ist HEAD bereits released).
      4. Hinweis, falls HEAD noch nicht auf origin liegt.
      5. Sicherheitsabfrage abwarten (außer -Force), dann annotiertes Tag setzen und pushen.

.PARAMETER Root
    Repo-/Worktree-Root. Default: via Resolve-Root.

.PARAMETER Force
    Die Sicherheitsabfrage überspringen (Tag wird trotzdem gesetzt UND gepusht — der Push ist
    der eigentliche Zweck des Commands).

.FUNCTIONALITY
    release
#>
function Invoke-Release {
    [CmdletBinding()]
    param(
        [string] $Root,
        [switch] $Force
    )

    $ErrorActionPreference = 'Stop'

    if (-not $Root) { $Root = Resolve-Root }
    if (-not $Root) { return }

    # 1) Working-Tree muss sauber sein — sonst bezöge sich der Release-Tag auf einen Stand, der so
    #    nicht committet (und damit nicht auf origin baubar) ist.
    if (& git -C $Root status --porcelain) {
        Write-Host "Working-Tree ist nicht sauber — bitte erst committen oder stashen. Kein Release-Tag gesetzt." -ForegroundColor Red
        & git -C $Root status --short
        return
    }

    # 2) Aktuelle Version aus der einzigen Autorität (ComputeGitVersion) lesen.
    $version = (Get-ProductVersion -Root $Root).Version
    if (-not $version) {
        throw "Konnte die aktuelle Produktversion nicht ermitteln."
    }

    # 3a) Kein Release aus dem 0.0.<count>-Fallback (kein erreichbares vX.Y.Z-Tag) oder aus einer
    #     nicht 3-teiligen Version.
    if ($version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version '$version' ist nicht im Format vX.Y.Z — abgebrochen."
    }
    if ($version -match '^0\.0\.') {
        throw "Version '$version' ist der Fallback ohne erreichbares vX.Y.Z-Tag — es gibt keine echte Version zum Pinnen. Abgebrochen."
    }

    $tag = "v$version"

    # 3b) Tag darf noch nicht existieren. Existiert er, ist HEAD bereits genau als diese Version
    #     getaggt (Höhe 0, keine neuen Commits seit dem letzten Release).
    if (& git -C $Root tag --list $tag) {
        Write-Host "Tag $tag existiert bereits — HEAD ist bereits als diese Version released (keine neuen Commits seit dem letzten Tag)." -ForegroundColor Yellow
        Write-Host "Für ein neues Release erst committen, oder Major/Minor per incminor/incmajor anheben." -ForegroundColor DarkGray
        return
    }

    $head    = (& git -C $Root rev-parse --short HEAD)
    $lastTag = (& git -C $Root describe --tags --abbrev=0 --match 'v[0-9]*.[0-9]*.[0-9]*' 2>$null)

    # 4) Hinweis, falls die zu taggenden Commits noch nicht auf origin liegen. Der Tag-Push sendet die
    #    Objekte zwar mit, der Branch-Head auf origin wandert dabei aber NICHT — der Release-Build
    #    baut den getaggten Commit, aber der Branch bliebe hinterher zurück.
    $ahead = $null
    if (& git -C $Root rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null) {
        $ahead = (& git -C $Root rev-list --count '@{u}..HEAD' 2>$null)
    }

    Write-Host ""
    if ($lastTag) { Write-Host ("  Letztes Tag : {0}" -f $lastTag) -ForegroundColor DarkGray }
    Write-Host ("  Release-Tag : {0}   (auf HEAD {1})" -f $tag, $head) -ForegroundColor Cyan
    Write-Host  "  Push        : origin  →  löst CI-Release-Build aus" -ForegroundColor Cyan
    if ($ahead -and $ahead -ne '0') {
        Write-Host ("  Achtung     : {0} Commit(s) noch nicht auf origin — der Branch-Head wandert beim Tag-Push nicht mit." -f $ahead) -ForegroundColor Yellow
    }

    # 5) Sicherheitsabfrage (außer -Force).
    if (-not $Force) {
        $answer = Read-Host "Release $tag jetzt anlegen und nach origin pushen? (j/N)"
        if ($answer -notin @('j', 'J', 'ja', 'Ja', 'y', 'Y', 'yes')) {
            Write-Host "Abgebrochen — kein Release-Tag gesetzt." -ForegroundColor Yellow
            return
        }
    }

    & git -C $Root tag -a $tag -m $tag
    if ($LASTEXITCODE) { throw "git tag fehlgeschlagen (Exit $LASTEXITCODE)." }
    Write-Host "Tag $tag angelegt." -ForegroundColor Green

    & git -C $Root push origin $tag
    if ($LASTEXITCODE) {
        Write-Host "git push fehlgeschlagen — lokales Tag $tag bleibt bestehen. Bei Bedarf: git tag -d $tag" -ForegroundColor Red
        throw "git push fehlgeschlagen (Exit $LASTEXITCODE)."
    }
    Write-Host "Release $tag nach origin gepusht — der CI-Release-Build läuft an." -ForegroundColor Green
}
