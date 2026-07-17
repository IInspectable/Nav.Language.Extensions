# Nav Language — VS-Code-Extension entwickeln & paketieren

VS-Code-spezifische Entwickler-Doku für die Extension (`vscode-nav-lsp`) und die Anbindung des
Nav-LSP-Servers (`nav.lsp`). Das **Gesamtbild** (bauen/testen/publizieren über alle Hosts) steht in
[`../doc/Development.md`](../doc/Development.md); die nutzerseitige Beschreibung in
[`README.md`](./README.md); der LSP-Status in [`../doc/nav-lsp-status.md`](../doc/nav-lsp-status.md).

## Voraussetzungen

- **.NET-10-SDK** (`dotnet` im PATH) — baut den LSP-Server (framework-dependent für den F5-Debug)
  bzw. publiziert ihn self-contained. Full-Framework-`MSBuild.exe` ist hierfür **nicht** nötig.
- **Node.js / npm** — für `npm install` und die VSIX-Paketierung.
- **VS Code**.

## Einrichten

1. Server bauen:

   ```
   dotnet build Nav.Language.Lsp\Nav.Language.Lsp.csproj -c Debug
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

Die Extension wird mit **esbuild** zu einer einzigen Datei `dist/extension.js` gebündelt —
`vscode-languageclient` samt Abhängigkeiten landet inline, `node_modules` wandert NICHT ins VSIX (sonst
Hunderte loser JS-Dateien und die vsce-Warnung „you should bundle your extension"). Das
`dist/`-Verzeichnis ist ein Build-Artefakt (in `.gitignore`).

- `npm run esbuild` — Dev-Build mit Sourcemap.
- `npm run esbuild-watch` — Watch-Build für die Entwicklung.
- `npm run vscode:prepublish` — Minify-Build; läuft automatisch bei `vsce package` (siehe unten).

Vor dem **F5**-Debuggen einmal `npm run esbuild` (oder `esbuild-watch` im Hintergrund) ausführen, damit
`dist/extension.js` existiert — `package.json` zeigt mit `"main": "./dist/extension.js"` darauf.

## Server-Auflösung (`extension.js`)

`resolveServer(extRoot)` leitet alle Pfade von `context.extensionPath` (der Extension-Wurzel) ab — NICHT
von `__dirname`, das nach dem Bündeln ins `dist/`-Verzeichnis zeigt. Gesucht wird in dieser Reihenfolge:

1. Konfigurierter Pfad (`navLanguageServer.serverPath`) — `.exe` direkt, `.dll` via `dotnet`.
2. **Eingebettet:** `server/nav.lsp.exe` in der Extension-Wurzel (greift im installierten VSIX).
3. **Repo (F5):** `../deploy/lsp/nav.lsp.exe` (nur falls dorthin manuell publiziert — `nav publish`
   legt den Server nicht mehr dort ab, sondern direkt in `server/`).
4. **Repo (F5):** `../Nav.Language.Lsp/bin/Debug/net10.0/nav.lsp.dll` via `dotnet`.

## VSIX paketieren & installieren

`nav publish` erzeugt in einem Aufruf ein **fertiges, self-contained VSIX** (als Teil des
Gesamt-Publishes; siehe [`../doc/Development.md`](../doc/Development.md)): Der LSP-Server wird
self-contained als Single-File (`win-x64`, genau eine Datei inkl. gebündelter .NET-Runtime — keine
separate Runtime, keine losen DLLs) **direkt** als `vscode-nav-lsp/server/nav.lsp.exe` in die Extension
publiziert und plattform-spezifisch via `npx @vscode/vsce package --target win32-x64` paketiert. Ein
eigenständiges `deploy\lsp` entsteht dabei nicht.

Unter der Haube (gekürzt): `dotnet publish …Nav.Language.Lsp.csproj -r win-x64 --self-contained true
-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
-p:SatelliteResourceLanguages=en -p:DebugType=embedded -o …\vscode-nav-lsp\server`.

Ergebnis: `deploy\vscode\nav-language-vscode-<version>-win32-x64.vsix` (bringt Server + .NET-Runtime mit —
kein separates `dotnet`, keine Pfad-Konfiguration nötig). Die `<version>` ermittelt das Skript
git-abgeleitet (`Get-ProductVersion`, aus `git describe`) — eine Quelle der Wahrheit; `package.json` wird
beim Paketieren nicht verändert. Die Marketplace-README wird beim Paketieren aus der Repo-Root-`README.md`
in die Extension gespiegelt (`vscode-nav-lsp\README.md` ist generiert und in `.gitignore`). Voraussetzung:
**Node/npm im PATH**.

Installieren in VS Code: **Extensions ▸ ⋯ ▸ „Install from VSIX…"** → die obige Datei wählen. Danach eine
`.nav`-Datei öffnen; der Server startet aus dem eingebetteten `server/nav.lsp.exe`.
