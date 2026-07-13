<#
.SYNOPSIS
    Ermittelt den Pfad zur TFS-Kommandozeile tf.exe über vswhere.

.DESCRIPTION
    Sucht via vswhere die neueste Visual-Studio-Installation und lokalisiert darin die
    Team-Explorer-tf.exe
    (`Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\tf.exe`). Bei
    Misserfolg wird ein freundlicher Hinweis ausgegeben und `$null` zurückgegeben (die
    aufrufenden Commands brechen dann ab). Interner Helper (Verb-Noun ohne
    .FUNCTIONALITY → kein nav-Command).
#>
function Resolve-Tf {
    [CmdletBinding()]
    param()

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        Write-Host "vswhere.exe nicht gefunden — ist Visual Studio installiert?" -ForegroundColor Red
        return $null
    }

    $tf = & $vswhere -latest `
        -find 'Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\tf.exe' |
        Select-Object -First 1

    if (-not $tf -or -not (Test-Path $tf)) {
        Write-Host "tf.exe konnte nicht gefunden werden — ist die Team-Explorer-Komponente installiert?" -ForegroundColor Red
        return $null
    }

    return $tf
}
