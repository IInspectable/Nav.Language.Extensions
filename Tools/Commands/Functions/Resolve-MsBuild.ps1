<#
.SYNOPSIS
    Ermittelt den Pfad zur Full-Framework-MSBuild.exe über vswhere.

.DESCRIPTION
    Sucht via vswhere die neueste Visual-Studio-Installation mit MSBuild-Komponente und
    gibt den Pfad zu MSBuild.exe zurück. Bei Misserfolg wird ein freundlicher Hinweis
    ausgegeben und `$null` zurückgegeben (die aufrufenden Commands brechen dann ab).

    `nav build` läuft über diese MSBuild.exe, weil die Solution die VS-Extension
    (Nav.Language.Extension2026, VSIX/VSSDK.BuildTools) enthält, die nur Full-Framework-
    MSBuild.exe baut. Der .NET-Teil (Engine, LSP, MCP, CLI, Tests) baut/publisht dagegen mit
    dem dotnet-SDK (die Single-File-Publishes in `nav publish` nutzen `dotnet publish`). Interner Helper (Verb-Noun ohne
    .FUNCTIONALITY → kein nav-Command).
#>
function Resolve-MsBuild {
    [CmdletBinding()]
    param()

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        Write-Host "vswhere.exe nicht gefunden — ist Visual Studio installiert?" -ForegroundColor Red
        return $null
    }

    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

    if (-not $msbuild -or -not (Test-Path $msbuild)) {
        Write-Host "MSBuild.exe konnte nicht gefunden werden." -ForegroundColor Red
        return $null
    }

    return $msbuild
}
