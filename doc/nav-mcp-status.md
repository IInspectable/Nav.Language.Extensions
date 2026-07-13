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
| `nav_outline` | `unit.TaskDefinitions` | Struktur: Tasks + Knoten (Art, Position) + effektive Sprachversion (`languageVersion` + `hasVersionDirective`). |
| `nav_workspace` | `NavSolution` | Alle `.nav`-Dateien der Solution (relativ + absolut), gefiltert/gepaged. |
| `nav_find_symbol` | `NavSymbolSearch.FindDefinitionsByPrefix` | Solution-weite Präfix-Suche nach Task-/Knoten-**Definitionen** (ohne Datei vorab). |
| `nav_goto` | `NavGoToService` | Definition(en) eines Namens (Nav→Nav, cross-file). |
| `nav_references` | `ReferenceFinder` | Alle solution-weiten Vorkommen (inkl. Deklaration). |
| `nav_rename` | `NavRenameService` | Umbenennungs-**Edit-Set** (read-only, file-local). |
| `nav_code_actions` | `NavCodeActionService` | Anwendbare Quick-Fixes/Refactorings + **Edit-Set**. |
| `nav_format` | `NavFormattingService` | Document-/Range-Formatierung: **Edit-Set** + komplett formatierter Text (read-only). |
| `nav_grammar` | `NavGrammar` (generiert) | EBNF-Grammatik der Nav-Sprache (gesamt oder eine Produktion), optional Terminal-Tabelle. **Statisch** — keine Datei/Solution. |
| `nav_preview_codegen` | `ICodeGeneratorProvider` (Codegen-Pipeline) | Vorschau des generierten C# je Task-Definition (Basisklasse/Interfaces/Stub) — **ohne Plattenschreiben, ohne Build**. |
| `nav_call_hierarchy` | `NavCallHierarchyService` | Aufrufbeziehungen einer Task auf Task-Ebene: ausgehend (Callees) + eingehend (Caller, solution-weit), cross-file via `taskref`. |

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
- **Mutierende Tools sind read-only.** `nav_rename`, `nav_code_actions` und `nav_format` schreiben
  **NICHTS** auf Platte; sie liefern das **Edit-Set** (1-basierte `{line, column, endLine, endColumn,
  newText}`) zurück, das der Agent selbst anwendet.
- **`nav_format` = Document-/Range-Formatting als Pull.** Gegenstück zu LSP
  `textDocument/formatting`/`…/rangeFormatting`, gleicher Engine-Kern (`NavFormattingService`). Der
  Formatter ist rein syntaktisch → `NavMcpWorkspace.GetFreshSyntaxTree` (Cache-Invalidierung wie
  `GetFreshUnit`, aber ohne Semantik-Build). Der Range ist **zeilenbasiert** (`startLine`/`endLine`,
  1-basiert, inklusiv) statt positions-basiert — ein Agent denkt in Zeilen; die Engine weitet den
  Range selbst auf ganze Anweisungen aus (`ExpandRange`). Neben dem Edit-Set liefert das Tool den
  **komplett formatierten Dokumenttext** (`formattedText`, auch beim Range-Format das ganze Dokument
  mit nur den Range-Änderungen): viele kleine Whitespace-Edits präzise anzuwenden ist für einen
  Agenten fehleranfällig, die Datei mit dem Ergebnistext zu überschreiben nicht. Per
  `includeFormattedText=false` abbestellbar (Token-Limit bei großen Dateien — dann nur Edits);
  bei „bereits formatiert" (0 Edits) bleibt er `null`. Die Optionen `insertSpaces`/`tabSize`
  überschreiben nur bei **expliziter** Angabe die kanonischen Defaults
  (`NavFormattingOptions.Default`: Tabs, Breite 4 — Korpus-Mehrheit) — anders als beim LSP gibt es
  keinen Editor-Konfig-Kanal, der immer liefert.
- **`nav_preview_codegen` = Codegen-Vorschau als Pull, ohne Build.** Der größte Agenten-Nutzen von Nav
  dreht sich um „welcher C# entsteht" (Begin-Overloads, exakte Event-/Logic-Methodennamen, die transitiv
  erreichbaren DI-Parameter je Logic-Methode). Bisher nur über `nav.exe` + Build + Lesen der
  `*.generated.cs` verifizierbar. Das Tool generiert stattdessen **in-memory** gegen die frisch von Platte
  gelesene `CodeGenerationUnit` (`GetFreshUnit`) über **dieselbe** Pipeline wie `nav.exe`/MSBuild
  (`CodeGeneratorProvider.Default` → `VersionDispatchingCodeGenerator` → V1/V2 je `LanguageVersion`),
  **ohne** `IFileGenerator` (kein Plattenschreiben). Ergebnis je Task-Definition: die Artefakte mit
  **Rolle** (`base`/`iwfs`/`ibegin`/`user`/`to`), Ziel-Dateiname, Zeilen-/Zeichenzahl,
  `OverwritePolicy` und (optional) Inhalt. Die Rolle wird **autoritativ** über denselben
  `IPathProvider` bestimmt, den der Codegen nutzt (kanonischer Dateiname → Rolle), mit Suffix-Heuristik
  als Fallback (TO-Stubs sind über den PathProvider nicht vorab bekannt). Entscheidungen:
  - **Fehler-Gate wie der Generator.** Der Codegen wirft auf Syntax-/Semantik-/Include-Fehler
    (`CodeGeneratorV1.Generate`). Das Tool prüft **vorab** (`DiagnosticsComputer.FromUnit` +
    `Includes…HasErrors()`) und liefert die Fehler strukturiert (`error` + `diagnostics`, nur Severity
    `Error`) statt eine Protokoll-Exception zu riskieren — der Agent behebt zuerst (wie `nav_validate`).
  - **Benutzer-Stubs standardmäßig aus.** `user` (der einmalige `{Task}WFS`-Stub, `OverwritePolicy.Never`)
    und `to` sind Benutzer-Eigentum und nahezu leer — die Signaturen stehen in Basisklasse + Interfaces.
    `includeUserFiles=true` blendet sie ein.
  - **Token-Budget.** `includeContent=false` liefert nur das Manifest (Rollen/Dateinamen/Zählungen);
    sprengt der Gesamtinhalt die Obergrenze (60k Zeichen), entfällt der Inhalt aller Artefakte und
    `contentOmitted=true` rät zu `task`-Eingrenzung. `task` grenzt auf eine Task-Definition ein.
  - **`projectRoot` (optional)** wirkt **nur** auf die Namespaces des generierten Codes (relativ zur
    Wurzel gebildet); für reine Signatur-Fragen entbehrlich. `nullableContext` schreibt `#nullable enable`.
- **`nav_call_hierarchy` = Aufrufhierarchie als Pull, name-basiert.** Der Nav-Aufrufgraph läuft über
  `task`-Knoten (`task Sub Foo;`), die — auch cross-file via `taskref` — eine Task-Deklaration
  referenzieren. Engine-Kern ist `NavCallHierarchyService` (VS-frei, dieselbe Basis wie die
  LSP-Call-Hierarchy). Der LSP verankert die Hierarchie **positions-basiert** (`PrepareCallHierarchy`
  am Caret); der MCP löst die Ausgangs-Task **name-basiert** auf — strikt Task-Ebene, daher direkt über
  `unit.TaskDefinitions` (Task-Namen sind je Datei eindeutig; ein Knotenname wird bewusst nicht
  aufgelöst → sprechender Fehler mit Verweis auf `nav_find_symbol`/`nav_outline`). `direction`
  (`incoming`/`outgoing`/`both`, Default `both`) wählt die Richtung; **eingehend** braucht die geladene
  Solution (`EnsureSolutionLoadedAsync`), **ausgehend** nur die Datei. Ergebnis je Beziehung: die andere
  Task (Ziel bzw. Aufrufer) + deren 1-basierte Position + die Aufrufstellen (`task`-Knoten-Bezeichner),
  nach Ziel/Aufrufer gruppiert. **Nebenfix in der Engine:**
  `NavSolution.ProcessCodeGenerationUnitsAsync` deduplizierte Dateien case-sensitiv (`HashSet<string>`,
  offenes `// TODO File/Path comparer`); bei abweichender Pfad-Schreibweise (normalisierter
  `startingUnit`-Pfad vs. Original-Casing der `SolutionFiles`) wurde dieselbe Datei doppelt verarbeitet
  → doppelte Treffer. Auf `StringComparer.OrdinalIgnoreCase` umgestellt (Windows-Pfade sind
  case-insensitiv; kommt auch `nav_references`/`nav_find_symbol` zugute). Abgesichert durch den
  Engine-Regressionstest `NavCallHierarchyServiceTests.Incoming_StartingUnitPathCasingDiffers_DoesNotDoubleCount`
  (vor dem Fix rot mit `CallSites=2`, danach grün) — verifiziert per Fix-Revert.
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
  `?`-Terminal (Questionmark) kommt seit dem Lückenschluss direkt aus `SyntaxFacts.Punctuations`.
- **Agentenfreundliche DTOs.** Schlanke, 1-basierte Sichten (nicht die LSP-DTOs), Fehler als `error`-
  Feld statt Exception (außer Protokollfehlern). Mapping-Helfer: `NavEditDto` (Offset→Zeile/Spalte via
  `sourceText.GetLocation`), `NavLocationDto`, `NavSymbolRef`, `NavSymbolKind`.

## 4. Tests / Verifikation

- **Engine:** `Nav.Language.Tests/Symbols/NavSymbolSearchTests.cs` (9, net472 + net10) deckt die
  Namens-Auflösung (`FindByName`) **und** die Präfix-Definitionssuche (`FindDefinitionsByPrefix`:
  Präfix-/Case-Insensitivität, leerer Präfix = alle, unbekannt = leer) ab — der einzige neue Engine-Code.
- **MCP-Schicht: automatisiertes Testprojekt `Nav.Language.Mcp.Tests`** (net10.0-only, NUnit) — überführt
  den früheren manuellen stdio-Smoke-Katalog in wiederholbare, regressionssichere Tests. Die Tools sind
  statische Methoden mit `NavMcpWorkspace` als erstem Parameter (die `[McpServerTool]`-Attribute sind
  passiv), also **direkt aufrufbar** — kein stdio, kein JSON-RPC, kein DI. Infrastruktur:
  `Infrastructure/McpTestWorkspace` (eindeutiges Temp-Wurzelverzeichnis, `WriteFile` schreibt UTF-8 **mit
  BOM**, `Workspace`-Property, fehlertolerantes `Dispose`) — der MCP arbeitet rein gegen Platte, die
  Temp-Dateien *sind* der Fixture-Mechanismus; `Infrastructure/EditApplier` wendet ein Edit-Set absteigend
  auf den Text an (prüft „angewandtes Edit-Set ergibt erwarteten Text"). `NavNameResolution` (`internal
  static`) ist über `InternalsVisibleTo` direkt testbar. **93 Tests**, alle grün:
  - `NavGrammarToolTests` (4) — volle Grammatik (`codeGenerationUnit ::=`), Einzelregel, unbekannte Regel
    (`arrayType` → `error` + `availableRules`), `includeTerminals`.
  - `NavNameResolutionTests` (8) + `NavFindSymbolToolTests` (10) — die drei Ausgänge
    `Resolved`/`NotFound`/`Ambiguous`, `task`-/`kind`-Disambiguierung inkl. Leerlauf-Regel, `KindMatches`;
    Präfix-Suche (case-insensitiv, cross-file, nur Definitionen), Dedup/Sortierung, Paging-Kanten
    (`limit`-Clamping, `truncated`).
  - `NavDiagnosticsToolTests` (13) + `NavValidateToolTests` (4) — Summary-Konsistenz
    (`error+warning+suggestion == count`), `severity`/`filter`, Paging über die Diagnostics, Quer-Check
    `nav_validate` == gefiltertes `nav_diagnostics`, **Fresh-Read** (Datei ändern → sofort sichtbar),
    fehlende Datei → `NotFound`.
  - `NavEditDtoTests` (7) + `NavRenameToolTests` (4) + `NavCodeActionsToolTests` (4) — Offset→1-basierte
    Koordinaten, `FromChanges`-Skip-Regeln; file-lokales Edit-Set, **Read-only-Versprechen** (Platte
    byte-identisch vor/nach), `NotFound`/`Ambiguous`-Fehlerpfade.
  - `NavFormatToolTests` (7) + `NavGotoToolTests` (4) + `NavReferencesToolTests` (5) +
    `NavOutlineToolTests` (4) + `NavWorkspaceToolTests` (4) — Voll-/Range-Format (Subset-Garantie),
    Idempotenz (0 Edits, `formattedText == null`), `insertSpaces`/`tabSize`, Fehlerfälle; GoTo/References
    same- **und** cross-file, Outline (Art/Position + `languageVersion`/`hasVersionDirective`), Workspace
    (relativ/absolut, `filter`, Paging).
  - `NavPreviewCodegenToolTests` (8) — generierte Artefakte + Rollen (`base`/`iwfs`/`ibegin`), Dateinamen
    + `OverwritePolicy`, `task`-Filter, `includeUserFiles` (Stub ein/aus), Manifest-Modus
    (`includeContent=false` → Inhalt `null`, Zählungen bleiben), **Fehler-Gate** (Datei mit Fehler →
    `error` + nur `Error`-Diagnostics, keine Tasks), `NotFound` sowie **V1/V2-Weiche** (`#version 2` →
    V2-Codegen, nachgewiesen über die CallContext-/`Unwrap()`-Marker der V2-Basisklasse).
  - `NavCallHierarchyToolTests` (7) — ausgehende/eingehende Aufrufe **cross-file** via `taskref`,
    Gruppierung mehrerer Aufrufstellen nach Ziel, Richtungsfilter (`incoming`/`outgoing`/`both`,
    Leaf-Task ohne Callees), ungültige Richtung, unbekannte Task, `NotFound`.
- **Lauf:** `dotnet test Nav.Language.Mcp.Tests\Nav.Language.Mcp.Tests.csproj` (single-TFM, kein `-f`
  nötig). Seit der Runner-Anbindung läuft die Suite auch in **`nav test`** mit — nach dem net472-NUnit-Lauf
  ruft `Invoke-Test` `dotnet test … -c <Configuration> --no-build`, gated auf die gebaute
  `bin\<Configuration>\net10.0\Nav.Language.Mcp.Tests.dll` (fehlt sie, Hinweis statt Fehler; Voraussetzung
  ist ein vorheriges `nav build`). Plan/Handoff des Testpakets: `doc/nav-mcp-tests-status.md`.
- **stdio-Transport** bleibt separat zu prüfen: Discovery/Serialisierung/JSON-Parameter-Binding über den
  tatsächlichen JSON-RPC-Kanal (`initialize` → `tools/list` → `tools/call`) deckt das direkte
  Methoden-Testen bewusst **nicht** ab — dafür der manuelle stdio-Smoke gegen die laufende `nav.mcp` bzw.
  den Single-File-Publish (ein optionaler In-Memory-Protokolltest über das MCP-SDK ist in
  `doc/nav-mcp-tests-status.md` als Step 6 skizziert).
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
  Symbolname); optionales `apply`-Flag für die mutierenden Tools (BOM-sicherer Schreibpfad, Lücke C der
  Agenten-Gap-Analyse). Graph/Reachability (`nav_diagram`/`nav_reachability`, Lücke B) sind engine-seitig
  vorbereitet, aber noch nicht als Tool. Fortschritt: `doc/nav-mcp-agent-gap-analysis.md`.
