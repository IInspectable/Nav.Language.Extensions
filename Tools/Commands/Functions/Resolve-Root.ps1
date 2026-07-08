<#
.SYNOPSIS
    Ermittelt den Repo-/Worktree-Root für die Nav-Commands.

.DESCRIPTION
    Löst den Root zur Aufruf-Zeit aus dem angegebenen Pfad (Default: aktuelles
    Verzeichnis) über Git auf — `git -C <Pfad> rev-parse --show-toplevel`. Dadurch
    funktionieren die Convenience-Funktionen von jedem Unterordner aus und treffen bei
    mehreren Worktrees automatisch den, in dem man gerade steht. Liefert einen
    Windows-Pfad (Backslashes). Liegt der Pfad in keinem Git-Repository, wird ein
    freundlicher Hinweis ausgegeben und `$null` zurückgegeben — die aufrufenden Commands
    brechen dann still ab (kein Exception-Stacktrace).

.PARAMETER Path
    Startpunkt der Auflösung. Default: aktuelles Arbeitsverzeichnis.
#>
function Resolve-Root {
    [CmdletBinding()]
    param(
        [string] $Path = (Get-Location).Path
    )

    $root = (& git -C $Path rev-parse --show-toplevel 2>$null) -replace '/', '\'
    if (-not $root) {
        Write-Host ""
        Write-Host "  Kein Git-Repository unter '$Path'." -ForegroundColor Red
        Write-Host "  Die Nav-Commands müssen innerhalb des Repos (oder eines Worktrees) laufen." -ForegroundColor Yellow
        Write-Host "  Wechsle ins Repo-Verzeichnis und versuche es erneut." -ForegroundColor DarkGray
        Write-Host ""
        return $null
    }
    return $root
}
