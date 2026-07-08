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
| **S4** | **Versions-Gate + Completion + Nav0124:** `NavLanguageFeature` (`Continuation`/`ChoiceParameters`, `RequiredVersion = Version2`) → **Nav5000**; **versionsbewusste Completion** (§4.1: `VisibleEdgeKeywordItems`/Choice-`[params]` hinter `NavLanguageFeatures.IsAvailable`); **Nav0124** (generische Member-Kollision, **versions-gated** — daher hierher gezogen) | §6.3 (Teil C), §4, §4.1 | `--^`/`o-^`/`[params]` in `#version 1` **nicht**, ab `#version 2` **doch** vorgeschlagen; Nav5000- + Nav0124-Fixtures | **S4a erledigt** (Gate + Nav5000 + Completion), **S4b offen** (Nav0124) — s.u. |
| **S5** | **`CodeGen/V2/`-Gerüst:** CallContext-Grundform (Voll-Fabrik + opaker `Result`, Maschinerie = `Unwrap()`-Aufruf), **alle** Transitionen — **ohne** Continuation/Choice; über Dispatcher geschaltet | §6.4 | Golden gegen Grundform; **V1 byte-identisch** | **erledigt** — s.u. |
| **S6** | **V2 Continuation:** `Show`/`Continuation` mit inline `.Concat(…)`, **`o-^` UND `--^`** (Builder wählt `OpenModalTask`/`GotoTask`); **`FrameworkStubs.cs`** um `.Concat`-Typfläche erweitern | §6.5 | Golden `o-^`+`--^` kompiliert gegen Stubs (kein Laufzeit-Test) | **erledigt** — `--^` **sofort ausgeliefert** (Gating-Entscheidung §8.1 zugunsten des Design-Defaults); s.u. |
| **S7** | **V2 Choices in C#:** Choice-Context + `Choice_XLogic` + Forward aus den Quellen (kein Dispatch), inkl. Choice→Choice, Union, Multi-Exit | §6.6 | Golden gegen 3-Quellen-Fall (§3.1) | **erledigt** — s.u. |
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
kollateralfrei (nur die Zieldiagnose feuert). **Diese drei Fixtures sind in S4a wieder entfallen** —
siehe die S4-Anmerkung unten (die Struktur-Analyzer schweigen jetzt unter `#version 1`, und `#version 2`
ist bis S5 nicht authorbar; ihre Fixtures kehren mit S5 zurück).

### S4-Anmerkung: Aufteilung in S4a/S4b + die „ein treffender Fehler"-Entscheidung

S4 ist für einen Commit zu groß (Gate + Nav5000 + versionsbewusste Completion + Nav0124 mit
Korpus-Argumentation). Aufgeteilt in **S4a** (Versions-Gate + Nav5000 + Completion — **erledigt**) und
**S4b** (Nav0124 generische Member-Kollision — **offen**). Die Reihenfolge ist zwingend: Nav0124 ist
versions-gated und braucht das `NavLanguageFeatures`-Gate, das erst S4a liefert.

**S4a umgesetzt:**

- **`NavLanguageVersion.Version2`** als *benannter* Bezugspunkt — aber bewusst **noch nicht** in
  `SupportedVersions`. Der V2-Codegenerator fehlt; bis er steht bleibt `#version 2` ein **Nav5001**
  (statt beim Codegen zu scheitern). Version2 wandert erst mit dem Generator (S5) in
  `SupportedVersionTable`. `IsAvailable`/`RequiredVersion` vergleichen nur numerisch — sie funktionieren
  für das Gate und die Completion, ohne dass Version2 „unterstützt" sein muss.
- **`NavLanguageFeature.Continuation`/`ChoiceParameters`** (`RequiredVersion = Version2`) — die ersten
  Einträge des zuvor leeren Enums; einzige Autorität für „welches Feature ab welcher Version".
- **Nav5000-Gate-Analyzer** (`Nav5000FeatureRequiresNavLanguageVersion`, Auto-Discovery): eine Meldung
  je Continuation-Kante (Anker = Fortsetzungs-Kantenmodus `o-^`/`--^`) und je Choice-`[params]`-Klausel,
  wenn die effektive `#version` das Feature nicht erreicht. Verankert `CodeGenerationUnit.LanguageVersion`.
- **„Ein treffender Fehler" (Team-Entscheidung, Roslyn-Stil, [[nav-parser-recovery-roslyn-style]]):**
  Feuert Nav5000, **schweigen** die Continuation-Struktur-Analyzer **Nav0120/0121/0122** (sie sind nun
  hinter `IsAvailable(Continuation, …)` gegatet). Ein `#version 1`-File mit `o-^` bekommt also **nur**
  Nav5000, nicht zusätzlich die Struktur-Diagnose. Konsequenz: unter `#version 1` sind die
  Struktur-Analyzer stumm, und `#version 2` ist bis S5 nicht sauber authorbar (→ Nav5001) — die drei
  S3-Struktur-Fixtures wurden daher **entfernt** und kehren mit S5 (dann `#version 2`) zurück. An ihre
  Stelle treten zwei Nav5000-Fixtures: `Nav5000ContinuationRequiresNavLanguageVersion.nav` (belegt
  zugleich die Unterdrückung — struktur-falsche Continuation unter v1 → **nur** Nav5000) und
  `Nav5000ChoiceParametersRequireNavLanguageVersion.nav`.
- **Versionsbewusste Completion** (§4.1): `AfterTarget` bietet `o-^`/`--^` erst ab `#version 2` an, die
  Choice-`[params]`-Klausel (Code-Block-Slot am `choice`-Knoten) ebenso — beide hinter derselben
  `NavLanguageFeatures.IsAvailable`-Autorität wie Nav5000. **Abweichung vom Design-Wortlaut §4.1:** die
  Continuation-Keywords liegen in `AfterTargetItems`, **nicht** in `VisibleEdgeKeywordItems` — eine
  Continuation leitet keine neue Transition ein (sie hängt hinter dem Zielknoten), sie sind daher — wie
  schon in `SyntaxFacts.ContinuationEdgeKeywords` — von den regulären Edge-Keywords getrennt. Zusätzlich
  nötig: `CodeBlockHostAt` kannte den `choice`-Knoten noch nicht (`CodeBlockHost.ChoiceNode`) — ergänzt.

Verifikation S4a: `nav build` + beide TFMs grün (**net10 1379/0, net472 1387/0** — je 3 explizite Skips);
zwei neue Diagnostics-Fixtures + drei neue Completion-Tests (`AfterTarget` v1/v2, Choice-`[params]`
v1/v2).

### S5-Anmerkung: was das `CodeGen/V2/`-Gerüst umfasst

Umgesetzt (CallContext-Grundform für **alle** Transitionen, **ohne** Continuation/Choice):

- **Version freigeschaltet:** `NavLanguageVersion.Version2` ist jetzt in `SupportedVersionTable.All` —
  `#version 2` übersetzt und wirft kein `Nav5001` mehr. `NavCodeGenFacts.For(Version2)` liefert eine
  eigene `CodeGenFactsV2`-Instanz, deren **Namensalgebra bewusst V1-identisch** ist (die aus
  Knotennamen abgeleiteten Member `Begin{Node}`/`After{Node}` und die `WFS`/`WFSBase`/`WFL`-Suffixe
  müssen die V1-Schreibweise behalten, damit die invarianten `IBegin{Task}WFS`-Schnittstellen
  cross-version konsumierbar bleiben, §5). Die bestehenden Versions-Tests bleiben grün, weil sie
  `#version 99`/`0` als „unbekannt" nutzen und die Completion die Werte-Liste aus
  `SupportedVersions` **ableitet** (self-referentiell).
- **Dispatcher:** `VersionDispatchingCodeGenerator` erzeugt für Version2 einen neuen
  **`CodeGeneratorV2`**. Der teilt sich die **invarianten** Interface-Familien `I{Task}WFS` und
  `IBegin{Task}WFS` (Emitter + CodeModel unverändert aus der V1-Schicht); neu sind nur die
  Maschinerie-Basisklasse (`WfsBaseEmitterV2`) und die OneShot-Datei (`WfsOneShotEmitterV2`).
- **CallContext-Gestalt (§3.2/§3.3):** Jede Transition (Init/Trigger/Exit) kollabiert auf
  `…Logic(args, new {Context}(this)).Unwrap()` und trägt einen geschachtelten
  `{Context}CallContext` mit opakem `Result` (`readonly struct`, `internal` ctor, `internal Unwrap()`,
  deferred `Func<…>`-Thunk). Rückgabetyp des `Unwrap()`: `IINIT_TASK` bei Init-Transitionen, sonst
  `INavCommand`. Callables je Kanten-Art: `Show{Node}` (Gui, mode-frei), `Begin{Node}`
  (`GotoTask`/`OpenModalTask<T>`/`StartNonModalTask` je Edge-Mode, je Init eine Überladung),
  `Exit({result})` (fixes Member, `InternalTaskResult`), `End()`, und immer `Cancel()`. Der
  V1-`switch(body)`, die Begin-Wrapper-Hilfsmethoden und der `TaskResult`-Helfer haben **kein**
  Gegenstück mehr. Die `{Task}WFS`-Partial-Klasse (Felder/Konstruktoren) bleibt V1-deckungsgleich.
- **Modell/Emitter:** eigenes `CodeGen/V2/` (`CallContextCodeModel` + `CallableMethodModel`,
  `TransitionCallContextCodeModel`, `CodeModelBuilderV2`, `WfsBaseCodeModelV2`, `WfsCodeModelV2`;
  Emitter `WfsBaseEmitterV2`/`WfsOneShotEmitterV2`). Version-neutrale Bausteine geteilt: der
  `CodeGeneratorContext` ist von `CodeGeneratorV1` **entkoppelt** (nimmt jetzt `GenerationOptions`
  statt des Generators) und nach `CodeGen/Shared/` gewandert; Reachability, Parameter-/Task-Begin-
  Analyse und die `EmitterCommon`-Bausteine kommen unverändert aus der geteilten bzw. V1-Schicht.
- **Golden-Fixture:** `BasicFlow.nav` generiert jetzt (Grundform-Golden unter `Regression\Tests\V2\`,
  vier `.expected.cs`). Der Harness-Filter `IsPendingVersion2Corpus` überspringt nur noch
  **`ContinuationFlow`/`ChoiceFlow`** (name-basiert) — sie brauchen den V2-Continuation-/Choice-Codegen
  (S6/S7). **Fallstrick:** die generierten V2-`.cs` liegen unter `Regression\Tests\V2\{WFL,IWFL}\` und
  mussten in `Nav.Language.Tests.csproj` explizit aus dem Compile genommen werden (`NoCompile` deckte
  nur den V1-`Tests\{WFL,IWFL}\`-Zweig ab) — sonst bricht der net472-Build (die Framework-`[using]`s
  der `.nav` sind Stubs, kein echtes Framework). `dotnet build` verdeckte das zunächst, weil dort erst
  **nach** dem Build generiert wurde.

Verifikation S5: `nav build` + beide TFMs grün (**net10 1383/0, net472 1391/0** — je 3 explizite
Skips); vier neue BasicFlow-`.expected.cs` (`nav snapshot`); V1-Regression byte-identisch.

### S6-Anmerkung: was der V2-Continuation-Codegen umfasst

Umgesetzt (Continuation `o-^` **und** `--^`, aufbauend auf dem S5-Gerüst; V1 und die V2-Grundform
byte-identisch):

- **Continuation-Callable (`CallContextCodeModel`):** GUI-Kanten werden jetzt **pro Ziel-Knoten
  gebündelt** (`GroupBy` über den View-Namen, §3.4-Union). Trägt keine Kante der Gruppe eine
  Continuation, bleibt es die schlichte `Show{Node}(ViewTO) => Result`-Grundform (S5, unverändert).
  Trägt **mindestens eine** Kante eine Continuation (`Call.ContinuationCall != null`), liefert
  `Show{Node}` statt `Result` einen geschachtelten **`Show{Node}Continuation`**-Typ mit je einer
  `Begin{Task}(…)`-Fortsetzung. Neue Modelltypen: abstrakte Basis `CallableModel`, konkret
  `CallableMethodModel` (schlicht, wie bisher) und `ShowContinuationCallableModel` (Continuation).
- **`.Concat`-Lowering:** `Begin{Task}(…)` baut deferred
  `_wfs.{GuiEngine}(_to).Concat({Boundary})` — `GuiEngine` ist der **Trägerkanten**-Modus (im Korpus
  `GotoGUI`), `Boundary` der Ziel-Task-Aufruf je **Continuations**-Modus: `o-^` → `OpenModalTask<T>`,
  `--^` → `GotoTask<T>` (dieselbe `TaskEngineMethod`-Weiche wie Plain-Task-Kanten). Der Boundary-
  Ausdruck ist über den neuen Helfer **`BuildTaskBegins`** mit der Plain-`Begin{Node}`-Fabrik geteilt
  (wortgleich, da beide Kontexte ein `_wfs`-Feld tragen).
- **Impliziter `Result`-Operator nur bei plain-Schwesterkante (§3.6):** existiert zusätzlich eine
  plain-Kante zur selben View, emittiert der Continuation-Typ `static implicit operator Result(…)`
  (Felder über den Operanden `v` referenziert); **fehlt** die plain-Schwester (erzwungene
  Continuation), fehlt der Operator → `return ctx.Show{Node}(to);` ist ein Compile-Fehler, der Autor
  **muss** `.Begin{Task}(…)` anhängen. Das `ContinuationFlow`-Golden ist bewusst continuation-only
  (kein impliziter Operator); die Union plain+Continuation zeigt der Choice-Golden (S7).
- **`FrameworkStubs.cs` erweitert (§3.8/①/②):** Tagging-Interfaces `ITASK_BOUNDARY`/
  `INOT_A_TASK_BOUNDARY`; `GOTO_TASK`/`START_MODAL_TASK` sind nun `ITASK_BOUNDARY`; `GOTO_GUI.Concat`
  in zwei Überladungen (`ITASK_BOUNDARY` → `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY`,
  `INOT_A_TASK_BOUNDARY` → `TWO_STEP_IINIT_TASK`, beide `IINIT_TASK`); `Cancel()`/`EndNonModal()`/
  `StartNonModalTask` + `END`; **`InternalTaskResult<T>` liefert jetzt `TASK_RESULT<T>`** (vereint
  `INavCommandBody` für V1 **und** `IINIT_TASK, ITASK_BOUNDARY` für V2 → castfrei, §3.8/②) — die
  vorher fehlende V2-Kommandofläche, die S5 nie compile-getestet hatte.
- **Fixtures:** `ContinuationFlow.nav` un-skippt (nur noch `ChoiceFlow` in `PendingVersion2Fixtures`),
  vier neue `.expected.cs`. **Neuer Compile-Test** (`CodeGenTests.CompileTest`, jetzt
  versionsbewusst — V2-Units über `CodeGeneratorV2`): ein self-contained `#version 2`-Nav mit `o-^`
  **und** `--^` kompiliert gegen die erweiterten Stubs (Roslyn in-memory, **kein** Laufzeit-Test).

**Fallstrick (bestätigt):** `nav snapshot` **generiert nicht** — es kopiert nur vorhandene `.cs` →
`.expected.cs`. Für ein neu un-skipptes Fixture erst die Regression-Generierung anstoßen
(`dotnet test … --filter RegressionTests` bzw. der Explicit-Test `RegressionTests.GenerateFiles`),
**dann** `nav snapshot`.

Verifikation S6: `nav build` + beide TFMs grün (**net10 1388/0, net472 1396/0** — 3 Explicit-Skips);
V1- **und** V2-Grundform-Regression byte-identisch.

### S7-Anmerkung: was der V2-Choice-Codegen umfasst

Umgesetzt (Choices als eigene C#-Bausteine — Context + `{Choice}Logic` + Forward statt Platt-Falten;
V1 und die V2-Grundform/-Continuation byte-identisch):

- **Nicht-transitive Reachability `EdgeExtensions.GetDirectCalls`:** die neue, semantik-nahe Grundlage.
  Anders als `GetReachableCalls` (das Choices transitiv **plattfaltet**, §2.1) behandelt sie ein
  Choice-Ziel als **terminalen `Call`** (`Call.Node` = `IChoiceNodeSymbol`), statt in dessen Ausgänge
  abzusteigen. Für **choice-freie** Quellen ist das Ergebnis deckungsgleich mit `GetReachableCalls`
  (je Kante genau ein Call, gleiche Reihenfolge, gleiches Dedup via `CallComparer.Default`) — daher
  bleiben BasicFlow (S5) und ContinuationFlow (S6) **byte-identisch**. Die drei
  `TransitionCallContextCodeModel`-Fabriken (Init/Exit/Trigger) speisen jetzt `GetDirectCalls` statt
  `GetReachableCalls`.
- **Choice-Forward (`CallContextCodeModel`):** zeigt eine Quelle direkt auf eine Choice, entsteht ein
  `{Choice}({params}) => new(() => _wfs.{Choice}Logic({args}, new(_wfs)).Unwrap())`-Callable (§3.5) —
  kein Dispatch, kein Marker. Die Choice-Parameter kommen aus
  `IChoiceNodeSymbol.Syntax.CodeParamsDeclaration`; den Choice-Context konstruiert das target-getypte
  `new(_wfs)`. **Choice→Choice** ist dieselbe Mechanik eine Ebene tiefer (der Choice-Context bekommt
  seinerseits einen `{Choice}(…)`-Forward) — die Kette wird **nicht** entfaltet (Anti-Bloat bleibt
  transitiv, jede `{Choice}Logic` existiert genau einmal).
- **Choice als eigener Baustein (`ChoiceCallContextCodeModel`):** je erreichbarer Choice die abstrakte
  `{Choice}Logic(params, {Choice}CallContext)` + ihr `CallContextCodeModel` (aus den **direkten**
  Choice-Ausgängen). **Keine** öffentliche Maschinerie-Methode (kein `Begin`/`On`/`After`) — eine
  Choice wird nur über die Forwards ihrer Quellen erreicht. Der `Result.Unwrap()`-Command-Typ folgt der
  **Init-Erreichbarkeit** (§3.8/④): `IINIT_TASK`, sobald die Choice von **irgendeinem** Init aus
  (transitiv über Choices) erreicht wird, sonst `INavCommand`.
- **Erreichbarkeits-Auswahl (`CodeModelBuilderV2.GetChoices`):** BFS von den Quellen-Wurzelkanten
  (Init-Ausgänge + erreichbare, nicht-`[notimplemented]` Task-Ausgänge + Trigger) über Choice-Ketten;
  nur der eine Schritt „Kante → Choice-Ziel" wird verfolgt (Views/Tasks/Exits sind terminal und tragen
  die Erreichbarkeit **nicht** weiter — genau die Init-Legalitäts-Semantik). Eine zweite, engere BFS
  nur ab den **Init**-Wurzeln liefert die Init-Erreichbarkeit je Choice. Unerreichbare Choices erzeugen
  — wie bei V1 — **keinen** Code. Ausgabereihenfolge = Deklarationsreihenfolge.
- **Union & Continuation aus der Choice heraus:** unverändert aus S6 wiederverwendet — `Choice_Retry`
  hat zur View `Home` eine plain- **und** eine `o-^ Msg`-Kante → **eine** `ShowHome`-Methode mit
  implizitem `Result`-Operator (plain) **und** `BeginMsg(…)` (Continuation, baut
  `GotoGUI(to).Concat(OpenModalTask<MsgResult>(…))`). **Multi-Exit:** `Choice_Escalate --> Done`/`--> Esc`
  kollabieren via `CallComparer.FoldExits` auf **eine** `Exit(bool par)`-Fabrik.
- **Golden un-skippt + Sonderfall-Filter entfernt:** `ChoiceFlow` war das letzte pending V2-Fixture —
  `PendingVersion2Fixtures`/`IsPendingVersion2Corpus` sind aus `RegressionTests` **ganz entfernt**
  (Auto-Discovery ohne Sonderfall, §7). Vier neue `ChoiceFlow`-`.expected.cs`. **Neuer Compile-Test**
  (`CodeGenTests.CompileTest`): ein self-contained `#version 2`-Nav mit 3 Quellen an eine Choice,
  Union, Choice→Choice und Multi-Exit kompiliert gegen die Stubs (Roslyn in-memory, **kein**
  Laufzeit-Test).

Verifikation S7: `nav build` + beide TFMs grün (**net10 1393/0, net472 1401/0** — 3 Explicit-Skips);
V1-, V2-Grundform- **und** V2-Continuation-Regression byte-identisch.

Danach folgt — außerhalb dieses Dokuments, in `nav-codegen-versioning.md` als **Step 7** verankert —
die **V2-Navigation end-to-end** (GoTo Nav↔C#, Rename, FindReferences, Cross-Version-`taskref`),
ggf. mit der versionierten Such-Strategie-Schnittstelle.

## Offene Gating-Entscheidungen (§8 des Design-Docs)

1. **`--^`-Laufzeitverifikation (§8.1) — entschieden: sofort ausgeliefert.** Der exakte
   `GotoGUI(view).Concat(GotoTask(…))`-TWO_STEP-Pfad ist am Framework un-exerziert (Framework-Autor-
   TODO im `TWO_STEP…`-Ctor); der Laufzeit-Smoke-Test läge im **Framework-Repo**, nicht hier. **In S6
   entschieden**, `--^` dem Design-Default folgend **sofort** auszuliefern (nicht zu gaten): das
   Nav-Repo verifiziert ohnehin nur bis Codegen (Compile-gegen-Stubs deckt `o-^` **und** `--^` ab),
   die Laufzeit-Korrektheit ist quellcode-verifiziert (§3.8/⑥) und Framework-Domäne. Ein späteres
   Gaten bliebe eine separate Entscheidung, falls der Framework-Smoke-Test etwas aufdeckt.
2. **Golden-`.nav`-Fixtures (§8.2) — vollständig erledigt.** Grundform (`BasicFlow`, S5), Continuation
   (`ContinuationFlow`, S6) **und** Choice-mit-3-Quellen (`ChoiceFlow`, S7) liegen als Golden vor. Der
   Backbone der drei gestaffelten Goldens (§7) steht; offen ist nur noch S8 (die isolierten
   Sonderform-Fixtures `[notimplemented]`/`[donotinject]`).

## Verifikation (Wiederholrezept)

- `. .\Tools\Commands\Import-NavCommands.ps1` einmalig; dann `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`.
- Codegen: neue V2-Golden-Snapshots unter **`Regression/Tests/V2/`** via `nav snapshot`; die
  concat-Branch-`.expected.cs` sind **nicht** Referenz (Design §2.2).
- **Dispatcher-Invariante:** V1-Units bleiben byte-/verhaltensidentisch (bestehende Regression
  unverändert grün); V2 greift nur für die neuen Fälle.
- Fallstrick: `nav test` **baut nicht** — bei Engine-Änderungen erst `nav build`.
