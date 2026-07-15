# Entwicklung & Tooling

Entwickler-Doku für das Gesamt-Repo `Nav.Language.Extensions`: bauen, testen, die einzelnen Hosts
lokal starten/debuggen und die Deliverables publizieren. Die nutzerseitige Beschreibung steht in
[`README.md`](../README.md); die verbindlichen Projektregeln in [`CLAUDE.md`](../CLAUDE.md).

## Überblick — eine Engine, mehrere Hosts

Die VS-freie Sprachlogik liegt in **`Nav.Language`** (`netstandard2.0`, Assembly
`Pharmatechnik.Nav.Language`) und wird von allen Hosts geteilt. Ein Host fügt nur Protokoll/UI hinzu,
nie eigene Sprachlogik:

| Host | TFM / Assembly | Rolle |
|---|---|---|
| `Nav.Language` | netstandard2.0 | Engine-Kern (Syntax → SemanticModel → Generator). |
| `Nav.Language.CodeAnalysis` | — | Roslyn-Brücke (Nav ↔ generierter C#-Code). |
| `Nav.Language.Extension2026` | net472, VSIX | Visual-Studio-2026-Extension. |
| `Nav.Language.Lsp` | net10.0, `nav.lsp` | LSP-Server (stdio, JSON-RPC). |
| `Nav.Language.Mcp` | net10.0, `nav.mcp` | MCP-Server (stdio). |
| `Nav.Cli` | net472, `nav.exe` | CLI-Codegenerator/Analyzer. |
| `Nav.Language.BuildTasks` | — | MSBuild-Task, übersetzt `.nav` beim Build zu C#. |

## Voraussetzungen

- **.NET-10-SDK** (`dotnet` im PATH) — baut, testet und publiziert den gesamten .NET-Teil (Engine,
  LSP, MCP, CLI, Tests). Die Codegen-Facts/-Emitter sind handgeschriebenes C#; es gibt keinen
  `dotnet run --file`-Schritt und keine `CodeTaskFactory` mehr im Build.
- **Full-Framework-`MSBuild.exe`** (Visual Studio 2026 oder Build Tools) — **nur** für die *ganze*
  Solution (`nav build`), und zwar ausschließlich wegen der VS-Extension
  (`Nav.Language.Extension2026`, VSIX/`VSSDK.BuildTools`), die `dotnet build` nicht baut. Einzelne
  .NET-Hosts (LSP/MCP/CLI) brauchen MSBuild.exe **nicht**.
- **Node.js / npm** — nur für die VS-Code-Paketierung (`npm install` + `@vscode/vsce`).

## Das `nav`-Command-System

Bevorzugter Einstieg statt MSBuild von Hand: der PowerShell-Dispatcher mit Alias **`nav`**. Er löst
MSBuild via `vswhere` und den Repo-Root via `git rev-parse` selbst auf — funktioniert aus jedem
Worktree. Einmalig im `$PROFILE` dot-sourcen:

```powershell
. "C:\ws\git\Nav.Language.Extensions\Tools\Commands\Import-NavCommands.ps1"
```

In einer frischen/nicht-interaktiven Shell läuft das `$PROFILE` nicht — dort einmal pro Sitzung
manuell dot-sourcen.

| Command | Zweck |
|---|---|
| `nav build` | Solution bauen (Restore + Build, MSBuild.exe). `-Configuration Release` wird durchgereicht. |
| `nav test` | Tests via gebündeltem NUnit-Console-Runner (net472). |
| `nav publish` | Debug bauen und alle Deliverables unter `deploy\` bereitstellen. |
| `nav install` | VS-2026-Extension-VSIX in Visual Studio installieren. |
| `nav snapshot` | Regression-Snapshots (`.expected.cs`) neu erzeugen. |
| `nav incminor` / `incmajor` | Nächstes Minor-/Major-Version-Tag (`vX.Y.0`) auf HEAD anlegen. |
| `nav newbranch <name>` / `rmbranch` | Branch + Geschwister-Worktree anlegen/entfernen. |
| `nav help` / `nav` | Übersicht / interaktives Menü. |

Quelle der Wahrheit sind die `.FUNCTIONALITY`-Tokens in `Tools\Commands\Functions\`; die
Command-Mechanik im Detail beschreibt [`Tools\Commands\README.md`](../Tools/Commands/README.md).

## Bauen

- **Ganze Solution:** `nav build` (Full-Framework-MSBuild.exe, inkl. VS-Extension).
- **Einzelner .NET-Host:** `dotnet build <Projekt>.csproj -c Debug` — z.B. für den LSP-Server:

  ```
  dotnet build Nav.Language.Lsp\Nav.Language.Lsp.csproj -c Debug
  ```

  Ergebnis: `Nav.Language.Lsp\bin\Debug\net10.0\nav.lsp.dll`.

## Testen

`Nav.Language.Tests` ist multi-target (`net472;net10.0`); neue Engine-Tests müssen auf **beiden** TFMs
grün sein. Test-Framework ist **NUnit**.

- **net472:** ausschließlich `nav test` (gebündelter NUnit-Console-Runner unter
  `Build\nunit.consolerunner\`). **`dotnet test -f net472` läuft ins Leere (0 Tests)** — der
  VSTest-Adapter ist in der Test-`.csproj` bewusst nur für net10.0 referenziert.
- **.NET 10:** `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`. `--filter`
  funktioniert hier, z.B. `--filter "FullyQualifiedName~Completion"`.

## Hosts lokal starten & debuggen

### LSP-Server (`nav.lsp`)

- Bauen wie oben (`dotnet build …Nav.Language.Lsp.csproj -c Debug`).
- Framework-dependent starten: `dotnet Nav.Language.Lsp\bin\Debug\net10.0\nav.lsp.dll`.
- **VS-Code-Client (F5):** siehe [`vscode-nav-lsp\Development.md`](../vscode-nav-lsp/Development.md).
- Statusdokument: [`nav-lsp-status.md`](nav-lsp-status.md).

`stdout` ist bei LSP/MCP **exklusiv** fürs JSON-RPC-Protokoll — alle Logs müssen nach `stderr`, sonst
zerstören sie die Protokoll-Frames.

### MCP-Server (`nav.mcp`)

- Bauen: `dotnet build Nav.Language.Mcp\Nav.Language.Mcp.csproj -c Debug`.
- Kommuniziert über stdio (JSON-RPC). Für einen stdio-Smoke muss `stdin` offen bleiben. Tools und
  Betrieb: [`nav-mcp-status.md`](nav-mcp-status.md).

### CLI (`nav.exe`)

- Kommandozeilen-Codegenerator/Analyzer (net472). Bauen über `nav build` oder
  `dotnet build Nav.Cli\Nav.Cli.csproj`.

## Publish / Deployment

`nav publish` baut die Solution in **Debug** und stellt in einem Aufruf alle Deliverables unter
`deploy\` bereit:

- `deploy\Build Tools` — Build-Task-DLL + `Pharmatechnik.Nav.Language.targets`, `nav.exe`
  (self-contained) und `NavGrammar.ebnf`.
- `deploy\vscode\nav-language-vscode-<version>-win32-x64.vsix` — VS-Code-Extension mit
  **eingebettetem** LSP-Server (self-contained Single-File, keine separate Runtime nötig).
- `deploy\mcp\nav.mcp.exe` — MCP-Server als self-contained Single-File.

Bewusst durchgängig Debug: die Release-Config der Solution ist unvollständig und baut nicht durch.

## Versionierung & Kodierung

- **Versionierung** ist git-abgeleitet (`git describe`); einzige Autorität ist das MSBuild-Target
  `ComputeGitVersion`. Details: [`nav-versioning-status.md`](nav-versioning-status.md).
- **Kodierung/Sprache:** alle Textdateien UTF-8 **mit** BOM, echte Umlaute (ä/ö/ü/ß) — Regeln in
  [`CLAUDE.md`](../CLAUDE.md).
