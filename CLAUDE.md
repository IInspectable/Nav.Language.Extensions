# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Sprache / Schreibweise

- **Immer echte Umlaute verwenden** (ä, ö, ü, Ä, Ö, Ü, ß) — in Code-Kommentaren, Commit-Messages,
  Dokumentation und sämtlichem erzeugten Text, insbesondere auch in `.cs`- und `.md`-Dateien.
  **Keine** ASCII-Ersatzschreibweisen wie „ae", „oe", „ue", „ss".
- **Standard-Dateikodierung ist UTF-8 *mit* BOM.** Gilt für **alle** Textdateien, die hier erzeugt
  oder bearbeitet werden — `.cs`, `.md`, sowie Projekt-/Build-Dateien (`.csproj`, `.props`, `.targets`
  und sonstige MSBuild-Files, `.slnx`/`.sln` etc.). Neue Dateien immer als UTF-8 mit BOM anlegen;
  bestehende Dateien beim Bearbeiten in dieser Kodierung belassen.
- **In der Quellcode-Dokumentation (Code-Kommentare, XML-Doku) nicht auf „Steps" eines Plans
  verweisen.** Plan-Steps sind ein temporäres Arbeits-Artefakt und haben im dauerhaften Code keinen
  Platz — Doku beschreibt den Code, nicht den Weg dorthin.

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
  `nav_diagnostics`, `nav_outline`, `nav_workspace`, `nav_find_symbol`, `nav_goto`, `nav_references`,
  `nav_rename`, `nav_code_actions`. `nav_diagnostics` ist das workspace-weite Gegenstück zu
  `nav_validate` (Pull-Äquivalent zum LSP-Diagnostics-Push): aggregiert Diagnostics über alle bzw. per
  `filter`/`severity` eingegrenzten `.nav`, gepaged. **Name-basiert** (ein Agent hat keinen Cursor — Symbole werden über ihren Namen
  adressiert, aufgelöst via `Nav.Language/Symbols/NavSymbolSearch`, dann in die positions-basierten
  Engine-Kerne gespeist). `nav_find_symbol` ist der Einstieg, wenn die Datei noch unbekannt ist: solution-
  weite Präfix-Suche nach Definitionen, deren Pfad dann an die übrigen (datei-gebundenen) Tools geht;
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
  dotnet-Generator (`Build\CodeGen\GenerateCodeGenFacts.cs`, via `Exec` `dotnet run --file`) statt
  der alten `CodeTaskFactory` (die in .NET-Core-MSBuild MSB4801 wirft). Die Single-File-Publishes
  (LSP/MCP) in `n publish` nutzen `dotnet publish`.
- **Die ganze Solution (`n build`) braucht weiterhin Full-Framework `MSBuild.exe`** — nur wegen der
  VS-Extension (`Nav.Language.Extension2026`, VSIX/`VSSDK.BuildTools`), die `dotnet build` nicht baut.
- **Bevorzugt das `n`-Command-System** (PowerShell-Dispatcher, Alias `n`) statt MSBuild von Hand —
  es löst MSBuild via `vswhere` und den Repo-Root via `git rev-parse` selbst auf (funktioniert auch
  aus jedem Worktree). Setup: `Tools\Commands\Import-NavCommands.ps1` im `$PROFILE` dot-sourcen.

| Command | Zweck |
|---|---|
| `n build` | Solution bauen (Restore + Build, MSBuild.exe). `-Configuration Release` wird durchgereicht. |
| `n test` | Tests via gebündeltem NUnit-Console-Runner (net472). |
| `n publish` | Solution (Debug) bauen und alle Deliverables unter `deploy\` bereitstellen: `Build Tools`, VS-Code-Extension (`deploy\vscode`, mit eingebettetem LSP) und MCP-Single-File (`deploy\mcp\nav.mcp.exe`). |
| `n install` | VS-2026-Extension-VSIX in Visual Studio installieren. |
| `n snapshot` | Regression-Snapshots (`.expected.cs`) neu erzeugen. |
| `n incminor` / `incmajor` | Nächstes Minor-/Major-Version-Tag (`vX.Y.0`) auf HEAD anlegen (`-Push`/`-Force`). Der Patch zählt automatisch — kein `incbuild` mehr. |
| `n newbranch <name>` / `rmbranch` | Branch + Geschwister-Worktree anlegen/entfernen. |
| `n help` / `n` | Übersicht / interaktives Menü. |

Quelle der Wahrheit für Commands sind die `.FUNCTIONALITY`-Tokens in `Tools\Commands\Functions\` —
neue Funktion mit `.FUNCTIONALITY <token>` genügt (Tab-Completion/Menü ziehen automatisch nach).

### Tests im Detail

- **net472:** `n test` (NUnit-Console-Runner unter `Build\nunit.consolerunner\`).
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

- **Versionierung:** Die Version wird **git-abgeleitet** (`git describe`) — es gibt kein
  `Version.props` mehr. Kern (3-teiliges SemVer, Pflicht für vsce + VS-VSIX-Manifest +
  AssemblyVersion): `Major.Minor.(Patch des letzten vX.Y.Z-Tags + Commits seit Tag)`. Major/Minor
  werden per Tag gesteuert (`n incminor`/`incmajor` → `git tag vX.Y.0`), der Patch zählt automatisch;
  ein getaggter Commit ist exakt die Release-Version. Branch + Kurz-SHA landen nur in
  `AssemblyInformationalVersion`/CLI, nie im Kern. **Einzige Autorität ist das MSBuild-Target
  `ComputeGitVersion`** (`Build\Version.targets`, über `Directory.Build.props` in jedem Build-Pfad
  aktiv) — es rechnet die Version selbst aus git, in `n build`/MSBuild.exe genauso wie in
  `dotnet build`/`dotnet publish`/VS-IDE. PowerShell rechnet **nichts** nach und reicht **kein**
  `-p:ProductVersion` durch; `Get-ProductVersion` liest die berechneten Werte nur per
  `dotnet msbuild … -getProperty` (für vsce-/VSIX-Dateinamen). Bei git-Ausfall fällt das Target
  robust auf `0.0.<Commit-Anzahl>` zurück (nur reine Zahlen, kein git-Fehlertext). Für SDK-Hosts
  ohne `GobalAssemblyInfo.cs` (LSP/MCP) speist `SetSdkVersionFromGit` die Werte in die
  SDK-Assembly-Info-Pipeline. Die `version` in `vscode-nav-lsp\package.json` bleibt Platzhalter
  (`0.0.0`). `AssemblyVersion` ist bewusst `Major.Minor.0.0` (stabile Binding-Identität); die volle
  Buildnummer steht in `AssemblyFileVersion`. Die `MyAssembly.{ProductVersion,…}`-Konstanten (von
  `GobalAssemblyInfo.cs` in Attribute gehoben und zur Laufzeit gelesen) erzeugt der Roslyn-Generator
  `Nav.AssemblyInfo.SourceGenerator` — Opt-in je Projekt mit `<UseAssemblyInfoGenerator>true</…>`
  (zentrales Wiring: `Build\SourceGenerators\SourceGenerator.targets` via `Directory.Build.targets`).
  Generator-Output ist pro Compilation, nie eine geteilte Projektverzeichnis-Datei → immun gegen die
  frühere VS-Design-Time-Falle. **Erzwungene Ausnahme** `Nav.Language.Extension2026` (legacy, non-SDK,
  WPF): Im WPF-Markup-Temp-Teilprojekt (`…_wpftmp.csproj`) laufen Quellgeneratoren **nicht** — mit dem
  Generator bräche der Build dort mit `CS0122` (empirisch verifiziert per kontrolliertem Differenz-Test).
  Deshalb schreibt hier `WriteMyAssemblyFile` (in `CustomBuild.targets`) `MyAssembly` als **physische**
  obj-Datei, die als `@(Compile)` auch ins wpftmp fließt. Das ist die unvermeidbare WPF-Ausnahme, **kein**
  Fallback — und **nicht** strukturell immun wie der Generator, sondern am Design-Time-Guard (+ funktionierendem
  git) hängend. Details: `doc/nav-versioning-status.md`.
- **Stdio-Protokoll-Hosts (LSP, MCP):** `stdout` ist **exklusiv** fürs JSON-RPC-Protokoll. Alle Logs
  MÜSSEN nach `stderr` — sonst zerstören sie die Protokoll-Frames.
- **URI-Fallstrick (Windows):** LSP-Clients prozent-kodieren den Laufwerks-Doppelpunkt
  (`file:///d%3A/...`). `System.Uri.LocalPath` liefert dafür einen kaputten Pfad. **Jeder** URI→Pfad
  am Rand MUSS `NavUri.ToFilePath` nutzen, NIE `rootUri.LocalPath` direkt.
- **`.nav`-Endung exakt prüfen** via `NavSolution.HasNavExtension` — Windows-`EnumerateFiles` matcht
  `*.nav` sonst auch `.navignore` (3-Zeichen-Endungs-Falle).
- `LangVersion` ist projektweit **10.0** (`Directory.Build.props`). NuGet via Central Package
  Management (`Directory.Packages.props`, transitives Pinning aktiv).
