# Nav-MCP — Status & Handoff

Stand-Dokument für den MCP-Server (`Nav.Language.Mcp`, net10.0, Assembly **`nav.mcp`**). Der MCP-Server
exponiert die VS-freien Engine-Kerne aus `Nav.Language` als MCP-Tools für einen KI-Agenten, der
`.nav`-Dateien bearbeitet. Schwester-Dokument für den LSP: `doc/nav-lsp-status.md`.

> **Eine Engine:** Der MCP fügt **keine** eigene Sprachlogik hinzu. Er übersetzt nur MCP-Tool-Aufrufe
> ↔ Engine-Kerne (dieselben, die der LSP-Server nutzt) und teilt mit ihm die Host-Schicht
> `NavWorkspaceCore`.

## 1. Aufbau

- **`Program.cs`** — `Host.CreateApplicationBuilder`, Logging nach **stderr** (stdout ist exklusiv fürs
  JSON-RPC-Protokoll), Workspace-Root aus `args[0]` oder `Directory.GetCurrentDirectory()`,
  `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. SDK: `ModelContextProtocol`.
- **`NavMcpWorkspace.cs`** — Schale über `NavWorkspaceCore`. **Kein** Overlay-Modell wie der LSP: MCP
  arbeitet rein request/response gegen Platte. `GetFreshUnit` invalidiert vor dem Lesen den Cache der
  Zieldatei (der Agent hat evtl. gerade editiert). `EnsureSolutionLoadedAsync` lädt die Solution
  (Globbing `**/*.nav`) **lazy + thread-safe** einmalig — nur solution-weite Tools brauchen sie.
- **`McpReferenceCollector.cs`** — `IFindReferencesContext`-Sammler (Port des LSP-`ReferenceCollector`).
- **`Tools/`** — je Tool eine `[McpServerToolType]`-Klasse + ein schlankes Result-DTO. Tools werden per
  Reflection entdeckt (`WithToolsFromAssembly`); `NavMcpWorkspace` wird per DI injiziert.

## 2. Tools (Stand: vollständig)

| Tool | Engine-Kern | Zweck |
|---|---|---|
| `nav_validate` | `DiagnosticsComputer` | Datei validieren → Diagnostics (inkl. Cross-File). |
| `nav_outline` | `unit.TaskDefinitions` | Struktur: Tasks + Knoten (Art, Position). |
| `nav_workspace` | `NavSolution` | Alle `.nav`-Dateien der Solution (relativ + absolut). |
| `nav_goto` | `NavGoToService` | Definition(en) eines Namens (Nav→Nav, cross-file). |
| `nav_references` | `ReferenceFinder` | Alle solution-weiten Vorkommen (inkl. Deklaration). |
| `nav_rename` | `NavRenameService` | Umbenennungs-**Edit-Set** (read-only, file-local). |
| `nav_code_actions` | `NavCodeActionService` | Anwendbare Quick-Fixes/Refactorings + **Edit-Set**. |

## 3. Design-Entscheidungen

- **Name-basiert statt positions-basiert.** Ein Agent hat keinen Cursor. Tools nehmen **Namen**
  (`nav_references(path, name)`), nicht Zeile/Spalte. Die VS-freie Engine-Brücke
  `Nav.Language/Symbols/NavSymbolSearch.FindByName` löst einen Namen zu Symbol(en) auf; deren
  `Location.Start` wird in die unveränderten, positions-basierten Engine-Services eingespeist — als
  läge der Caret dort. **Mehrdeutige Namen** liefern statt eines Ergebnisses die **Kandidaten**
  (`candidates`, je mit `kind` + enthaltender `task`); **zwei** optionale Disambiguatoren grenzen ein:
  - **`task`** — grenzt *in eine Task-Definition hinein* ein (Task + ihre Knoten); löst „gleicher
    Knotenname in mehreren Tasks".
  - **`kind`** — filtert nach Symbol-Art (`task` vs. `node`, oder eine konkrete Art wie `gui`); löst den
    Fall, den `task` **nicht** kann: eine Task **und** ein gleichnamiger Knoten in eben dieser Task. Die
    Filterung liegt im MCP-Layer (`NavNameResolution.Resolve` → `KindMatches`), nicht in der Engine.
    Greift der Filter ins Leere, bleiben die ursprünglichen Kandidaten erhalten (kein falsches
    „nicht gefunden").
- **Mutierende Tools sind read-only.** `nav_rename` und `nav_code_actions` schreiben **NICHTS** auf
  Platte; sie liefern das **Edit-Set** (1-basierte `{line, column, endLine, endColumn, newText}`)
  zurück, das der Agent selbst anwendet.
- **Agentenfreundliche DTOs.** Schlanke, 1-basierte Sichten (nicht die LSP-DTOs), Fehler als `error`-
  Feld statt Exception (außer Protokollfehlern). Mapping-Helfer: `NavEditDto` (Offset→Zeile/Spalte via
  `sourceText.GetLocation`), `NavLocationDto`, `NavSymbolRef`, `NavSymbolKind`.

## 4. Tests / Verifikation

- **Engine:** `Nav.Language.Tests/Symbols/NavSymbolSearchTests.cs` (5, net472 + net10) deckt die
  Namens-Auflösung ab (der einzige neue Engine-Code).
- **MCP-Tools:** per **stdio-Smoke** gegen die laufende `nav.mcp` verifiziert (newline-delimited
  JSON-RPC: `initialize` → `notifications/initialized` → `tools/list` → `tools/call`). Abgedeckt:
  Outline, Workspace, GoTo (same/cross-file), References (same/cross-file), Mehrdeutigkeit +
  Disambiguierung (`task`- **und** `kind`-Achse), Rename (scoped/Task, file-local), Invalid-Name-Fehler,
  Remove-Unused-Nodes.
- **Build:** `dotnet build Nav.Language.Mcp/Nav.Language.Mcp.csproj` (net10), 0 Warnungen.
  Server lokal: `dotnet Nav.Language.Mcp/bin/Debug/net10.0/nav.mcp.dll <workspace-root>`.

## 5. Bekannte Grenzen / offene Punkte

- **Pfad-Casing.** Ausgabe-Pfade sind durchgängig normalisiert (klein, Backslashes) über
  `PathHelper.NormalizePath` — konsistent und round-trip-sicher (Windows case-insensitiv), aber nicht
  in der Original-Schreibweise des Aufrufers. Rein kosmetisch.
- **Cache-Frische cross-file.** `GetFreshUnit` invalidiert nur die **Zieldatei**. Bei solution-weiten
  Tools (`nav_references`) können Ergebnisse aus **anderen**, zwischenzeitlich extern geänderten
  Dateien minimal veraltet sein (kein Full-Rescan pro Aufruf). Für den typischen „editiere eine Datei,
  frage sie ab"-Fluss unkritisch.
- **Bewusst nicht enthalten** (geringer Agent-Nutzen / reine Editor-UI): Completion, Hover/QuickInfo,
  Semantic Tokens, Folding, CodeLens, DocumentHighlight; Plattenänderung durch die Tools.
- **Mögliche Erweiterungen:** Whole-File-Modus für `nav_code_actions` (alle Fixes einer Datei ohne
  Symbolname); optionales `apply`-Flag für die mutierenden Tools.
