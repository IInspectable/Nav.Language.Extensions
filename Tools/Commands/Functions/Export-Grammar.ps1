<#
.SYNOPSIS
    Interner Helfer: exportiert die EBNF-Grammatik der Nav-Sprache nach deploy\Build Tools\NavGrammar.ebnf.

.DESCRIPTION
    Kein eigener nav-Command (keine .FUNCTIONALITY) — wird von `nav publish` (Invoke-Publish) NACH
    Publish-Cli aufgerufen. Führt die zuvor dorthin gelegte self-contained nav.exe mit dem Subcommand
    `grammar` aus und schreibt deren Ausgabe als NavGrammar.ebnf in dasselbe Verzeichnis — derselbe
    Zielort wie früher die ANTLR-`.g4`. Die Grammatik selbst wird zur Compile-Zeit aus den Parse*-
    EBNF-Fragmenten des handgeschriebenen Parsers zusammengesetzt (NavGrammar.Ebnf in der Engine).

    WICHTIG (Reihenfolge): Muss NACH Publish-Cli laufen — die nav.exe muss in deploy\Build Tools
    liegen. Das Verzeichnis wird bewusst NICHT geleert (Task-DLL/Targets/nav.exe bleiben erhalten).

    Kodierung: UTF-8 ohne BOM — reines, maschinengeneriertes Daten-Artefakt (kein Repo-Quelltext);
    BOM-frei für maximale Werkzeug-Verträglichkeit.

.PARAMETER Configuration
    Nur fürs einheitliche Signaturbild der Publish-Helfer; der Export selbst ist konfigurationsneutral.
#>
function Export-Grammar {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $buildTools = Join-Path $root 'deploy\Build Tools'
    $exe        = Join-Path $buildTools 'nav.exe'
    if (-not (Test-Path $exe)) {
        throw "nav.exe nicht gefunden: '$exe'. Export-Grammar muss nach Publish-Cli laufen."
    }

    $target  = Join-Path $buildTools 'NavGrammar.ebnf'
    $grammar = & $exe grammar
    if ($LASTEXITCODE) { throw "nav grammar fehlgeschlagen (Exit $LASTEXITCODE)." }

    $enc = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($target, (($grammar -join "`r`n") + "`r`n"), $enc)

    Write-Host ""
    Write-Host "Grammatik exportiert nach: $target" -ForegroundColor Green
}
