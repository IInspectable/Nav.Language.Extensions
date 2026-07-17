# Nav `cancel`-Keyword — Status & Umsetzungsplan

## Ziel

Ein neues Nav-Schlüsselwort **`cancel`** als **rechtsseitiges Kantenziel** (kein deklarierter Knoten),
mit dem der „Abbrechen/Nichts-tun"-Ausgang einer Transition **im Modell sichtbar und deklariert** wird.
Am Ende gilt: In **V2** wird die generierte `Cancel()`-Aufruffläche eines Call-Contexts **nur noch dann**
emittiert, wenn die zugehörige Quelle (Choice-Arm oder direkte Init-/Trigger-Kante) einen `cancel`-Ausgang
deklariert. **V1 bleibt unverändert** (unbedingtes Cancel wie bisher). So kann in V2 Deklaration und
Implementierung nicht mehr auseinanderlaufen — Voraussetzung für deterministische V1→V2-Migration.

## Entscheidungen (mit Begründung)

### E1 — Gating ist **V2-only**; V1 bleibt unangetastet
Der unbedingte Cancel-Default entfällt **nur in V2**. **Warum:** Der Produktivstand ist zu 100 % V1
(1908 `.nav`, **0** mit `#version 2`) und enthält **97** Handaufrufe `return Cancel()`. Ein Gating in V1
würde jeden dieser 97 Aufrufe in den generierten `default:`-Zweig fallen lassen → **`InvalidOperationException`
zur Laufzeit** in Produktivworkflows. V2 ist Grünfläche (kein Produktiv-Bestand) → dort ist Opt-in ohne
Bruch möglich. **Verworfen:** (a) choice-only-Gating in V1 (Flächenbruch), (b) unbedingtes Beibehalten in
V2 (die Drift, die V2-Migration blockiert, bliebe bestehen).

### E2 — Der eigentliche Grund ist **Migrations-Determinismus**, nicht Code-Hygiene
Die 97 V1-Aufrufe sind nicht 97× Bestätigung des Designs, sondern 97× **Symptom der Drift**: Die `.nav`
schweigt zum Cancel, die `.cs` ruft ihn. Ein Migrations-Tool, das nur die `.nav` liest, kann diesen
Kontrollfluss nicht kennen. `cancel` deklarationspflichtig zu machen (in V2) macht die `.nav` zur
**vollständigen** Kontrollfluss-Beschreibung. Das ist der Wert des Features.

### E3 — Enforcement ist ein **Compile-Fehler**, kein Analyzer und kein Laufzeit-Throw
In V2 gibt die Logik-Gegenstelle den **opaken `{Context}.Result`-Typ** zurück. `Result` kann **nur** von
den Callables des Contexts erzeugt werden. Die geerbte Framework-Methode `Cancel()` liefert dagegen den
Typ `CANCEL` — **kein** `{Context}.Result`. Folge: Wird `Cancel()` in einer V2-Logik aufgerufen, ohne dass
der Context die Cancel-Callable emittiert (weil nicht deklariert), ist das ein **Typfehler zur
Compile-Zeit**. Deklaration und Implementierung *können* nicht mehr auseinanderlaufen — ohne zusätzlichen
Analyzer, ohne neue Laufzeit-Diagnose. **Verworfen:** separater Analyzer/Nav-Diagnose für „Cancel ohne
Deklaration" — das Typsystem erledigt es umsonst. (In V1 wäre dieselbe Umstellung ein Laufzeit-Throw,
siehe E1 — weiterer Grund für V2-only.)

### E4 — `cancel` ist **RHS-Ziel ohne Deklaration** (anders als `end`)
`end` wird deklariert (`end;` → `EndNodeDeclarationSyntax`/`IEndNodeSymbol`) **und** als Ziel referenziert
(`EndTargetNodeSyntax`). `cancel` bekommt **nur** die Ziel-Seite, **keine** Deklaration. **Warum:** Cancel
hat keinen Namen, keine Parameter, kein Ergebnis, keine Identität — es gibt nichts zu deklarieren; ein
Pflicht-`cancel;` wäre reines Boilerplate. Begrifflich ist Cancel die *Abwesenheit* von Navigation
(`CANCEL.Execute` gibt `contextNode` zurück, bleibt stehen), kein Ort, zu dem man navigiert.
**Verworfen:** cancel als deklarierter Singleton-Knoten wie `end`.

### E5 — `cancel` erlaubt an Choice-Armen **und** direkten Init-/Trigger-Kanten (nicht choice-only)
Der **bedingte** Cancel gehört an einen Choice-Arm (`Choice --> cancel if "…";`). Der **unbedingte
Swallow** (ein Trigger, der bewusst immer nichts tut) darf eine **direkte** Kante nutzen
(`View --> cancel on OnEscape;`), statt in eine künstliche einarmige Choice gezwungen zu werden. Beide
Formen sind Deklarationen → beide deterministisch. **Verworfen:** choice-only (erzwingt die absurde
einarmige Choice für den Swallow-Fall).

### E6 — Bewiesen: eine Choice deckt **jeden bedingten** Cancel ab
Es gibt kein Gegenbeispiel „Cancel aus einer Stelle, die keine Nav-Kante ist". Alle Cancel-Stellen stehen
in `{X}Logic`-Methoden, und **jede** davon gehört zu einer `<NavTrigger>`- oder Init-Kante (verifiziert an
`ExecuteOnInit`, `AutomaticAction`, `WeiterOderFertigClick`, `OnEscape` — alle `<NavTrigger>`, kein
Framework-Lifecycle-Hook). Jede Trigger-/Init-Kante darf auf eine Choice zeigen; ein
`if (cond) return Cancel(); else …` **ist** eine zweiarmige Choice. Damit ist choice-basiertes Cancel für
den bedingten Fall vollständig; nur der unbedingte Swallow braucht E5.

## Verifizierte Fakten (Codebase, `Pfad:Zeile`)

**Produktiv-Zahlen (`d:\tfs\Main`):** 1908 `.nav` in `XTplusApplication`, **0** mit `#version 2`. **97**
`return Cancel();` in nicht-generierter Handlogik, **0** davon in `*ChoiceLogic`/mit „Choice" im Namen;
33+ in Triggern (`On…/After…/Grid…/Request…/Cell…`), `BeginLogic` **15×** (Init-Transition).

**V2-Gating-Stellen (Engine, dieses Repo):**
- `Nav.Language\CodeGen\V2\CodeModel\CallContextCodeModel.cs:122-123` — der **unbedingte**
  `entries.Add(… "Cancel" … "$"{WfsFieldName}.Cancel()"")`. Genau hier muss das Gating greifen.
- `CallContextCodeModel.cs:102-120` — die `foreach (var call in distinct)`-Schleife über die
  ausgehenden Calls der Quelle (`directCalls`); Cancel müsste als einer dieser Calls erkennbar werden.
- `CallContextCodeModel.Build(...)` bekommt `directCalls: choiceNode.Outgoings.GetDirectCalls()` bzw.
  die Transition-Calls (`ChoiceCallContextCodeModel.cs:74-79`).

**V2 gibt `{Context}.Result` zurück (Basis für E3):**
- `Nav.Language\CodeGen\V2\Emitters\WfsBaseEmitterV2.cs:187` — `protected abstract
  {Context}.Result {LogicName}(…)` (Transition), `:210` (Choice).
- `WfsBaseEmitterV2.cs:254/299` — Callables als `public Result … => new(() => {Thunk});`.
- `WfsBaseEmitterV2.cs:320` — `public readonly struct Result` (nur Context-intern erzeugbar).

**V1 bleibt unangetastet:**
- `Nav.Language\CodeGen\V1\CodeModel\TransitionCodeModel.cs:44-46` — `.Concat(new[] {new CanceCallCodeModel()})`.
- `Nav.Language\CodeGen\V1\CodeModel\CallCodeModel.cs:42-51` — `CanceCallCodeModel` (SortOrder = Int32.MaxValue).

**Framework-Semantik (`d:\tfs\Main`, nur Kontext):**
- `framework\src\Framework.NavigationEngine\WFL\BaseWFService.cs:254` — `Cancel()` → `new CANCEL()`.
- `…\IWFL\NavCommands\CANCEL.cs` — „Just do nothing", `context.Cancel()`.
- `…\IWFL\ClientNavCommands\CANCEL.cs:15` — `Execute` gibt `contextNode` zurück (kein Re-Render).
- `…\IWFL\ClientNavCommands\GOTO_GUI.cs:17` — `ReplaceTO` + `OnTOChanged` (Gegenbeispiel: TO-Zurückgeben
  = echter Refresh, **nicht** dasselbe wie Cancel).

**`end`-Blaupause für die Grammatik/Symbolseite:**
- Keyword: `Nav.Language\Syntax\SyntaxFacts.cs:33` (`EndKeyword = "end"`), Keyword-Menge `NavKeywords` ab :105.
- Deklaration: `NavParser.cs:808` `ParseEndNodeDeclaration` (`"end" ";"`) → `EndNodeDeclarationSyntax`.
- **Ziel** (relevant für cancel): `NavParser.cs:1150` `ParseTargetNode`, `:1160` `ParseEndTargetNode`
  (`endTargetNode ::= "end"`) → `EndTargetNodeSyntax`; `:1187` `StartsTargetNode`.
- Symbol: `Nav.Language\SemanticModel\INodeSymbol.cs:133` `IEndNodeSymbol: ITargetNodeSymbol`;
  `:62` `IsConnectionPoint` (init/exit/end).
- Analyzer: `Nav0106EndNode0MustOnlyReachedByGoTo`, `Nav0108EndNodeHasNoIncomingEdges`,
  `Nav0118EndNode0NotAllowedBecauseReachableFromInit1` (Muster für cancel-Diagnosen).

**Freie Diagnose-Nummern:** `Nav5002`, ab `Nav0127` (belegt: …0122, 0124, **0125+0126 = cancel, S3**,
5000, 5001; `Nav0123` bleibt Grabstein — toter V2-Zwischenstand, nie wiederverwenden).

**Versionsgate-Autorität:** `NavLanguageVersion` (Version1/SupportedVersions/IsSupported) + `Nav5000` —
siehe `doc/nav-pragmas-versioning-status.md`. `cancel` ist ein V2-Feature → Gate wie andere V2-Konstrukte.

## Entschiedene Design-Frage (vor S2, jetzt festgelegt): **Referenz-Marker**, kein Knoten-Symbol

Wie fließt der Cancel-Ausgang ins Modell? **Entschieden: Referenz-Marker** (`ICancelNodeReferenceSymbol`),
**kein** synthetisches Knoten-Symbol.

**Warum nicht das (ursprünglich empfohlene) synthetische `ICancelNodeSymbol`:** Es kollidiert frontal mit
dem Kontrakt **`INodeSymbol.Syntax` = `NodeDeclarationSyntax` (non-null)** — jedes abgeleitete
Knoten-Interface verengt `Syntax` sogar auf seine Deklarations-Syntax (`IEndNodeSymbol.Syntax` →
`EndNodeDeclarationSyntax`). `cancel` hat per E4 **keine** Deklaration → es gäbe keinen `NodeDeclarationSyntax`,
den ein Cancel-Knoten zurückgeben könnte. Ein Knoten-Symbol ginge nur, wenn man `INodeSymbol.Syntax`
**nullable** macht (Kern-Kontrakt aufweichen, Visitor neu, alle generischen `.Syntax`-Konsumenten prüfen).

**Der gewählte Marker** respektiert den non-null-Kontrakt vollständig und greift minimal ein:
- `cancel` erscheint **nie** als `Call` (kein Knoten → `GetDirectCalls` überspringt es, `targetNode` ist null).
- Das Gating (S4) fragt die Kanten der Quelle ab: **`edge.TargetsCancel()`** bzw.
  `edge.TargetReference is ICancelNodeReferenceSymbol` — für direkte Kanten `source.Outgoings.Any(e => e.TargetsCancel())`,
  für Choice-Arme `choiceNode.Outgoings.Any(e => e.TargetsCancel())`.
- Von der `end`-Symbolmaschinerie (References/Reachability/`SymbolsAndSelf`/`NodeDeclarations`) wird **nichts**
  gebraucht: Cancel-Knoten existieren nicht, also tauchen sie in keinem dieser Pfade auf.

## Plan (Steps)

- **S1 — Grammatik/Syntax. ✅ ERLEDIGT** (Commit `8734a564`). `CancelKeyword`-Token in `SyntaxFacts.cs` +
  `SyntaxTokenType` (= 58); `CancelTargetNodeSyntax` + `ParseCancelTargetNode` (analog
  `EndTargetNodeSyntax`); `ParseTargetNode` (jetzt Switch) und `StartsTargetNode` um das
  cancel-Keyword erweitert; Lexer-Keyword-Tabelle + Klassifikation via `Tok(…, Keyword)`;
  `Rule.CancelTargetNode` + Snippet-Einstieg `Syntax.ParseCancelTargetNode`. **Berührt:** `Syntax\`
  (Facts, Parser, Lexer, `TargetNodeSyntax.cs`, `Syntax.cs`). **DoD erfüllt:** `X --> cancel …` parst,
  `cancel` wird als Keyword klassifiziert; kein `cancel;` als Deklaration (E4).
  - **Entwurfsentscheidung (S1a):** `cancel` ist ein **hartes Wort-Keyword** (wie `end`), Aufnahme in
    `NavKeywords` — *nicht* eine eigene Kategorie wie die Continuation-*Operatoren* (`--^`/`o-^`). Ein
    Wort-Token, das der Lexer als Keyword ausgibt, muss reserviert sein (`IsKeyword`=true,
    `IsValidIdentifier`=false); alles andere wäre inkonsistent. `end` ist der grammatische Zwilling.
    Versionsunabhängig gelext (wie die Continuation-Kanten); die V2-only-Wirksamkeit ist S3.
  - **V1-Bruch empirisch ausgeschlossen:** Im Produktivkorpus (`d:\tfs\Main`, 1908 `.nav`) kommt
    kleingeschriebenes `cancel` **ausschließlich** in Kommentaren vor (22 Treffer, alle `// Ok/cancel`,
    Trivia) — kein einziger `cancel`-Identifier-Token. Die Reservierung bricht V1 also nicht.
  - **Loose End für S5 (Completion):** Weil `cancel ∈ NavKeywords`, bietet
    `NavCompletionService.FallbackItems` es in mehrdeutigen Positionen **auch in V1** an. Das
    V2-Gating der Completion (S5) muss diesen Fallback-Pfad mit abdecken, nicht nur die dedizierte
    Ziel-Vorschlagsstelle (bei `end`, `NavCompletionService.cs:271`).
  - **Tests:** `SyntaxFactsTest` (ExpectedKeywords + `CancelKeywordTest`), `SyntaxTreeAllRulesTests` +
    `SyntaxWalkerTests` (V2-Snippet `V --> cancel;`, Knoten-Zähler 52→53), neu
    `Syntax\CancelSyntaxTests.cs`. net472 1925/0, net10 1865/0 + MCP 115/0.
- **S2 — Semantic Model. ✅ ERLEDIGT.** Zielauflösung von `cancel` als **Referenz-Marker** (siehe
  entschiedene Design-Frage oben). **Umgesetzt:**
  - `ICancelNodeReferenceSymbol: INodeReferenceSymbol` (Marker ohne Typ-Parameter, `Declaration` stets
    null) — `SemanticModel\INodeReferenceSymbol.cs`; Impl `CancelNodeReferenceSymbol` (sealed partial,
    Deklaration fest null) — `SemanticModel\NodeReferenceSymbol.cs`.
  - `TaskDefinitionSymbolBuilder.CreateNodeReference`: `CancelTargetNodeSyntax` wird **vor** der
    Namensauflösung zur `CancelNodeReferenceSymbol` (kein `TryFindSymbol` — „cancel" steht in keiner
    Knotentabelle).
  - `EdgeExtensions.TargetsCancel(this IEdge)` — die Abfrage des Cancel-Ausgangs (Grundlage für S3/S4).
  - `Nav0011CannotResolveNode0` nimmt `ICancelNodeReferenceSymbol` aus (`{ Declaration: null } and not
    ICancelNodeReferenceSymbol`) — die fehlende Deklaration ist bei cancel gewollt (E4).
  - **DoD erfüllt:** Für eine Quelle mit `--> cancel` liefert `edge.TargetsCancel()`/
    `edge.TargetReference is ICancelNodeReferenceSymbol` den Cancel-Ausgang; kein Nav0011.
  - **Tests:** `Nav.Language.Tests\CancelSemanticTests.cs` (Trigger-Kante + Choice-Arm: Referenztyp,
    `Declaration==null`, `TargetsCancel`, `Edge`-Rückverweis; kein Nav0011; **kein** Call; Kontrolle:
    echter unauflösbarer Name feuert weiterhin Nav0011). net472 1930/0 (+3 explicit-skip) + MCP 115/0;
    net10 1869/0 (Cache-Test `SecondScan_ReturnsSameUnitInstances` flaky unter Volllast, isoliert grün).
- **S3 — Analyzer/Diagnosen. ✅ ERLEDIGT.** Drei Prüfungen umgesetzt:
  - **Versionsgate (Nav5000, bestehende ID):** neues `NavLanguageFeature.Cancel` → `Version2`
    (`NavLanguageFeature.cs`); `Nav5000FeatureRequiresNavLanguageVersion` gated jede Kante mit
    `edge.TargetsCancel()`, verankert am `cancel`-Keyword (`edge.TargetReference.Location`).
  - **Nav0125 „Cancel nur per Goto `-->`" (neu):** analog `Nav0106` (End) — `o->`/`==>` auf cancel
    ist ein Fehler, verankert am Kanten-Operator (`edge.EdgeMode.Location`); cancel hat keinen Namen,
    die Meldung nennt daher keinen (`Cancel can only be reached by a goto edge (-->)`).
  - **Nav0126 „Cancel nicht an Exit-Transition" (neu, Stellen-Restriktion E5):** cancel ist nur an
    Init-/Trigger-/Choice-Kanten zulässig, **nicht** als Ziel einer `IExitTransition`
    (`taskDefinition.ExitTransitions.Where(t => t.TargetsCancel())`), verankert am `cancel`-Keyword.
  - **Folgefehler-Unterdrückung:** Nav0125/0126 schweigen, wenn `cancel` unter der wirksamen `#version`
    gar nicht verfügbar ist (`NavLanguageFeatures.IsAvailable`) — dann ist Nav5000 die eine treffende
    Diagnose (Muster von Nav0120/0121/0122). Nav0104/0105/0106 feuern **nicht** auf cancel (deren
    Guards prüfen `TargetReference.Declaration`-Typen; cancel hat `Declaration == null`).
  - **Auto-Discovery:** beide Analyzer werden vom `Nav.Analyzer.SourceGenerator` automatisch
    eingesammelt (kein Registry-Eintrag nötig).
  - **DoD erfüllt:** Fehlnutzung (falsche Version, `o->`/`==>` auf cancel, cancel an Exit) wird als
    Nav-Diagnose gemeldet.
  - **Tests:** fünf Fixtures im Diagnostics-Harness (`Nav.Language.Tests\Diagnostics\Tests\`):
    `Nav5000CancelRequiresNavLanguageVersion` (V1 → Nav5000), `Nav0125…_OnModalEdge`/`_OnNonModalEdge`
    (V2 `o->`/`==>` → Nav0125), `Nav0126CancelNotAllowedAfterExitTransition` (V2 Exit → Nav0126),
    `CancelViaGotoWithoutErrors` (V2 Goto-Trigger → 0 Diagnosen). net472 1935/0 (+3 explicit-skip) +
    MCP 115/0; net10 grün (bekannter flaky Cache-Test `NavWorkspaceCoreSemanticCacheTests` unter
    Volllast, isoliert 5/5 grün). Build 0 Warnungen/0 Fehler. Die S2-Fixtures in `CancelSemanticTests.cs`
    (ohne `#version` → V1) tragen nun zusätzlich Nav5000; sie prüfen aber nur Nav0011-Abwesenheit +
    Modell-Struktur (versionsunabhängig) und bleiben grün.
- **S4 — V2-Codegen-Gating (Kern). ✅ ERLEDIGT** (uncommitted). Die Cancel-Callable wird in
  `CallContextCodeModel.Build` **nur** emittiert, wenn die Quelle einen Cancel-Ausgang trägt:
  - Neues **`bool declaresCancel`**-Argument an `CallContextCodeModel.Build`; das Cancel-`Entry` hängt
    hinter `if (declaresCancel)`. Ohne Deklaration fehlt die Callable → `return next.Cancel()` in der
    Logik ist Compile-Fehler (E3 — die geerbte Framework-`Cancel()` liefert `CANCEL`, nicht den opaken
    Context-`Result`).
  - Alle vier Aufrufer berechnen `declaresCancel` aus den Outgoings ihrer Quelle via
    `edge.TargetsCancel()` (S2): `TransitionCallContextCodeModel.FromInit`/`FromExit`
    (`…Outgoings.Any(e => e.TargetsCancel())`), `FromTrigger` (`triggerTransition.TargetsCancel()`,
    Einzelkante) und `ChoiceCallContextCodeModel.FromChoice` (`choiceNode.Outgoings.Any(…)`).
    `FromExit` ist mitgegatet, obwohl cancel dort per Nav0126 (E5) unzulässig ist — in gültigen
    Programmen ist der Wert dort immer `false`, das Gating bleibt uniform.
  - **V1 unberührt:** `CallContextCodeModel` ist rein V2; V1 (`TransitionCodeModel.cs:44`) emittiert das
    unbedingte Cancel weiter. Die 23 V1-Regression-Goldens blieben **byte-identisch**; nur die 5 V2-Goldens
    verloren ihre 30 `public Result Cancel()`-Zeilen (keine deklariert cancel).
  - **DoD erfüllt:** V2-Unit ohne `cancel` hat keine `Cancel()`-Methode; V2-Unit mit `--> cancel`
    (Trigger-Kante **oder** Choice-Arm) hat genau eine; V1-Regression byte-identisch.
  - **Tests:** `Nav.Language.Tests\CancelCodeGenTests.cs` (3 Fälle: kein cancel → 0 `Cancel()`; direkte
    Trigger-Kante → 1; Choice-Arm → 1, jeweils per Zählung im generierten `WFSBase`). V2-Goldens via
    `nav snapshot` neu (5 Dateien, −30 Zeilen). `BasicFlow.nav`-Kommentar (Cancel „immer verfügbar")
    auf das Gating aktualisiert. CodeAnalysis-Golden `InitCallSite_JumpsToAfterMethod.expected`
    nachgezogen (Span 71→70, weil im V2-`InitFlowWFSBase.generated.cs` die Cancel-Zeile des
    Init-Contexts entfällt). net472 1938/0 (+3 explicit-skip); net10 1877/1878 grün (der eine Fehler
    = bekannter Last-Flaky `NavWorkspaceCoreSemanticCacheTests.OutOfBandChange…`, isoliert 5/5 grün).
- **S5 — Completion. ✅ ERLEDIGT** (uncommitted). `cancel` als Zielvorschlag analog `end`, hinter dem
  Versionsgate:
  - `NavCompletionService.TargetItems` bekommt die effektive `NavLanguageVersion` und fügt
    `SyntaxFacts.CancelKeyword` **nur** an, wenn `NavLanguageFeatures.IsAvailable(NavLanguageFeature.Cancel,
    version)` — dieselbe Nav5000-Gate-Autorität wie Continuation/choice-`params`. `end` bleibt
    versionsunabhängig. Die Kanten-Modus-Restriktion (nur Goto, Nav0125) wird — wie bei `end` (Nav0106) —
    bewusst **nicht** zusätzlich gepruned (`TargetSlot` wird für alle regulären Edge-Modi erreicht;
    Parität mit `end`).
  - **Loose End S1a geschlossen:** `FallbackItems` bekommt ebenfalls die Version und filtert `cancel`
    über `IsNavKeywordAvailable` heraus, wenn das Feature nicht verfügbar ist — sonst böte der
    konservative Fallback `cancel` (∈ `NavKeywords`) auch in V1 an.
  - VS + LSP teilen `NavCompletionService` → beide Hosts ziehen automatisch nach (keine host-eigene
    `end`/`cancel`-Zielliste).
  - **DoD erfüllt:** Completion bietet `cancel` an der Zielposition **nur** ab `#version 2` an.
  - **Tests:** gepaarte `NavCompletionServiceTests` (`TargetSlot_UnderVersion1_DoesNotOfferCancelKeyword`
    / `…_UnderVersion2_AlsoOffersCancelKeyword`, Muster der `…OnChoiceNode_UnderVersion1/2`-Paare).
    Completion 89/89; net472 1940/0 (+3 explicit-skip); net10 1880/1880.
- **S6 — Golden-Fixtures + Doku. ✅ ERLEDIGT** (uncommitted). Golden-Fixture
  `Regression\Tests\V2\CancelFlow.nav` (`#version 2`, distinkter Task-Name **und** `[namespaceprefix]`,
  §7-Port-Caveat) deckt **beide** Formen in einem Fixture ab:
  - `Home --> cancel on OnEscape` (unbedingter Swallow, E5) → **nur** der `OnEscapeCallContext` bekommt
    `public Result Cancel() => new(() => _wfs.Cancel());`.
  - `Choice_Confirm --> cancel if "Abbruch"` (bedingter Cancel, E5) → **nur** der
    `Choice_ConfirmCallContext` bekommt `Cancel()`.
  - Kontrast im selben Golden: der `Init1CallContext` (`Init1 --> Home`) und der `OnDecideCallContext`
    (`Home --> Choice_Confirm on OnDecide`, zeigt auf die Choice, deklariert **selbst** kein cancel)
    tragen **kein** `Cancel()` — genau der V2-only-Unterschied zu V1 (dort trüge jeder Context den
    unbedingten Cancel-Default).
  - **Snapshots:** vier `.expected.cs` via Regression-Generierung (`RegressionTests.GenerateFiles`) +
    `nav snapshot`; in `Nav.Language.Tests.csproj` als `Content` eingehängt (die generierten `.cs` fallen
    unter die bestehenden `NoCompile`-Globs `Regression\Tests\V2\{WFL,IWFL}\**`).
  - **Doku fortgeschrieben:** `doc/nav-codegen-v2-concat-design.md` — die „immer `Cancel()`"-Zeile der
    §3.4-Tabelle auf `--> cancel` (deklariert) korrigiert + neuer Abschnitt **§3.4a** (Cancel ist in V2
    deklarationspflichtig); `doc/nav-codegen-v2-status.md` § „Offene Gating-Entscheidungen" Punkt 3 auf
    „vollständig umgesetzt (S1–S6)" gesetzt.
  - **DoD erfüllt:** Goldens grün, V1- **und** bestehende V2-Regression byte-identisch, Design-Doku aktuell.
  - **Verifikation:** `nav build` 0/0; `nav test` (net472) 1944/0 (+3 explicit-skip) + MCP 115/0; net10
    1883/1884 (der eine Fehler = bekannter Last-Flaky `NavWorkspaceCoreSemanticCacheTests.OutOfBandChange…`,
    isoliert 5/5 grün).

## Fallen (in dieser/früheren Sessions als teuer erkannt)

- **`nav test` baut nicht** — bei Engine-Änderungen **erst `nav build`** (MSBuild.exe wegen VS-Extension),
  dann `nav test`. In frischer Shell einmalig `. .\Tools\Commands\Import-NavCommands.ps1`.
- **Multi-Target:** `Nav.Language.Tests` läuft `net472;net10.0` — neue Tests auf **beiden** grün.
  `dotnet test -f net472` läuft ins Leere (0 Tests); net472 nur via `nav test`.
- **V1-Regression byte-identisch halten** (Dispatcher-Invariante): V2 greift nur für die neuen Fälle;
  bestehende V1-Snapshots dürfen sich **nicht** ändern. V2-Snapshots via `nav snapshot`.
- **UTF-8 mit BOM:** `Edit`/`Write` hinterlassen **kein** BOM (Hook zieht nach; im Zweifel prüfen). Nie
  Win-1252-Altlasten „nur mit BOM reparieren" — siehe CLAUDE.md / `nav fixenc`.
- **Raw-Strings** für alle `.nav`-Fixtures/Erwartungswerte (CLAUDE.md; `[[raw-strings-in-golden-tests]]`).
- **`cancel` ≠ TO-Zurückgeben:** falls in Doku/Beispielen begründet — `--> cancel` ist ein No-Op
  (`contextNode`), `return to` ist `GotoGUI` = `ReplaceTO`+`OnTOChanged` (Re-Render). Nicht vermischen.

## Stand

- **S1 umgesetzt** (Commit `8734a564`, auf `master`). `cancel` parst als RHS-Kantenziel
  (`CancelTargetNodeSyntax`) und wird als Keyword klassifiziert.
- **S2 umgesetzt** (Commit `46bffd03`). Design-Frage entschieden: **Referenz-Marker**
  (`ICancelNodeReferenceSymbol`), **kein** synthetisches Knoten-Symbol (Kollision mit dem non-null-
  `INodeSymbol.Syntax`-Kontrakt, siehe oben). Cancel-Ausgang über `edge.TargetsCancel()` abfragbar;
  Nav0011 nimmt cancel aus.
- **S3 umgesetzt** (Commit `0c666412`). Drei Diagnosen: **Nav5000** (Versionsgate via neuem
  `NavLanguageFeature.Cancel`), **Nav0125** (nur per Goto `-->`, analog Nav0106), **Nav0126**
  (nicht an Exit-Transition, E5-Stellen-Restriktion). Nav0125/0126 schweigen in V1 (Nav5000 ist dort
  die eine Diagnose). Fünf Diagnostics-Fixtures. Build 0 Fehler/0 Warnungen.
- **S4 umgesetzt** (uncommitted). Cancel-Gating im V2-Codegen: `bool declaresCancel` an
  `CallContextCodeModel.Build`, in allen vier Aufrufern aus `edge.TargetsCancel()` berechnet; ohne
  Deklaration keine `Cancel()`-Callable → `return next.Cancel()` ist Compile-Fehler (E3). V1
  byte-identisch (5 V2-Goldens −30 Cancel-Zeilen, 23 V1-Goldens unverändert). Neue
  `CancelCodeGenTests` (3 Fälle) + nachgezogener CodeAnalysis-Golden (Span-Shift 71→70).
- **S5 umgesetzt** (uncommitted). Completion-Gating: `NavCompletionService.TargetItems` bietet `cancel`
  (analog `end`) nur ab `#version 2` an (`NavLanguageFeature.Cancel`); `FallbackItems` filtert `cancel`
  in V1 heraus (Loose End S1a). VS + LSP ziehen über den geteilten Service automatisch nach. Gepaarte
  `NavCompletionServiceTests` (Version1/Version2). net472 1940/0; net10 1880/1880.
- **S6 umgesetzt** (uncommitted). Golden-Fixture `Regression\Tests\V2\CancelFlow.nav` mit **beiden**
  Formen (`Home --> cancel on OnEscape` + `Choice_Confirm --> cancel if "Abbruch"`); vier `.expected.cs`
  via Regression-Generierung + `nav snapshot`. Das Golden zeigt das Gating direkt: `OnEscape`- und
  `Choice_Confirm`-Context tragen `Cancel()`, `Init1`- und `OnDecide`-Context nicht. V1- und bestehende
  V2-Regression byte-identisch. Design-Doku (`nav-codegen-v2-concat-design.md` §3.4/§3.4a,
  `nav-codegen-v2-status.md` § Gating-Punkt 3) fortgeschrieben.
- **Damit sind S1–S6 abgeschlossen — das `cancel`-Keyword steht vollständig** (Syntax, Semantic Model,
  Analyzer, V2-Codegen-Gating, Completion, Golden + Doku).
- Dieses Doc ist in `Nav.Language.Extensions.slnx` unter `/doc/` eingehängt.

## Verifikation (Wiederholrezept)

- Einmalig `. .\Tools\Commands\Import-NavCommands.ps1`.
- Bei Engine-Änderung: **`nav build`**, dann **`nav test`** (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`
  (`--filter` funktioniert nur auf net10.0, z.B. `--filter "FullyQualifiedName~Cancel"`).
- Codegen: `nav snapshot` erzeugt V2-Golden unter `Regression\Tests\V2\` neu; V1-`.expected.cs`
  müssen **unverändert** bleiben.
