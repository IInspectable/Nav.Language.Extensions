# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Sprache / Schreibweise

- **Immer echte Umlaute verwenden** (ä, ö, ü, Ä, Ö, Ü, ß) — in Code-Kommentaren, Commit-Messages,
  Dokumentation und sämtlichem erzeugten Text. **Keine** ASCII-Ersatzschreibweisen wie „ae", „oe",
  „ue", „ss".

## Was ist das hier?

Die **Nav Language** ist eine DSL der Pharmatechnik zur Beschreibung von Workflows (im Stil von
UML-Aktivitätsdiagrammen). Aus `.nav`-Dateien wird beim Build **C#-Code generiert**. Dieses Repo
enthält die Sprach-Engine plus mehrere Hosts, die sie konsumieren (VS-Extension, LSP-Server,
MCP-Server, CLI).

## Architektur — eine Engine, mehrere Hosts

Zentrale Idee („eine Engine"): die VS-freie Sprachlogik liegt in **`Nav.Language`** (`netstandard2.0`,
Assembly `Pharmatechnik.Nav.Language`) und wird von **allen** Hosts geteilt. Ein Host fügt nur
Protokoll/UI hinzu, nie eigene Sprachlogik.

- **`Nav.Language`** (netstandard2.0) — der Engine-Kern. Pipeline: `Syntax/` (Lexer/Parser →
  `SyntaxTree`) → `SemanticModel/` (`CodeGenerationUnit`, Symbole) → `Generator/`
  (`NavCodeGeneratorPipeline`, StringTemplate-basierte C#-Erzeugung). VS-freie Feature-Kerne, die
  Hosts gemeinsam nutzen, liegen je Feature in eigenen Ordnern: `Completion/`, `CodeActions/`,
  `CodeFixes/`, `GoTo/`, `QuickInfo/`, `References/`, `FindReferences/`, `Rename/`, `Workspace/`
  (Solution-Discovery, Overlay, `IncludeDependencyGraph`). **Neue Sprach-Features hier bauen**, nicht
  im jeweiligen Host.
- **`Nav.Language.CodeAnalysis`** — Roslyn-Brücke (Nav ↔ generierter C#-Code: `FindSymbols`,
  `FindReferences`, `Annotation`). Bewusst VS/Roslyn-gekoppelt.
- **`Nav.Language.ExtensionShared`** (`.shproj`) + **`Nav.Language.Extension2026`** (net472, VSIX) —
  die Visual-Studio-Extension. Editor-Integration (Classification, Tagging, IntelliSense, Outlining,
  Optionen). Roslyn-/`Microsoft.VisualStudio.Text`-lastig.
- **`Nav.Language.Lsp`** (net10.0, Assembly **`nav.lsp`**) — LSP-Server über `StreamJsonRpc` +
  `Microsoft.VisualStudio.LanguageServer.Protocol`. Stdio, JSON-RPC. Übersetzt nur LSP-DTOs ↔
  Engine-Kerne. Statusdokument: `doc/nav-lsp-status.md` (Pflichtlektüre für LSP-Arbeit).
- **`Nav.Language.Mcp`** (net10.0, Assembly **`nav.mcp`**) — MCP-Server (stdio), teilt die
  Workspace-Host-Schicht mit dem LSP. Tools in `Nav.Language.Mcp/Tools/`: `nav_validate`,
  `nav_outline`, `nav_workspace`, `nav_goto`, `nav_references`, `nav_rename`, `nav_code_actions`.
  **Name-basiert** (ein Agent hat keinen Cursor — Symbole werden über ihren Namen adressiert, aufgelöst
  via `Nav.Language/Symbols/NavSymbolSearch`, dann in die positions-basierten Engine-Kerne gespeist);
  die mutierenden Tools (`nav_rename`, `nav_code_actions`) sind **read-only** und liefern nur das
  Edit-Set. Statusdokument: `doc/nav-mcp-status.md`.
- **`Nav.Cli`** (net472, Assembly **`nav.exe`**) — Kommandozeilen-Codegenerator/Analyzer.
- **`Nav.Language.BuildTasks`** — MSBuild-Task + `Pharmatechnik.Nav.Language.targets`, übersetzt
  `.nav` beim Build zu C#.

`RootNamespace` ist überall `Pharmatechnik.Nav.Language[.*]` (auch wenn der `AssemblyName` abweicht,
z.B. `nav.lsp`) — Namespaces im Code bleiben dadurch stabil.

## Build / Test — Fallstricke zuerst

- **Der .NET-Teil (Engine, LSP, MCP, CLI, Tests) baut mit `dotnet build`/`dotnet publish`.** Der
  StringTemplate-Export in `Nav.Language\CustomBuild.targets` läuft über einen file-based
  dotnet-Generator (`_build\CodeGen\GenerateCodeGenFacts.cs`, via `Exec` `dotnet run --file`) statt
  der alten `CodeTaskFactory` (die in .NET-Core-MSBuild MSB4801 wirft). `n publishlsp` nutzt
  `dotnet publish`.
- **Die ganze Solution (`n build`) braucht weiterhin Full-Framework `MSBuild.exe`** — nur wegen der
  VS-Extension (`Nav.Language.Extension2026`, VSIX/`VSSDK.BuildTools`), die `dotnet build` nicht baut.
- **Bevorzugt das `n`-Command-System** (PowerShell-Dispatcher, Alias `n`) statt MSBuild von Hand —
  es löst MSBuild via `vswhere` und den Repo-Root via `git rev-parse` selbst auf (funktioniert auch
  aus jedem Worktree). Setup: `Tools\Commands\Import-NavCommands.ps1` im `$PROFILE` dot-sourcen.

| Command | Zweck |
|---|---|
| `n build` | Solution bauen (Restore + Build, MSBuild.exe). `-Configuration Release` wird durchgereicht. |
| `n test` | Tests via gebündeltem NUnit-Console-Runner (net472). |
| `n publishlsp` | LSP-Server self-contained als Single-File `deploy\lsp\nav.lsp.exe`. |
| `n packagevscode` | LSP bauen, einbetten, plattform-spezifisches VSIX nach `deploy\vscode`. |
| `n install` | VS-2026-Extension-VSIX in Visual Studio installieren. |
| `n snapshot` | Regression-Snapshots (`.expected.cs`) neu erzeugen. |
| `n incbuild` / `incminor` / `incmajor` | Version in `Version.props` hochzählen. |
| `n newbranch <name>` / `rmbranch` | Branch + Geschwister-Worktree anlegen/entfernen. |
| `n help` / `n` | Übersicht / interaktives Menü. |

Quelle der Wahrheit für Commands sind die `.FUNCTIONALITY`-Tokens in `Tools\Commands\Functions\` —
neue Funktion mit `.FUNCTIONALITY <token>` genügt (Tab-Completion/Menü ziehen automatisch nach).

### Tests im Detail

- **net472:** `n test` (NUnit-Console-Runner unter `_build\nunit.consolerunner\`).
- **.NET 10:** `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (baut bei Bedarf
  selbst; `--no-build` nur als Beschleunigung, wenn vorher schon gebaut wurde).
- `Nav.Language.Tests` ist multi-target (`net472;net10.0`) — neue Engine-Tests müssen auf **beiden**
  TFMs grün sein. Test-Framework ist **NUnit**.
- LSP-Features zusätzlich gern per stdio-Smoke gegen die laufende `nav.lsp` verifizieren.

### LSP-Server lokal (Debug)

- Bauen: `dotnet build Nav.Language.Lsp\Nav.Language.Lsp.csproj -c Debug` →
  `Nav.Language.Lsp\bin\Debug\net10.0\nav.lsp.dll`.
- Starten (framework-dependent): `dotnet Nav.Language.Lsp\bin\Debug\net10.0\nav.lsp.dll`.
- VS-Code-Client (`vscode-nav-lsp`): `npm install`, `npm run esbuild`, Ordner in VS Code öffnen → F5.
  Bündelung via **esbuild** zu `dist/extension.js` (`node_modules` NICHT ins VSIX). Solange der
  Dev-Host läuft, ist die Server-DLL gesperrt → vor Server-Rebuild den Host schließen.

## Arbeitsweise / Workflow

- **Große Aufgaben nach Möglichkeit in mehrere überschaubare Steps zerlegen** und sie nacheinander
  abarbeiten — statt einen großen Wurf auf einmal.
- **Nach jedem Step:** erst ein **Code-Review + Check** (Build/Tests), dann eine fertige
  **Commit-Message** liefern — als Text/Kommentar, nicht als Aktion.
- **Niemals selbst einchecken/committen.** Den eigentlichen Commit macht ausschließlich der Nutzer
  (nach Review + Check). Die gelieferte Commit-Message ist genau dafür da.

## Konventionen

- **Versionierung:** `Version.props` (`<ProductVersion>`) ist die **einzige** Quelle der Wahrheit.
  Die `version` in `vscode-nav-lsp\package.json` ist nur Platzhalter (`0.0.0`); das VSIX bekommt seine
  Version beim Paketieren aus `Version.props`.
- **Stdio-Protokoll-Hosts (LSP, MCP):** `stdout` ist **exklusiv** fürs JSON-RPC-Protokoll. Alle Logs
  MÜSSEN nach `stderr` — sonst zerstören sie die Protokoll-Frames.
- **URI-Fallstrick (Windows):** LSP-Clients prozent-kodieren den Laufwerks-Doppelpunkt
  (`file:///d%3A/...`). `System.Uri.LocalPath` liefert dafür einen kaputten Pfad. **Jeder** URI→Pfad
  am Rand MUSS `NavUri.ToFilePath` nutzen, NIE `rootUri.LocalPath` direkt.
- **`.nav`-Endung exakt prüfen** via `NavSolution.HasNavExtension` — Windows-`EnumerateFiles` matcht
  `*.nav` sonst auch `.navignore` (3-Zeichen-Endungs-Falle).
- `LangVersion` ist projektweit **10.0** (`Directory.Build.props`). NuGet via Central Package
  Management (`Directory.Packages.props`, transitives Pinning aktiv).
