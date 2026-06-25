<#
.SYNOPSIS
    Erzeugt die Regression-Snapshots (.expected.cs) neu.

.DESCRIPTION
    Löscht im Ordner Nav.Language.Tests\Regression\Tests alle vorhandenen *.expected.cs und
    legt sie aus den aktuellen *.cs neu an (Copy → .expected.cs). Damit wird der erwartete
    Generator-Output auf den aktuellen Stand gehoben. Der Repo-Root wird zur Aufruf-Zeit
    aufgelöst (Resolve-Root).

    ACHTUNG: Übernimmt den aktuellen Ist-Zustand als Soll — nur nach bewusster Prüfung der
    Generator-Änderungen aufrufen.

.FUNCTIONALITY
    snapshot
#>
function Invoke-Snapshot {
    [CmdletBinding()]
    param()

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $dir = Join-Path $root 'Nav.Language.Tests\Regression\Tests'
    if (-not (Test-Path $dir)) { throw "Regression-Ordner nicht gefunden: '$dir'." }

    Get-ChildItem $dir -Recurse -Filter '*.expected.cs' | Remove-Item -Verbose

    Get-ChildItem $dir -Recurse -Filter '*.cs' | ForEach-Object {
        $newName = [IO.Path]::ChangeExtension($_.FullName, 'expected.cs')
        Copy-Item $_.FullName $newName -PassThru | Select-Object FullName
    }
}
