# V2-Codegen-Design: CallContext, Continuation & Choices in C#

> **Finale Spezifikation** des V2-Zielbilds vor der Umsetzung (Fahrplan §6, offene Punkte §8).
> Framework-Verifikation der Laufzeit-Touchpoints ①–⑤: `doc/WFS-Spracherweiterung —
> Framework-Verifikation.md`; die `--^`-Verifikation ⑥ ist in §3.8 dokumentiert.

## 1. Motivation / Kontext

Zwei zusammenhängende Vorhaben:

1. **Continuation** (Fachlichkeit): Ein Workflow-Übergang zeigt eine View/Dialog **und** ruft direkt
   „obendrauf" den nächsten Task auf (typisch eine Messagebox) — der Task *setzt* den Übergang
   *fort*. Nav-Syntax: `Quelle --> View o-^ Task` bzw. `Quelle --> View --^ Task`.
2. **Codegen-Umstellung**: Nicht mehr **alle** `IBeginTask`-Wrapper als Parameter in die
   Logic-Methoden reichen, sondern einen **CallContext**, über den die im Nav-Workflow definierten
   Tasks aufgerufen werden.

V2 stellt **alle** Transitionen auf den CallContext um (nicht Continuation-only); die Continuation
ist eine Spezialform darauf. Und weil **Choices an mehreren Targets/Quellen** hängen können, werden
die **Choices in C#-Code** abgebildet, statt sie an jeder Quelle einzufalten.

> **Namensgebung (`Continuation`, nicht „Concat").** Das Feature heißt durchgängig **Continuation** —
> die Nav-Kanten `o-^`/`--^`, die Syntax (`ContinuationTransitionSyntax`), das Semantic Model
> (`IContinuationTransition`/`IContinuableEdge`/`ContinuationCall`) und der C#-Rückgabetyp
> (`Show{View}Continuation`) tragen alle diesen Namen. „Continuation" benennt die **Absicht** („View
> zeigen, dann in einen Folge-Task *fortsetzen*"); das frühere **`Concat`** war ein reiner
> **Mechanismus-Name**, geliehen von der Framework-Instanzmethode `.Concat(ITASK_BOUNDARY)`. Genau
> **dort** — und nur dort — bleibt `Concat` stehen: es ist der Framework-Call (nicht unser Name), auf
> den eine Continuation *lowered*. Der `concat`-**Branch** und seine Referenz-Dateinamen
> (`ConcatSample.nav` …) behalten ihren historischen Namen, weil sie real so existieren (§2.2).

Grundsatz-Festlegungen:

- **Voll-Fabrik + opaker Ergebnistyp.** ALLE Übergänge (View, Task, Continuation, Choice, Exit, End,
  Cancel) laufen über den CallContext; die Logic-Methode gibt einen opaken Typ zurück, den **nur der
  Context erzeugen kann** → illegale Übergänge werden **Compile-Fehler** statt
  Laufzeit-`InvalidOperationException`.
- **CallContext immer.** Jede Logic-Methode bekommt ihren Context, auch wenn er (noch) klein ist.
  Nutzen: Nav-Änderungen erweitern nur den Context um Methoden, brechen aber keine
  Logic-Signaturen mehr (in V1 bricht jede neue Task-Kante die Signatur, weil ein
  Wrapper-Parameter dazukommt).
- **Choice-Datenfluss: Nav-Spracherweiterung `choice X [params …]`.** Die geteilte Choice-Logic
  bekommt typisierte Parameter (analog `init … [params …]`); jede Quelle übergibt die Argumente bei
  der Delegation.
- **Migration: Default = V1, V2 opt-in via `#version 2`.** Kein Auto-Upgrade, kein Default-Flip.
  Passt bruchlos auf die vorhandene Infrastruktur (`VersionDispatchingCodeGenerator` schaltet je
  `CodeGenerationUnit.LanguageVersion`; `NavCodeGenFacts.For(Default) == V1`). Ein späteres Umlegen
  des Defaults auf V2 bleibt eine **separate Einzeiler-Entscheidung im Dispatcher** und ist **nicht**
  Teil dieses Designs.

## 2. Ausgangslage

### 2.1 Wie V1 heute generiert

Quelle: `Nav.Language/CodeGen/V1/Emitters/WfsBaseEmitter.cs`, `CodeGen/V1/CodeModel/*`.

- Begin-Wrapper der Ziel-Tasks liegen als **Felder** in der Base (`readonly IBeginXWFS _x`), injiziert
  per Konstruktor. **(Bleibt in V2.)**
- Jede Transition erzeugt eine `virtual`-Maschinerie-Methode (`Begin`/`AfterX`/`{Trigger}`) **und**
  eine **`abstract …Logic(...)`**. Die Logic bekommt heute die Wrapper als **zusätzliche Parameter**:
  `…Logic(<transition-params>, <alle erreichbaren IBeginTask-Wrapper>)`.
- Im Logic-Body ruft der Nutzer `Begin{Node}(wrapper, args)` (eine `protected`-Hilfsmethode der Base)
  → liefert `new TaskCall(NodeName, () => wrapper.Begin(args))`.
- Die Maschinerie-Methode wertet das Ergebnis in `switch(body)` über die **erreichbaren Calls**
  (`ReachableCalls`) aus: `ViewTO → GotoGUI`, `TaskCall … → OpenModalTask<…>(taskCall.BeginWrapper,
  AfterX)`, `CANCEL`, `TASK_RESULT`, `END` …
- **Choices existieren im Codegen nicht als eigenes Konstrukt.** Sie werden über die Reachability
  **aufgelöst**: die Ausgangskanten einer Choice werden zu `ReachableCalls` der *Quell*-Transition.
  Steht dieselbe Choice hinter mehreren Quellen, wird ihre Logik an jeder Quelle **erneut
  eingefaltet**.

> **Wichtig:** `TaskCall`/`OpenModalTask` sind **schon heute** die Mechanik. Der Unterschied zwischen
> V1 und V2 liegt vor allem darin, **wie** die Wrapper in die Logic gelangen (Parameter vs.
> CallContext) — nicht in der Grundmechanik der Task-Aufrufe.

### 2.2 Referenz: der `concat`-Branch

Auf dem Remote-Branch **`concat`** wurde die Continuation + CallContext bereits angefangen —
allerdings **alt**: Merge-Base (`f44b91a3`) liegt vor 145 master-Commits und vor der gesamten
**Codegen-Versionierung** auf `feature/nav-parser`. Der Branch codiert die Continuation noch in der
alten **StringTemplate-(`.stg`)-Welt** und nannte das Feature dort durchweg „Concat" (§1).

→ Der Branch ist **Referenz-Zielbild**, wird **nicht** gemergt/cherry-gepickt. Umgesetzt wird in der
heutigen Emitter/`CodeBuilder`-Welt als neues `CodeGen/V2/`. Er ist **konzeptionelle Referenz für
die Continuation-Mechanik** (`Show`/`Continuation`), **nicht** für die Code-Gestalt: das V2-Zielbild
weicht bewusst vom Branch-Output ab (dort: CallContext nur als Continuation-Vehikel,
`INavCommandBody`-Rückgabe, Records mit public `wfs`, Choices weiterhin an jeder Quelle
eingefaltet). Es gibt **neue Golden-Snapshots**; die concat-Branch-`.expected.cs` bleiben nur
konzeptionelle Referenz.

#### Concept-tragende Artefakte auf `concat`

| Ebene | Inhalt |
|---|---|
| Syntax | Tokens `--^` (Branch: `ConcatGoToEdgeKeyword` → V2 **`ContinuationGoToEdgeKeyword`**), `o-^` (`ConcatModalEdgeKeyword` → **`ContinuationModalEdgeKeyword`**); `ConcatTransitionSyntax` → **`ContinuationTransitionSyntax`** (`Edge: ContinuationEdgeSyntax?`, `TargetNode: TargetNodeSyntax?`). `ModalEdgeKeywordAlt "*->"` wird entfernt. |
| Semantic Model | `ContinuationTransition : IContinuationTransition` (Branch: `Concat…`), `IContinuableEdge` (Branch: `IConcatableEdge`), `ContinuationCall` in `Call`, Erweiterungen an `ITransition`/`Transition`/`ExitTransition`/`TaskDefinitionSymbol(+Builder)`/`EdgeExtensions`. |
| Diagnostics | Nav1020 (Source einer Continuation muss View-Node sein), Nav1021 (Target muss Task-Node sein), Nav1022 (verschiedene Views in einer Continuation nicht unterstützt) — beim Port umnummeriert nach **Nav0120/0121/0122** (§4); Fix an **Nav0222** (Reachability bei unterschiedlichen Edge-Modes). `IntroduceChoiceCodeFix` berücksichtigt die Continuation. |
| Codegen (`.stg`) | `CallContextCodeModel` + `ContinuationCodeModel` (neu), Umbauten `TransitionCodeModel`/`WfsBaseCodeModel`/`CallCodeModel(+Builder)`/`Init|Exit|TriggerTransitionCodeModel`, Template `WFSBase.stg`. |
| Referenz-Output | `Nav.Language.Tests/Regression/Tests/WFL/generated/ConcatSampleWFSBase.generated.expected.cs` (+ `IBegin…`, `IConcat…`) und `ConcatSample.nav`. |

#### Beispiel `ConcatSample.nav` (Auszug)

```
Init2               --> Choice_Init;
Choice_Init         --> View if "Foo";
Choice_Init         --> View o-^ A;
View                --> Choice_OnFoo on OnFoo;
Choice_OnFoo        --> View;
Choice_OnFoo        --> View o-^ B if "Fehler";
B:Exit              --> View o-^ C;
C:Exit              --> Exit;
```

## 3. V2-Zielbild

### 3.1 Durchgängiges Beispiel (Golden-Fall „Choice mit 3 Quellen + Continuation")

```
task Sample
{
    init Init1 [params string message];
    exit Exit;
    task A;                                  // taskref [result FooResult r1]
    task Msg;                                // Messagebox, init [params string text]
    view View;
    choice Choice_Retry [params string reason];      // NEU: params an Choice

    Init1        --> Choice_Retry;                   // Quelle 1: Init-Transition
    View         --> Choice_Retry on OnRetry;        // Quelle 2: Trigger-Transition
    A:Exit       --> Choice_Retry;                   // Quelle 3: Exit-Transition
    View         --> A o-> on OnStartA;

    Choice_Retry --> View;
    Choice_Retry --> View o-^ Msg if "Fehler";       // Continuation aus der Choice heraus
    Msg:Exit     --> View;
}
```

### 3.2 CallContext = Voll-Fabrik mit opakem `Result`

Jede Logic-Methode bekommt **genau einen** Context und gibt dessen geschachtelten, opaken
`Result`-Typ zurück:

```csharp
protected abstract Init1CallContext.Result BeginLogic(string message, Init1CallContext callContext);
```

Festlegungen:

- **Contexte sind `sealed class` mit `internal` Konstruktor** (keine Records wie im concat-Branch):
  der Nutzer kann weder Context noch Result selbst konstruieren; das WFS-Feld (`_wfs`) ist nicht
  öffentlich sichtbar.
- **Pro Context ein geschachtelter `Result`** — **`readonly struct` mit `internal` ctor**. Damit ist
  auch **Cross-Transition-Leckage** ausgeschlossen: ein aus dem `OnRetry`-Context stammendes
  Ergebnis lässt sich nicht aus `BeginLogic` zurückgeben — falscher Typ, Compile-Fehler. (Ein
  gemeinsamer Result-Typ je WFS käme mit weniger Typen aus, ließe aber genau dieses Leck offen.)
- **`Result.Unwrap()` lebt in der Kommando-Welt, nicht in der Body-Welt.** In V1 gab die Logic einen
  `INavCommandBody`-Marker zurück; in V2 liefert `Result.Unwrap()` das **fertige Framework-Kommando**
  (`IINIT_TASK` bei Init-Transitionen, `INavCommand` bei Trigger/Exit). Die Body→Kommando-Übersetzung,
  die in V1 der `switch` macht, sitzt in den Context-Methoden (§3.3).
  **`Unwrap()` ist bewusst eine Methode, keine Property** (`Body`): der Zugriff feuert deferred
  **Seiteneffekte** (GUI-Navigation) und kann **werfen** (`return default;`, s.u.) — beides schließt
  laut .NET-Design-Guidelines eine Property aus; `Unwrap()` benennt den Vorgang ehrlich und meidet die
  begriffliche Kollision mit der aufgegebenen V1-`INavCommandBody`-„Body"-Welt.
- **`Result` kapselt das Kommando *deferred* (`Func<…>`), gebaut erst beim `Unwrap()`-Aufruf.**
  Grund: die Konstruktoren von `GOTO_GUI`/`OPEN_MODAL_GUI`/`.Concat(ITASK_BOUNDARY)` haben
  **Seiteneffekte** (Framework-verifiziert, §3.8/⑤). Würde die Fabrikmethode eager bauen, feuerte
  der Effekt schon beim Aufruf — auch wenn das Ergebnis nie zurückgegeben wird. Der Thunk verschiebt
  ihn auf den `Unwrap()`-Aufruf in der Maschinerie (V1-Timing) und ist robust gegen „Fabrik aufrufen,
  aber nicht zurückgeben".

**Null-/`default`-Schutz strukturell — es gibt keinen Laufzeit-Guard.** Weil `Result` ein
`readonly struct` ist, ist `return null;` ein **Compile-Fehler** — das häufigste „ich habe einen
Zweig vergessen"-Muster fängt der Compiler. Der V1-`switch`-`default:` fing `null` **und**
unerwartete Marker ab; der Marker-Fall ist in V2 strukturell unmöglich (opaker `Result`), der
`null`-Fall ein Compile-Fehler. Übrig bleibt nur explizites `return default;` (Func == null) — das
prüft der **`Unwrap()`-Aufruf selbst** mit einer generischen, handlungsweisenden Meldung
(`"A Logic method returned default(Result); every code path must return a navigation result via the
call context."`); der Maschinerie-Methodenname ergibt sich aus dem **Stacktrace**. Kein
pro-Klasse-Helfer, keine pro-Transition-Duplikation;
`NavCommandBody.ComposeUnexpectedTransitionMessage` wird in V2 **nirgends aufgerufen**. Der Fall ist
extrem selten (nur explizites `return default;`). Leitästhetik: strukturelle Korrektheit vor
Laufzeit-Guard.

**Uniform deferred (ein `Func<>`-Feld); Context bleibt `class`.** Auch reine Kommandos
(`OPEN_MODAL_TASK`/`START_NONMODAL_TASK`/`TASK_RESULT`/`CANCEL`/`END`/`GOTO_TASK`) werden **nicht**
eager gebaut. Grund: der Massenfall (`--> View` → `GOTO_GUI`) ist seiteneffektbehaftet und **muss**
ohnehin thunken; selektiv-eager hülfe nur den selteneren reinen Kanten und kostete einen
Zwei-Nutzlast-`Result` (Kommando **oder** `Func<>` → Diskriminator/zweites Feld → fetterer,
by-value-kopierter Struct) plus Emitter-Komplexität. Bei Interaktions-Tempo ist der Alloc-Gewinn
Null. Nur der ephemere `Result` ist ein Struct (in der Logic erzeugt, sofort via `Unwrap()` aufgelöst, nie
in Feldern/Collections gehalten → ideale Value-Semantik); der CallContext bleibt `class`
(Nutzer-API-Fläche — der eine gesparte Alloc wäre immateriell).

**Allokation:** V2 allokiert **strikt mehr** als V1 — der häufigste Fall `--> View` geht von 1
Heap-Objekt (nur `GOTO_GUI`; das `ViewTO` existiert schon) auf `Result`+Thunk-Closure+`GOTO_GUI`
(~4 Objekte); nur für Task-Calls ist V2 mit V1 vergleichbar. Bei Interaktions- (nicht
Schleifen-)Tempo immateriell.

**`Result.Unwrap()` ist uniform `internal` — für Transition wie Choice.** Ein `private` `Unwrap()` ist kein
gültiger Kandidat: die klassische Nesting-Regel gilt nur *einseitig* — ein genesteter Typ erreicht
`private` Member seines Containers, **nicht umgekehrt**. Die Accessibility-Domain eines `private`
Members ist der Programmtext *seines deklarierenden Typs*; die Maschinerie in `{Task}WFSBase` liegt
außerhalb von `{Ctx}.Result` und kann dessen `Unwrap()` daher **nicht** lesen. Empirisch belegt
(`CS0122`; ein oder zwei Nesting-Ebenen ändern nichts):

```csharp
public class Wfs {                       // Container
    public sealed class Ctx {
        public readonly struct Result {
            readonly System.Func<int> _c; internal Result(System.Func<int> c)=>_c=c;
            int Unwrap() => _c();         // private → CS0122 unten; internal → ok
        }
        public Result Make()=>new(()=>1);
    }
    public int Begin()=>new Ctx().Make().Unwrap();   // Container erreicht nested private NICHT
}
```

Der internal-Leak ist **irreduzibel**: es gibt keine Accessibility-Stufe zwischen „für abgeleitete
Klasse im selben Assembly sichtbar" und „für die Container-Maschinerie erreichbar" — beides ist
`internal`. Ein Forwarding-Helfer in der Basisklasse senkte die Sichtbarkeit ebenfalls nicht unter
`internal` (er läge selbst im Container) und kostete nur einen zusätzlichen pro-Choice-Helfer;
`[EditorBrowsable(Never)]` als Milderung zieht nicht (Roslyn ignoriert es für Symbole im *selben*
Assembly/Solution — der Override-Autor sieht `Unwrap()` in IntelliSense trotzdem). Der **Footgun** —
Override-Code *kann* `Unwrap()` selbst früh aufrufen und den deferred Seiteneffekt fehlzünden — gilt
für alle `Result` gleichermaßen und ist harmlos (bewusster Fehlgriff nötig, der `Result`-ctor ist
`internal`, der Autor kommt nur über eine Context-Methode an eine Instanz) → **nur dokumentieren**,
keine Codegen-Maßnahme.

**Keine gemeinsame CallContext-Basisklasse.** Die generierten Contexte bleiben eigenständige
`sealed class`. Die Leck-Prevention (pro Context genesteter `Result` mit `internal` ctor;
Rückgabetyp **pro Context** verschieden — auch `Cancel()` liefert den kontext-eigenen `Result`)
macht **jedes bedeutungstragende Member context-lokal**. Eine Basis könnte nur `_wfs` + trivialen
ctor hochziehen (`_wfs` ist zwingend auf die konkrete `{Task}WFSBase` typisiert — die Methoden
greifen WFS-private Member wie `_b`, `After{Task}`, `{Choice}Logic`). Der scheinbar identische
Body `ShowView(to) => new(() => _wfs.GotoGUI(to))` ist **nicht** teilbar, weil `new(...)` je einen
anderen `Result` konstruiert. Vererbung brächte 2 Boilerplate-Zeilen Ersparnis gegen einen
zusätzlichen Typ + Indirektion, die die selbsterklärenden Contexte verschleiert → nicht wert.

### 3.3 Maschinerie = `Unwrap()`

Der V1-`switch(body)` in **jeder** Maschinerie-Methode (hundertfach im Korpus) tut **zwei** Dinge:
**(a) Validierung** (`default: throw`, dass die Logic nichts Undeklariertes liefert) und **(b)
Mapping Body → Kommando** — die Logic gibt einen *Body-Marker* aus der `INavCommandBody`-Welt zurück
(`ViewTO`/`TaskCall`/…), der `switch` bildet ihn auf das echte Framework-Kommando aus der
**getrennten** `INavCommand`-Welt ab (`GotoGUI(viewTO)`/`OpenModalTask(…)`). Beides gibt es in V2
nicht mehr:

- **(a)** ist durch den opaken `Result` strukturell erledigt (§3.2).
- **(b)** sitzt **in der Context-Methode**: statt einen `TaskCall`-Marker zu liefern, ruft
  `ctx.BeginB(…)` `OpenModalTask` **selbst** und verpackt das **fertige Kommando** im `Result`.

Damit kollabiert jede Maschinerie-Methode auf einen **nackten `Unwrap()`-Aufruf** — kein `switch`, kein
geteilter Choice-Dispatch, keine Marker-Laufzeittypen. Der `Unwrap()`-Aufruf wertet den Thunk aus und
feuert die Kommando-Konstruktion (inkl. etwaiger Seiteneffekte) an genau der Stelle, an der V1 den
`switch` läuft — **nach** der Logic:

```csharp
public virtual IINIT_TASK Begin(string message)
    => BeginLogic(message, new Init1CallContext(this)).Unwrap();

public virtual INavCommand OnFoo(ViewTO to) {
    to = BeforeTriggerLogic(to);                                     // Trigger-Vorlauf bleibt
    return OnFooLogic(to, new OnFooCallContext(this)).Unwrap();      // kein switch
}
```

Die Context-Methoden sind expression-bodied Einzeiler, die den Kommando-Bau in einen `Func<…>`
kapseln (§3.2 — der Thunk verschiebt den Seiteneffekt der `GOTO_GUI`/`OPEN_MODAL_GUI`/`Concat`-
Konstruktoren auf den `Unwrap()`-Aufruf; der Begin-Aufruf des Sub-Tasks bleibt zusätzlich als
`BeginTaskWrapper`-Thunk deferred):

```csharp
public Result ShowView(ViewTO to)   => new(() => _wfs.GotoGUI(to));   // plain-only: direkt Result
public Result BeginB(string b1)     => new(() => _wfs.OpenModalTask<FooResult>(() => _wfs._b.Begin(b1), _wfs.AfterB));

// … mit dem geschachtelten Result-Typ (readonly struct; Unwrap() ist internal — die Maschinerie in
// {Task}WFSBase ist Container von Result und kann dessen private Member NICHT lesen, §3.2):
public readonly struct Result {
    readonly Func<IINIT_TASK> _command;
    internal Result(Func<IINIT_TASK> command) => _command = command;
    internal IINIT_TASK Unwrap()                 // feuert die Konstruktion beim Aufruf
        => _command is null                      // nur bei explizitem `return default;`
            ? throw new InvalidOperationException(
                  "A Logic method returned default(Result); every code path must return a navigation result via the call context.")
            : _command();
}
```

### 3.4 Die Context-Fläche je Kanten-Art

Der Context ist die **vollständige, benannte Übergangs-Fläche** der Transition bzw. Choice — pro
tatsächlich vorhandener Nav-Kante eine Methode:

Die Spalte „baut (deferred)" ist das Framework-Kommando, das der `Result`-Thunk beim `Unwrap()`-Aufruf
konstruiert (§3.2/§3.3) — kein Zwischenmarker:

| Nav-Kante der Quelle | Context-Methode | baut (deferred im Thunk) |
|---|---|---|
| `-->` / `o->` / `==>` **GUI-Knoten** (View **oder** Dialog) | `Show{Node}(ViewTO)` — **mode-frei** | `GotoGUI` / `OpenModalGUI` / `StartNonModalGUI` je Edge-Mode; Modal/Nonmodal nur im Task-Kontext (§3.8/④) |
| `--> View o-^ Task` / `--> View --^ Task` (Continuation) | `Show{View}(to).Begin{Task}(…)` — **selber Einstieg**, Rückgabetyp `Continuation` | `GotoGUI(to).Concat(OpenModalTask(…)/GotoTask(…), After{Task})` je Edge-Mode (`o-^`/`--^`) |
| `-->`/`o->`/`==>` `Task` | `Begin{Task}(…)` je Init-Überladung | `GotoTask`/`OpenModalTask`/`StartNonModalTask(() => _wfs._x.Begin(…), After{Task})` |
| `-->`/`o->`/`==>` `Task` **`[notimplemented]`** | `Begin{Task}(…)` (existiert weiter) | `throw new NotImplementedException("Task {Task} is specified as [notimplemented]")` im Thunk — V1-Timing (s. Absatz unten) |
| `-->`/`o->`/`==>` `Task` **`[donotinject]`** | `Begin{Task}(IBegin{Task}WFS wrapper, …)` — **expliziter** Wrapper-Parameter | `…{mode}Task(() => wrapper.Begin(…), After{Task})` — Wrapper vom Nutzer laufzeit-selektiert (s. Absatz unten) |
| `--> Choice` (auch **Choice→Choice**) | `{Choice}({params})` | `_wfs.{Choice}Logic({params}, new(_wfs)).Unwrap()` (Forward, §3.5; rekursiv bei Choice-Ketten) |
| `--> Exit` | `Exit({result})` | `InternalTaskResult(result)` → `TASK_RESULT<T>`, castfrei (§3.8/②) |
| `--> End` | `End()` | `EndNonModal()` → `END` |
| immer | `Cancel()` | `Cancel()` → `CANCEL` |

**Die drei Positionen einer Continuation (`Quelle --> GUI o-^/--^ Task`).** Wer *was* sein darf, ist
strikt getrennt — die `Show{Node}`/`Begin{Node}`-Spalten oben spiegeln das bereits:

| Position | im Muster `Quelle --> View o-^ Task` | erlaubt | Gate |
|---|---|---|---|
| **Quelle** (links von `-->`) | `Quelle` | jede Transitionsquelle — **Init, View (Trigger), Exit, Choice** (z.B. `B:Exit --> View o-^ C`, `Choice_Retry --> View o-^ Msg`, §3.1) | — |
| **tragender Knoten** (bekommt die Continuation) | `View` | **GUI-Knoten: View *oder* Dialog** (beide `IGuiNodeSymbol` → dasselbe `Show{Node}`) — **nie** ein Task | **Nav0120** |
| **Continuation-Ziel** (rechts von `o-^`/`--^`) | `Task` | **nur Task** | **Nav0121** |

„Nur Tasks" gilt also **präzise für das Ziel** (rechts vom `o-^`/`--^`) — und das ist **strukturell**,
nicht willkürlich: `.Begin{Task}(…)` baut `.Concat(OpenModalTask/GotoTask(…))`, was eine
`ITASK_BOUNDARY` verlangt; ein View-/Choice-/Exit-Ziel hätte weder eine `Begin`-Fabrik noch einen
Task-Boundary-Command (§3.8/①). Der **tragende** Knoten dagegen ist immer ein GUI-Knoten
(View/Dialog), **nie** ein Task — er baut das `GOTO_GUI`/`OPEN_MODAL_GUI`, auf dem `.Concat(…)` sitzt.
Zwei verschiedene tragende Views in *einer* Continuation sind unzulässig (**Nav0122**).

**`[notimplemented]`/`[donotinject]` (beide korpus-real — 5 bzw. 6 echte `.nav`, kein opt-out).**
Beide werden in V2 unterstützt — ein „in `#version 2` verboten" schüfe eine nie migrierbare V1-Insel.
`[notimplemented]`: der Ziel-Task bleibt begin-bar, scheitert aber beim `Unwrap()`-Aufruf mit
`NotImplementedException` — exakt V1s Laufzeitverhalten, nur ins Thunk-Modell überführt:
`public Result BeginFoo(/*args*/) => new(() => throw new NotImplementedException("Task Foo is specified as [notimplemented]"));`.
`[donotinject]`: der Wrapper wird **nicht** injiziert (kein `_wfs._x`-Feld), also nimmt `ctx.Begin{Task}`
ihn als **expliziten Parameter** — der originalgetreue V1-Port (`BeginDoSomething(IBeginShowSomethingWFS wfs)`).
Fachlich steht `[donotinject]` für eine **Familie konkreter Implementierungen, laufzeit-selektiert**
(belegt in `DublettenMischenWFS.cs`: `_valueEditorList.SingleOrDefault(c => editor.IsAssignableFrom(c.GetType()))`)
— es *gibt* nichts zu injizieren; der explizite Parameter ist die ehrliche Signatur. Die
Laufzeit-Auswahl bleibt unverändert im `…Logic(args, ctx)`-Override. Beide Attribute bekommen je ein
isoliertes Golden-Fixture (§7).

**Mehrere Exit-Knoten:** `Exit({result})` ist **ein fixes** Member auch bei Tasks mit mehreren
`exit`-Knoten (`Wizard`: `exit Done; exit Esc;`). Der Task-Result ist einwertig (`CodeTaskResult`
pro Task), und das reale Framework `InternalTaskResult<T>(result)` (`BaseWFService.cs:236`) trägt
**keine** Exit-Identität → `--> Done`/`--> Esc` kollabieren auf dasselbe `TASK_RESULT<T>` (V1
unterscheidet sie am Child-Ende ebenfalls nicht). Kein `Exit{Node}`; Korpus: 0 Produktion /
1 Framework-Test → kein Ergonomie-Verlust.

Die `Begin{Task}`-Überladung folgt dem Edge-Mode: `-->` → `GotoTask` (init-legal aus jedem Kontext),
`o->`/`==>` → `OpenModalTask`/`StartNonModalTask` (**nur** im Task-Kontext, da nicht `IINIT_TASK`;
§3.8/④). Aus einem Init sind nur `GotoTask`/`Show{View}` (Goto-Mode)/`Exit`/`Cancel`/`Show{View}(…).Begin…`
(Continuation) zulässig. Das V1-Idiom `return to;` gibt es in V2 nicht mehr; an seine Stelle tritt
`return ctx.Show{View}(to);`.

**Task-Namensschema ist V1-Präzedenz:** `Begin{Node}` ist **nicht neu** — V1 emittiert bereits
`{BeginMethodPrefix}{TaskNodeName}` (`WfsBaseEmitter.cs:280`), aus dem lokalen `taskNode.Name`
(`BeginWrapperCodeModel.cs`), überladen über die Init-Transitionen des Ziel-Tasks. V2 ändert nur den
**Rückgabetyp** (V1: `TaskCall`-Marker + Switch-Mapping pro Kante auf `GotoTask`/`OpenModalTask`; V2:
opaker `Result`, Modus wandert **in** `ctx.Begin{Node}`). Weil der **CallContext pro Quelle** ist,
kollidiert das nicht: erreichen zwei *verschiedene* Quellen denselben Task-Knoten mit verschiedenem
Modus, sind das zwei Context-Klassen mit je eigenem `Begin{Node}`; die Anzeige-Modus-Kollision (§4)
entsteht nur, wenn *eine* Quelle zwei verschieden-modale Kanten zum selben Knoten hat.

**Casing-/Unterstrich-Regel der Member-Namen (silent, verbatim, kein Diagnostic).** Alle aus einem
Nav-Knotennamen abgeleiteten Member (`Show{Node}`/`Begin{Node}`, der Choice-Forward `{Choice}`, das
`{Choice}Logic`) übernehmen den Knotennamen **verbatim**, mit **einzig** dem V1-Präzedenzschritt
`.ToPascalcase()` (nur der erste Buchstabe groß — `StringExtensions.cs`, wie V1 ihn schon auf
Task-Knoten anwendet; fixt z.B. camelCase `hasWawiExtraLizenz` → `HasWawiExtraLizenz`).
**Unterstriche bleiben erhalten:** im realen Korpus (1910 `.nav`, 5639 `choice`) tragen 1366/4044
unique Namen einen Unterstrich, davon 1351 als bewusster `Gruppe_Rest`-Trenner mit PascalCase auf
beiden Seiten (nur 16 echt snake-ish) — Strippen verschlechterte diese Namen zu Konkatenationen,
entkoppelte `.nav`- vom C#-Namen und stünde **inkonsistent** neben den **invarianten**
`After{Task}`/`Begin{Node}`-Membern, die (wegen Cross-Version-`taskref`) die V1-Schreibweise inkl.
Unterstriche behalten müssen. Der `.ToPascalcase()`-Schritt ist korpusweit **kollisionsfrei**
(0 zusammenfallende Namen). Es gibt daher **keine** Casing-/Unterstrich-Diagnose: ein Warning feuerte
korpusweit ~1713-mal auf überwiegend absichtliche Namen (der Flood-Nachteil). Wer einen einzelnen
Namen säubern will, nutzt die bestehende Rename-Infrastruktur (`ChoiceRenameCodeFix`/
`RenameNodeCodeFix`, mit `Nav0022`-Dublettencheck). Eine spätere, **V2-gegatete Verschärfung** (dann
als **Error**, nicht als Warning) bleibt möglich, ist aber **nicht** Teil dieses Designs.

**Choices bleiben Bare-Name (kein Auto-`Choice`-Präfix).** Der Choice-Forward heißt `{Choice}(…)`
verbatim — es wird **kein** `Choice`-Präfix vorangestellt (`choice Foo;` → `ctx.Foo(…)`/`FooLogic`,
nicht `ChoiceFoo`). Der Real-Korpus präfixt selbst nicht; ein Auto-Präfix erzeugte Doppel-/Schräg-
Namen (`OnF9Choice` → `ChoiceOnF9Choice`, historische `Choice_Init` → `ChoiceChoice_Init`) und
verlangte eine fragile Strip-Heuristik. Der einzige Nutzen — Choice ist der einzige *bare-name*
Member und damit der einzige Kollisionskandidat mit den reservierten `Cancel`/`Exit`/`End`/`Result` —
ist korpus-0 und wird bereits von **Nav0124** (§4) als Fehler abgedeckt. `Show`/`Begin` tragen ein
Verb; eine Choice hat keins, und das `Logic`-Suffix trennt call (`ctx.{Choice}(…)`) von implement
(`{Choice}Logic`) bereits sauber (§3.7).

**Namenskonvention View-Kanten: EIN mode-freies Verb `Show{NodeName}`.** Der Anzeige-Modus
(`GotoGUI`/`OpenModalGUI`/`StartNonModalGUI`) ist im Nav via Edge festgelegt (`-->`/`o->`/`==>`) und
lebt nur im generierten Body, **nicht** im Namen — der Autor hat keine Wahl, ein mode-tragender
Methodenname suggerierte eine Wahl, die es nicht gibt; also gibt es kein Verb-Lexikon. Der Node-Name
bleibt als Suffix (quellenstabil, mehrere GUI-Knoten unterscheidbar). Modus optional per XML-Doc am
Member dokumentierbar. `Begin{Task}` (Task-Kanten) bleibt unverändert.

**`Show{Node}` deckt beide GUI-Knoten-Arten ab:** `IViewNodeSymbol` **und** `IDialogNodeSymbol` sind
beide `IGuiNodeSymbol` und bauen dieselben GUI-Kommandos. View vs. Dialog ist eine **Aussehens-**,
keine Verhaltens-Unterscheidung → dasselbe Verb `Show`, der Node-Name trägt den Unterschied
(`ShowHomeView`, `ShowLoginDialog`). Die **Modalität ist eine Eigenschaft des Aufrufers (der Kante),
nicht des Knotens** — genauer: des aufgerufenen Sub-Workflows („starte modal / nicht modal"),
unabhängig davon, ob er ein Fenster hochschaltet. Deshalb ersetzt die Knoten-Art den Edge-Mode
**nicht**, und die „mode-frei"-These hält auch an der Dialog-Kante. Empirischer Anker: `o->`/`==>`
ist aus `init` verboten (Modus = Aufruf-Eigenschaft, ein Init ruft nicht modal auf) — genau das
erzwingt **Nav0110** bereits (§3.8/④).

**Continuation wird durch den Rückgabetyp kodiert, nicht durch den Namen:** `Show{Node}(to)` liefert
`Result` (direkt returnbar) ohne Continuation, `Continuation` (mit `.Begin{Task}(…)`) bei einer
Continuation. Damit ist die Kernforderung **strukturell** erfüllt: bei erzwungener Continuation ist
`return ctx.Show{Node}(to);` ein Compile-Fehler; und **symmetrisch** kann keine Continuation
hinzugefügt werden, die das Nav nicht definiert. Das Typsystem spiegelt die Nav-Definition exakt (§3.6).

**Union pro Ziel-Knoten** (nicht pro Kante): hat eine Quelle mehrere Kanten zur *selben* View,
bündelt **eine** `Show{View}`-Methode deren Behandlungen. Der Rückgabetyp entsteht pro Ziel aus der
Union:

| Kanten Quelle→View | `Show{View}(to)` liefert | `return ctx.Show{View}(to);` |
|---|---|---|
| nur plain | direkt `Result` | ✓ |
| nur Continuation (**erzwungen**) | `Continuation` (kein `Result`) | ✗ → `.Begin{Task}(…)` erzwungen |
| plain **und** Continuation | Typ mit implizitem `Result` **und** `.Begin{Task}(…)` | ✓ plain / `.Begin{Task}` Continuation |

Der implizite `Result`-Operator wird genau dann emittiert, wenn eine plain-Kante existiert; je
Continuation-Kante ein `.Begin{Task}`. So spiegelt der Typ exakt die Nav-Definition („erzwungene
Continuation" = continuation-only, keine plain-Schwesterkante). Die Guards (`if/else`) sind in V2
Doku-Charakter — die
Union ist genau die Menge der vom Nav deklarierten legalen Ausgänge.

**Reservierte Namen:** die fixen Member `Cancel`/`Exit`/`End` und der genestete Typ `Result` sind
reserviert; `Show` ist Verb-Präfix (kommt nie bloß vor) und **kein** fixes Member; die
Continuation-Typen sind node-suffigiert (`Show{View}Continuation`). Weil nur der **bare-name
Choice-Forward** `{Choice}(…)` überhaupt kollidieren kann (Views/Tasks sind `Show`/`Begin`-präfixt),
deckt **eine generische Member-Kollisions-Diagnose (Nav0124, §4)** reservierte Namen, Präfix-Klasch
(`Show{X}`/`Begin{X}`-Choice trifft Knoten `X`) und Modus-Kollision einheitlich ab — kein eigener
Reservierte-Namen-Analyzer, kein stilles Namens-Mangling. Ihr Eigenwert ist der **still
kompilierende Overload**, den `csc` verschweigt. Korpus: 0 Kollisionen jeder Art.

### 3.5 Choices in C#: Context + abstrakte Logic

Eine Choice wird zu **zwei einmal generierten Bausteinen** — egal, wie viele Quellen auf sie zeigen:
der Context baut die finalen Kommandos der Choice-Ausgänge, die abstrakte Logic gibt sie fertig
zurück; die Quellen forwarden. Einen geteilten Dispatch gibt es nicht (§3.3).

```csharp
// Baustein 1: der Choice-Context — baut die finalen Kommandos der Choice-Ausgänge.
// Choice_Retry hat ZWEI Kanten zur selben View (--> View  und  --> View o-^ Msg) → EINE
// mode-freie Show-Methode mit Union-Rückgabetyp (§3.4).
protected sealed class Choice_RetryCallContext {

    readonly SampleWFSBase _wfs;
    internal Choice_RetryCallContext(SampleWFSBase wfs) => _wfs = wfs;

    /// Opaker Ergebnistyp: nur dieser Context kann ihn erzeugen; das Kommando wird deferred
    /// gebaut (Thunk, §3.2). Das Unwrap()-Ergebnis ist init-legal (IINIT_TASK), da Choice_Retry aus einem Init
    /// erreichbar ist und das Semantic Model init-legale Ausgänge erzwingt (§3.8/④).
    public readonly struct Result {
        readonly Func<IINIT_TASK> _command;
        internal Result(Func<IINIT_TASK> command) => _command = command;
        // internal: schon der Container-Zugriff der Maschinerie verlangt internal (nested private
        // ist für den Container unerreichbar, §3.2); der Geschwister-Forward braucht dieselbe
        // Stufe, kein Mehr.
        internal IINIT_TASK Unwrap()
            => _command is null
                ? throw new InvalidOperationException(
                      "A Logic method returned default(Result); every code path must return a navigation result via the call context.")
                : _command();
    }

    // Beide Choice_Retry --> View-Kanten (plain + o-^ Msg) → EINE Methode, Union der Behandlungen.
    public ShowViewContinuation ShowView(ViewTO to) => new(_wfs, to);
    public sealed class ShowViewContinuation {
        readonly SampleWFSBase _wfs; readonly ViewTO _to;
        internal ShowViewContinuation(SampleWFSBase wfs, ViewTO to) { _wfs = wfs; _to = to; }

        // plain-Kante (--> View) existiert → direkt als Result returnbar:
        public static implicit operator Result(ShowViewContinuation v)
            => new(() => v._wfs.GotoGUI(v._to));

        // Continuation-Kante (--> View o-^ Msg) existiert → Continuation-Rückgabetyp (§3.6):
        public Result BeginMsg(string text) =>
            new(() => _wfs.GotoGUI(_to).Concat(_wfs.OpenModalTask<MsgResult>(() => _wfs._msg.Begin(text), _wfs.AfterMsg)));
    }

    public Result Cancel() => new(() => _wfs.Cancel());
}

// Baustein 2: die ENTSCHEIDUNG liegt einmal beim Nutzer:
protected abstract Choice_RetryCallContext.Result Choice_RetryLogic(
    string reason, Choice_RetryCallContext callContext);
```

**Die Delegation** ist eine Methode im Context jeder Quelle und läuft **synchron**: sie ruft die
abstrakte Choice-Logic direkt auf und **forwardet** deren fertiges Kommando (`Unwrap()`) in den eigenen
`Result` — kein Marker, kein geteilter Dispatch:

```csharp
protected sealed class Init1CallContext {
    readonly SampleWFSBase _wfs;
    internal Init1CallContext(SampleWFSBase wfs) => _wfs = wfs;

    public readonly struct Result {                       // Unwrap() internal (Container WFSBase
                                                          // erreicht nested private nicht, §3.2)
        readonly Func<IINIT_TASK> _command;
        internal Result(Func<IINIT_TASK> command) => _command = command;
        internal IINIT_TASK Unwrap()
            => _command is null
                ? throw new InvalidOperationException(
                      "A Logic method returned default(Result); every code path must return a navigation result via the call context.")
                : _command();
    }

    // Init1 --> Choice_Retry: Choice-Logic aufrufen und Kommando durchreichen (deferred:
    // die Choice-Entscheidung UND der Kommando-Bau feuern erst beim Unwrap()-Aufruf)
    public Result Choice_Retry(string reason) =>
        new(() => _wfs.Choice_RetryLogic(reason, new(_wfs)).Unwrap());

    public Result Cancel() => new(() => _wfs.Cancel());
}

// Maschinerie: nackter Unwrap()-Aufruf (§3.3)
public virtual IINIT_TASK Begin(string message)
    => BeginLogic(message, new Init1CallContext(this)).Unwrap();

protected abstract Init1CallContext.Result BeginLogic(string message, Init1CallContext callContext);
```

Der `Result`-Typ der Quelle ist bewusst ein **eigener** (nicht der Choice-`Result`): das Forwarden
re-boxt und erhält so die Leck-Verhinderung (ein `Choice_RetryCallContext.Result` lässt sich **nicht**
direkt aus `BeginLogic` zurückgeben — die Quelle *muss* durch `ctx.Choice_Retry(…)`).

Die Guards (`if "Fehler"`/`else`) an Choice-Kanten behalten ihren heutigen **Doku-Charakter** — die
Entscheidung trifft frei formulierter Nutzer-Code in der Choice-Logic, nicht der Generator.

**Verschachtelte Choices `Choice_A --> Choice_B`: rekursives Forwarding.** In Nav legal; die
Reachability löst Choice-Ketten rekursiv auf (`EdgeExtensions.GetReachableCallsImpl`, mit
Zyklenschutz). V2 faltet **nicht** platt (das brächte die von V2 eliminierte Duplikation für Ketten
zurück), sondern forwardet **eine Ebene tiefer**: `Choice_A`s Context bekommt `{Choice_B}({params})`
→ `_wfs.Choice_BLogic({params}, new(_wfs)).Unwrap()` (deferred) — dieselbe Mechanik wie
Transition→Choice. **Anti-Bloat bleibt transitiv** (jede `Choice_XLogic` existiert einmal), und die
Init-Legalitäts-Typisierung greift automatisch (ist `Choice_A` init-erreichbar, ist `Choice_B` es
transitiv auch → beider `Result.Unwrap()` liefert `IINIT_TASK`). Ein Choice-**Zyklus** ergäbe sich
gegenseitig referenzierende Context-Methoden (kompiliert sauber); ob er zur Laufzeit kreist,
entscheidet allein die Nutzer-Logik — kein Codegen-Problem.

### 3.6 Continuation: derselbe `Show{View}`-Einstieg, Rückgabetyp `Continuation`

Die Continuation ist **kein eigener Einstieg**: dieselbe `Show{View}`-Methode liefert statt `Result`
eine `Continuation`, sobald die Kante eine Continuation trägt. Deren `Begin{Task}(…)` baut das
Continuation-Kommando
**deferred** im `Result`-Thunk: `GotoGUI(to).Concat(OpenModalTask(…, After{Task}))` — die Mechanik
sitzt vollständig in der Context-Methode, ohne Marker-Typ und ohne Sub-Switch. Der Rücksprung aus
dem angehängten Task erfolgt wie in V1 über `After{Task}`. Wichtig: `GotoGUI` **und**
`.Concat(ITASK_BOUNDARY)` haben Konstruktor-Seiteneffekte (§3.8/⑤) — daher zwingend im Thunk, nicht
eager. `OpenModalTask` → `OPEN_MODAL_TASK : ITASK_BOUNDARY` wählt am Framework die Überladung
`Concat(ITASK_BOUNDARY)` → `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : IINIT_TASK`. **`.Concat(…)` ist
die einzige neue Framework-API** (§3.8).

**Rückgabetyp pro Ziel-View (Union, §3.4):** existiert **nur** eine Continuation-Kante zu dieser View
(keine plain-Schwester), fehlt der implizite `Result`-Operator → `return ctx.Show{View}(to);` ist
ein Compile-Fehler, der Autor **muss** `.Begin{Task}(…)` anhängen. Existiert zusätzlich eine
plain-Kante, ist beides zulässig (Nutzer-Beispiel `Choice_Retry`, §3.5). So spiegelt der Typ exakt
die Nav-Definition — „erzwungene Continuation" = continuation-only.

Die `static implicit operator Result(Show{View}Continuation)`-Lösung hat eine bekannte, akzeptierte
Schwäche: der plain-Pfad ist unsichtbar, und im continuation-only-Fall meldet `csc` nur einen
generischen `CS0029`. Milderung: sprechender Continuation-Typname + XML-Doc am Member („mit
`.Begin{Task}(…)` fortsetzen"), plus ein Ergonomie-/Golden-Test, der den continuation-only-`CS0029`
als erwartetes Verhalten festschreibt. Der unsichtbare Pfad ist der harmlose (plain); `.Begin{Task}` ist auf dem Rückgabewert
sichtbar.

**`o-^` UND `--^` werden unterstützt.** Der Continuation-Builder wählt je Edge-Mode `OpenModalTask`
(`o-^`) bzw. `GotoTask` (`--^`) als Concat-Boundary — dieselbe Mechanik wie bei Plain-Task-Kanten
(`GOTO_TASK ∈ ITASK_BOUNDARY` → dieselbe `Concat(ITASK_BOUNDARY)`-Überladung, dasselbe
`TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY`-Ergebnis). Beide Formen sind am realen Framework verifiziert
(§3.8/⑥).

### 3.7 Nutzer-Code (der Elegance-Payoff)

```csharp
// Entscheidung EINMAL:
protected override Choice_RetryCallContext.Result Choice_RetryLogic(
        string reason, Choice_RetryCallContext ctx) {
    if (reason is null) return ctx.ShowView(CreateViewTO());          // plain (implizit → Result)
    return ctx.ShowView(CreateViewTO()).BeginMsg(reason);             // Messagebox obendrauf (Continuation)
}

// Drei Quellen, drei Einzeiler — typisiert, compile-sicher:
protected override Init1CallContext.Result BeginLogic(string message, Init1CallContext ctx)
    => ctx.Choice_Retry(reason: null);

protected override OnRetryCallContext.Result OnRetryLogic(ViewTO to, OnRetryCallContext ctx)
    => ctx.Choice_Retry(reason: to.LastError);

protected override AfterACallContext.Result AfterALogic(FooResult r1, AfterACallContext ctx)
    => ctx.Choice_Retry(reason: r1.Error);
```

In V1 stünde die `reason`-Fallunterscheidung dreimal im Nutzer-Code (an jeder Quelle eingefaltet);
in V2 einmal, und die Quellen liefern nur noch ihre jeweiligen Daten zu.

**`Logic`-Suffix an den Override-Methoden bleibt.** Zwar bildete die Logic-Methode durch ihren
zusätzlichen `callContext`-Parameter auch ohne Suffix eine saubere Überladung neben der
Maschinerie-Methode (compiler-eindeutig, keine Kollision), aber der Suffix trägt drei menschliche
Vorteile: (a) er trennt beim **Choice** die Rolle **call** (`ctx.Choice_X(…)`, an der Quelle) von
**implement** (`Choice_XLogic(…)`, die Entscheidung) — ohne Suffix hieße beides `Choice_X`; (b) die
Maschinerie liest nicht als Selbst-Rekursion (`Begin(m) => BeginLogic(m, ctx).Unwrap()` statt scheinbar
`Begin(m) => Begin(m, ctx)`); (c) klarere Fehlerdiagnose bei Signatur-Tippfehlern im Override. Der
Gewinn „sauberere Namen" wiegt das nicht auf, da die Override-Methoden die Haupt-Berührungsfläche
des Nutzers sind.

### 3.8 Laufzeit-Bausteine & Rückgabetyp-Regel

Die neue Laufzeit-Fläche ist minimal — **`.Concat(…)` ist die einzige neue Framework-API**; es gibt
keine Marker-Laufzeittypen und keinen geteilten Dispatch. Alle Laufzeit-Touchpoints sind am
**realen** Framework (`Framework.NavigationEngine`, nicht Stubs) verifiziert — ①–⑤ mit Details und
Quellen in `doc/WFS-Spracherweiterung — Framework-Verifikation.md`:

- **① `.Concat(…)`** — einzige neue Framework-API, öffentliche **Instanzmethode** auf `GOTO_GUI`
  (keine Extension; überladen auch auf `OPEN_MODAL_GUI`/`TWO_STEP_IINIT_TASK`). Parameter sind die
  **Tagging-Interfaces** `INOT_A_TASK_BOUNDARY`/`ITASK_BOUNDARY` (nicht `INavCommand` allgemein).
  `GotoGUI(to).Concat(OpenModalTask(…))` → `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : TWO_STEP, IINIT_TASK`
  → `IINIT_TASK` **und** `INavCommand`.
- **② Exit ohne Cast** — `InternalTaskResult<T>` liefert real `TASK_RESULT<T>` (ein Objekt vereint
  `INavCommandBody` **und** `IINIT_TASK, ITASK_BOUNDARY, NavCommand`). Die `ctx.Exit`-Fabrik konkret
  als `TASK_RESULT<T>` typisieren → **statischer Upcast, kein Cast, keine kommando-typisierte
  Schwester nötig**. (Nur an einem `INavCommandBody`-typisierten Zwischenwert wäre `(TASK_RESULT)…`
  nötig — dann laufzeitsicher.)
- **③ `ctx.Cancel()`** — `_wfs.Cancel()` ist eine echte **Factory-Methode** (`new CANCEL()`); `CANCEL`
  ist `IINIT_TASK` **und** `INavCommandBody`. Kein Singleton/Property/`EscapeTask`.
- **⑤ Seiteneffekte in Konstruktoren → Thunk zwingend:** `GOTO_GUI`, `OPEN_MODAL_GUI` und
  `.Concat(ITASK_BOUNDARY)` feuern im **Konstruktor** Seiteneffekte (GUI-Navigation bzw.
  `ExecuteCallResult`); nur die feld-speichernden Commands (`OPEN_MODAL_TASK`/`START_NONMODAL_TASK`/
  `TASK_RESULT`/`CANCEL`/`END`/`GOTO_TASK`) sind rein. Deshalb kapselt `Result` den Bau **deferred**
  (§3.2) — der Effekt feuert erst beim `Unwrap()`-Aufruf, wie in V1.
- **⑥ `--^` (Goto-Continuation) am Framework verifiziert.** `ExecuteCallResult` (`BaseWFService.cs:263`)
  ist **typ-agnostisch** (polymorphe `while (result is NavCommand)`-Schleife) → `GOTO_TASK` als
  Concat-Boundary wird ausgeführt, **nicht** per Typ-`switch` abgelehnt. `context.GotoTask`/
  `OpenModalTask` (`ServerExecutionContext.cs:264/270`) pushen **denselben** `After{Task}`-Rückkehr-
  Frame; einziger Unterschied ist der modale Node-Proxy, den nur `OpenModalTask` anlegt.
  `GOTO_TASK.Execute`/`OPEN_MODAL_TASK.Execute` sind strukturelle Zwillinge
  (`context.{Mode}Task(_after, _args); return _wrapped();`). Semantik: `--^` = „View zeigen, per
  Goto in den Sub-Task voll-navigieren, über `After{Task}` zur View zurück"
  (Drill-down-mit-Rückkehr); `o-^` = modaler Overlay-Zwilling — beides legitime UX.
  **Rest-Risiko (Framework-Domäne):** der exakte `GotoGUI(view).Concat(GotoTask(…))`-TWO_STEP-Pfad
  ist framework-seitig un-exerziert, und der `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY`-Ctor (`:8–13`)
  trägt einen Framework-Autor-TODO über nicht durchdachte Stack-Effekte — siehe offene Punkte (§8).
  Das Nav-Repo testet **kein Laufzeitverhalten** (nur bis Codegen); die Laufzeit-Korrektheit ist
  Framework-Zuständigkeit. Nav-seitig steht der quellcode-verifizierte Befund + Golden gegen
  erweiterte Stubs (§6/§7). Quellen: `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY.cs:8–13`,
  `BaseWFService.cs:263 (ExecuteCallResult) / :167 (GotoTask) / :202 (OpenModalTask)`,
  `GOTO_TASK.cs:27–30`, `OPEN_MODAL_TASK.cs:18–21`, `ServerExecutionContext.cs:264/270`.

**Rückgabetyp-Regel für `Result.Unwrap()`:**

- **Transition-Context:** `IINIT_TASK` bei Init-Transitionen, `INavCommand` bei Trigger/Exit —
  entspricht exakt dem Maschinerie-Rückgabetyp, also `return …Logic(…).Unwrap();` **ohne Cast**.
- **Choice-Context:** `IINIT_TASK`, sobald die Choice aus **irgendeiner** Init-Quelle erreichbar ist,
  sonst `INavCommand`. Weil `IINIT_TASK : INavCommand`, ist ein init-typisierter Choice-`Result.Unwrap()`-Rückgabewert
  auch von Trigger-/Exit-Quellen zuweisbar (Forward in §3.5).
- **④ Init-Legalität ist eine echte Einschränkung, keine Selbstverständlichkeit.** Das Framework macht
  `IINIT_TASK` **gezielt selektiv**: `OPEN_MODAL_TASK`/`OPEN_MODAL_GUI`/`START_NONMODAL_TASK`/`END` sind
  **nicht** `IINIT_TASK` (nur `GOTO_GUI`/`GOTO_TASK`/`TASK_RESULT`/`CANCEL`/`GotoGUI(…).Concat(…)`;
  Framework-Regel: „a task can only start with GOTO_TASK, GOTO_GUI or TASK_RESULT"). Ein
  init-typisierter `Result.Unwrap()`-Rückgabewert ist also nur baubar, wenn **alle** aus einem Init erreichbaren Ausgänge
  in dieser Menge liegen. Das **erzwingt das Semantic Model** (§4).
- **④a `--> End` aus Init ist die konkrete Lücke — empirisch verifiziert.** `END : NavCommand,
  ITASK_BOUNDARY, INavCommandBody` (bestätigt an `END.cs`) trägt **kein** `IINIT_TASK`. Der V1-Generator
  emittiert für `init --> end` aber `public virtual IINIT_TASK Begin() { … case END _: return
  EndNonModal(); }` (`WfsBaseEmitter.cs:194`+`:333–337`), und `EndNonModal()` liefert `END` → die
  Zuweisung `END → IINIT_TASK` ist **CS0266**. V1 erzeugt für diesen (im Korpus offenbar nicht
  vorkommenden) Fall also bereits **nicht-kompilierenden** Code. **Nav0110 fängt es *nicht*:** `--> End`
  ist eine **Goto-Mode**-Kante und passiert Nav0110 (das nur `EdgeMode != Goto` in Init-Reichweite
  verbietet) — die `IINIT_TASK`-Mitgliedschaft ist ein *anderes* Kriterium als der Edge-Mode, und für
  `End` fallen beide auseinander. Die Lücke schließt der Analyzer **Nav0118** (§4, umgesetzt,
  versionsUNabhängig). Ein reiner `nav.exe`-Codegen-Erfolg beweist die Kompilierbarkeit **nicht** —
  erst `csc` gegen das Framework ist das maßgebliche Gate (Roslyn/IDE-verifiziert: `CS0266: Cannot
  implicitly convert type '…IWFL.END' to '…IWFL.IINIT_TASK'` an `return EndNonModal();`).

## 4. Syntax, Semantic Model & Completion (versionsunabhängig)

Syntax und Semantic Model sind **nicht** versionsspezifisch und werden einmal für alle
Codegen-Versionen vorwärts portiert — **nach** dem Design; die Completion (§4.1) bleibt ebenfalls
ein einziger Service und filtert nur versionsbewusst:

- **Syntax:** Tokens `--^`/`o-^`, Grammatik/Lexer/Parser, `ContinuationTransitionSyntax`, generierte
  Visitor/Walker; der alternative Modal-Edge-Token `*->` (`ModalEdgeKeywordAlt`) wird entfernt.
  **Neu:** `[params …]`-Klausel an der `choice`-Deklaration, analog `init` (Wiederverwendung
  `ParameterListSyntax`).
- **Semantic Model:** `IContinuationTransition`/`ContinuationTransition`, `IContinuableEdge`,
  `ContinuationCall` in `Call`, Edge-Mode-Behandlung, Parameter am `IChoiceNodeSymbol`; **Nav0222**-Fix
  (Reachability bei unterschiedlichen Edge-Modes).
- **Continuation-Struktur-Analyzer Nav0120/0121/0122:** Continuation-Quelle muss GUI/View-Knoten sein
  (**Nav0120**), Continuation-Ziel muss Task-Knoten sein (**Nav0121**), verschiedene Views in einer
  Continuation nicht unterstützt (**Nav0122**). Die concat-Branch-Nummern Nav1020/1021/1022 tragen
  Error-Semantik im `Nav1xxx`-Band, das in diesem Repo **strikt DeadCode/Warning** ist — Error gehört
  ins 01xx-Strukturband; da das Feature nie ausgeliefert wurde (der Branch ist reines
  Referenz-Zielbild), besteht keine Kompatibilitätsbindung an die alten Nummern.
- **Init-Legalitäts-Analyzer (aus Framework-Verifikation ④).** Aus einem Init erreichbare
  Ausgangskanten dürfen nur Kommandos der **`IINIT_TASK`-Menge** erzeugen (`GotoGUI`/`GotoTask`/
  `TASK_RESULT`/`CANCEL`/`GotoGUI(…).Concat(…)`); `o->`/`==>` direkt aus einem Init (→ `OPEN_MODAL_GUI`/
  `OPEN_MODAL_TASK`/`START_NONMODAL_TASK`, **nicht** `IINIT_TASK`) sowie `--> End` aus init-Reichweite
  werden abgelehnt — sonst ist der `IINIT_TASK`-typisierte `Result.Unwrap()`-Rückgabewert nicht baubar (§3.8).
  Arbeitsteilung: **Nav0110** deckt den *Edge-Mode*-Teil ab (`o->`/`==>` aus Init-Reichweite =
  `EdgeMode != Goto` → Fehler), **nicht** aber `--> End` (Goto-Mode-Kante, `END` ist kein
  `IINIT_TASK` → CS0266, §3.8/④a); **Nav0222** trägt nichts bei (nur Edge-Mode-Konsistenz). Die
  End-Lücke schließt der **umgesetzte, versionsUNabhängige Analyzer Nav0118**
  (`Nav0118EndNode0NotAllowedBecauseReachableFromInit1`, Severity Error): aus einem Init per Goto
  erreichbare End-Knoten (direkt oder über Choices) werden abgelehnt; Nav0110 blieb unangetastet.
  Zusammen decken **Nav0110 + Nav0118** die `IINIT_TASK`-Menge für den V1-Command-Satz vollständig
  ab (View→`GOTO_GUI`, Task→`GOTO_TASK`, Exit→`TASK_RESULT`, Cancel→`CANCEL` sind init-legal; nur
  `--> End`→`END` war die Lücke). Modal/Nonmodal/Modal-GUI nur *innerhalb* eines Tasks (erst
  `GotoGUI`, dann `.Concat(…)`).
- **Init-Signatur-Eindeutigkeit: Analyzer Nav0119 (umgesetzt, versionsUNabhängig, V1-geerbt).** Zwei
  Init-Knoten desselben Tasks dürfen keine **identische Parameter-Typ-Signatur** haben. `Begin{Node}`
  (V2) bzw. `IBegin{Task}WFS.Begin` (V1, `IBeginWfsEmitter.cs`) wird über die Init-Transitionen
  **überladen** — der Init-Knotenname landet nur als Annotation, nicht im Methodennamen. Zwei
  signaturgleiche Inits → `Begin(sig)` doppelt → **CS0111** (dupliziertes Member): ein latenter,
  nicht-kompilierender Fall wie `--> End` (§3.8/④a). **Korpus-Beweis: 0 Verstöße** (1913 `.nav`,
  2804 task/taskref-Blöcke, 3487 Init-Knoten, 419 Blöcke mit >1 Init — kein einziger mit doppelter
  Signatur) → der Analyzer ist korpussicher und bricht keine bestehende `.nav`. Umgesetzt als
  **Nav0119** (`Nav0119InitNode0HasSameSignatureAsInitNode1`, Severity Error, Sibling/Klon-Struktur
  von Nav0118, Auto-Discovery): Signatur = geordnete Parameter-Typen (Whitespace entfernt,
  `List<int>` ≡ `List< int >`; Namen irrelevant); pro Task erste Signatur = Referenz, jede weitere
  Kollision wird am Identifier des Duplikats gemeldet. Greift auch für **edge-lose** Inits (der
  V1-Generator emittiert `Begin()` für *jeden* Init-Knoten, `CodeModelBuilder.GetInitTransitions`).
- **Versions-Gate:** Continuation-Kanten (`o-^` **und** `--^`) und Choice-`[params]` sind nur ab
  `#version 2` erlaubt — in V1-Units meldet das **bestehende Nav5000** („requires Nav language
  version {1}"), **keine** neue ID, kein neuer Code. Registriert werden sie als die **ersten**
  `NavLanguageFeature`-Werte (z.B. `Continuation`, `ChoiceParameters`; `RequiredVersion = Version2`)
  im bereits vorhandenen `NavLanguageFeatures`-Gate — das Enum ist heute leer und dokumentiert genau
  diesen Vorbehalt. Dieselbe Gate-Autorität speist auch die Completion (§4.1).
- **Generische Member-Kollisions-Diagnose Nav0124.** **Eine** Diagnose statt getrennter Analyzer für
  reservierte Namen und Anzeige-Modus-Kollision: berechnet aus der **generierten Member-Menge** einer
  Quelle, verankert an der `.nav`-Deklaration/-Kante des Verursachers; Severity **Error**,
  versions-gated (nur wo V2-Contexte entstehen). Deckt einheitlich ab:
  - **Reservierte Namen:** ein Choice-Forward `{Choice}(…)` (der einzige **bare-name** Member — Views/
    Tasks sind `Show`/`Begin`-präfixt) namens `Cancel`/`Exit`/`End`/`Result`.
  - **Präfix-Klasch:** ein Choice namens `Show{X}`/`Begin{X}`, der auf den präfixten Member eines
    gleichnamigen GUI-/Task-Knotens `X` derselben Quelle trifft.
  - **Anzeige-Modus-Kollision:** eine Quelle mit zwei Kanten zum **selben** Ziel bei
    **unterschiedlichem Anzeige-Modus** (goto vs. modal vs. nonmodal, beide ohne Continuation) →
    gleiche `Show{Node}(ViewTO)`- bzw. `Begin{Node}(…)`-Signatur, nicht über Rückgabetyp lösbar
    (anders als plain+Continuation, §3.4-Union — das ist **keine** Kollision). Fachlich vermutlich
    ohnehin sinnlos/illegal — die Diagnose deckt es auf, kein Codegen-Sonderfall.

  Ihr **Eigenwert** ist der **still kompilierende Overload** (unterschiedliche Signaturen), den `csc`
  **nicht** meldet — die harten Fälle (CS0102/CS0111) fängt der Compiler zwar, aber ein `csc`-Fehler im
  *generierten* Code ist kaum auf die `.nav`-Stelle rückführbar; die Nav-Diagnose ist die **frühe,
  zeigende** Meldung. Zukunftssicher (keine enumerierte Sonderfall-Liste), kein stilles
  Namens-Mangling. **Korpus: 0 Kollisionen** (Choices namens `Cancel`/`Exit`/`End`/`Result`: 0/5637;
  `Show*`/`Begin*`-Choices existieren zwar zahlreich, kollidieren aber mit keinem gleichnamigen
  Knoten) → frühwarnende Versicherung, kein häufiger Fall — der Wert liegt in der Rückführbarkeit,
  nicht der Frequenz.

### 4.1 Completion — ein versionsunabhängiger Service, versions*bewusst* gefiltert

Code-Completion braucht **keinen** V1/V2-Split (keinen Dispatcher wie der Codegen, §5). Sie bleibt
**ein** versionsunabhängiger Service (`NavCompletionService`, VS + LSP geteilt, syntaxbaum-getrieben),
wird aber **versionsbewusst**: die versionsgateten Vorschläge — die Continuation-Kanten `o-^`/`--^`
und die Choice-`[params]`-Klausel — werden nur angeboten, wenn die **effektive** `#version` sie
zulässt. Sonst böte die Completion Konstrukte an, die sofort **Nav5000** werfen — ein Selbstwiderspruch.

**Dieselbe Autorität wie das Gate, kein dupliziertes Versionswissen.** Die Mindestversion je Feature
lebt in **einer** Quelle: dem `NavLanguageFeature`/`NavLanguageFeatures`-Gate
(`NavLanguageFeatures.RequiredVersion`/`IsAvailable`), das auch **Nav5000** (`ReportIfUnavailable`)
speist (§4, Versions-Gate). Die Completion ruft dieselbe
`IsAvailable(feature, unit.LanguageVersion)`-Abfrage, bevor sie einen gateten Vorschlag aufnimmt:
`VisibleEdgeKeywordItems` gatet `--^`/`o-^`, die Choice-Vorschläge gaten `[params]`. Das spiegelt
den **bereits bestehenden** Präzedenzfall — die Completion zieht für den `#version`-Werte-Slot schon
heute `NavLanguageVersion.SupportedVersions`, dieselbe Tabelle, die **Nav5001** validiert. Die
effektive Version steht ihr über `CodeGenerationUnit.LanguageVersion` (Ergebnis von
`ResolveLanguageVersion`) bereits zur Verfügung — `GetCompletions(CodeGenerationUnit unit, …)` bekommt
das Semantic Model, nicht nur den Syntaxbaum.

Damit fällt die Completion sauber in den §4-„versionsunabhängig"-Topf (**ein** Service, keine
Versions-Weiche) plus einen reinen Feature-Filter — es gibt keine „V1-Completion"/„V2-Completion".
Bewusst **nicht** umgesetzt: den gateten Token in einer V1-Unit doch anzubieten und per Fixup
`#version 2` einzuziehen — das widerspräche dem „kein Auto-Upgrade"-Grundsatz (§1); der stille
Filter passt besser. Umsetzung als Teil von Fahrplan-Schritt 2/3 (§6), wo Tokens und
Choice-`[params]` ohnehin entstehen.

## 5. Architektur-Einbettung (`feature/nav-parser`) & Anti-Bloat

Versionierungs-Infrastruktur steht bereits:

- `CodeGen/Shared/` — `CodeBuilder`, `CodeInfo/*`, `Facts/` (`CodeGenInvariants`, `ICodeGenFacts`,
  `NavCodeGenFacts`).
- `CodeGen/V1/` — `CodeGeneratorV1`, `CodeModel/*`, `Emitters/*`.
- `CodeGen/VersionDispatchingCodeGenerator.cs` — Versions-Weiche.

→ **Codegen** ist versionsspezifisch: neues **`CodeGen/V2/`** (eigene `CodeModel/*` + `Emitters/*` im
`CodeBuilder`-Stil), über den Dispatcher geschaltet.

**Invarianten:**

- **Schnittstellen-Invariante hält:** Die public Maschinerie-Signaturen
  (`Begin(…)`/`On{Trigger}(ViewTO)`/`After{Task}(…)`) bleiben V1-identisch → `I{Task}WFS` /
  `IBegin{Task}WFS` unverändert → **Cross-Version-`taskref` funktioniert weiter** (eine V1-Unit
  kann eine V2-Unit referenzieren und umgekehrt; konsumiert werden nur die invarianten
  IBegin-Interfaces).
- Begin-Wrapper-Felder + Konstruktoren wie V1.

**Anti-Bloat auf Generator-Seite — ein Modell, zwei Verwender:**

- **EIN `CallContextCodeModel`** (Name + Liste von Callable-Modellen: View/Begin/Show-Continuation/
  Choice-Forward/Exit/End/Cancel) beschreibt Transitions- **und** Choice-Kontexte — beide sind
  „Aufruffläche einer Kanten-Quelle" und unterscheiden sich nur in Namensquelle und Parametern.
- **EIN `CallContextEmitter`** (Context-Klasse + `Result` + Continuations). Einen separaten
  Dispatch-/Switch-Emitter gibt es nicht: die Maschinerie-Methode ist nur der `Unwrap()`-Aufruf (§3.3),
  ein triviales Template-Fragment. Der in V1 dreifach fast identische Switch-Block in
  `WfsBaseEmitter.WriteInit/Exit/TriggerTransition` hat in V2 **kein Gegenstück**.
- `EmitterCommon` (Header/Usings/Annotations) wird V1/V2-geteilt (nach `CodeGen/Shared/` heben; V1
  referenziert weiter, byte-identisches V1-Verhalten per Regression abgesichert).
- **Anti-Bloat im generierten Code:** kein Dispatch-Switch (weder pro Quelle noch geteilt); die
  Choice-Logik des Nutzers existiert genau einmal, die Quellen forwarden als Einzeiler. Der
  `{Task}WFS`-One-Shot-Stub (`WfsOneShotEmitter`) generiert die neuen Override-Signaturen.

## 6. Fahrplan

Jeder Umsetzungs-Step mit Review + Build/Test + gelieferter Commit-Message (kein Selbst-Commit).

1. **Golden-`.nav`-Fälle festschreiben** — CallContext-Grundform, Continuation, Choice-mit-3-Quellen
   aus §3.1. Die Fixtures für Grundform und Continuation sind noch zu schreiben (§8 Nr. 2).
2. **Syntax vorwärts portieren** — Tokens/Parser/`ContinuationTransitionSyntax`, **Choice-`[params]`**,
   Visitor/Walker; Parser-/Syntax-Tests.
3. **Semantic Model vorwärts portieren** — `ContinuationTransition`/`IContinuableEdge`/`ContinuationCall`,
   Choice-Parameter, Analyzer **Nav0120/0121/0122** (Continuation-Struktur) + **Nav0124** (generische
   Member-Kollision) + Nav0222-Fix + Versions-Gate über bestehendes **Nav5000** samt
   `NavLanguageFeature`-Registrierung (`Continuation`/`ChoiceParameters`) + **versionsbewusste
   Completion-Filterung** (§4.1: `VisibleEdgeKeywordItems`/Choice-`[params]` hinter
   `NavLanguageFeatures.IsAvailable`); Diagnostics-Fixtures + Completion-Tests je `#version`.
4. **`CodeGen/V2/`-Gerüst** — CallContext-Grundform (Voll-Fabrik + opaker `Result`, Maschinerie =
   `Unwrap()`-Aufruf, alle Transitionen, ohne Continuation/Choice); Golden gegen die Grundform.
5. **V2 Continuation** — `Show`/`Continuation` mit inline `.Concat(…)`, **`o-^` UND `--^`** (Builder
   wählt `OpenModalTask`/`GotoTask` je Edge-Mode). **`FrameworkStubs.cs` um die `.Concat`-Typfläche
   erweitern** (`.Concat(INOT_A_TASK_BOUNDARY)`/`.Concat(ITASK_BOUNDARY)`-Überladungen auf `GOTO_GUI`,
   `TWO_STEP_IINIT_TASK`/`…_TO_TASK_BOUNDARY`, Tagging-Interfaces `ITASK_BOUNDARY`/`INOT_A_TASK_BOUNDARY`),
   damit die generierten Continuation-Fälle **gegen Stubs kompilieren**. Golden gegen beide
   Continuation-Fälle (`o-^`/`--^`). **Kein Laufzeit-Test** — das Nav-Repo verifiziert nur bis Codegen
   (Compile-gegen-Stubs); Laufzeit ist Framework-Domäne (§3.8/⑥, offener Punkt §8 Nr. 1).
6. **V2 Choices in C#** — Choice-Context + `Choice_XLogic` + Forward aus den Quellen (kein Dispatch);
   Golden gegen den 3-Quellen-Fall aus §3.1.

## 7. Verifikation

- `. .\Tools\Commands\Import-NavCommands.ps1` einmalig; dann `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (beide TFMs grün).
- Codegen: **neue V2-Golden-Snapshots** (CallContext-Grundform, Continuation-Fall, Choice-mit-3-Quellen)
  via Snapshot-/`nav parity`-Workflow, Vergleich nach WS-Normalisierung. Die
  concat-Branch-`.expected.cs` sind **nicht** Golden-Referenz (§2.2).
- **Korpus-Organisation:** Der Harness (`RegressionTests`) discovert alle `.nav` unter
  `Regression\Tests\` **rekursiv**, jagt sie durch die `NavCodeGeneratorPipeline` und vergleicht je
  generierte `.cs` per-File gegen ihre `.expected.cs` — ein Fixture kostet nur „`.nav` reinlegen +
  `nav snapshot`", kein Wiring. Die V2-Fixtures liegen unter **`Regression\Tests\V2\`** (hält die
  V1-Parity-Goldens optisch/diff-technisch getrennt und macht die Invariante „V1 byte-identisch" auf
  einen Blick prüfbar; der Harness nutzt den Relativpfad als Identity, ein Unterordner ist
  unkritisch). Backbone = **drei gestaffelte Goldens** (Grundform / Continuation `o-^`+`--^` / Choice
  [3-Quellen + Union + Choice→Choice + Multi-Exit]). **`[notimplemented]`/`[donotinject]` je als
  isoliertes Minimal-Fixture**, nicht in ein Backbone-Golden gefaltet: beide sind Signatur-/Body-
  Sonderformen (notimplemented: `throw`-Thunk; donotinject: expliziter Wrapper-Parameter), ihr
  Review-Wert ist der **saubere Ein-Konzept-Diff** — in die große Choice-Backbone gefaltet, würde
  eine Codegen-Änderung an ihnen das ohnehin große `…WFSBase.generated.cs`-Golden churnen. Isolation
  ist billig (Auto-Discovery) und passt zur bestehenden „wenige kleine fokussierte `.nav`"-Konvention.
  Alle V2-`.nav` mit `#version 2`. **Port-Caveat:** jedes isolierte Fixture braucht distinkten
  Task-Namen **und** `[namespaceprefix]`, sonst kollidieren die generierten Dateinamen im geteilten
  Baum.
- Diagnostics-Fixtures **Nav0120/0121/0122 + Nav0124** + Versions-Gate (Nav5000) (mit
  `//==>>`-Erwartungen) als Semantic-Tests — Negatives sind **Diagnostics**-Fixtures, keine Goldens.
- **Completion versionsbewusst (§4.1):** `NavCompletionServiceTests`-Fälle, die belegen, dass `--^`/
  `o-^` und Choice-`[params]` in einer `#version 1`-Unit **nicht** und ab `#version 2` **doch**
  vorgeschlagen werden (dieselbe `NavLanguageFeatures.IsAvailable`-Autorität wie Nav5000).
- **Continuation kompiliert gegen erweiterte Stubs:** `FrameworkStubs.cs` wird um die `.Concat`-Typfläche
  ergänzt (`.Concat`-Überladungen, `TWO_STEP_*`, Tagging-Interfaces `ITASK_BOUNDARY`/`INOT_A_TASK_BOUNDARY`);
  der Golden-Compile deckt `o-^` **und** `--^` ab. **Kein** Laufzeit-Test im Nav-Repo — die
  Verifikation endet bei Codegen; die `--^`-Laufzeitsemantik ist quellcode-verifiziert (§3.8/⑥,
  Framework-Domäne).
- Dispatcher-Invariante: **V1-Units bleiben byte-/verhaltensidentisch** (bestehende Regression
  unverändert grün), V2 greift nur für die neuen Fälle.

## 8. Offene Punkte

Zwei inhaltliche Entscheidungen stehen aus (Team-Entscheidungen, nicht Teil dieser Spezifikation):

1. **`--^`-Laufzeitverifikation ist im Plan verwaist.** Der exakte
   `GotoGUI(view).Concat(GotoTask(…))`-TWO_STEP-Pfad ist am Framework **un-exerziert**, und der
   `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY`-Ctor trägt einen Framework-Autor-TODO über nicht
   durchdachte Stack-Effekte (§3.8/⑥). Ein Laufzeit-Smoke-Test (QuickTests-Stil, echter
   `ServerExecutionContext`, Assertion auf Client-Kommando-Sequenz + Stackframe) läge im
   Framework-Repo (`QuickTests`), nicht im Nav-Fahrplan. **Offen:** `--^`-Codegen sofort ausliefern
   **oder** bis zum Framework-Smoke-Test gaten.
2. **Golden-`.nav`-Fixtures für Grundform + Continuation sind noch nicht geschrieben.** Nur der Choice-Fall
   (§3.1) liegt konkret vor. Teil von Fahrplan-Schritt 1 (§6).
