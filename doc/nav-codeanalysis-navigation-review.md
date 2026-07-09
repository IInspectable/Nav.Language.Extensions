# CodeAnalysis-Navigation — Review-Backlog (Lücken & Konsistenz)

> **Lebendes Tracking-Dokument** aus dem Review der `Nav.Language.CodeAnalysis.Tests` (Roslyn-Brücke
> Nav ↔ generierter C#-Code, `LocationFinder`). Nachdem die Konstrukt-Reihe (Choice/Task/Trigger/Init/Exit)
> steht (Commits `cbc82614`…`541fa9cd`), erfasst dieses Doc die im Review gefundenen **restlichen
> Test-Lücken** und **Konsistenz-/Refactor-Punkte**, damit sie über mehrere Sessions abgearbeitet werden
> können, ohne den Review neu zu erheben.
>
> **Scope:** ausschließlich die Roslyn-Brücke `Nav.Language.CodeAnalysis` (`LocationFinder`,
> `AnnotationReader`) und ihre VS-Provider (`Nav.Language.ExtensionShared/GoToLocation/Provider`). Die
> reine **Nav→Nav-Navigation** (`taskref`/`task X;` → `task`-Definition) läuft über `NavGoToService` in der
> Engine und wird in `Nav.Language.Tests\GoTo` getestet — **nicht** Teil dieses Backlogs.
>
> Einordnung/Vorgeschichte: [[nav-codeanalysis-tests]], `doc/nav-codegen-v2-choice-navigation-status.md`.

## Arbeitsweise (Wiederholung der Projektregeln)

- Jeder Punkt ist als **eigener Step + eigener Commit** gedacht; nach jedem Step Review + `nav test`, dann
  eine fertige Commit-Message liefern — **nie** selbst committen.
- Tests: `nav test` (net472-Console-Runner; Projekt ist net472-only, `dotnet test` läuft ins Leere).
  Golden regenerieren nach bewusster Prüfung: Lauf mit `NAV_UPDATE_GOLDEN=1`, gezielt
  `nav test --where "class =~ {K}GoTo"`. **`nav test` baut nicht** → bei Codeänderung vorher `nav build`.
- Neue `.md` immer als UTF-8 **mit BOM** und in `Nav.Language.Extensions.slnx` unter `/doc/` einhängen.
- Ordner-Konvention der Tests: konstrukt-first, je Konstrukt `{K}Fixtures.cs` + `{K}GoToCSharpTests.cs` +
  `{K}GoToNavTests.cs` + `Snapshots/`. Klassennamen benennen den **Landeplatz** (`…GoToCSharp`/`…GoToNav`).

## Abdeckungsmatrix (Stand Review)

| Konstrukt | Nav→C# | C#→Nav | C#→C# | Negativpfad |
|---|---|---|---|---|
| Task | ✅ Decl→WFS (+2. Task) | ✅ WFS→Decl (+2.) | — | ✅ Nav→C# (MissingWfs) + C#→Nav (task==null) |
| **Task → IBegin-Interface** | ✅ Decl→IBegin (+2. Task) | — | — | ✅ MissingItf |
| Trigger | ✅ (+2.) | ✅ (+2.) | — | ✅ Nav→C# + C#→Nav (trigger fehlt) |
| Init | ✅ Node→BeginLogic | ✅ (+Child) | ✅ CallSite→ChildBeginLogic | ✅ Nav→C# + C#→Nav (init fehlt) + Call-Site |
| Exit | ✅ Punkt→AfterLogic | ✅ mehrdeutig (E1/E2) | ✅ After→Begin-Caller | ✅ Nav→C# + C#→Nav (Knoten fehlt) |
| Choice | ✅ (+2.) | ✅ Logic+CallSite (+Escalate) | ✅ CallSite→Logic + Logic→Aufrufer (beidseitig) | ✅ Nav→C# + C#→Nav (choice fehlt) + Call-Site |

---

## A. Fehlende Testfälle

### A1 — `FindTaskIBeginInterfaceDeclarationLocations` komplett ungetestet  ✅ (erledigt)

**Erledigt** (`TaskGoToCSharpTests`): `TaskDeclaration_JumpsToIBeginInterface` +
`SecondTaskDeclaration_JumpsToItsOwnIBeginInterface` (Nav→C#, pinnen den `IBegin{Task}WFS`-Identifier-Span
in `IBegin{Task}WFS.generated.cs`) + `MissingIBeginInterface_ThrowsLocationNotFound` (Negativpfad über
`ForeignProject()`). Zugang über neuen Harness-Helfer `CodeAnalysisTestContext.TaskDeclarationInfo(name)`
(→ `TaskDeclarationCodeInfo.FromTaskDeclaration(TaskDefinition(name).AsTaskDeclaration)`).

---

Einzige öffentliche `LocationFinder`-Navigationsmethode mit **null** Abdeckung. Zwei live-verdrahtete
VS-Provider hängen dran:
- `Nav.Language.ExtensionShared/GoToLocation/Provider/TaskIBeginInterfaceDeclarationLocationInfoProvider.cs`
- `…/TaskIBeginInterfaceDeclarationCodeFileLocationInfoProvider.cs`

Feature: „F12 auf `task X` → `IBegin{Task}WFS`-Interface-Deklaration". Methode:
`LocationFinder.FindTaskIBeginInterfaceDeclarationLocations(project, TaskDeclarationCodeInfo, ct)`
(`Nav.Language.CodeAnalysis/FindSymbols/LocationFinder.cs:310`).

**How to:** in `Task/TaskGoToCSharpTests.cs` einen Golden-Test ergänzen (Nav→C#). Es braucht die
`TaskDeclarationCodeInfo` — Zugang analog `TaskInfo(...)` im `CodeAnalysisTestContext` schaffen (z.B.
`TaskDeclarationInfo(name)` → `TaskDeclarationCodeInfo.FromTaskDefinition(...)`, exakten Fabrik-Namen im
Code prüfen). Ziel-Span pinnt die `interface IBeginTaskFlowWFS`-Deklaration. Plus `MissingWfs`-Negativpfad
über `ForeignProject()`.

### A2 — `NavExitBeginCallerLocationInfoProvider` ungetestet (Exit-Analogon zu Choice-Caller)  ✅ (erledigt)

**Erledigt** (mit B3): `ExitGoToCSharpTests.AfterLogic_JumpsToAllBeginCallSites` prüft die echte
`LocationFinder.FindCallerLocations`-Suche (C#→C#): von der `AfterSubLogic` klassenweit auf die
`next.BeginSub()`-Aufrufstelle. Neuer Fixture-Teil `ExitFixtures.ExitFlowUserCode` (ruft den Begin-Wrapper
auf), Klassen-Anker ist die konkrete `ExitFlowWFS`, Filter = `NavInitCallAnnotation` +
TaskName/NavFileName + `Begin`-Prefix + `ExitTaskName`. Damit ist die frühere Asymmetrie zum
getesteten Choice-Caller aufgehoben.

---

Richtung C#→C#: „After-Methode → C#-Aufrufstellen der `Begin{Node}`". Auffälligste **Asymmetrie**: das
exakte Choice-Analogon (`ChoiceLogic_JumpsToAllForwardCallSites`) ist getestet, die Exit-Seite nicht.
Provider: `…/GoToLocation/Provider/NavExitBeginCallerLocationInfoProvider.cs`.

**Abhängig von B3** (siehe unten): die Provider-Logik lebt heute VS-gekoppelt und ist so nur als
*vereinfachter Kern* testbar (wie beim Choice-Caller: `ChoiceCallAnnotations(...).Select(ToLocation)`).
Sauberer Weg = erst B3 (VS-freie Caller-Suche in `LocationFinder`), dann beide Richtungen (Choice + Exit)
echt testen. Alternativ sofort den vereinfachten Kern-Test in `Exit/ExitGoToCSharpTests.cs` (liest die
`NavInitCallAnnotation`-Aufrufstellen der `Begin{Node}` und mappt `ToLocation`) — dann braucht die
Exit-Fixture Nutzer-Code, der `next.BeginSub()` aufruft (vgl. `InitFixtures.InitFlowUserCode`).

### A3 — C#→Nav-„nicht gefunden"-Negativpfade fehlen durchgängig  ✅ (erledigt)

**Erledigt**: je Konstrukt ein `Missing{K}Annotation_ThrowsLocationNotFound` in `…GoToNavTests.cs`. Umgesetzt
über die im How-to genannte zweite Variante — **eine echte Annotation an eine gefälschte Symbol-Name-Fassung
umhängen**: aus dem jeweiligen Fixture die reale Annotation lesen, dann eine gleichnamige `NavXxxAnnotation`
mit **derselben Task-/Datei-/Method-Verankerung**, aber einem unbekannten Sub-Namen konstruieren
(`new NavTriggerAnnotation(real, real.MethodDeclarationSyntax, "OnGhost")` usw.) und gegen die **eigene**
`ctx.NavSource` laufen lassen. So trifft der Test punktgenau den **inneren** „nicht gefunden"-Zweig je
Konstrukt, ohne ein Sibling-`.nav` zu brauchen:
- Trigger `trigger == null` (`GetTriggerLocations`), Choice `choiceNode == null` (`GetChoiceLocationByName`),
  Init `initNode == null` (`GetInitLocations`), Exit „keine Exit-Transitions" (`GetExitLocations`).
- Task: der **äußere** `task == null`-Zweig des generischen `FindNavLocationsAsync<…>` — hier eine
  `NavTaskAnnotation` mit unbekanntem Task-Namen (`ClassDeclarationSyntax`/`NavFileName` der echten
  wiederverwendet), da `GetTaskLocations` selbst nie wirft.

Assertion überall `Throws.TypeOf<LocationNotFoundException>()` (kein Golden nötig).

### A4 — Call-Site-Negativpfade fehlen  ✅ (erledigt)

**Erledigt**: `FindCallBeginLogicDeclarationLocationsAsync` (`Init/InitGoToCSharpTests` →
`MissingCallBeginLogic_ThrowsLocationNotFound`) und `FindCallChoiceLogicDeclarationLocationAsync`
(`Choice/ChoiceGoToCSharpTests` → `MissingCallChoiceLogic_ThrowsLocationNotFound`) laufen jetzt je über
`ForeignProject()`: die echte `<NavInitCall>`- bzw. `<NavChoiceCall>`-Aufrufstelle stammt aus dem
Feature-Fixture, die Roslyn-Bühne aber aus dem fremden Task ohne das Ziel-Interface `IBeginChildWFS` bzw.
ohne die `{Task}WFSBase` → `LocationNotFoundException`. Damit haben nun beide Call-Site-Pfade denselben
`MissingWfs`-Negativpfad wie die genuinen Nav→C#-Pfade.

---

## B. Konsistenz / Implementierung

### B1 — Versions-Asymmetrie nur implizit abgesichert  ✅ (erledigt)

**Erledigt**: Die zuvor nur *zufällig* erfüllte Annahme des annotationsgetriebenen Call-Site-Pfads ist jetzt
eine **ausführbare, golden-unabhängige Invariante** — `CallSiteVersionAssumptionTests.
CallSitePath_AssumesDefaultLogicNamesHoldForEverySupportedVersion` behauptet, dass die
Default-Generations-Namensbausteine (`BeginMethodPrefix`, `LogicMethodSuffix`), aus denen die Call-Site-Pfade
`BeginLogic`/`{Choice}Logic` bauen, für **jede** `NavLanguageVersion.SupportedVersions` gelten. Der Assert
schlägt mit einer Meldung fehl, die den fälligen Umbau benennt („Option B": Sprach-Version in die
Call-Annotation einbetten). Zusätzlich tragen die drei betroffenen Call-Site-Tests (`InitCallSite_*`,
`ChoiceCallSite_*`, `EscalateCallSite_*`) je einen Querverweis-Kommentar auf diesen Guard.

Wesentliche Klärung dabei: Die betroffenen Bausteine sind **implementierungs-intern** (abstrakte
Logic-Methoden der `{Task}WFSBase`) und damit **nicht** von der Cross-Version-Invariante der
`IBegin{Task}WFS`-Schnittstellen gedeckt (die nur interface-seitige Namen bindet) — eine künftige Generation
darf sie also divergieren lassen. Der volle Umbau („Option B", versionsbewusster Call-Site-Pfad) bleibt
bewusst **offen**: Nutzen erst bei echter Divergenz; der Guard bricht dann laut und lokalisiert.

**Ursprüngliche Analyse (zur Nachvollziehbarkeit):** Call-Site-Pfade laufen auf der **Default-Generation**
(`DefaultBeginLogicMethodName` `:54`, `DefaultLogicMethodSuffix` `:63`), die genuinen Nav→C#-Pfade
versionsrichtig aus `*CodeInfo` (dokumentiert, `LocationFinder.cs:46-63`). Die V2-Fixtures fingen die Gefahr
**zufällig** ab (V2-Namen == Default-Namen → Tests grün), ohne dass eine Assertion das festhielt.

### B2 — `GetChoiceLocations` == `GetChoiceCallLocations` (Duplikat)  ✅ (erledigt)

**Erledigt**: gemeinsame private Hilfsmethode `GetChoiceLocationByName(task, choiceName)` in
`LocationFinder.cs`; `GetChoiceLocations`/`GetChoiceCallLocations` leiten nur noch den jeweiligen
`ChoiceName` dorthin weiter. Reiner Refactor, kein Verhaltenswechsel — durch die 17 `Choice`-Tests
(Logic- und Aufrufstellen-Pfad) abgesichert.

### B3 — Aufrufer-Suche dupliziert im VS-Host (Architektur, „eine Engine")  ✅ (erledigt, hat A2 entblockt)

**Erledigt**: Die gemeinsame Aufrufer-Suche liegt jetzt VS-frei in der Engine —
`LocationFinder.FindCallerLocations(project, classSymbol, filter, ct)` (neuer Ergebnistyp
`CallerLocation: Location` mit `CallerName`): sammelt alle (partiellen) Deklarationsdokumente der Klasse,
liest die `NavInvocationAnnotation`en, filtert per Prädikat, mappt auf die Aufrufstelle, sortiert nach
Datei/Position. Beide VS-Provider (`NavChoiceCallerLocationInfoProvider`,
`NavExitBeginCallerLocationInfoProvider`) rufen sie nur noch mit ihrem Prädikat auf; die
buffer-gebundene Klassen-Auflösung wurde in `CodeAnalysisLocationInfoProvider.FindContainingClassSymbolAsync`
gehoben (einmal statt zweimal). Am Roslyn-Level echt getestet: die Choice-Aufrufer-Tests
(`ChoiceLogic_JumpsToAllForwardCallSites`, `EscalateChoiceLogic_JumpsToChoiceToChoiceCallSite`) laufen jetzt
über `FindCallerLocations` mit Klassen-Scoping (Goldens byte-identisch) statt über den früheren
vereinfachten Kern; A2 (Exit) ist damit gleich mit abgedeckt.

---

`NavChoiceCallerLocationInfoProvider.cs` und `NavExitBeginCallerLocationInfoProvider.cs` enthalten
~identische Logik: Klasse im Tree finden → alle partiellen Dokumente sammeln → Annotationen lesen →
filtern (`TaskName`/`NavFileName`/Name) → `ToLocation` → sortieren. Diese **echte Navigationslogik** sitzt
im VS-Layer → in `CodeAnalysis.Tests` nicht testbar (die vorhandenen Tests replizieren nur einen
vereinfachten Kern: Filter allein nach Name, ohne `TaskName`/`NavFileName`/Klassen-Scoping).

**Ziel:** eine VS-freie `LocationFinder.FindCallerLocations(...)`-Methode (nimmt Solution/Project +
Klassen-Symbol/-Name + Filterprädikat), die beide Provider entdoppelt **und** beide Richtungen (Choice +
Exit) am Roslyn-Level echt testbar macht. Schließt A2 gleich mit ab und ersetzt die vereinfachten
Kern-Tests (`ChoiceLogic_JumpsToAllForwardCallSites` etc.) durch Tests der echten Suchlogik.

### B4 — `AmbiguousLocation` nur beim Exit — **kein Handlungsbedarf**

Sachlich gerechtfertigt: Exit-Punkte teilen eine Namensfamilie und brauchen den Verbindungspunkt-Namen als
Disambiguator; Choice-Aufrufstellen sind ohnehin distinkte Orte (`List<Location>`). Nur zur Dokumentation
hier, damit es nicht erneut als „Inkonsistenz" gemeldet wird.

---

## Zweiter Review-Durchlauf (offen)

> Frischer Durchlauf, nachdem A1–A4/B1–B4 abgearbeitet waren (Inventur: 12 öffentliche
> `LocationFinder`-Finder, das Testprojekt, 18 VS-Provider). **Gesamturteil: die Navigation ist im Kern
> konsistent** — 11 der 12 Finder sind direkt getestet, Provider→Finder ist 1:1, die Namens-Asymmetrie
> ist seit B1 per Guard abgesichert, die Duplikate sind entdoppelt (B2/B3). Es bleiben eine echte
> Restlücke (zugleich der letzte „eine Engine"-Bruch) plus kleinere Symmetrie-/Hygiene-Punkte.

### A5 — Init-Call-Site „After-Methode" ungetestet (abhängig von B5)  ✅ (erledigt)

**Erledigt** (mit B5): `Init/InitGoToCSharpTests.InitCallSite_JumpsToAfterMethod` pinnt golden das zweite
Ziel der Aufrufstelle `next.BeginChild()` — die `After{Node}`-Rücksprungmethode (`AfterChild` in
`InitFlowWFSBase.generated.cs`) — über die neue VS-freie `LocationFinder.FindInitCallAfterLocation`. Dazu
`InitCallSite_NoMatchingExit_ReturnsNull`, das den bewussten Nicht-Wirf-Contract pinnt (ohne passende
Exit-Annotation → `null`, dann bietet der Host nur `BeginLogic` an). Der Weg (b) wurde umgesetzt.

**Ursprünglich:** `NavInitCallLocationInfoProvider` liefert bei F12 auf `next.Begin{Sub}()` **zwei** Ziele:
`BeginLogic` (getestet) **und** die umgebende „After"-Methode, die zuvor inline im Provider (nur über
`LocationFinder.ToLocation`) gebaut und **nicht** getestet war.

### A6 — Init: zweiter Init-Knoten Nav→C# fehlt (Symmetrie)

Task/Trigger/Choice haben je einen `Second…`-Nav→C#-Test (zweites Geschwister-Konstrukt landet auf
seinem *eigenen* Ziel). Init nur einseitig: `InitNode_JumpsToBeginLogic` deckt einen Knoten, der zweite
Init ist nur C#→Nav (`ChildInitAnnotation_JumpsBackToChildInitNode`).

**How to:** in `Init/InitGoToCSharpTests.cs` ein `SecondInitNode_JumpsToItsOwnBeginLogic` ergänzen (die
Fixture hat mit `Child` bereits einen zweiten Init) — schließt die Reihe symmetrisch.

### A7 — `FindCallerLocations` „keine Aufrufer" (leere Liste) ungetestet

`FindCallerLocations` wirft **nie** `LocationNotFoundException`, sondern gibt eine leere Liste zurück —
bewusst anderer Contract als die werfenden Finder. Weder Choice noch Exit pinnen diesen Nicht-Wirf-Pfad.

**How to:** ein `…_NoCallers_ReturnsEmpty` (z.B. in `Exit/ExitGoToCSharpTests.cs`, Klassen-Symbol ohne
passende Annotation) fixiert den Contract explizit — Ergänzung zur B4-Doku-Linie (Contract dokumentieren,
kein Golden nötig).

### B5 — Init-Call „After-Methode" lebt noch im VS-Layer (letzter „eine Engine"-Bruch; entblockt A5)  ✅ (erledigt)

**Erledigt**: Die genuine Navigationslogik (Begin-Prefix abstreifen + passende `NavExitAnnotation` wählen +
`ToLocation`) sitzt jetzt VS-frei in `LocationFinder.FindInitCallAfterLocation(initCallAnnotation,
exitAnnotations)` (liefert die `After{Node}`-Stelle als benannte `CallerLocation` oder `null`). Der
`NavInitCallLocationInfoProvider` reicht nur noch die Exit-Annotations-Kandidaten durch und ruft die
Engine; `IntraTextGoToTagSpanBuilder` entfällt das inline `BeginMethodRegex`-Matching. Der Match ist
zusätzlich um Task-/Datei-Verankerung gehärtet (vorher nur `ExitTaskName` im Einzeldokument). Der
Begin-Prefix läuft — wie die übrigen Call-Site-Pfade — auf der Default-Generation (durch B1 gepinnt). Damit
ist der letzte Rest genuiner Navigationslogik aus dem VS-Layer in die Engine gehoben.

### B6 — `FindTriggerMethodSymbol` öffentlich ohne lebenden Aufrufer

`LocationFinder.FindTriggerMethodSymbol` (`LocationFinder.cs:494`) ist die einzige öffentliche
Finder-Methode ohne direkten Test und die einzige, die ein rohes `ISymbol` statt einer `Location`
zurückgibt; nur indirekt über `FindTriggerDeclarationLocationsAsync` gedeckt. Einziger externer Aufrufer
(`Commands/RenameCommandHandler.cs:75`) ist **auskommentiert**.

**Empfehlung:** Entscheidung erzwingen — direkter Test (falls für ein künftiges Rename gebraucht) **oder**
Sichtbarkeit auf `internal` reduzieren, damit die öffentliche Fläche nur Konsumiertes anbietet. Reine
Hygiene, kein Verhaltensrisiko.

### B7 — Keine V1-generierte Navigations-Fixture — **kein Handlungsbedarf**

Alle Fixtures sind `#version 2`; der Harness kann V1. Die versionsrichtigen Nav→C#-Pfade
(`FindTaskBeginDeclarationLocationAsync`, `FindChoiceLogicDeclarationLocationAsync`) werden dadurch nur
dort geübt, wo V2-Namen == Default-Namen. Wert gering: `CallSiteVersionAssumptionTests` (B1) hält die
Namensgleichheit bereits als Invariante fest, Choice-Logik ist ohnehin V2-only. Konsistent mit B1: erst
bei echter Namens-Divergenz relevant — dann bricht der Guard laut. Wie B4 nur zur Dokumentation, damit
es nicht erneut als Lücke gemeldet wird.

---

## Reihenfolge-Empfehlung

Erster Durchlauf (abgeschlossen):

1. ~~**A1** (echte Feature-Lücke, isoliert, hoher Wert).~~ ✅ erledigt.
2. ~~**B2** (trivialer DRY-Fix, warm-up).~~ ✅ erledigt.
3. ~~**B3** → dadurch **A2** (Refactor entblockt den Test; erledigt Architektur + Symmetrie in einem).~~ ✅ erledigt.
4. ~~**A3 + A4** (Negativpfad-Härtung, gut parallelisierbar über die Konstrukte).~~ ✅ erledigt.
5. ~~**B1** (Doku/Assert; voll erst mit „Option B").~~ ✅ erledigt
   (offen bleibt nur der optionale „Option B"-Umbau, erst bei echter Namens-Divergenz).

Zweiter Durchlauf (session-weise abzuarbeiten):

6. ~~**B5** → dadurch **A5** (Extraktion entblockt den Test; höchster Wert, letzter „eine Engine"-Bruch).~~ ✅ erledigt.
7. **A6** (trivialer Symmetrie-Test, warm-up).
8. **A7** (Contract-Pinning der leeren Aufrufer-Liste).
9. **B6** (Sichtbarkeits-/Hygiene-Entscheid).
10. **B7** ist bereits als „kein Handlungsbedarf" dokumentiert — nichts zu tun.

## Referenz-Dateien

- Kern: `Nav.Language.CodeAnalysis/FindSymbols/LocationFinder.cs`
- VS-Provider: `Nav.Language.ExtensionShared/GoToLocation/Provider/*`
- Test-Harness: `Nav.Language.CodeAnalysis.Tests/{CodeAnalysisTestContext,GoldenAssert,NavigationSnapshot,NavigationDirection,CommonFixtures}.cs`
- Konstrukt-Tests: `Nav.Language.CodeAnalysis.Tests/{Task,Trigger,Init,Exit,Choice}/`
