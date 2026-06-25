# Nav Language — VS-Code-Client

VS-Code-Client, der den Nav-LSP-Server (`nav.lsp`) über stdio startet und `.nav`-Dateien mit
Diagnostics, Hover, Completion, GoTo/References, Rename, Code Actions u. a. versorgt.

## Voraussetzungen

- **.NET-10-Runtime** (`dotnet` im PATH) — nur für den framework-dependent Debug-Lauf.
  Beim self-contained Publish (siehe „Deployment") bringt `nav.lsp.exe` die Runtime selbst mit.
- **Node.js / npm** (für `npm install`)
- **VS Code**

## Einrichten

1. Server bauen (Full-Framework-MSBuild, da die Engine `CodeTaskFactory` nutzt):

   ```
   MSBuild.exe Nav.Language.Server\Nav.Language.Server.csproj -t:Build -p:Configuration=Debug
   ```

   Ergebnis: `Nav.Language.Server\bin\Debug\net10.0\nav.lsp.dll`.

2. Client-Abhängigkeiten installieren:

   ```
   cd vscode-nav-lsp
   npm install
   ```

## Ausprobieren

- Ordner `vscode-nav-lsp` in VS Code öffnen und **F5** drücken
  („Nav LSP Extension (Extension Development Host)").
- Im Entwicklungs-Host einen **Ordner mit `.nav`-Dateien** öffnen.
- Eine `.nav`-Datei öffnen → Fehler/Warnungen erscheinen als Squiggles und im Problems-Panel.
  Beim Tippen aktualisieren sich die Diagnostics (Overlay), beim Schließen gilt wieder der Platteninhalt.

## Konfiguration

- `navLanguageServer.serverPath`: Pfad zum Server. Endung `.exe` → wird direkt gestartet
  (z. B. der self-contained `nav.lsp.exe`); Endung `.dll` → wird via `dotnet <dll>` gestartet.
  Leer = Auto-Erkennung: zuerst der eingebettete `server/nav.lsp.exe` (paketiertes VSIX), dann
  `../deploy/lsp/nav.lsp.exe` (Repo, self-contained Publish), sonst der Debug-Build
  `../Nav.Language.Server/bin/Debug/net10.0/nav.lsp.dll` via `dotnet`.

## Paketieren & Installieren (VSIX)

`Package-VsCode.bat` (Repo-Root) erzeugt in einem Aufruf ein **fertiges, self-contained VSIX**:
es baut den Server (`Publish-Lsp.bat`), bettet `nav.lsp.exe` als `vscode-nav-lsp/server/nav.lsp.exe`
in die Extension ein und paketiert plattform-spezifisch via `npx @vscode/vsce package --target win32-x64`.

Ergebnis: `deploy\vscode\nav-language-1.0.0-win32-x64.vsix` (~40 MB, bringt Server + .NET-Runtime mit —
kein separates `dotnet`, keine Pfad-Konfiguration nötig). Voraussetzung: **Node/npm im PATH**.

Installieren in VS Code: **Extensions ▸ … ▸ „Install from VSIX…"** → die obige Datei wählen. Danach eine
`.nav`-Datei öffnen; der Server startet aus dem eingebetteten `server/nav.lsp.exe`.

## Deployment (self-contained)

`Publish-Lsp.bat` (Repo-Root) erzeugt einen **self-contained Single-File**-Server unter `deploy\lsp`:
**genau eine Datei** `deploy\lsp\nav.lsp.exe` (inkl. gebündelter .NET-Runtime, ~39 MB komprimiert — keine
separate Runtime-Installation, keine losen DLLs, keine Satellite-Ressourcen-Ordner). Die Extension findet
diese Ausgabe automatisch.

Der Publish läuft bewusst über die Full-Framework-`MSBuild.exe` (wie `Build.bat`): die referenzierte
Engine nutzt in `Nav.Language\CustomBuild.targets` die `CodeTaskFactory`, die `dotnet build`/
`dotnet publish` nicht kennt (MSB4801). Unter der Haube (gekürzt):
`MSBuild.exe …Nav.Language.Server.csproj -restore -t:Publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en`.
