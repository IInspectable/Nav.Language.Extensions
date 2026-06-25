<#
.SYNOPSIS
    Baut den LSP-Server, bettet ihn in die VS-Code-Extension ein und erzeugt ein VSIX.

.DESCRIPTION
    One-Shot-Orchestrierung nach deploy\vscode:
      1. Version aus Version.props (ProductVersion) lesen — eine Quelle der Wahrheit.
      2. Server frisch self-contained publizieren (Publish-Lsp → deploy\lsp\nav.lsp.exe).
      3. Laufzeit-Abhängigkeiten der Extension sicherstellen (npm install).
      4. Server in die Extension einbetten (vscode-nav-lsp\server\nav.lsp.exe).
      5. VSIX paketieren (plattform-spezifisch win32-x64, passend zum win-x64-Server).

    Voraussetzung: Node/npm im PATH (npm install + npx @vscode/vsce) sowie Visual Studio
    (MSBuild) für den Server-Build. Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root).

.PARAMETER Configuration
    Build-Konfiguration für den Server-Publish. Default: Release.

.FUNCTIONALITY
    packagevscode
#>
function Invoke-PackageVsCode {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Release'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $extDir    = Join-Path $root 'vscode-nav-lsp'
    $serverExe = Join-Path $root 'deploy\lsp\nav.lsp.exe'
    $vsixDir   = Join-Path $root 'deploy\vscode'

    # 1) Version aus Version.props ziehen.
    $propsFile = Join-Path $root 'Version.props'
    $version = ([xml](Get-Content -Raw $propsFile)).SelectSingleNode('//ProductVersion').InnerText.Trim()
    if (-not $version) { throw "Konnte ProductVersion nicht aus Version.props lesen." }
    $vsixName = "nav-language-$version-win32-x64.vsix"

    # 2) Server frisch self-contained publizieren.
    Publish-Lsp -Configuration $Configuration
    if (-not (Test-Path $serverExe)) {
        throw "Erwartete Server-Datei nicht gefunden: '$serverExe'."
    }

    # 3) Laufzeit-Abhängigkeiten der Extension sicherstellen (idempotent).
    Push-Location $extDir
    try {
        npm install
        if ($LASTEXITCODE) { throw "npm install fehlgeschlagen (Exit $LASTEXITCODE)." }
    }
    finally { Pop-Location }

    # 4) Server in die Extension einbetten (server\nav.lsp.exe neben extension.js).
    $serverDir = Join-Path $extDir 'server'
    if (Test-Path $serverDir) { Remove-Item -Recurse -Force $serverDir }
    New-Item -ItemType Directory -Path $serverDir | Out-Null
    Copy-Item -Force $serverExe (Join-Path $serverDir 'nav.lsp.exe')

    # 5) VSIX paketieren. Version aus Version.props explizit setzen;
    #    --no-update-package-json/--no-git-tag-version halten package.json und git unangetastet,
    #    --skip-license unterdrückt die LICENSE-Warnung.
    if (-not (Test-Path $vsixDir)) { New-Item -ItemType Directory -Path $vsixDir | Out-Null }
    $vsixPath = Join-Path $vsixDir $vsixName
    Push-Location $extDir
    try {
        npx @vscode/vsce package $version --no-git-tag-version --no-update-package-json `
            --skip-license --target win32-x64 --out $vsixPath
        if ($LASTEXITCODE) { throw "vsce package fehlgeschlagen (Exit $LASTEXITCODE)." }
    }
    finally { Pop-Location }

    Write-Host ""
    Write-Host "VS-Code-Paket erzeugt: $vsixPath" -ForegroundColor Green
    Write-Host 'Installation: VS Code > Extensions > "Install from VSIX..." > obige Datei wählen.' -ForegroundColor DarkGray
}
