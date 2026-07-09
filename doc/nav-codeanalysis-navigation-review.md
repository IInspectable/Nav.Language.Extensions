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
| Task | ✅ Decl→WFS (+2. Task) | ✅ WFS→Decl (+2.) | — | ✅ nur Nav→C# |
| **Task → IBegin-Interface** | ✅ Decl→IBegin (+2. Task) | — | — | ✅ MissingItf |
| Trigger | ✅ (+2.) | ✅ (+2.) | — | ✅ nur Nav→C# |
| Init | ✅ Node→BeginLogic | ✅ (+Child) | ✅ CallSite→ChildBeginLogic | ✅ nur Nav→C#-Node |
| Exit | ✅ Punkt→AfterLogic | ✅ mehrdeutig (E1/E2) | ✅ After→Begin-Caller | ✅ nur Nav→C# |
| Choice | ✅ (+2.) | ✅ Logic+CallSite (+Escalate) | ✅ CallSite→Logic + Logic→Aufrufer (beidseitig) | ✅ nur Nav→C# |

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

### A3 — C#→Nav-„nicht gefunden"-Negativpfade fehlen durchgängig  ⬜

Alle `GetXxxLocations` in `LocationFinder` werfen `LocationNotFoundException`, wenn das benannte Symbol
nicht im `.nav` liegt — **kein** Test übt diese Zweige:
- `GetTaskLocations`/Task-Lookup (`LocationFinder.cs:120`, `:131`) — Annotation mit Task-Name, der nicht
  im `.nav` steht.
- `GetTriggerLocations` (`:142`), `GetChoiceLocations` (`:157`), `GetInitLocations` (`:183`),
  `GetExitLocations` (`:200`).

**How to:** je Konstrukt in `…GoToNavTests.cs` ein `…Annotation_Missing_ThrowsLocationNotFound`. Annotation
mit gefälschtem Namen bauen (entweder eine echte Annotation gegen einen ohne das Symbol generierten Kontext
laufen lassen, oder eine `NavXxxAnnotation` mit unbekanntem Namen konstruieren — den einfachsten Weg im
`AnnotationReader`/Annotation-Konstruktor prüfen). Assertion: `Throws.TypeOf<LocationNotFoundException>()`.

### A4 — Call-Site-Negativpfade fehlen  ⬜

`FindCallBeginLogicDeclarationLocationsAsync` (`:218`) und `FindCallChoiceLogicDeclarationLocationAsync`
(`:558`) haben keinen `MissingWfs`-Negativpfad (nur die genuinen Nav→C#-Pfade haben ihn). Je einen Test
über `ForeignProject()` bzw. eine Bühne ohne Ziel-`WFSBase` ergänzen (`Init/…CSharpTests`,
`Choice/…CSharpTests`).

---

## B. Konsistenz / Implementierung

### B1 — Versions-Asymmetrie nur implizit abgesichert  ⬜ (Doku/Assert, klein)

Call-Site-Pfade laufen auf der **Default-Generation** (`DefaultBeginLogicMethodName` `:54`,
`DefaultLogicMethodSuffix` `:63`), die genuinen Nav→C#-Pfade versionsrichtig aus `*CodeInfo`. Dokumentiert
(„Option B" offen, `LocationFinder.cs:46-63`). Die V2-Fixtures fangen die Gefahr **zufällig** ab (V2-Namen
== Default-Namen → Tests grün), aber **keine Assertion hält fest, dass das so ist**. Sobald eine
Sprach-Version die Namen divergieren lässt, brechen `InitCallSite_*`/`ChoiceCallSite_*` still.

**How to:** einen expliziten Kommentar/Assert an den Call-Site-Tests, der festhält „dieser Pfad verlässt
sich auf Default==V2-Namen"; ideal ein späterer Test mit einer Version, in der die Namen abweichen (erst
sinnvoll, wenn „Option B" — versionierte Nav→C#-Such-Strategie — angegangen wird).

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

## Reihenfolge-Empfehlung

1. ~~**A1** (echte Feature-Lücke, isoliert, hoher Wert).~~ ✅ erledigt.
2. ~~**B2** (trivialer DRY-Fix, warm-up).~~ ✅ erledigt.
3. ~~**B3** → dadurch **A2** (Refactor entblockt den Test; erledigt Architektur + Symmetrie in einem).~~ ✅ erledigt.
4. **A3 + A4** (Negativpfad-Härtung, gut parallelisierbar über die Konstrukte). ← **als Nächstes**
5. **B1** (Doku/Assert; voll erst mit „Option B").

## Referenz-Dateien

- Kern: `Nav.Language.CodeAnalysis/FindSymbols/LocationFinder.cs`
- VS-Provider: `Nav.Language.ExtensionShared/GoToLocation/Provider/*`
- Test-Harness: `Nav.Language.CodeAnalysis.Tests/{CodeAnalysisTestContext,GoldenAssert,NavigationSnapshot,NavigationDirection,CommonFixtures}.cs`
- Konstrukt-Tests: `Nav.Language.CodeAnalysis.Tests/{Task,Trigger,Init,Exit,Choice}/`
