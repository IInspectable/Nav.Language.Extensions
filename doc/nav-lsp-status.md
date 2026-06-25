# Nav-LSP — Status & Handoff

Stand-Dokument für die Weiterarbeit am LSP-Server (Branch `feature/nav-lsp`,
Worktree `D:\git\Nav.Language.Extensions-nav-lsp`). Ergänzt die ursprüngliche Plandatei
`C:\Users\mnhaenel\.claude\plans\mache-dir-mal-ein-enumerated-kazoo.md` (Bewertung + 3-Phasen-Fahrplan).

> **Nächste Session-Aufgabe:** **Korrektheit/Robustheit** — die **dependency-aware Re-Diagnose** bei
> `didChange`/`didClose` **und** `workspace/didChangeWatchedFiles` (externe Datei-Änderungen) sind **erledigt**
> (siehe unten); der MessagePack-Pin (GHSA-hv8m-jj95-wg3x) ist ebenfalls **erledigt**. Als Nächstes
> inkrementeller Doc-Sync oder der **Phase-3-Spike** (VS als RPC-Client). Die Completion ist vollständig
> (Nav + Edge + Path); nur die C#-Code-Block-Completion (`CodeCompletionSource`) bleibt bewusst VS/Roslyn-only.
> **Code Actions** (`textDocument/codeAction`), **Rename** (`textDocument/rename`),
> **Completion (Nav+Edge+Path)**, **Folding** (`textDocument/foldingRange`), **Hover** (`textDocument/hover`),
> References/documentHighlight und GoTo Definition (Option B) sind **erledigt** — siehe unten.

---

## 1. Build / Run / Test — Fallstricke zuerst

- **Engine baut nur mit Full-Framework `MSBuild.exe`, NICHT `dotnet build`.** Grund: `CodeTaskFactory`
  in `Nav.Language\CustomBuild.targets` (StringTemplate-Export). `dotnet build` → Fehler MSB4801.
  ```
  "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" \
      Nav.Language.Lsp\Nav.Language.Lsp.csproj -t:Build -p:Configuration=Debug
  ```
- **Server starten (framework-dependent):** `dotnet Nav.Language.Lsp\bin\Debug\net10.0\nav.lsp.dll`
  (.NET-10-Runtime ist installiert). **AssemblyName ist `nav.lsp`** (nicht mehr `Pharmatechnik.Nav.Language.Lsp`);
  `RootNamespace` bleibt `Pharmatechnik.Nav.Language.Lsp`, die Namespaces im Code sind unverändert.
- **Self-contained Server publizieren:** `n publishlsp` → `deploy\lsp\nav.lsp.exe` — **genau eine Datei**
  (Single-File, inkl. .NET-Runtime, ~39 MB komprimiert; keine separate Runtime nötig). Läuft über
  `MSBuild.exe -t:Publish` (siehe Abschnitt 4 „Toolchain/Deployment").
- **Tests:**
  - net472: gebündelter NUnit-Console-Runner — `n test` (`_build\nunit.consolerunner\3.8.0\tools\nunit3-console.exe`).
  - .NET 10: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0 --no-build` (Projekt ist multi-target `net472;net10.0`).
- **VS-Code-PoC:** `cd vscode-nav-lsp && npm install`, dann Ordner in VS Code öffnen → **F5**.
  **Achtung:** Solange der PoC läuft, ist die Server-DLL gesperrt → vor einem Server-Rebuild den
  Extension-Development-Host **schließen** (der LSP-Client startet den Server sonst automatisch neu).
- **URI-Test-Fallstrick:** LSP-Clients (VS Code/VS) prozent-kodieren den Laufwerks-Doppelpunkt
  (`file:///d%3A/...`). `System.Uri.LocalPath` liefert dafür einen kaputten Pfad → siehe `NavUri`.
  Node `url.pathToFileURL` kodiert das **nicht** — Test-Harnesse müssen die `%3A`-Form explizit verwenden.
  **Jeder** URI→Pfad am Rand MUSS `NavUri.ToFilePath` nutzen, NIE `rootUri.LocalPath` direkt — sonst
  scheitert bei der `%3A`-Form `Directory.Exists`, die Solution bleibt leer und alle solution-weiten
  Features (Pfad-Completion, Cross-File-References) liefern nichts. War genau der Bug bei `ResolveRootPath`.

---

## 2. Erledigt (committet auf `feature/nav-lsp`)

**Phase 1 — Engine portabel** (`005c54a5`):
- `Nav.Language` + `Nav.Utilities` → `netstandard2.0` (ein Build für net472-Extension UND .NET-10-Server).
- `System.CodeDom`-Paket ergänzt (für `Microsoft.CSharp.CSharpCodeProvider.IsValidIdentifier`).
- `Nav.Language.Tests` multi-target `net472;net10.0`; Testsuite grün auf **beiden** TFMs (624 passed).
- Testdaten-Pfadauflösung tiefenunabhängig gemacht (`TestDataDirectory`).

**Phase 2 — LSP-Server** (`Nav.Language.Lsp`, .NET 10, `StreamJsonRpc` + `Microsoft.VisualStudio.LanguageServer.Protocol` 17.2.8):
- Lebenszyklus `initialize`/`initialized`/`shutdown`/`exit`; Dokument-Sync `didOpen`/`didChange`/`didClose` (Full-Sync).
- **Diagnostics** (`publishDiagnostics`): einzelnes Dokument + **Workspace-Discovery** (`NavSolution`, globt `**/*.nav`, Cross-File-Modell).
- **Overlay-Modell** „offenes Dokument schlägt Platte" (`OverlaySyntaxProvider`) als gemeinsamer Provider für Scan + offene Docs; **URI-Normalisierung** am Rand (`NavUri`).
- **Semantic Tokens** (`textDocument/semanticTokens/full`): reuse der Engine-`TextClassification` (`SemanticTokensBuilder`).
- **Document Symbols** (`textDocument/documentSymbol`): Outline aus dem semantischen Modell (`DocumentSymbolBuilder`).
- **Folding** (`textDocument/foldingRange`): rein syntaktisch aus dem `SyntaxTree` (`FoldingRangeBuilder`), analog zum VS-`OutliningTagger`, aber zeilenbasiert. Faltet using-Blöcke (≥2, Kind `imports`), zusammenhängende `taskref "file"`-Include-Läufe (≥2, `imports`), Task-Definitionen/-Deklarationen, Node- und Transition-Blöcke (`region`) sowie mehrzeilige Kommentare (`comment`). Dedup über (startLine,endLine,kind). `FoldingRangeKind` serialisiert dank `StringEnumConverter`/`EnumMember` korrekt zu `comment`/`imports`/`region`.
- **Completion (Nav + Edge + Path)** (`textDocument/completion`): VS-freier Engine-Kern `Nav.Language/Completion/NavCompletionService.GetCompletions(unit, offset, solution?)` → `IReadOnlyList<NavCompletionItem>` (neutraler `NavCompletionItemKind`). Vereint die VS-Quellen `NavCompletionSource`, `EdgeCompletionSource` **und** `PathCompletionSource` (die VS getrennt mischt):
  - **Nav:** nach `task ` die deklarierten Tasks, nach `knoten:` die Exit-Connection-Points, in einer Task-Definition die Knoten (unreferenzierte zuerst), die Nav-Keywords (ohne versteckte/Edge-Keywords).
  - **Edge:** sichtbare Edge-Keywords, wenn vor der (angefangenen) Edge ein Whitespace/Zeilenanfang steht (`IsEdgeContext`/`GetStartOfEdge` über das Edge-Zeichen-Set).
  - **Path:** innerhalb von `taskref "…"` werden **alle** der Solution bekannten Nav-Files angeboten (die Discovery globt bereits `**/*.nav` unter dem Workspace-Root — **kein** Dateisystem-Enumerieren, **keine** `..`/Verzeichnis-Navigation). **Anzeige** (`Label`) = Dateiname, **`Detail`** = relativer Pfad; gefiltert wird clientseitig über den **Dateinamen** (`FilterText` = Dateiname, wie in der VS-`PathCompletionSource`, die über die `DisplayText`/`applicableToSpan` filtert), sodass eine Datei allein durch Tippen ihres Namens gefunden wird („Messageb" → tief verschachteltes „MessageBoxes.nav"). **Bewusst nicht** über den relativen Pfad gefiltert: dessen führendes `..\\..\\` dominiert sonst den Fuzzy-Match des Clients, sodass das Tippen eines bloßen Dateinamens **nichts** findet (das war der Bug — „No suggestions" trotz vorhandener Items). Eingefügt wird der zum aktuellen Nav-File relative Pfad (`PathHelper.GetRelativePath`) per **`TextEdit`**, dessen Range den gesamten Inhalt zwischen den `"` abdeckt. Die aktuell editierte Datei wird ausgeschlossen. Läuft **vor** der Quote-Unterdrückung (Path lebt ja IN den Quotes); `null`-Rückgabe von `GetPathCompletions` heißt „kein taskref-String-Kontext" → Nav/Edge übernehmen. Item trägt `InsertText` (relativer Pfad) + `ReplacementExtent` + `Detail`, Kind `File`. **Bewusste Abweichung vom VS-Port:** die VS-`PathCompletionSource` navigiert verzeichnisweise; hier ist es eine flache Suche über die Solution (Wunsch: Datei allein per Name finden). End-to-End gegen den laufenden Server per stdio-Smoke-Test verifiziert.
  - Keine Nav/Edge-Vorschläge in Kommentaren/Strings/Code-Blöcken (`unit.Syntax.FindToken` + `StringExtensions.IsInQuotation`/`IsInTextBlock`). Zeilen-Helfer (`GetStartOfIdentifier`/`PreviousNonWhitespace`/`GetPreviousIdentifier`/`GetStartOfEdge`) als offset-basierter faithful Port der VS-`TextSnaphotLineExtensions`.
  - Server: `textDocument/completion`-Handler (übergibt `_workspace.Solution`) + `CompletionProvider`-Capability (TriggerChars `:` `-` `"` `/` `\`); `SortText` nach Listen-Index erhält die Reihenfolge; Path-Items bekommen einen expliziten `TextEdit` (Range = `ReplacementExtent`, NewText = relativer Pfad) statt des Default-Wortersatzes, `FilterText` = **Dateiname** (Filtern allein per Name; der relative Pfad als FilterText würde wegen des führenden `..\\..\\` jeden bloßen Dateinamen-Match verhindern), `Detail` = relativer Pfad. **Noch offen:** C#-Code-Block-Completion (`CodeCompletionSource`) bleibt VS/Roslyn-only. Tests: `Nav.Language.Tests/Completion/NavCompletionServiceTests.cs` (7) + `NavCompletionPathTests.cs` (5) grün net472+net10 (Suite 652).
- **VS-Code-PoC** (`vscode-nav-lsp`): startet den Server per stdio, registriert Sprache `nav`.
- Serialisierungs-Fix: Newtonsoft-Serializer camelCased unbenannte LSP-DTO-Properties (sonst PascalCase → Client verwirft z.B. die Semantic-Tokens-Legend).

**Server-Dateien** (`Nav.Language.Lsp/`): `Program.cs` (stdio + JSON-RPC + camelCase-Resolver + stdout-Hardening),
`NavLanguageServer.cs` (Handler + Capabilities), `NavWorkspace.cs` (Workspace + Overlay + Unit/Tree-Zugriff),
`OverlaySyntaxProvider.cs`, `NavUri.cs`, `DiagnosticsComputer.cs`, `LspMapper.cs`, `SemanticTokensBuilder.cs`, `DocumentSymbolBuilder.cs`, `FoldingRangeBuilder.cs`.
Completion-Mapping (Engine-Kind → LSP-`CompletionItemKind` + `SortText`) liegt im `Completion`-Handler in `NavLanguageServer.cs`; der Logik-Kern in `Nav.Language/Completion/` (`NavCompletionService.cs`, `NavCompletionItem.cs`).
**Code-Actions-Kern:** `Nav.Language/CodeActions/` (`NavCodeActionService.cs`, `NavCodeAction.cs`); der `codeAction`-Handler (Range→`TextExtent`, `NavCodeAction`→`Lsp.CodeAction`/`WorkspaceEdit`, Kategorie→`CodeActionKind`, Titel-Dedup) liegt in `NavLanguageServer.cs`.

---

## 3. Option B — GoTo-Resolution in die Engine extrahieren — ERLEDIGT (`bba07a95`)

**Umgesetzt:** Engine-Kern `Nav.Language/GoTo/` — `NavGoToService.GetGoToLocations(unit, offset)` (public)
+ interner `GoToTargetResolver: SymbolVisitor<IEnumerable<Location>>`. Bildet die **Nav→Nav-Zweige** des
VS-`GoToSymbolBuilder` ab: Include→Datei, TaskNode→Task-Deklaration (cross-file), NodeReference→Node-Decl,
ExitRef→Exit-Definition. Server: `textDocument/definition`-Handler + `DefinitionProvider`-Capability,
`LspMapper.ToOffset`/`ToLocation`. Tests: `Nav.Language.Tests/GoTo/NavGoToServiceTests.cs` (4) grün net472+net10.

> **Korrektur zur ursprünglichen Q1-Annahme:** Die unten in Q1 als Nav→Nav vermuteten Provider
> (`TaskDeclarationLocationInfoProvider`, `TaskExitDeclarationLocationInfoProvider`,
> `TriggerDeclarationLocationInfoProvider`, `TaskBeginDeclarationLocationInfoProvider`) sind **alle Roslyn**
> (erben `CodeAnalysisLocationInfoProvider`, springen in den generierten C#-Code). Die echte Trennlinie ist
> **Provider-Klasse**, nicht Symboltyp: `SimpleLocationInfoProvider`-Zweige im `GoToSymbolBuilder` = Nav→Nav
> (extrahiert), `CodeAnalysisLocationInfoProvider`-Zweige = VS/Roslyn (bleiben).

**Noch offen (optional):** Der VS-`GoToSymbolBuilder` ruft den neuen Engine-Kern noch **nicht** auf — erst
damit ist „eine Engine" real (aktuell nutzt nur der Server den Kern).

### Ursprüngliches Analysebriefing (Kontext)

**Ziel:** Eine wiederverwendbare Nav→Nav-Navigationslogik in `Nav.Language`, die sowohl die VS-Extension
als auch der LSP-Server nutzen (Ideal „eine Engine"). Aktuell liegt GoTo nur VS-gekoppelt vor.

**Ist-Zustand (VS-gekoppelt, NICHT serverfähig):** `Nav.Language.ExtensionShared/GoToLocation/`
- `GoToLocationService.cs` (UI), `GoToTag.cs` (Tagger, `Microsoft.VisualStudio.Text.Tagging`), `LocationInfo.cs`.
- `Provider/*` (~16 Provider). Kopplung: `Microsoft.VisualStudio.Text` (`ITextBuffer`), `…Imaging` (`ImageMoniker`),
  `Microsoft.CodeAnalysis` (Roslyn) und `Nav.Language.CodeAnalysis.FindSymbols.LocationFinder` —
  etliche Provider springen in den **generierten C#-Code**/Call-Sites (Roslyn) → das bleibt VS-/Roslyn-only.

**Wiederverwendbarer Kern (Engine, schon vorhanden):**
- `CodeGenerationUnit.Symbols[TextExtent]` → Symbole an einer Position (Indexer in `SymbolList`/`SymbolCollection`).
- Referenz → Deklaration: `INodeReferenceSymbol.Declaration` (→ `INodeSymbol`), `ITaskNodeSymbol.Declaration`
  (→ `ITaskDeclarationSymbol`, cross-file via `IsIncluded`/`Origin`/`Location`), `IExitConnectionPointReferenceSymbol.Declaration`.
- `ISymbol.Location` (Name), `SyntaxNode.GetLocation()` (voller Bereich), `SyntaxToken.GetLocation()`.
- `Nav.Language/FindReferences/ReferenceFinder` (`FindReferencesVisitor`) — engine-level Referenzsuche (für References/Highlight).

**Vorschlag:** Ein VS-freier Service in `Nav.Language`, z.B. `NavNavigationService` / `GoToResolver` mit
`Resolve(CodeGenerationUnit unit, int position) → IEnumerable<Location>` (Nav→Nav). Die VS-Provider rufen
diesen Kern auf und ergänzen nur UI/Codegen; der LSP-Server nutzt ihn direkt für `textDocument/definition`.

**Zu analysierende offene Fragen:**
1. Provider-Kategorisierung: welche sind reines Nav→Nav (extrahierbar) vs. Codegen/Roslyn (bleiben VS)?
   Kandidaten Nav→Nav: `TaskDeclarationLocationInfoProvider`, `TaskExitDeclarationLocationInfoProvider`,
   `TriggerDeclarationLocationInfoProvider`, `TaskBeginDeclarationLocationInfoProvider`. Codegen/Roslyn:
   `CodeAnalysisLocationInfoProvider` (Basis), `*CodeFile*`, `Nav*Annotation*`, `NavInitCall*`/`NavExitBeginCaller*`.
2. Cross-File: liefert `ITaskDeclarationSymbol.Location` bei `IsIncluded==true` einen brauchbaren `FilePath`+Position?
   (Server hat den Workspace, kann die Zieldatei öffnen — VS nutzt Includes anders.)
3. Wie löst der VS-Tagger heute „Caret → welcher Provider"? (Muss der Kern nachbilden: `unit.Symbols[caretExtent]`.)
4. Mapping mehrerer Treffer (das VS-„Go To…"-Menü) → LSP `Location[]`.

---

## 4. Weitere offene TODOs (priorisiert)

**LSP-Features (Phase 2, additiv, risikoarm):**
- ~~**GoTo Definition** (`textDocument/definition`)~~ — **erledigt** (Option B, `bba07a95`).
- ~~**References / documentHighlight**~~ — **erledigt.** `references` cross-file über die vorhandene Engine-API
  `ReferenceFinder.FindReferencesAsync` (iteriert `NavSolution.ProcessCodeGenerationUnitsAsync`, also
  solution-weit für Task-Definitionen) mit sammelndem `ReferenceCollector: IFindReferencesContext` im Server.
  `documentHighlight` same-file über den nach `Nav.Language/References/` portierten Symbol-Walker
  (`ReferenceRootFinder` + `HighlightSymbolFinder`, VS-frei) hinter `NavReferenceService`. Caret-Auflösung über
  neuen `SymbolPosition.SymbolsAt` (striktes Enthaltensein) statt des greedy `Symbols[extent, includeOverlapping]`-
  Indexers; GoTo mit umgestellt. Tests: `Nav.Language.Tests/References/NavReferenceServiceTests.cs`.
  > **Fallstrick (kein Bug):** „documentHighlight flackert kurz auf und verschwindet" lag NICHT am Server
  > (Antworten sind korrekt/stabil), sondern daran, dass VS Code Occurrence-Highlights beim **Fokusverlust des
  > Editors** löscht. Auslöser war die PoC-Extension, die das Output-Panel aufdrängte
  > (`revealOutputChannelOn: Info` + `log.show`). Behoben in `vscode-nav-lsp/extension.js` (→ `Never`).
- ~~**Hover** (`textDocument/hover` ↔ `SymbolQuickInfoSource`)~~ — **erledigt.** Engine-Kern
  `Nav.Language/QuickInfo/NavHoverService.GetHover(unit, offset)` (VS-frei) liefert `NavHoverInfo`
  (klassifizierte `DisplayParts` aus dem vorhandenen `DisplayPartsBuilder` + Symbol-`Location` + erreichbare
  `Calls`). Caret-Auflösung über `SymbolPosition.SymbolsAt` (spezifischstes Symbol; `init`-mit-Alias wird wie in
  der VS-QuickInfo übersprungen). Server: `textDocument/hover`-Handler + `HoverProvider`-Capability;
  rendert die Signatur als ` ```nav `-Codeblock (`MarkupContent`, Markdown) mit Symbol-Range.
  **Choice/Edge-Auflösung** (wie die VS-QuickInfo): bei einem `IChoiceNodeSymbol`/`IChoiceNodeReferenceSymbol`
  listet der Hover unter der `choice`-Kopfzeile alle transitiv erreichbaren Knoten (`ChoiceNodeSymbolExtensions.ExpandCalls`);
  bei einem `IEdgeModeSymbol` (Caret auf dem Pfeil/Verb) nur die erreichbaren Knoten ohne Kopfzeile
  (`EdgeExtensions.GetReachableCalls`, Choices transitiv aufgelöst). Je Zeile „&lt;Pfeil-Token&gt; Zielsignatur"
  (das getippte `-->`/`o->`/`*->`/`==>` via `IEdgeModeSymbol.Name`, nicht das ausgeschriebene `Verb`), nach
  Knotennamen sortiert (wie VS). Die Reachable-Logik ist engine-level/VS-frei — nur das WPF-*Rendering* (Icons,
  `EdgeQuickInfoControl`/`StackPanel` in `QuickinfoBuilderService.Visitor`) bleibt VS-only.
  PoC braucht **keine** Client-Änderung (vscode-languageclient fragt Hover bei advertiseter Capability automatisch).
  Tests: `Nav.Language.Tests/QuickInfo/NavHoverServiceTests.cs` (7) grün net472+net10 (Suite 640).
- ~~**Rename** (`textDocument/rename`)~~ — **erledigt.** Engine-Kern `Nav.Language/Rename/NavRenameService.GetRenameFix(unit, offset, settings)`
  (VS-frei) liefert den passenden `RenameCodeFix` aus der vorhandenen Refactoring-Infrastruktur
  (`RenameCodeFixProvider`/`CodeFixContext`), die **auch die VS-Extension** nutzt (`RenameCommandHandler`) —
  „eine Engine". Caret-Auflösung über `SymbolPosition.SymbolsAt` (konsistent zu GoTo/References/Hover).
  Server: `textDocument/rename`-Handler + `RenameProvider`-Capability; baut aus `RenameCodeFix.GetTextChanges(newName)`
  (Engine-`TextChange`, offset-basiert) eine `WorkspaceEdit` mit `TextEdit[]` je Dokument. **Dateilokal wie der
  VS-Rename:** alle Änderungen beziehen sich auf das aktuelle Dokument (Cross-File-Referenzen einer Task-Definition
  via `taskref` werden nicht mit umbenannt; eine Task-Referenz wird über ihren lokalen Alias umbenannt — exakt das
  VS-Verhalten). **Validierung:** `RenameCodeFix.ValidateSymbolName` vor dem Erzeugen der Änderungen; ungültiger Name
  (Keyword, bereits vergeben, …) → `LocalRpcException` mit der Engine-Meldung, die VS Code direkt am Rename-Feld
  zeigt. Caret auf keinem Symbol → „You must rename an identifier." (wie die VS-Fehlermeldung). Der Zeilenumbruch
  für die Engine wird aus dem Dokument erkannt (CRLF vs. LF, `EditorSettingsFor`), TabSize 4. **Kein** `prepareRename`
  (das Protokoll-Paket 17.2.8 kennt es nicht — keine `PrepareRename`-Typen). PoC braucht **keine** Client-Änderung
  (vscode-languageclient fragt Rename bei advertiseter Capability automatisch). Tests:
  `Nav.Language.Tests/Rename/NavRenameServiceTests.cs` (6) grün net472+net10 (Suite 662); zusätzlich end-to-end per
  stdio-Smoke gegen die laufende `nav.lsp` (Rename liefert beide Vorkommen, Duplikat/Keyword liefern den erwarteten Fehler).
- ~~**Code Actions** (`textDocument/codeAction`)~~ — **erledigt.** Engine-Kern
  `Nav.Language/CodeActions/NavCodeActionService.GetCodeActions(unit, range, settings)` (VS-frei) bündelt
  **exakt** die sechs CodeFix-Provider, die die VS-Extension als SuggestedActions exportiert, und nutzt damit
  dieselbe Refactoring-/CodeFix-Infrastruktur wie die VS-Lightbulb — „eine Engine":
  **ErrorFix** `AddMissingExitTransition`; **StyleFix** `RemoveUnusedNodes`, `RemoveUnusedTaskDeclaration`,
  `RemoveUnusedIncludeDirective`, `AddMissingSemicolonsOnIncludeDirectives`; **Refactoring** `IntroduceChoice`.
  Jede Aktion liefert ihre aufgelösten, offset-basierten `TextChange` direkt mit (`NavCodeAction`), der Server
  baut daraus je Aktion eine dateilokale `WorkspaceEdit` (`TextEdit[]` unter der vom Client gesendeten URI-Form,
  `%3A` bleibt erhalten) — **kein** `codeAction/resolve` nötig. Server: `textDocument/codeAction`-Handler +
  `CodeActionProvider`-Capability (`CodeActionOptions` mit `CodeActionKinds` = `quickfix` + `refactor`);
  Kategorie→Kind-Mapping: `Refactoring`→`Refactor`, sonst (`ErrorFix`/`StyleFix`/`CodeFix`)→`QuickFix`.
  **Caret-Expansion (Fallstrick):** ein leerer LSP-Range (reiner Caret, Start==End) findet über den
  nicht-überlappenden Symbol-/Token-Indexer der Provider (`includeOverlapping=false`, Elemente müssen
  **vollständig** im Bereich liegen → bei Länge 0 unmöglich) **nichts**; der Service dehnt ihn daher auf den
  Token-Extent an der Position aus (`unit.Syntax.FindToken`), sodass die Provider wie bei einer Selektion auf dem
  Bezeichner greifen. Eine reale Selektion wird unverändert durchgereicht. Doppelte Titel werden im Server
  zusammengefasst (erster Treffer bleibt — wie die VS-`FilterDuplicateTitles`). **Bewusst NICHT enthalten:** der
  parametrische `RenameCodeFix` (läuft über den eigenen `textDocument/rename`-Pfad mit Eingabefeld); `IntroduceChoice`
  bekommt ohne Dialog den von der Engine vorgeschlagenen, garantiert gültigen Namen (`SuggestChoiceName`), den der
  Nutzer danach umbenennen kann. PoC braucht **keine** Client-Änderung (vscode-languageclient fragt Code-Aktionen
  bei advertiseter Capability automatisch). Tests: `Nav.Language.Tests/CodeActions/NavCodeActionServiceTests.cs` (5)
  grün net472+net10 (Suite 663); zusätzlich end-to-end per stdio-Smoke gegen die laufende `nav.lsp` (Capability +
  „Remove Unused Nodes"-WorkspaceEdit verifiziert, camelCase + `CodeActionKind`-Enum→String korrekt).

**Korrektheit / Robustheit:**
- ~~**Dependency-aware Re-Diagnose:**~~ **erledigt** (committet, `2b114aa2`). Bei `didChange`/`didClose` werden
  jetzt zusätzlich die (transitiv) **inkludierenden** Dateien neu diagnostiziert und publiziert — sie bleiben
  sonst veraltet, obwohl sich ihre Cross-File-Diagnostics geändert haben können. Neu: VS-freie Engine-Klasse
  `Nav.Language/Workspace/IncludeDependencyGraph.cs` (Vorwärtskanten Inkludierer→inkludierte Datei, alle Pfade
  via `PathHelper.NormalizePath`; `GetDependentsClosure` = transitiver Reverse-BFS, zyklensicher). `NavWorkspace`
  pflegt den Graphen: beim Solution-Scan (`PublishAllDiagnosticsAsync`) vollständig befüllt (auch nie geöffnete
  Inkludierer) und bei **jeder** Unit-Berechnung über `RecordIncludes(unit)` (aus `unit.Includes[*].FileName`)
  aufgefrischt; neue Methode `PublishDependentsAsync(changedFilePath, publishAsync, ct)` re-diagnostiziert die
  Closure (Publish-URI aus `FileInfo.FullName` wie beim Scan → keine Geister-Diagnostics durch Casing).
  `NavLanguageServer`: `didChange` → `UpdateOverlayAndPublishAsync(..., publishDependents: true)`,
  `didClose` → `PublishDocumentAndDependentsAsync` (Inhalt fällt auf Platte zurück → Abhängige können sich
  ändern); `didOpen` bleibt bewusst einfach (Öffnen ändert den Inhalt nicht). **Funktioniert ohne
  Semantic-Cache-Invalidierung**, weil der `SemanticModelProvider` Units ohnehin frisch baut und der
  `OverlaySyntaxProvider` die geänderte Datei bereits invalidiert — es fehlte allein das Anstoßen+Publizieren der
  Abhängigen. **Grenze:** Disk-Edits nie geöffneter Inkludierer werden (noch) nicht erfasst → das deckt
  `workspace/didChangeWatchedFiles` ab. Tests: `Nav.Language.Tests/Workspace/IncludeDependencyGraphTests.cs` (7,
  net472+net10, Suite 670); zusätzlich end-to-end per stdio-Smoke verifiziert (zwei Dateien: Umbenennen der in
  `lib.nav` deklarierten Task lässt main.nav 0→3 Diagnostics bekommen, obwohl nicht editiert, und nach dem
  Reparieren wieder 3→0).
- ~~**`workspace/didChangeWatchedFiles`**~~ (externe Änderungen, auch geschlossener Dateien) **erledigt**
  (committet, `36a33d91`). Client meldet *.nav-Datei-Events über `synchronize.fileEvents`
  (`workspace.createFileSystemWatcher('**/*.nav')` in `extension.js`). Handler `DidChangeWatchedFilesAsync`
  in `NavLanguageServer`: je Event **offene Dokumente überspringen** (Overlay schlägt Platte),
  `NavWorkspace.InvalidateDiskCache(path)` (neue `OverlaySyntaxProvider.InvalidateCache` — nur Platten-Cache,
  Overlay bleibt), bei **Created** `AddSolutionFile` / bei **Deleted** `RemoveSolutionFile` (inkrementell, KEIN
  Re-Globben; Letzteres entfernt auch die Graph-Kanten der Datei via `IncludeDependencyGraph.Remove`), dann die
  Datei selbst (gelöscht → leere Diagnostics = Anzeige am Client gelöscht) **plus ihre Abhängigen**
  (`PublishDependentsAsync`) neu publizieren. **Created-Sonderfall:** eine zuvor fehlgeschlagene `taskref`-Direktive
  hinterlässt KEINE Graph-Kante (unaufgelöste Includes werden nicht verzeichnet, s. `ProcessIncludeDirective`),
  daher findet der Graph die Inkludierer einer neuen Datei nicht → beim Anlegen einmalig (gebündelt für den
  Batch) `PublishAllDiagnosticsAsync` (Voll-Rescan). Verifiziert per stdio-Smoke: lib.nav nur auf Platte
  geändert/gelöscht/neu angelegt (NICHT geöffnet) → main.nav reagiert (Changed 0→3, Fixed 3→0, Deleted →
  „include not found" + lib geleert, Created → 0). **Offen:** `didChangeWorkspaceFolders`.
- Inkrementeller Doc-Sync statt Full (optional).
- Severity-Mapping `Suggestion → Information` ggf. auf `Hint` ändern (Geschmack).

**Toolchain / Deployment:**
- ~~**Self-contained Publish:**~~ **erledigt.** `n publishlsp` (analog `n build` via `vswhere`)
  → `deploy\lsp\nav.lsp.exe` als **Single-File** (`win-x64`, **genau 1 Datei**, ~39 MB komprimiert, inkl. .NET-Runtime).
  Kern: `MSBuild.exe …Nav.Language.Lsp.csproj -restore -t:Publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true`
  `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`
  `-p:SatelliteResourceLanguages=en -p:DebugType=embedded -p:PublishDir=…\deploy\lsp\`.
  `SatelliteResourceLanguages=en` entfernt die lokalisierten Satellite-Ordner (cs/de/es/.../zh-Hant); `DebugType=embedded`
  spart die separate `.pdb`. **Single-File ist nur Bündelung (KEIN Trimming)** — Newtonsoft/StreamJsonRpc/LSP-DTOs
  (reflection-lastig) bleiben heil; Trimming wäre tabu. `AssemblyName=nav.lsp` (im `.csproj`, `RootNamespace` unverändert).
  Kein `Assembly.Location`-Gebrauch im Server/Engine (geprüft) → single-file-sicher; `GetManifestResourceStream`
  (Engine-Templates) funktioniert im Bundle. Verifiziert: stdio-Smoke gegen die Single-File-`nav.lsp.exe`
  (`initialize` → volle Capabilities, sauberer `exit`). `deploy` ist gitignored. Extension findet die Ausgabe
  automatisch (s. u.).
- ~~**VS-Code-Paketierung (installierbares VSIX):**~~ **erledigt.** `n packagevscode` erzeugt in
  einem Aufruf ein fertiges, self-contained VSIX: ruft `n publishlsp` (frischer Server-Build), bettet
  `nav.lsp.exe` als `vscode-nav-lsp\server\nav.lsp.exe` in die Extension ein (relativ zu `__dirname` aufgelöst —
  im installierten VSIX gibt es **keinen** Schwesterordner `deploy\lsp`) und paketiert plattform-spezifisch via
  `npx @vscode/vsce package <version> --no-update-package-json --no-git-tag-version --skip-license --target win32-x64 --out deploy\vscode\nav-language-<version>-win32-x64.vsix`.
  Die `<version>` stammt aus `Version.props` (`ProductVersion`, derzeit 5.18.0) — eine Quelle der Wahrheit;
  `package.json` wird beim Paketieren nicht angefasst.
  `extension.js#resolveServer`: neue Reihenfolge **konfigurierter Pfad → eingebettet `./server` → Repo
  `..\deploy\lsp` (F5) → Debug-DLL via `dotnet` (F5)**. Extension auf fertige Identität umbenannt
  (`name=nav-language`, `displayName=Nav Language`, `v1.0.0`, `repository`-Feld gegen den interaktiven
  vsce-Prompt). Neu: `vscode-nav-lsp\.vscodeignore` (node_modules/server bleiben drin). `vscode-nav-lsp\server`
  ist gitignored (Build-Artefakt). Voraussetzung: Node/npm im PATH.
- **2a (`dotnet publish` ermöglichen) — bewusst NICHT umgesetzt (Fallstrick dokumentiert):**
  `CodeTaskFactory → RoslynCodeTaskFactory` in `CustomBuild.targets` reicht **nicht**. Verifiziert:
  RoslynCodeTaskFactory *läuft* zwar unter `dotnet`, aber die inline-kompilierte Codegen-Task kann die
  Facade-Closure der **netstandard1.x**-Codegen-Libs (StringTemplate4 `netstandard1.3` → `System.Runtime 4.0.20.0`,
  Antlr3.Runtime `netstandard1.1` → `System.Collections 4.0.10.0`) nicht auflösen (CS0012); fügt man das
  NETStandard.Library.Ref-`netstandard.dll` hinzu, verliert die Kompilierung die corlib komplett (CS0518).
  Robust wäre nur, den Codegen in ein **kompiliertes netstandard2.0-Task-Projekt** auszulagern (eigenes
  Projekt + Bootstrapping via `ProjectReference`/`UsingTask`, beide MSBuilds + Tests neu verifizieren) —
  ein echter Eingriff in den Kern-Engine-Build. Vorerst zurückgestellt; Publish läuft über `MSBuild.exe`.
- ~~**Security:**~~ **erledigt** (committet, `5222e534`). `StreamJsonRpc 2.22.11` zog transitiv `MessagePack 2.5.192`
  (GHSA-hv8m-jj95-wg3x, High — LZ4-Out-of-Bounds-Read; bei uns ohnehin nur JSON-Transport). In
  `Directory.Packages.props` auf die gepatchte **2.5.301** (gleiche 2.x-Linie, erfüllt StreamJsonRpcs
  `>=2.5.192` → kompatibel) hochgepinnt; greift dank `CentralPackageTransitivePinningEnabled` ohne direkten
  `PackageReference`. Build danach **0 Warnungen** (NU1903 weg); Server-Laufzeit per stdio-Smoke bestätigt.

**Phase 3 (eigentliches Ziel 1 — Last raus aus `devenv.exe`):**
- VS als RPC-Client: `ParserService`/`SemanticModelService` → Proxy gegen den Server. Großer Posten
  (Objektgraph → serialisierbare DTOs, ~38 Konsumenten). Plan empfiehlt zuerst einen **Spike** (nur Diagnostics
  end-to-end in VS) zur Aufwandsschätzung. Option B passt gut hier hinein (gemeinsame Engine-Services).

---

## 5. Memories (laden automatisch in neue Sessions)
- `nav-lsp-build-toolchain` — Build/Test-Toolchain-Constraints.
- `nav-lsp-uri-windows-gotcha` — `System.Uri.LocalPath`-Fallstrick bei `%3A`.
- `generic-xaml-assembly-name-coupling` — (älter, Extension-Theming).
