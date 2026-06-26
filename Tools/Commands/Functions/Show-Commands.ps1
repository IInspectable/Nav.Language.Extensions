<#
.SYNOPSIS
    Listet alle Nav-Commands (Aufruf, Kurzbeschreibung) auf.

.DESCRIPTION
    Ermittelt die Befehle generisch über Get-NavCommandInfo (parst die Functions-Dateien
    per AST) und gibt sie in zwei Gruppen aus:

      * Repo-Commands → Aufruf über den nav-Dispatcher (`nav <token>`)
      * Navigation    → eigenständige Funktionen (z. B. nav:)

    Dadurch erscheint jeder neue Repo-Command automatisch, sobald seine Funktion eine
    `.FUNCTIONALITY <token>`-Help enthält — ohne diese Liste zu pflegen. Aufruf über `nav` (ohne
    Argument: Auswahlliste) oder `nav help`.

.FUNCTIONALITY
    help
#>
function Show-Commands {
    [CmdletBinding()]
    param()

    $all  = @(Get-NavCommandInfo)
    $repo = @($all | Where-Object { $_.Kind -eq 'Repo' } | ForEach-Object {
            [pscustomobject]@{ Name = $_.Name; Aufruf = "nav $($_.Token)"; Synopsis = $_.Synopsis }
        })
    $nav  = @($all | Where-Object { $_.Kind -eq 'Nav' } | ForEach-Object {
            [pscustomobject]@{ Name = $_.Name; Aufruf = ''; Synopsis = $_.Synopsis }
        })

    # Spaltenbreiten dynamisch aus Inhalt + Überschrift (Funktionsnamen/Tokens variabler Länge).
    $items       = @($repo + $nav)
    $nameWidth   = (@('Funktion') + $items.Name   | Measure-Object -Maximum -Property Length).Maximum
    $aufrufWidth = (@('Aufruf')   + $items.Aufruf | Measure-Object -Maximum -Property Length).Maximum
    $format      = "  {0,-$nameWidth} {1,-$aufrufWidth} {2}"

    function Write-Section {
        param([string] $Title, [object[]] $Items)

        if (-not $Items) { return }
        Write-Host ''
        Write-Host $Title -ForegroundColor Cyan
        foreach ($item in ($Items | Sort-Object Name)) {
            Write-Host ($format -f $item.Name, $item.Aufruf, $item.Synopsis)
        }
    }

    Write-Host ''
    Write-Host 'Nav-Commands' -ForegroundColor Green
    Write-Host ($format -f 'Funktion', 'Aufruf', 'Zweck') -ForegroundColor DarkGray
    Write-Section -Title 'Repo'       -Items $repo
    Write-Section -Title 'Navigation' -Items $nav
    Write-Host ''
    Write-Host 'Details: Tools/Commands/README.md' -ForegroundColor DarkGray
    Write-Host ''
}
