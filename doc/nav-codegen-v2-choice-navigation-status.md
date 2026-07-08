# V2-Choice-Navigation — Gap-Analyse & Step-Zuschnitt

> **Lebendes Tracking-Dokument** für die **Navigation der V2-Choices** (Nav↔C#, FindReferences).
> Einordnung: Dies ist die erste Scheibe von **Step 7** aus `doc/nav-codegen-versioning.md`
> („V2-Navigation end-to-end"), angestoßen nachdem der V2-**Codegen** (S0–S8,
> `doc/nav-codegen-v2-status.md`) vollständig steht. **Scope dieser Scheibe:** Engine
> (`Nav.Language` + `Nav.Language.CodeAnalysis`) **und** die VS-Extension
> (`Nav.Language.ExtensionShared`). LSP/MCP bleiben **außen vor** (deren GoTo ist heute Nav→Nav;
> C#-Sprünge sind Roslyn-/VS-gekoppelt) — separate Scheibe, falls überhaupt.

## Warum das fehlt

In **V1** erzeugte eine Choice **keinen** Code — sie wurde beim Codegen transitiv plattgefaltet
(`GetReachableCalls`, §2.1 des Design-Docs). Erst der **V2-Codegen** (S7) gibt der Choice eine eigene
C#-Gestalt: die abstrakte Entscheidungsmethode **`{Choice}Logic`** plus ihren **`{Choice}CallContext`**,
erreicht über je einen **`{Choice}(…)`-Forward** aus jeder Quelle (Init/Trigger/Exit oder eine andere
Choice). Damit gibt es zum ersten Mal ein Choice-Artefakt, zu dem und von dem navigiert werden könnte —
und **keiner** der drei Navigations-Pfade (Nav→C#, C#→Nav, FindReferences) kennt bisher einen
Choice-Fall.

Konkret am `ChoiceFlow`-Golden (`Regression/Tests/V2/WFL/generated/ChoiceFlowWFSBase.generated.expected.cs`):

- `Choice_RetryLogic` (`:187`) und `Choice_EscalateLogic` (`:217`) tragen — anders als `OnRetryLogic`
  (`:137`, mit `#region Nav Annotations / <NavTrigger>OnRetry</NavTrigger>`) — **keinen**
  Annotations-Block.
- Die Forwards `public Result Choice_Retry(string reason) => new(() => _wfs.Choice_RetryLogic(…))`
  (`:67` in `Init1CallContext`, `:95` in `AfterACallContext`, `:151` in `OnRetryCallContext`) sind die
  „Aufrufer", zu denen FindReferences zurückführen soll.

Die Choice-Knoten heißen im `.nav` bereits `Choice_Retry`/`Choice_Escalate` (`ChoiceFlow.nav:49/50`) —
der `Choice_`-Präfix ist **Teil des Knotennamens**, kein Codegen-Zusatz. Damit ist
`{Choice}Logic == Name.ToPascalcase() + LogicMethodSuffix` (trivial, wie `TaskExitCodeInfo`).

## Der Dreiklang, wie ihn Init/Trigger/Exit schon haben

| Richtung | Vorbild (Trigger) | Choice-Lücke |
|---|---|---|
| **Nav → C#** (Knoten → Logic) | `GoToSymbolBuilder.VisitSignalTriggerSymbol` → `SignalTriggerCodeInfo` → `LocationFinder.FindTriggerDeclarationLocationsAsync` | kein `VisitChoiceNodeSymbol`, kein `ChoiceCodeInfo`, kein `FindChoiceLogic…` |
| **C# → Nav** (Logic → Knoten) | `<NavTrigger>` → `NavTriggerAnnotation` → `IntraTextGoToTagSpanBuilder.VisitNavTriggerAnnotation` → `LocationFinder.FindNavLocationsAsync(NavTriggerAnnotation)`/`GetTriggerLocations` | `WfsBaseEmitterV2.WriteChoice` (`:182`) emittiert **keine** Annotation; kein `NavChoiceAnnotation` |
| **FindReferences** (Logic → Aufrufer) | `WfsReferenceFinder` (Begin-/After-Aufruffindung) | keine Choice-Aufruffindung |

## Step-Zuschnitt

Reihenfolge so, dass jeder Step für sich baubar/testbar ist. **A ist Fundament** für C und D (die
Annotation trägt beide). Nach jedem Step: Review + `nav test` (net472) **und** `dotnet test … -f net10.0`
(beide TFMs grün) + gelieferte Commit-Message — der Commit macht der Nutzer.

| # | Inhalt | Kern-Dateien | Fertig, wenn | Status |
|---|---|---|---|---|
| **A** | **Annotation + Emitter:** `<NavChoice>` auf `{Choice}Logic` | `CodeGenInvariants`/`CodeGenFacts` (Tag `NavChoice`), `EmitterCommon.WriteNavChoiceAnnotation`, `WfsBaseEmitterV2.WriteChoice`; Golden-Regen; Invariant-Test | ChoiceFlow-Golden trägt `<NavChoice>` je Logic; **V1 byte-identisch**; `AnnotationTagNavChoice`-Invariant-Test grün | **erledigt** — s.u. |
| **B** | **Nav → C#** (Choice-Knoten/-Referenz → `{Choice}Logic`) | `ChoiceCodeInfo` (Shared), `LocationFinder.FindChoiceLogicDeclarationLocationAsync`, `GoToSymbolBuilder.VisitChoiceNodeSymbol` + `ChoiceLogicDeclarationLocationInfoProvider` | F12 auf `choice X` springt in `{Choice}Logic`; Referenzen (`--> X`) erben den Sprung via `VisitNodeReferenceSymbol` | **erledigt** — s.u. |
| **C** | **C# → Nav** (`{Choice}Logic` → Choice-Knoten) | `NavChoiceAnnotation`, T4-Visitor-Regen, `AnnotationReader.ReadNavChoiceAnnotation`, `LocationFinder.FindNavLocationsAsync(NavChoiceAnnotation)`/`GetChoiceLocations`, `IntraTextGoToTagSpanBuilder.VisitNavChoiceAnnotation` + `NavChoiceAnnotationLocationInfoProvider` | Intra-Text-GoTo auf `{Choice}Logic` springt auf `choice X` im `.nav` | offen |
| **D** | **FindReferences** (`{Choice}Logic` → `{Choice}(…)`-Forwards) | `WfsReferenceFinder` (Choice-Zweig, C#-Forward-Aufrufstellen) | „Alle Referenzen" auf eine Choice listet die C#-Forward-Aufrufstellen | offen |

### A — Annotation + Emitter (Fundament)

- **Tag-Konstante `AnnotationTagNavChoice = "NavChoice"`** in `CodeGen/Shared/Facts/CodeGenInvariants.cs`
  **und** im V1-Spiegel `CodeGen/V1/CodeGenFacts.cs` (der `AnnotationReader` liest über
  `CodeGenFacts.AnnotationTag*` — Konstante muss dort existieren, auch wenn V1 nie eine Choice emittiert).
  Dazu der Invariant-Test in `CodeGenFactsTests` (wie `InvariantAnnotationTagNavTrigger`).
- **`EmitterCommon.WriteNavChoiceAnnotation(cb, choiceName)`** — wortgleich zum Trigger-Muster
  (`#region Nav Annotations / <NavChoice>{choiceName}</NavChoice> / #endregion`).
- **`WfsBaseEmitterV2.WriteChoice`** (`WfsBaseEmitterV2.cs:182`): die Annotation **vor** der abstrakten
  `{Choice}Logic` schreiben (die Choice hat keine öffentliche Maschinerie-Methode, nur die Logic — also
  **eine** Annotation, kein Doppel wie bei Transitionen). Der `{Choice}CallContext` bleibt annotationslos.
- **Golden:** nur `ChoiceFlow` hat erreichbare Choices → Regression-Regen + `nav snapshot`. **Fallstrick
  (aus S6):** `nav snapshot` **generiert nicht**, es kopiert nur — erst die Regression-Generierung
  anstoßen, dann `nav snapshot`. V1- und die übrigen V2-Goldens bleiben byte-identisch.

**A umgesetzt.** Tag-Konstante `AnnotationTagNavChoice = "NavChoice"` in `CodeGenInvariants` **und**
`CodeGenFacts` (V1-Spiegel, den der `AnnotationReader` liest) + Invariant-Test
`InvariantAnnotationTagNavChoice`. `EmitterCommon.WriteNavChoiceAnnotation` (Trigger-Muster).
`WfsBaseEmitterV2.WriteChoice` schreibt die Annotation **vor** der abstrakten `{Choice}Logic` (einzige
Annotation der Choice — der `{Choice}CallContext` bleibt annotationslos). Träger ist die neue Property
`ChoiceCallContextCodeModel.ChoiceName` (= `choiceNode.Name`, unverändert, z.B. `Choice_Retry`) — bewusst
der **Knotenname**, nicht der Logic-Methodenname, damit Step C ihn 1:1 gegen `IChoiceNodeSymbol.Name`
auflösen kann. Golden-Regen: **nur** `ChoiceFlowWFSBase.generated.expected.cs` ändert sich (+2
`#region Nav Annotations`-Blöcke für `Choice_Retry`/`Choice_Escalate`); V1 und alle übrigen V2-Goldens
byte-identisch. Verifikation: **net10 1407/0, net472 1415/0** (3 explizite Skips).

### B — Nav → C# (Choice-Knoten → `{Choice}Logic`)

- **`ChoiceCodeInfo`** (`CodeGen/Shared/CodeInfo/`): `ContainingTask` (`TaskCodeInfo`) +
  `ChoiceLogicMethodName`. **Versionsrichtig** aus dem Nav-Symbol abgeleitet (wie `TaskExitCodeInfo` über
  `containingTask.Facts` — **kein** Option-B-Fall, da ein Nav-Symbol mit Sprachversion vorliegt).
- **`LocationFinder.FindChoiceLogicDeclarationLocationAsync(project, ChoiceCodeInfo, ct)`** — Mechanik
  wie `FindTriggerDeclarationLocationsAsync`/`FindTaskExitDeclarationLocationAsync`: von der `{Task}WFSBase`
  zu den abgeleiteten Klassen absteigen, `GetMembers(ChoiceLogicMethodName)`, Location der Methode.
- **VS:** `GoToSymbolBuilder.VisitChoiceNodeSymbol(IChoiceNodeSymbol)` → neuer
  `ChoiceLogicDeclarationLocationInfoProvider` (Spiegel `TriggerDeclarationLocationInfoProvider`). Die
  **Referenzen** auf die Choice (`--> Choice_Retry`) bekommen den Sprung **gratis**: `VisitNodeReferenceSymbol`
  ruft bereits `Visit(nodeReferenceSymbol.Declaration)` und hängt dessen Provider an
  (`GoToSymbolBuilder.cs:100`).

**B umgesetzt.** `ChoiceCodeInfo` (`CodeGen/Shared/CodeInfo/`, Spiegel `TaskExitCodeInfo`):
`ContainingTask` + `ChoiceLogicMethodName = {choiceNode.Name.ToPascalcase()}{ContainingTask.Facts.LogicMethodSuffix}`
— **versionsrichtig** aus dem Nav-Symbol (`TaskCodeInfo.FromTaskDefinition` → Facts aus der Sprach-Version),
deckt sich mit der Emitter-Ableitung in `ChoiceCallContextCodeModel.FromChoice`.
`LocationFinder.FindChoiceLogicDeclarationLocationAsync` ist die exakte Mechanik von
`FindTaskExitDeclarationLocationAsync`: vom `{Task}WFSBase` (via `FullyQualifiedWfsBaseName`,
`GetTypeByMetadataName`) zu den abgeleiteten Benutzer-Klassen absteigen, `GetMembers(ChoiceLogicMethodName)`
→ die **Override im Nutzer-Code** (nicht die abstrakte Deklaration im generierten File). VS:
`GoToSymbolBuilder.VisitChoiceNodeSymbol` + `ChoiceLogicDeclarationLocationInfoProvider` (Spiegel
`TaskExitDeclarationLocationInfoProvider`, DisplayName `{Wfs}.{ChoiceLogic}`). **Fallstrick (erledigt):** die
Shared-Project-Dateiliste `Nav.Language.ExtensionShared.projitems` listet Dateien **explizit** (kein Glob) →
der neue Provider musste dort eingetragen werden, sonst `CS0246` im `_wpftmp`-Teilprojekt.

**V1-Weiche (Nachtrag).** Anders als Trigger/Exit (deren Logic in *jeder* Version existiert, ihr „not found"
also nur transient „C# noch nicht gebaut" ist) faltet **V1** Choices platt → es entsteht **nie** eine
`{Choice}Logic`. Da `NavLanguageVersion.Default => Version1` ist, böte ein bedingungsloser Tag auf *jeder*
versionslosen `.nav` einen Choice-GoTo an, der beim Klick garantiert fehlschlägt. Deshalb gated
`VisitChoiceNodeSymbol` das Angebot: unter `LanguageVersion < NavLanguageVersion.Version2` → `DefaultVisit`
(kein Tag), analog zum `Alias`-Rückfall in `VisitInitNodeSymbol` und exakt dem Versions-Idiom aus
`Nav0124GeneratedMember0CollidesWithAnotherMember` (`< Version2 → yield break`). Die Choice-**Referenzen**
erben die Weiche gratis (`VisitNodeReferenceSymbol` → `Visit(declaration)`). Bewusst **kein** Fähigkeits-Flag
auf `ICodeGenFacts` (dessen Charta ist „nur Namen"; Gestalt-/Fähigkeits-Unterschiede bleiben laut Doku im
Emitter bzw. am direkten Versionsvergleich). Engine/`ChoiceCodeInfo` bleiben versions-agnostisch — nur die
*Angebots*-Entscheidung sitzt im Visitor. In V2 wirft der Finder weiterhin `LocationNotFoundException`
(→ `FromError`), wenn der Sprung transient nicht auflösbar ist (Code noch nicht gebaut / WFS-Klasse fehlt) —
wie bei Trigger/Exit.

Verifikation: **`nav build` grün** (inkl. VS-Extension), **net472 1415/0** (3 Skips), **net10 1407/0** (der
Guard betrifft nur die nicht-test-abgedeckte VS-Extension; die Engine ist gegenüber dem grünen Testlauf
unverändert).

### C — C# → Nav (`{Choice}Logic` → Choice-Knoten)

- **`NavChoiceAnnotation : NavMethodAnnotation`** (Spiegel `NavTriggerAnnotation`) mit `ChoiceName`.
- **T4-Visitor-Regen — VS-only (Fallstrick):** `NavTaskAnnotationVisitor.Generated.tt` reflektiert per
  **EnvDTE** über die nicht-abstrakten `NavTaskAnnotation`-Ableitungen und erzeugt `VisitNavChoiceAnnotation`
  automatisch — aber **nur beim Ausführen des T4 in Visual Studio** (Custom Tool). `dotnet`/CLI regeneriert
  das **nicht**. Also: nach dem Anlegen von `NavChoiceAnnotation` in VS „Run Custom Tool" auf der `.tt`
  (oder die `.Generated.cs` von Hand um die vier `VisitNavChoiceAnnotation`-Stellen ergänzen — exakt dem
  Trigger-Block nachgebildet).
- **`AnnotationReader.ReadNavChoiceAnnotation`** + Dispatch analog zum Trigger (`AnnotationReader.cs:342`).
- **`LocationFinder.FindNavLocationsAsync(sourceText, NavChoiceAnnotation, ct)`** + `GetChoiceLocations(task,
  annotation)`: die `IChoiceNodeSymbol` per Name im Task finden → deren `Location`.
- **VS:** `IntraTextGoToTagSpanBuilder.VisitNavChoiceAnnotation` + `NavChoiceAnnotationLocationInfoProvider`
  (Spiegel Trigger).

### D — FindReferences (`{Choice}Logic` → C#-Forward-Aufrufstellen)

**Bestätigt:** D meint die **C#-Forward-Aufrufstellen** — die `new(() => _wfs.{Choice}Logic(…))`-Rümpfe
in den Quell-Contexts (`Init1CallContext.Choice_Retry` usw.). Das C#-Pendant zu den Begin-/After-Findern in
`WfsReferenceFinder`, per Roslyn `FindReferencesAsync` auf das `{Choice}Logic`-Symbol (bzw. die
`{Choice}(…)`-Forward-Member). Die reine Nav-Seite (`… --> Choice_Retry`) steht bereits über
`FindReferencesVisitor.VisitChoiceNodeSymbol` (`FindReferences/FindReferencesVisitor.cs:433`) und ist
**nicht** Teil von D.

## Fallstricke (gesammelt)

- **T4-Visitor ist VS-Design-Time** (Step C) — Regen nur in Visual Studio, nicht via CLI (s.o.).
- **Golden-Byte-Identität:** V1 bleibt identisch; das **V2-`ChoiceFlow`-Golden ändert sich in Step A**
  (Annotation) — erwartet, Regen. `nav snapshot` kopiert nur, generiert nicht (erst Regression-Generierung).
- **VS-Extension baut nur via `nav build`** (MSBuild.exe, VSSDK) — `dotnet build` deckt sie nicht ab. Die
  Engine-/CodeAnalysis-Teile (A–D-Kern) laufen dagegen über `dotnet build`/beide TFMs.
- **Versionsbewusstheit:** B (`ChoiceCodeInfo`) ist versionsrichtig aus dem Nav-Symbol; C/D fahren über die
  **Annotation** (Choice-Name steht drin) → **kein** Option-B-Namensableitungs-Problem.

## Verifikation (Wiederholrezept)

- `. .\Tools\Commands\Import-NavCommands.ps1` einmalig; dann `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`.
- Step A: Golden-Regen (Regression-Generierung → `nav snapshot`); V1 + übrige V2-Goldens byte-identisch.
- VS-Extension-Teile: `nav build` (MSBuild.exe); T4-Regen in VS.
