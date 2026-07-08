# ---------------------------------------------------------------------------------------------
#  Baut das Demo mit der Full-Framework-MSBuild.exe (NICHT `dotnet build` — der Nav-Task ist
#  net472 und läuft im MSBuild-Prozess). MSBuild.exe wird über vswhere aufgelöst.
#
#  Standalone-Komfort ohne das nav-Command-System. Im Engine-Repo tut `nav demo` dasselbe
#  (inkl. desselben Build-Tools-Guards). In Visual Studio genügt ein normaler Build.
#
#  Voraussetzung: Die Nav Build Tools müssen publiziert sein (Repo-Root\deploy\Build Tools mit
#  nav.exe) — dort holt der Codegen Task-DLL, Targets und nav.exe. Erzeugt werden sie mit
#  `nav publish` im Engine-Repo.
#
#  Aufruf:  .\build.ps1            # Restore + Build
#           .\build.ps1 -t:Rebuild # zusätzliche MSBuild-Argumente werden durchgereicht
# ---------------------------------------------------------------------------------------------
[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments = $true)] [string[]] $MSBuildArgs)

$ErrorActionPreference = 'Stop'

# Repo-Root: von Demos\NavV2NavigationDemo\ zwei Ebenen hoch.
$repoRoot   = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$buildTools = Join-Path $repoRoot 'deploy\Build Tools'
$navExe     = Join-Path $buildTools 'nav.exe'

if (-not (Test-Path $navExe)) {
    throw "Nav Build Tools nicht gefunden unter '$buildTools' (nav.exe fehlt). " +
          "Bitte zuerst im Engine-Repo 'nav publish' ausführen."
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    $vswhere = "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe"
}
if (-not (Test-Path $vswhere)) {
    throw 'vswhere.exe nicht gefunden — ist Visual Studio installiert?'
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
                      -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild.exe konnte über vswhere nicht gefunden werden.'
}

Write-Host "Verwende MSBuild: $msbuild"

$project = Join-Path $PSScriptRoot 'NavV2NavigationDemo.csproj'
& $msbuild $project -t:Restore,Build -v:minimal -nologo @MSBuildArgs
exit $LASTEXITCODE
