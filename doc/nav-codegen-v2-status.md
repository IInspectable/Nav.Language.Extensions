# V2-Codegen — Umsetzungs-Status & Step-Plan

> **Lebendes Tracking-Dokument** für die Umsetzung des V2-Codegens (CallContext, Continuation,
> Choices in C#). Die **finale Spezifikation** steht in `doc/nav-codegen-v2-concat-design.md` — dieses
> Dokument fasst sie **nicht** zusammen, sondern verfolgt nur den Fortschritt und den feinkörnigen
> Step-Zuschnitt. Einordnung in die Versionierungs-Infrastruktur: `doc/nav-codegen-versioning.md`
> (dort ist das hier Step 6 „V2-Inhalte"; die anschließende V2-Navigation ist dort Step 7).

## Einordnung

Der Fahrplan §6 des Design-Dokuments nennt sechs grobe Schritte. Für die Arbeitsweise „ein Step =
Review + Build/Test + gelieferte Commit-Message" (siehe `CLAUDE.md`) sind einige davon zu groß —
insbesondere §6.3 (Semantic Model) bündelt Semantik-Kern, mehrere Analyzer, Versions-Gate und
Completion. Dieses Dokument löst den Fahrplan daher in **commit-große Steps S0–S8** auf; die Spalte
**Design-Ref** verweist zurück auf die Spezifikation.

Die V2-Arbeit spannt bewusst zwei Bänder auf:

- **Syntax + Semantic Model + Completion** (S1–S4) — laut Design §4 **versionsunabhängig**, einmal
  für alle Codegen-Versionen vorwärts portiert. (Das ist *nicht* die Pragma-Infrastruktur aus
  `nav-pragmas-versioning-status.md`, die bereits steht — hier entstehen die **neuen** Konstrukte
  `--^`/`o-^`, `choice [params]`, Nav0120–0124.)
- **Codegen V2** (S5–S8) — das neue `CodeGen/V2/` hinter dem bestehenden
  `VersionDispatchingCodeGenerator`.

## Step-Plan

Reihenfolge so gewählt, dass jeder Step für sich baubar/testbar ist und die V1-Neutralität
(bestehende Regression byte-identisch) nach jedem Step gilt. Nach jedem Step: Code-Review +
`nav test` (net472) **und** `dotnet test … -f net10.0` (beide TFMs grün) + gelieferte
Commit-Message — der Commit macht der Nutzer.

| # | Inhalt | Design-Ref | Fertig, wenn | Status |
|---|---|---|---|---|
| **S0** | Status-Doc (dieses) + Golden-**Input**-`.nav` festschreiben: CallContext-Grundform, Continuation, Choice-mit-3-Quellen | §6.1, §8.2 | `.nav` liegen unter `Regression/Tests/V2/`, alle `#version 2`, je distinkter Task-Name **und** `[namespaceprefix]` (sonst Dateinamens-Kollision, §7) | **erledigt** — `BasicFlow.nav`/`ContinuationFlow.nav`/`ChoiceFlow.nav`; Harness überspringt den `V2\`-Teilbaum vorläufig (s.u.) |
| **S1** | **Syntax vorwärts portieren:** Tokens `--^`/`o-^`, `ContinuationTransitionSyntax` (`Edge`/`TargetNode` optional), `choice [params]` (Wiederverwendung `ParameterListSyntax`), Visitor/Walker; `ModalEdgeKeywordAlt "*->"` entfernen | §6.2 | Parser-/Syntax-Tests grün, beide TFMs | **erledigt** — s.u. |
| **S2** | **Semantic-Model-Kern:** `IContinuationTransition`/`ContinuationTransition`, `IContinuableEdge`, `ContinuationCall` in `Call`, Edge-Mode-Behandlung, Parameter am `IChoiceNodeSymbol`; **Nav0222-Fix** (Reachability bei unterschiedlichen Edge-Modes) | §6.3 (Teil A) | Semantik-Tests grün | **erledigt** — s.u. |
| **S3** | **Struktur-Analyzer:** Nav0120 (Continuation-Quelle = GUI/View-Knoten), Nav0121 (Ziel = Task), Nav0122 (verschiedene Views unzulässig) + Diagnostics-Fixtures (`//==>>`) | §6.3 (Teil B), §4 | Diagnostics-Fixtures grün | **erledigt** — s.u. (Nav0124 → S4 verschoben) |
| **S4** | **Versions-Gate + Completion + Nav0124:** `NavLanguageFeature` (`Continuation`/`ChoiceParameters`, `RequiredVersion = Version2`) → **Nav5000**; **versionsbewusste Completion** (§4.1: `VisibleEdgeKeywordItems`/Choice-`[params]` hinter `NavLanguageFeatures.IsAvailable`); **Nav0124** (generische Member-Kollision, **versions-gated** — daher hierher gezogen) | §6.3 (Teil C), §4, §4.1 | `--^`/`o-^`/`[params]` in `#version 1` **nicht**, ab `#version 2` **doch** vorgeschlagen; Nav5000- + Nav0124-Fixtures | offen |
| **S5** | **`CodeGen/V2/`-Gerüst:** CallContext-Grundform (Voll-Fabrik + opaker `Result`, Maschinerie = `Unwrap()`-Aufruf), **alle** Transitionen — **ohne** Continuation/Choice; über Dispatcher geschaltet | §6.4 | Golden gegen Grundform; **V1 byte-identisch** | offen |
| **S6** | **V2 Continuation:** `Show`/`Continuation` mit inline `.Concat(…)`, **`o-^` UND `--^`** (Builder wählt `OpenModalTask`/`GotoTask`); **`FrameworkStubs.cs`** um `.Concat`-Typfläche erweitern | §6.5 | Golden `o-^`+`--^` kompiliert gegen Stubs (kein Laufzeit-Test) | offen — **gated?** (§Gating) |
| **S7** | **V2 Choices in C#:** Choice-Context + `Choice_XLogic` + Forward aus den Quellen (kein Dispatch), inkl. Choice→Choice, Union, Multi-Exit | §6.6 | Golden gegen 3-Quellen-Fall (§3.1) | offen |
| **S8** | **Isolierte Sonderform-Fixtures:** `[notimplemented]` (throw-Thunk) und `[donotinject]` (expliziter Wrapper-Parameter) je als Ein-Konzept-Golden | §7 | je isoliertes Minimal-Golden | offen |

### S0-Anmerkung: vorläufiger Harness-Skip des `V2\`-Teilbaums

Die drei Golden-**Input**-`.nav` liegen bereits am Zielort (`Regression\Tests\V2\`), können aber noch
nicht durch die `NavCodeGeneratorPipeline` laufen: Sprachversion 2 ist noch nicht in
`NavLanguageVersion.SupportedVersions` freigeschaltet (jedes `#version 2` würde **Nav5001** werfen und
`RunResult.Failed` erzwingen — die V1-Regression bräche), und die neue Syntax (`o-^`/`--^`,
`choice [params]`) parst erst ab S1. Bis der V2-Codegenerator steht (S5 ff.), überspringt der
`RegressionTests`-Harness den `V2\`-Teilbaum daher explizit (`IsPendingVersion2Corpus` in
`Nav.Language.Tests\Regression\RegressionTests.cs`, greift in `CollectNavFiles` **und**
`PlainGetFileTestCases`). Die Dateien sind bis dahin **reine Referenz-Eingaben** — kein `.expected.cs`.
Sobald V2 generiert, entfällt der Filter und der Teilbaum bekommt seine `.expected.cs` (dann greift die
Auto-Discovery aus §7 ohne Sonderfall).

### S1-Anmerkung: was die Syntax-Portierung umfasst

Umgesetzt (versionsunabhängig, rein syntaktisch — Versions-Wirksamkeit ist erst S4):

- **Tokens:** `SyntaxTokenType.ContinuationGoToEdgeKeyword` (`--^`) und `ContinuationModalEdgeKeyword`
  (`o-^`); der `NavLexer` erkennt sie — wie die regulären Mehrzeichen-Kanten — **vor** dem Identifier.
  In `SyntaxFacts` liegen sie in einer **eigenen** Menge `ContinuationEdgeKeywords`
  (`IsContinuationEdgeKeyword`), bewusst **nicht** in `NavKeywords`/`EdgeKeywords`: eine Continuation
  leitet keine neue Transition ein. Der alte Alt-Token `*->` (`ModalEdgeKeywordAlt`) ist **entfernt**
  (er wurde vom Lexer ohnehin nie erzeugt).
- **Syntaxknoten:** `ContinuationEdgeSyntax` (abstrakt) mit `ContinuationModalEdgeSyntax` (Modal) und
  `ContinuationGoToEdgeSyntax` (Goto); `ContinuationTransitionSyntax` (`Edge`/`TargetNode`, beide
  fehlertolerant optional). `TransitionDefinitionSyntax` **und** `ExitTransitionDefinitionSyntax`
  tragen ein optionales `ContinuationTransition`-Kind (hinter dem Zielknoten, vor Trigger/Bedingung).
  `ChoiceNodeDeclarationSyntax` bekommt ein optionales `CodeParamsDeclaration` (Wiederverwendung von
  `ParameterListSyntax`, geparst wie beim `init`-Knoten über `CodeBlockHost.ChoiceNode`).
- **Parser:** `ParseContinuationTransition`/`ParseContinuationEdge` samt Grammatik-Fragmenten; die
  Continuation wird nur bei `o-^`/`--^` geparst — V1-Transitionen bleiben byte-identisch.
- **Visitor/Walker** entstehen automatisch (Quellgenerator über alle konkreten `*Syntax`-Typen).
- **Grammatik-Konsistenz:** die EBNF-Fragmente sind geschlossen (NAV001/NAV002 grün); der
  Literal-Terminal-Check kennt jetzt auch `IsContinuationEdgeKeyword`.

Verifikation: `nav build` + beide TFMs grün (net10 1369/0, net472 1377/0 — je 3 explizite Skips).

### S2-Anmerkung: was der Semantic-Model-Kern umfasst

Umgesetzt (versionsunabhängig, reiner Modell-Aufbau — Struktur-Diagnostik ist erst S3):

- **Interfaces:** `IContinuableEdge: IEdge` (trägt optional eine `ContinuationTransition`) — `ITransition`
  **und** `IExitTransition` erben davon; `IContinuationTransition: IEdge` (selbst **nicht**
  `IContinuableEdge`: eine Continuation kann keine weitere tragen).
- **Klasse `ContinuationTransition`:** implementiert `IContinuationTransition` mit **generisch** typisierten
  `IEdge`-Referenzen (Quelle/Ziel als `INodeReferenceSymbol?`). Bewusst **keine** stark typisierten
  Gui-/Task-Shadows wie im concat-Branch: der Branch erzwang die Typen im Builder und emittierte dort die
  Struktur-Diagnosen — das kollidiert mit der sauberen Analyzer-Trennung dieses Repos. Quelle/Ziel-Typprüfung
  (GUI- bzw. Task-Knoten) macht der Analyzer in S3.
- **Trägerkanten:** `Transition` (Basis von Init/Trigger/Choice) und `ExitTransition` bekommen den
  `ContinuationTransition`-Parameter, die `ContinuationTransition`-Property und ziehen deren Symbole in
  `Symbols()` nach; die drei Transition-Subklassen reichen den Parameter durch.
- **`Call.ContinuationCall`:** der `Call`-Ctor nimmt jetzt die **Edge** statt nur des `EdgeMode` und leitet
  daraus bei einer `IContinuableEdge` mit Continuation den Folge-Task-`Call` ab. `CallComparer` bezieht
  `ContinuationCall` in Gleichheit **und** Hash ein (manuelles Hashing beibehalten — `System.HashCode`
  fehlt in netstandard2.0).
- **Reachability:** `EdgeExtensions.GetReachableContinuationCalls` (Folge-Task-Calls einer Kante);
  `GetReachableCalls` baut Calls nun über `new Call(target, edge)`.
- **Builder:** `CreateContinuationTransition` (im Trigger/Init/Choice/Exit-Pfad) baut die Continuation aus
  dem `ContinuationTransition`-Syntaxknoten; Quelle ist der tragende GUI-Knoten (= Zielknoten der
  umgebenden Transition), Ziel der Folge-Task (via `WireTargetNodeReferences` in den Referenzgraphen
  gehängt). `CreateTargetNodeReference` wurde zum richtungs-parametrischen `CreateNodeReference`
  refaktoriert (Quelle **und** Ziel teilen die Reference-Fabrik).
- **Choice-Parameter:** `choice X [params …]` ist bereits über `IChoiceNodeSymbol.Syntax.CodeParamsDeclaration`
  erreichbar (S1-Syntax + vorhandener `Syntax`-Accessor) — **kein** eigenes Symbol, exakt die
  `init [params …]`-Präzedenz (auch dort lesen Konsumenten `Syntax.CodeParamsDeclaration` direkt).

**Nav0222-Scope-Entscheidung.** Der „Nav0222-Fix" ist die **Reachability-Ebene**: `Call` trägt jetzt die
Edge → `ContinuationCall` → korrektes Dedup zweier nur-durch-Continuation-verschiedener Calls. Der
Nav0222-**Analyzer** bleibt V1-**identisch** (bestehende Regression unverändert). Der concat-Branch hatte
den Analyzer breiter umgeschrieben (Exit-Transitionen pro Task-Knoten **gepoolt** statt per-Kante) — das
**ändert die V1-Diagnostik** und verletzte die „V1 unverändert"-Invariante. Continuation-Modus-Konflikte
(dieselbe Continuation-Ziel-Task via `o-^` **und** `--^`) gehören daher V1-sicher zu den S3-Analyzern
(überlappt mit Nav0124), nicht in Nav0222.

Verifikation: `nav build` + beide TFMs grün (net10 1374/0, net472 1382/0 — je 3 explizite Skips);
neue `ContinuationSemanticTests` (5 Fälle).

### S3-Anmerkung: was die Struktur-Analyzer umfassen (und was nach S4 wandert)

Umgesetzt (drei **versionsUNabhängige** Struktur-Analyzer, Severity **Error**, Auto-Discovery wie die
übrigen `NavAnalyzer`, keine Builder-Diagnostik — die Typprüfung von Quelle/Ziel bleibt sauber im
Analyzer, siehe S2-Anmerkung):

- **Nav0120** (`Nav0120SourceNode0OfContinuationMustBeViewOrDialog`): der **tragende** Knoten einer
  Continuation (Zielknoten der umgebenden Transition, `ContinuationTransition.SourceReference`) muss ein
  GUI-Knoten (`IGuiNodeSymbol` = View *oder* Dialog) sein. Iteriert alle continuation-tragenden Kanten
  (`taskDefinition.Edges().OfType<IContinuableEdge>()`), meldet den **aufgelösten** Falschtyp am
  Quell-Referenz-Ort; unaufgelöste Knoten bleiben Nav0011 überlassen (Roslyn-Stil: eine treffende
  Diagnose, keine Folgefehler).
- **Nav0121** (`Nav0121TargetNode0OfContinuationMustBeTask`): das Continuation-**Ziel** (rechts von
  `o-^`/`--^`, `TargetReference`) muss ein Task-Knoten (`ITaskNodeSymbol`) sein — nur ein Task hat die
  `Begin`-Fabrik/`ITASK_BOUNDARY`, auf die `.Concat(…)` lowert. Gleiche Mechanik/Recovery wie Nav0120.
- **Nav0122** (`Nav0122DifferentViewsInContinuationNotSupported`): aus **einer** Quelle erreichbare
  Continuations dürfen nicht auf verschiedene tragende Views zeigen (eine mode-freie `Show{View}` je
  Quelle kann nur **einen** GUI-Knoten tragen). Quelle ist — wie beim concat-Referenzbild —
  unterschiedlich gepoolt: pro **Init-Knoten** alle Ausgänge, pro **Trigger-Transition** einzeln, pro
  **Task-Knoten** alle Exit-Ausgänge. Nutzt die **neue** Reachability
  `EdgeExtensions.GetReachableContinuations` (liefert die `IContinuationTransition`-Anhänge inkl.
  tragendem GUI-Knoten, Choice-Ketten rekursiv aufgelöst — das Pendant zu
  `GetReachableContinuationCalls`, das nur den Folge-Task-Call kennt und für Nav0122 nicht reicht). Bei
  >1 distinktem tragenden Knoten eine Diagnose mit mehreren Locations.

Die concat-Branch-Nummern **Nav1020/1021/1022** wurden nach **Nav0120/0121/0122** umnummeriert (Error
gehört ins `01xx`-Strukturband, nicht ins `Nav1xxx`-DeadCode/Warning-Band, §4) und die Meldungen von
„concatenation" auf **„continuation"** umbenannt (Feature-Name, §1). Anders als der concat-Branch (der
Nav1020/1021 im `TaskDefinitionSymbolBuilder` während der Konstruktion warf) sitzen **alle drei** als
Analyzer im `SemanticAnalyzer/`.

**Nav0124 bewusst nach S4 verschoben.** Der Step-Plan führte Nav0124 (generische Member-Kollision)
ursprünglich in S3. Nav0124 ist aber laut §4 **versions-gated** („nur wo V2-Contexte entstehen") und
rechnet aus der **generierten V2-Member-Menge** einer Quelle (`Show{Node}`/`Begin{Node}`/`{Choice}`) —
beides hängt an Infrastruktur, die erst **S4** (das `NavLanguageFeatures`-Gate) bzw. konzeptionell die
V2-Namensregeln liefert. Ohne Gate feuerte Nav0124 fälschlich auf V1-Units. Nav0120/0121/0122 dagegen
sind **versionsunabhängige reine Struktur** (der concat-Branch feuerte sie ungegated) und haben keine
solche Abhängigkeit — deshalb sauber in S3, Nav0124 zusammen mit dem Gate in S4.

Verifikation: `nav build` + beide TFMs grün (net10 1377/0, net472 1385/0 — je 3 explizite Skips);
drei neue Diagnostics-Fixtures (`Nav0120…`/`Nav0121…`/`Nav0122….nav`) mit `//==>>`-Erwartungen, je
kollateralfrei (nur die Zieldiagnose feuert).

Danach folgt — außerhalb dieses Dokuments, in `nav-codegen-versioning.md` als **Step 7** verankert —
die **V2-Navigation end-to-end** (GoTo Nav↔C#, Rename, FindReferences, Cross-Version-`taskref`),
ggf. mit der versionierten Such-Strategie-Schnittstelle.

## Offene Gating-Entscheidungen (§8 des Design-Docs)

Zwei Team-Entscheidungen, **nicht** Teil der Spezifikation, die die Umsetzung berühren:

1. **`--^`-Laufzeitverifikation ist verwaist (§8.1).** Der exakte
   `GotoGUI(view).Concat(GotoTask(…))`-TWO_STEP-Pfad ist am Framework un-exerziert; der zugehörige
   Ctor trägt einen Framework-Autor-TODO. Der Laufzeit-Smoke-Test läge im **Framework-Repo**, nicht
   hier. **Entscheidung offen:** `--^`-Codegen in **S6** sofort ausliefern **oder** bis zum
   Framework-Smoke-Test gaten.
2. **Golden-`.nav`-Fixtures für Grundform + Continuation fehlen (§8.2).** Nur der Choice-Fall (§3.1)
   liegt konkret vor → das ist genau der Inhalt von **S0**.

## Verifikation (Wiederholrezept)

- `. .\Tools\Commands\Import-NavCommands.ps1` einmalig; dann `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`.
- Codegen: neue V2-Golden-Snapshots unter **`Regression/Tests/V2/`** via `nav snapshot`; die
  concat-Branch-`.expected.cs` sind **nicht** Referenz (Design §2.2).
- **Dispatcher-Invariante:** V1-Units bleiben byte-/verhaltensidentisch (bestehende Regression
  unverändert grün); V2 greift nur für die neuen Fälle.
- Fallstrick: `nav test` **baut nicht** — bei Engine-Änderungen erst `nav build`.
