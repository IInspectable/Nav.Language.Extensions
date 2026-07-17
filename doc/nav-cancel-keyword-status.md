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

**Freie Diagnose-Nummern:** `Nav0123`, `Nav0125`, `Nav5002` (belegt: …0122, 0124, 5000, 5001).

**Versionsgate-Autorität:** `NavLanguageVersion` (Version1/SupportedVersions/IsSupported) + `Nav5000` —
siehe `doc/nav-pragmas-versioning-status.md`. `cancel` ist ein V2-Feature → Gate wie andere V2-Konstrukte.

## Offene Design-Frage (vor S2 entscheiden — **Vermutung/Empfehlung**, nicht verifiziert)

Wie fließt der Cancel-Ausgang in die `directCalls`-Iteration (`CallContextCodeModel.cs:102`)?
- **Empfehlung:** ein **synthetisches `ICancelNodeSymbol`** (analog `IEndNodeSymbol`, aber **ohne**
  Deklaration — on-demand synthetisiert, sobald ein Ziel `cancel` referenziert). Dann bleibt die
  `foreach`-Schleife uniform und das Gating ist schlicht
  `directCalls.Any(c => c.Node is ICancelNodeSymbol)`.
- **Alternative:** ein Flag auf der Kante/Transition statt eines Knoten-Symbols — spart das Symbol,
  bricht aber die einheitliche Call-Iteration.

Die Wahl bestimmt, wie viel von der `end`-Symbolmaschinerie (References, Reachability,
`SymbolsAndSelf`) für cancel überhaupt gebraucht wird. **Vor S2 festlegen.**

## Plan (Steps)

- **S1 — Grammatik/Syntax.** `CancelKeyword`/`CancelKeyword`-Token in `SyntaxFacts.cs` +
  `SyntaxTokenType`; `CancelTargetNodeSyntax` + `ParseCancelTargetNode` (analog
  `EndTargetNodeSyntax`/`:1160`); `ParseTargetNode` (`:1150`) und `StartsTargetNode` (`:1187`) um das
  cancel-Keyword erweitern; Lexer/Klassifikation (Keyword). **Berührt:** `Syntax\` (Facts, Parser, Lexer,
  neue Target-Syntax). **DoD:** `X --> cancel …` parst, `cancel` wird als Keyword klassifiziert; kein
  `cancel;` als Deklaration (E4).
- **S2 — Semantic Model.** Zielauflösung von `cancel` gemäß Design-Frage oben (empfohlen: synthetisches
  `ICancelNodeSymbol`); der Cancel-Ausgang erscheint in den `Outgoings`/`GetDirectCalls` der Quelle.
  **DoD:** Für eine Quelle mit `--> cancel` ist der Cancel-Ausgang im Semantic Model abfragbar.
- **S3 — Analyzer/Diagnosen.** Versionsgate (`cancel` erst ab V2 → `Nav5xxx`, Autorität
  `NavLanguageVersion`); „cancel nur per Goto `-->` erreichbar" (analog `Nav0106`); ggf.
  Stellen-Restriktion (nur Choice-Arm oder direkte Init/Trigger-Kante, E5). **DoD:** Fehlnutzung
  (falsche Version, `o->`/`==>` auf cancel) wird als Nav-Diagnose gemeldet.
- **S4 — V2-Codegen-Gating (Kern).** In `CallContextCodeModel.cs:122-123` die Cancel-Callable **nur**
  emittieren, wenn die Quelle einen Cancel-Ausgang trägt; `ChoiceCallContextCodeModel` zieht nach. V1
  (`TransitionCodeModel.cs:44`) bleibt unverändert. **DoD:** V2-Unit **ohne** `cancel`-Deklaration hat
  **keine** `Cancel()`-Methode am Context (und `return Cancel()` in Logik ist dann Compile-Fehler, E3);
  V2-Unit **mit** Deklaration hat sie; **V1-Regression byte-identisch** (Dispatcher-Invariante).
- **S5 — Completion.** `cancel` als Zielvorschlag (analog `end`) — nur im V2-Kontext, hinter dem
  Versionsgate. **DoD:** Completion bietet `cancel` an passender Kantenposition in V2 an.
- **S6 — Golden-Fixtures + Doku.** `.nav`-Golden mit `Choice --> cancel if …` **und**
  `View --> cancel on …`; V2-Snapshots via `nav snapshot` unter `Regression/Tests/V2/`. Design-Doc
  `doc/nav-codegen-v2-concat-design.md` und den Gating-Abschnitt in `doc/nav-codegen-v2-status.md`
  (§ „Offene Gating-Entscheidungen") fortschreiben. **DoD:** Goldens grün, Design-Doku aktuell.

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

- **Nichts umgesetzt.** Arbeitsbaum sauber (`master`, `be520208 formatting`). Kein `cancel`-Keyword im
  Repo (nur `CancellationToken`-Treffer).
- **Nächster Schritt: S1.** Vor S2 die offene Design-Frage (synthetisches Symbol vs. Kanten-Flag) klären.
- Dieses Doc ist in `Nav.Language.Extensions.slnx` unter `/doc/` eingehängt.

## Verifikation (Wiederholrezept)

- Einmalig `. .\Tools\Commands\Import-NavCommands.ps1`.
- Bei Engine-Änderung: **`nav build`**, dann **`nav test`** (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`
  (`--filter` funktioniert nur auf net10.0, z.B. `--filter "FullyQualifiedName~Cancel"`).
- Codegen: `nav snapshot` erzeugt V2-Golden unter `Regression\Tests\V2\` neu; V1-`.expected.cs`
  müssen **unverändert** bleiben.
