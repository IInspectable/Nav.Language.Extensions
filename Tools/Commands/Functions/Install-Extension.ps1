<#
.SYNOPSIS
    Installiert die VS-2026-Extension (VSIX) in Visual Studio.

.DESCRIPTION
    Sucht das gebaute VSIX des Projekts Nav.Language.Extension2026
    (bin\<Configuration>\Pharmatechnik.Nav.Language.Extension.2026.vsix) und startet den
    VSIXInstaller von Visual Studio (über vswhere ermittelt). Der Repo-Root wird zur
    Aufruf-Zeit aufgelöst (Resolve-Root).

    Hinweis: Der frühere Install.bat hat das VSIX fälschlich "dot-gesourct" — das funktioniert
    nicht; korrekt ist der Aufruf über VSIXInstaller.exe.

.PARAMETER Configuration
    Konfiguration des gebauten VSIX. Default: Debug.

.FUNCTIONALITY
    install
#>
function Install-Extension {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $vsix = Join-Path $root "Nav.Language.Extension2026\bin\$Configuration\Pharmatechnik.Nav.Language.Extension.2026.vsix"
    if (-not (Test-Path $vsix)) {
        Write-Host "VSIX nicht gefunden: '$vsix'" -ForegroundColor Red
        Write-Host "  Zuerst die Extension bauen (z. B. 'n build')." -ForegroundColor Yellow
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
