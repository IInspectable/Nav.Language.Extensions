<#
.SYNOPSIS
    Installiert die VS-2026-Extension (VSIX) in Visual Studio.

.DESCRIPTION
    Nimmt das publizierte VSIX aus dem Deploy-Verzeichnis
    (deploy\Vsix\Pharmatechnik.Nav.Language.Extension.2026-<ProductVersion>.vsix) und startet den
    VSIXInstaller von Visual Studio (über vswhere ermittelt). Die Version wird git-abgeleitet
    ermittelt (Get-ProductVersion, eine Quelle der Wahrheit), der Repo-Root wird zur Aufruf-Zeit
    aufgelöst (Resolve-Root).

    Bewusst aus deploy\ statt aus bin\: das DeployFiles-Target befüllt deploy\Vsix bei jedem
    Extension-Build (also via `nav build` wie `nav publish`) und benennt das VSIX dabei auf den
    versionierten Namen um. Fehlt das VSIX, weist der Befehl darauf hin, statt versehentlich
    einen alten/halben Stand zu installieren.

    Hinweis: Der frühere Install.bat hat das VSIX fälschlich "dot-gesourct" — das funktioniert
    nicht; korrekt ist der Aufruf über VSIXInstaller.exe.

.FUNCTIONALITY
    install
#>
function Install-Extension {
    [CmdletBinding()]
    param()

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    # Git-abgeleitete Version ermitteln (eine Quelle der Wahrheit) — sie ist Teil des Deploy-Namens.
    $version = (Get-ProductVersion -Root $root).Version
    if (-not $version) { throw "Konnte Produktversion nicht ermitteln." }

    $vsix = Join-Path $root "deploy\Vsix\Pharmatechnik.Nav.Language.Extension.2026-$version.vsix"
    if (-not (Test-Path $vsix)) {
        Write-Host "VSIX nicht gefunden: '$vsix'" -ForegroundColor Red
        Write-Host "  Zuerst bauen oder publizieren ('nav build' bzw. 'nav publish') — das befüllt deploy\Vsix." -ForegroundColor Yellow
        return
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        Write-Host "vswhere.exe nicht gefunden — ist Visual Studio installiert?" -ForegroundColor Red
        return
    }
    $installPath = & $vswhere -latest -property installationPath | Select-Object -First 1
    $installer = Join-Path $installPath 'Common7\IDE\VSIXInstaller.exe'
    if (-not (Test-Path $installer)) {
        Write-Host "VSIXInstaller.exe nicht gefunden unter '$installer'." -ForegroundColor Red
        return
    }

    Write-Host "Installiere $vsix ..." -ForegroundColor Cyan
    & $installer $vsix
}
