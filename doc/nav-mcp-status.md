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
| `nav_diagnostics` | `DiagnosticsComputer` (Fan-out) | **Workspace-weit:** alle (bzw. gefilterten) `.nav` validieren → aggregierte Diagnostics + Severity-Summary, gefiltert/gepaged. |
| `nav_outline` | `unit.TaskDefinitions` | Struktur: Tasks + Knoten (Art, Position). |
| `nav_workspace` | `NavSolution` | Alle `.nav`-Dateien der Solution (relativ + absolut), gefiltert/gepaged. |
| `nav_find_symbol` | `NavSymbolSearch.FindDefinitionsByPrefix` | Solution-weite Präfix-Suche nach Task-/Knoten-**Definitionen** (ohne Datei vorab). |
| `nav_goto` | `NavGoToService` | Definition(en) eines Namens (Nav→Nav, cross-file). |
| `nav_references` | `ReferenceFinder` | Alle solution-weiten Vorkommen (inkl. Deklaration). |
| `nav_rename` | `NavRenameService` | Umbenennungs-**Edit-Set** (read-only, file-local). |
| `nav_code_actions` | `NavCodeActionService` | Anwendbare Quick-Fixes/Refactorings + **Edit-Set**. |
| `nav_grammar` | `NavGrammar` (generiert) | EBNF-Grammatik der Nav-Sprache (gesamt oder eine Produktion), optional Terminal-Tabelle. **Statisch** — keine Datei/Solution. |

## 3. Design-Entscheidungen

- **Einstieg ohne Datei: `nav_find_symbol`.** Die übrigen name-basierten Tools verlangen `path` (die
  Datei, in der der Name lebt) — das setzt voraus, dass der Agent die Datei schon kennt. `nav_find_symbol`
  schließt diese Lücke: solution-weite **Präfix**-Suche (case-insensitiv) über
  `NavSymbolSearch.FindDefinitionsByPrefix`, iteriert via `NavSolution.ProcessCodeGenerationUnitsAsync`
  (derselbe Iterator wie die Referenzsuche) über alle Units. Liefert bewusst **nur Definitionen**
  (Task-Definitionen + deren Knoten, NICHT die `taskref`-Deklarationen — Verwendungsstellen gibt
  `nav_references`). Typischer Fluss: `nav_find_symbol "Login"` → Datei(en) → `nav_goto`/`nav_references`/
  `nav_outline` mit dem gefundenen Pfad.
- **Paging gegen das Token-Limit.** List-liefernde Tools (`nav_workspace`, `nav_references`,
  `nav_find_symbol`, `nav_diagnostics`) pagen: `limit` (Default 100, Max **200**) + `offset`, dazu
  `matchCount`/`returned`/`truncated` im Result (`nav_diagnostics` paged über die **Diagnostics**, nicht
  die Dateien; `count` = Gesamtzahl vor Paging, `summary` bleibt vollständig). Die Obergrenze ist bewusst niedrig — selbst eine voll gefüllte Seite muss sicher
  unter dem MCP-Tool-Result-Limit (~25k Tokens) bleiben; ein zu hoher Max-Wert lief trotz Paging ins Limit
  (`nav_workspace`-Einträge tragen relativen + absoluten Pfad, ~240 Zeichen). `nav_workspace`/
  `nav_references` haben zusätzlich `filter` (Substring auf Pfad), `nav_find_symbol` filtert über den `prefix`.
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
- **`nav_diagnostics` = Pull-Äquivalent zum LSP-Push.** Der LSP veröffentlicht beim `initialized`
  workspace-weit Diagnostics für **jede** `.nav` (Push, `textDocument/publishDiagnostics`, Schleife
  `NavWorkspace.PublishAllDiagnosticsAsync` → `ProcessCodeGenerationUnitsAsync` + `DiagnosticsComputer`);
  VS Code aggregiert das in der „Problems"-Ansicht. Der MCP hat keinen Push-Kanal — `nav_diagnostics`
  bietet dieselbe Aggregation **on demand**. Bewusst **keine** gemeinsame Extraktion: der MCP iteriert
  selbst (eigene `filter`/`severity`/Paging-Logik) und teilt nur `DiagnosticsComputer.FromUnit`. Anders
  als der cache-getriebene LSP-Push liest `nav_diagnostics` **fresh pro Datei** (`GetFileDiagnostics` →
  `GetFreshUnit`, dieselbe Semantik wie `nav_validate`) — korrekt auch nach Agent-Edits; ein voller
  Sweep ohne `filter` ist bewusst teuer, `filter` ist die Mitigation. Optionaler `severity`-Filter
  (`error`/`warning`/`suggestion`); kein `task`-Feld pro Diagnose (spätere Erweiterung).
- **Mutierende Tools sind read-only.** `nav_rename` und `nav_code_actions` schreiben **NICHTS** auf
  Platte; sie liefern das **Edit-Set** (1-basierte `{line, column, endLine, endColumn, newText}`)
  zurück, das der Agent selbst anwendet.
- **`nav_grammar` = statische Sprach-Referenz, zustandslos.** Einziges Tool **ohne** `NavMcpWorkspace`-
  Parameter — die Grammatik ist ein `public const`/`static` in der Engine (`NavGrammar.Ebnf` + `Rules`),
  zur Compile-Zeit aus den `Parse*`-EBNF-Fragmenten des handgeschriebenen Parsers zusammengesetzt
  (Generator + Drift-Diagnosen NAV001/NAV002, siehe `doc/nav-grammar-status.md`). Daher keine Datei-/
  Solution-Auflösung, kein Paging (die volle Grammatik liegt klar unter dem Result-Limit). `rule` zieht
  eine Einzelproduktion über `NavGrammar.Rules` (Schlüssel = linke Seite); **Stolperstein:**
  Nebenproduktionen (z.B. `arrayType`) haben keinen eigenen Schlüssel, sondern stecken im Fragment ihrer
  Hauptregel (`codeType`) — bei unbekanntem `rule` liefert das Result `error` + `availableRules` (die
  bekannten Schlüssel) statt einer Exception. `includeTerminals` spiegelt die Terminal-Tabelle aus
  `SyntaxFacts` (Keywords + Punctuation + kategorische Terminale Identifier/StringLiteral/EOF); das
  `?`-Terminal (Questionmark) ist **nicht** in `SyntaxFacts.Punctuations` und wird gesondert ergänzt.
- **Agentenfreundliche DTOs.** Schlanke, 1-basierte Sichten (nicht die LSP-DTOs), Fehler als `error`-
  Feld statt Exception (außer Protokollfehlern). Mapping-Helfer: `NavEditDto` (Offset→Zeile/Spalte via
  `sourceText.GetLocation`), `NavLocationDto`, `NavSymbolRef`, `NavSymbolKind`.

## 4. Tests / Verifikation

- **Engine:** `Nav.Language.Tests/Symbols/NavSymbolSearchTests.cs` (9, net472 + net10) deckt die
  Namens-Auflösung (`FindByName`) **und** die Präfix-Definitionssuche (`FindDefinitionsByPrefix`:
  Präfix-/Case-Insensitivität, leerer Präfix = alle, unbekannt = leer) ab — der einzige neue Engine-Code.
- **MCP-Tools:** per **stdio-Smoke** gegen die laufende `nav.mcp` verifiziert (newline-delimited
  JSON-RPC: `initialize` → `notifications/initialized` → `tools/list` → `tools/call`). Abgedeckt:
  Outline, Workspace, FindSymbol (Präfix, kind-Filter), GoTo (same/cross-file), References (same/cross-file), Mehrdeutigkeit +
  Disambiguierung (`task`- **und** `kind`-Achse), Rename (scoped/Task, file-local), Invalid-Name-Fehler,
  Remove-Unused-Nodes. **`nav_diagnostics`** gegen `Nav.Language.Tests/Diagnostics/Tests` verifiziert:
  voller Sweep (Summary-Konsistenz `error+warning+suggestion == count`), `severity`-Filter, `filter` +
  Paging/`truncated`, plus Quer-Check `nav_validate` einer Einzeldatei == gefiltertes `nav_diagnostics`
  (identische Counts/Codes). **`nav_grammar`** per stdio-Smoke: volle Grammatik (enthält
  `codeGenerationUnit ::=`), Einzelregel (`taskDefinition`) + `includeTerminals`, unbekannte Regel
  (`arrayType` → `error` + `availableRules`).
- **Build:** `dotnet build Nav.Language.Mcp/Nav.Language.Mcp.csproj` (net10), 0 Warnungen.
  Server lokal: `dotnet Nav.Language.Mcp/bin/Debug/net10.0/nav.mcp.dll <workspace-root>`.
- **Publish:** `nav publish` veröffentlicht den MCP-Server als **self-contained Single-File**
  `deploy\mcp\nav.mcp.exe` (`win-x64`, **genau eine Datei**, inkl. gebündelter .NET-Runtime — keine
  separate Runtime, keine losen DLLs). Flags analog zum LSP-Publish (`PublishSingleFile`,
  `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`,
  `DebugType=embedded`). Start: `deploy\mcp\nav.mcp.exe <workspace-root>`.

## 5. Bekannte Grenzen / offene Punkte

- **Pfad-Casing.** Ausgabe-Pfade sind durchgängig normalisiert (klein, Backslashes) über
  `PathHelper.NormalizePath` — konsistent und round-trip-sicher (Windows case-insensitiv), aber nicht
  in der Original-Schreibweise des Aufrufers. Rein kosmetisch.
- **Cache-Frische cross-file.** `GetFreshUnit` invalidiert nur die **Zieldatei**. Bei solution-weiten
  Tools (`nav_references`, `nav_find_symbol`) können Ergebnisse aus **anderen**, zwischenzeitlich extern
  geänderten Dateien minimal veraltet sein (kein Full-Rescan pro Aufruf) — `nav_find_symbol` liest die
  Units über den gemeinsamen Cache, invalidiert also nicht pro Datei. Für den typischen „editiere eine
  Datei, frage sie ab"-Fluss unkritisch.
- **Bewusst nicht enthalten** (geringer Agent-Nutzen / reine Editor-UI): Completion, Hover/QuickInfo,
  Semantic Tokens, Folding, CodeLens, DocumentHighlight; Plattenänderung durch die Tools.
- **Mögliche Erweiterungen:** Whole-File-Modus für `nav_code_actions` (alle Fixes einer Datei ohne
  Symbolname); optionales `apply`-Flag für die mutierenden Tools.
