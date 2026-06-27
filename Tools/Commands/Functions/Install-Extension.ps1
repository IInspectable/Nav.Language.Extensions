<#
.SYNOPSIS
    Installiert die VS-2026-Extension (VSIX) in Visual Studio.

.DESCRIPTION
    Sucht das gebaute VSIX des Projekts Nav.Language.Extension2026
    (bin\<Configuration>\Pharmatechnik.Nav.Language.Extension.2026-<Version>.vsix, neuestes per Glob)
    und startet den
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

    # Der VSIX-Name enthält die Version (TargetVsixContainerName, siehe CustomBuild.targets), daher
    # per Glob das neueste VSIX greifen statt einen festen Namen anzunehmen.
    $binDir = Join-Path $root "Nav.Language.Extension2026\bin\$Configuration"
    $vsix = Get-ChildItem -Path $binDir -Filter 'Pharmatechnik.Nav.Language.Extension.2026*.vsix' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    if (-not $vsix) {
        Write-Host "VSIX nicht gefunden in: '$binDir'" -ForegroundColor Red
        Write-Host "  Zuerst die Extension bauen (z. B. 'nav build')." -ForegroundColor Yellow
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
