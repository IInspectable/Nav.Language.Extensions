# Nav Language — Entwicklung & Paketierung

Entwickler-Doku für die VS-Code-Extension (`vscode-nav-lsp`) und den Nav-LSP-Server (`nav.lsp`). Die
nutzerseitige Beschreibung steht in [`README.md`](./README.md); der Gesamt-Status des LSP-Projekts in
[`../doc/nav-lsp-status.md`](../doc/nav-lsp-status.md).

## Voraussetzungen

- **Visual Studio / Full-Framework-`MSBuild.exe`** — für den Server-Build (die Engine nutzt in
  `Nav.Language\CustomBuild.targets` die `CodeTaskFactory`, die `dotnet build`/`dotnet publish` nicht kennt).
- **.NET-10-Runtime** (`dotnet` im PATH) — nur für den framework-dependent Debug-Lauf. Der self-contained
  Publish bringt die Runtime selbst mit.
- **Node.js / npm** — für `npm install` und die VSIX-Paketierung.
- **VS Code**.

## Einrichten

1. Server bauen (Full-Framework-MSBuild):

   ```
   MSBuild.exe Nav.Language.Lsp\Nav.Language.Lsp.csproj -t:Build -p:Configuration=Debug
   ```

   Ergebnis: `Nav.Language.Lsp\bin\Debug\net10.0\nav.lsp.dll`.

2. Client-Abhängigkeiten installieren:

   ```
   cd vscode-nav-lsp
   npm install
   ```

## Debuggen (F5)

- Ordner `vscode-nav-lsp` in VS Code öffnen und **F5** drücken
  („Nav LSP Extension (Extension Development Host)").
- Im Entwicklungs-Host einen **Ordner mit `.nav`-Dateien** öffnen.
- Eine `.nav`-Datei öffnen → Fehler/Warnungen erscheinen als Squiggles und im Problems-Panel.
  Beim Tippen aktualisieren sich die Diagnostics (Overlay), beim Schließen gilt wieder der Platteninhalt.

## Bündeln (esbuild)

Die Extension wird mit **esbuild** zu einer einzigen Datei `dist/extension.js` gebündelt — `vscode-languageclient`
samt Abhängigkeiten landet inline, `node_modules` wandert NICHT ins VSIX (sonst Hunderte loser JS-Dateien und die
vsce-Warnung „you should bundle your extension"). Das `dist/`-Verzeichnis ist ein Build-Artefakt (in `.gitignore`).

- `npm run esbuild` — Dev-Build mit Sourcemap.
- `npm run esbuild-watch` — Watch-Build für die Entwicklung.
- `npm run vscode:prepublish` — Minify-Build; läuft automatisch bei `vsce package` (siehe unten).

Vor dem **F5**-Debuggen einmal `npm run esbuild` (oder `esbuild-watch` im Hintergrund) ausführen, damit
`dist/extension.js` existiert — `package.json` zeigt mit `"main": "./dist/extension.js"` darauf.

## Server-Auflösung (`extension.js`)

`resolveServer(extRoot)` leitet alle Pfade von `context.extensionPath` (der Extension-Wurzel) ab — NICHT von
`__dirname`, das nach dem Bündeln ins `dist/`-Verzeichnis zeigt. Gesucht wird in dieser Reihenfolge:

1. Konfigurierter Pfad (`navLanguageServer.serverPath`) — `.exe` direkt, `.dll` via `dotnet`.
2. **Eingebettet:** `server/nav.lsp.exe` in der Extension-Wurzel (greift im installierten VSIX).
3. **Repo (F5):** `../deploy/lsp/nav.lsp.exe` (self-contained Publish).
4. **Repo (F5):** `../Nav.Language.Lsp/bin/Debug/net10.0/nav.lsp.dll` via `dotnet`.

## Server publizieren (self-contained)

`n publishlsp` erzeugt einen **self-contained Single-File**-Server unter `deploy\lsp`:
**genau eine Datei** `deploy\lsp\nav.lsp.exe` (inkl. gebündelter .NET-Runtime, ~39 MB komprimiert — keine
separate Runtime-Installation, keine losen DLLs, keine Satellite-Ressourcen-Ordner). Die Extension findet
diese Ausgabe im F5-Workflow automatisch.

Der Publish läuft bewusst über die Full-Framework-`MSBuild.exe` (wie `n build`). Unter der Haube (gekürzt):
`MSBuild.exe …Nav.Language.Lsp.csproj -restore -t:Publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en`.

## VSIX paketieren & installieren

`n packagevscode` erzeugt in einem Aufruf ein **fertiges, self-contained VSIX**:
es baut den Server (`n publishlsp`), bettet `nav.lsp.exe` als `vscode-nav-lsp/server/nav.lsp.exe`
in die Extension ein und paketiert plattform-spezifisch via `npx @vscode/vsce package --target win32-x64`.

Ergebnis: `deploy\vscode\nav-language-<version>-win32-x64.vsix` (~33 MB, bringt Server + .NET-Runtime mit —
kein separates `dotnet`, keine Pfad-Konfiguration nötig). Die `<version>` zieht das Skript aus
`Version.props` (`ProductVersion`) im Repo-Root — eine Quelle der Wahrheit; `package.json` wird beim
Paketieren nicht verändert. Voraussetzung: **Node/npm im PATH**.

Installieren in VS Code: **Extensions ▸ ⋯ ▸ „Install from VSIX…"** → die obige Datei wählen. Danach eine
`.nav`-Datei öffnen; der Server startet aus dem eingebetteten `server/nav.lsp.exe`.
