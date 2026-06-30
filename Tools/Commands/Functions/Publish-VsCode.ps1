<#
.SYNOPSIS
    Interner Helfer: publiziert den LSP-Server self-contained direkt in die VS-Code-Extension
    und paketiert daraus ein VSIX nach deploy\vscode.

.DESCRIPTION
    Kein eigener nav-Command (keine .FUNCTIONALITY) — wird von `nav publish` (Invoke-Publish)
    aufgerufen. Ablauf:
      1. Git-abgeleitete Version ermitteln (Get-ProductVersion) — eine Quelle der Wahrheit.
      2. LSP-Server self-contained als Single-File direkt nach vscode-nav-lsp\server\nav.lsp.exe
         publizieren (kein Umweg über deploy\lsp). Flags: PublishSingleFile +
         IncludeNativeLibrariesForSelfExtract → alles in eine exe; EnableCompressionInSingleFile →
         kleinere exe; SatelliteResourceLanguages=en → keine lokalisierten Satellites;
         DebugType=embedded → keine separate .pdb.
      3. Laufzeit-Abhängigkeiten der Extension sicherstellen (npm install).
      4. VSIX paketieren (plattform-spezifisch win32-x64, passend zum win-x64-Server) nach
         deploy\vscode.

    Voraussetzung: Node/npm im PATH (npm install + npx @vscode/vsce) sowie dotnet-SDK für den
    Server-Publish. Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root).

.PARAMETER Configuration
    Build-Konfiguration für den Server-Publish. Default: Debug.
#>
function Publish-VsCode {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $rid       = 'win-x64'
    $extDir    = Join-Path $root 'vscode-nav-lsp'
    $serverDir = Join-Path $extDir 'server'
    $serverExe = Join-Path $serverDir 'nav.lsp.exe'
    $vsixDir   = Join-Path $root 'deploy\vscode'
    $project   = Join-Path $root 'Nav.Language.Lsp\Nav.Language.Lsp.csproj'
    if (-not (Test-Path $project)) { throw "LSP-Projekt nicht gefunden: '$project'." }

    # 1) Git-abgeleitete Version ermitteln (eine Quelle der Wahrheit, 3-teilig — passt zu vsce).
    $version = (Get-ProductVersion -Root $root).Version
    if (-not $version) { throw "Konnte Produktversion nicht ermitteln." }
    $vsixName = "nav-language-$version-win32-x64.vsix"

    # 2) Server frisch self-contained als Single-File direkt in die Extension publizieren.
    #    Zielverzeichnis vorher leeren — der self-contained Publish räumt Altbestand nicht selbst auf.
    if (Test-Path $serverDir) { Remove-Item -Recurse -Force $serverDir }

    & dotnet publish $project -c $Configuration -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en `
        -p:DebugType=embedded `
        -o $serverDir -v:m
    if ($LASTEXITCODE) { throw "LSP-Publish fehlgeschlagen (Exit $LASTEXITCODE)." }
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

    # 4) VSIX paketieren. Git-abgeleitete Version explizit setzen;
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
}
