# V2-Codegen-Design: CallContext, Concat & Choices in C#

> **Status: lebendes Dokument, Runde 2.** Dieses Dokument wird über mehrere Runden weiter ausgefeilt.
> Offene Punkte sind als solche markiert; die „Offenen Design-Fragen" sind der Arbeitsvorrat für die
> nächsten Runden.

## 1. Motivation / Kontext

Zwei zusammenhängende Vorhaben:

1. **Concat** (Fachlichkeit): Ein Workflow-Übergang zeigt eine View/Dialog **und** ruft direkt
   „obendrauf" den nächsten Task auf (typisch eine Messagebox). Nav-Syntax:
   `Quelle --> View o-^ Task` bzw. `Quelle --> View --^ Task`.
2. **Codegen-Umstellung**: Nicht mehr **alle** `IBeginTask`-Wrapper als Parameter in die
   Logic-Methoden reichen, sondern einen **CallContext**, über den die im Nav-Workflow definierten
   Tasks aufgerufen werden.

Ursprünglich war der CallContext nur als Concat-Vehikel gedacht. Inzwischen die Einsicht: **wenn, dann
alles umstellen (V2)** — alle Transitionen laufen über CallContext, Concat ist eine Spezialform
darauf. Und weil **Choices an mehreren Targets/Quellen** hängen können, sollen die **Choices in
C#-Code** abgebildet werden, statt sie an jeder Quelle einzufalten.

### Leitentscheidungen (Runde 1)

- **V2-Codegen zuerst ausdesignen** (dieses Dokument). Der Syntax-/Semantic-Model-Port folgt danach.
- **Alle Transitionen** auf CallContext (nicht Concat-only).
- **Choices-in-C#** gleich mitdesignen.

### Leitentscheidungen (Runde 2)

Die vier Kern-Gabelungen sind entschieden:

1. **Choice-Datenfluss: Nav-Spracherweiterung `choice X [params …]`.** Die geteilte Choice-Logic
   bekommt typisierte Parameter (analog `init … [params …]`); jede Quelle übergibt die Argumente bei
   der Delegation.
2. **Context-Umfang: Voll-Fabrik + opaker Ergebnistyp.** ALLE Übergänge (View, Task, Concat, Choice,
   Exit, End, Cancel) laufen über den CallContext; die Logic-Methode gibt einen opaken Typ zurück,
   den **nur der Context erzeugen kann** → illegale Übergänge werden **Compile-Fehler** statt
   Laufzeit-`InvalidOperationException`.
3. **CallContext immer.** Jede Logic-Methode bekommt ihren Context, auch wenn er (noch) klein ist.
   Nutzen: Nav-Änderungen erweitern nur den Context um Methoden, brechen aber keine
   Logic-Signaturen mehr (in V1 bricht jede neue Task-Kante die Signatur, weil ein
   Wrapper-Parameter dazukommt).
4. **Concat zum Start nur `o-^`** (→ `OpenModalTask`). `--^` wird geparst, aber per Diagnostic
   abgelehnt, bis die Laufzeit-Semantik (Goto-Task nach `GotoGUI`?) mit dem Framework geklärt ist.

### Leitentscheidung (Runde 3): Dispatch-`switch` eliminiert

Der V1-`switch(body)` in **jeder** Maschinerie-Methode (hundertfach im Korpus) tat **zwei** Dinge:
**(a) Validierung** (`default: throw`, dass die Logic nichts Undeklariertes liefert) und **(b)
Mapping Body → Kommando** — die Logic gab einen *Body-Marker* aus der `INavCommandBody`-Welt zurück
(`ViewTO`/`TaskCall`/…), der `switch` bildete ihn auf das echte Framework-Kommando aus der
**getrennten** `INavCommand`-Welt ab (`GotoGUI(viewTO)`/`OpenModalTask(…)`). Beides fällt in V2 weg:

- **(a)** ist durch den opaken `Result` bereits strukturell erledigt.
- **(b)** wandert **in die Context-Methode**: statt einen `TaskCall`-Marker zu liefern, ruft
  `ctx.BeginB(…)` `OpenModalTask` **selbst** und verpackt das **fertige Kommando** im `Result`.

Damit kollabiert jede Maschinerie-Methode auf einen **`.Body`-Unwrap** (Details §4.2a). Es
**verschwinden** — nicht nur schrumpfen — der geteilte `DispatchChoice_X`, das lokale `ContinueWith`
und mit ihnen die Marker-Laufzeittypen `ChoiceCall` und `ConcatCommand`. Die einzige neue
Framework-API, die übrig bleibt, ist `.Concat(…)` (die der concat-Branch ohnehin brauchte). Der
Anti-Bloat-Gewinn der Choice bleibt voll erhalten: `Choice_XLogic` + `Choice_XCallContext` werden
weiter **einmal** erzeugt, die Quellen **forwarden** nur.

**Konsequenz für die Referenz-Snapshots:** Das V2-Zielbild weicht damit bewusst vom
`concat`-Branch-Output ab (dort: CallContext nur als Concat-Vehikel, `INavCommandBody`-Rückgabe,
Records mit public `wfs`, Choices weiterhin an jeder Quelle eingefaltet). Offene Frage §7.4 aus
Runde 1 ist entschieden: es gibt **neue Golden-Snapshots**; die concat-Branch-`.expected.cs` bleiben
nur konzeptionelle Referenz.

### Leitentscheidungen (Runde 4): Base class, Migration, Namen

Vier weitere Gabelungen entschieden — Runde 4 schließt die zuvor offenen §7.1/§7.4 ab:

1. **Keine gemeinsame CallContext-Basisklasse.** Die generierten Contexte bleiben eigenständige
   `sealed class`. Die Leak-Prevention (pro Context genesteter `Result` mit `internal` ctor;
   Rückgabetyp **pro Context** verschieden — auch `Cancel()` liefert den kontext-eigenen `Result`)
   macht **jedes bedeutungstragende Member context-lokal**. Eine Basis könnte nur `_wfs` + trivialen
   ctor hochziehen (`_wfs` ist zwingend auf die konkrete `{Task}WFSBase` typisiert — die Methoden
   greifen WFS-private Member wie `_b`, `After{Task}`, `{Choice}Logic`). Der scheinbar identische
   Body `GotoView(to) => new(_wfs.GotoGUI(to))` ist **nicht** teilbar, weil `new(...)` je einen
   anderen `Result` konstruiert. Vererbung brächte 2 Boilerplate-Zeilen Ersparnis gegen einen
   zusätzlichen Typ + Indirektion, die die selbsterklärenden Contexte verschleiert → nicht wert.
2. **Migration: Default = V1, V2 opt-in via `#version 2`.** Kein Auto-Upgrade, kein Default-Flip.
   Passt bruchlos auf die vorhandene Infrastruktur (`VersionDispatchingCodeGenerator` schaltet je
   `CodeGenerationUnit.LanguageVersion`; `NavCodeGenFacts.For(Default) == V1`). Ein späteres Umlegen
   des Defaults auf V2 bleibt eine **separate Einzeiler-Entscheidung im Dispatcher** und ist **nicht**
   Teil dieses Designs.
3. **Namenskonvention: node-basiert, Mode-Verb sichtbar.** View-/Task-Context-Methoden heißen
   `{Mode-Verb}{NodeName}` (Node-Name **im** Methodennamen → quellenstabil, mehrere Views
   unterscheidbar): `GotoView`/`OpenModalView`/`ShowNonModalView`, analog `Begin{Task}`. Das
   **Verb-Lexikon** (`Goto`/`OpenModal`/`ShowNonModal`) ist Arbeitsname; „Goto" konkret ist ein
   **bewusst vertagtes** Lexikon-Detail (kein Blocker).
4. **Namens-Kollision: reservierte Namen + Diagnostic.** Die fixen Context-Member
   `Cancel`/`Exit`/`End`/`Show` und die genesteten Typnamen `Result`/`Continuation` sind
   **reserviert**. Ein Node, dessen generierter Membername damit kollidiert — insbesondere ein
   **Choice-Forward** `{Choice}(…)`, der den **bloßen** Node-Namen nutzt (z.B. Choice namens
   `Show`/`Cancel`) — erzeugt eine **Nav-Diagnose** (Autor benennt um). Kein stilles Namens-Mangling.

## 2. Referenz: der `concat`-Branch

Auf dem Remote-Branch **`concat`** wurde beides bereits angefangen — allerdings **alt**: Merge-Base
(`f44b91a3`) liegt vor 145 master-Commits und vor der gesamten **Codegen-Versionierung** auf
`feature/nav-parser`. Der Branch codiert Concat noch in der alten **StringTemplate-(`.stg`)-Welt**.

→ Der Branch ist **Referenz-Zielbild**, wird **nicht** gemergt/cherry-gepickt. Umgesetzt wird in der
heutigen Emitter/`CodeBuilder`-Welt als neues `CodeGen/V2/`. Seit Runde 2 gilt: konzeptionelle
Referenz für die Concat-Mechanik (`Show`/`Continuation`/`ConcatCommand`/`ContinueWith`), **nicht**
mehr für die Code-Gestalt (siehe Leitentscheidungen Runde 2).

### Concept-tragende Artefakte auf `concat`

| Ebene | Inhalt |
|---|---|
| Syntax | Tokens `--^` (`ConcatGoToEdgeKeyword`), `o-^` (`ConcatModalEdgeKeyword`); `ConcatTransitionSyntax` (`Edge: ConcatEdgeSyntax?`, `TargetNode: TargetNodeSyntax?`). `ModalEdgeKeywordAlt "*->"` entfällt. |
| Semantic Model | `ConcatTransition : IConcatTransition`, `IConcatableEdge`, `ContinuationCall` in `Call`, Erweiterungen an `ITransition`/`Transition`/`ExitTransition`/`TaskDefinitionSymbol(+Builder)`/`EdgeExtensions`. |
| Diagnostics | **Nav1020** (Source eines Concat muss View-Node sein), **Nav1021** (Target muss Task-Node sein), **Nav1022** (verschiedene Views in einer Concatenation nicht unterstützt); Fix an **Nav0222** (Reachability bei unterschiedlichen Edge-Modes). `IntroduceChoiceCodeFix` berücksichtigt Concat. |
| Codegen (`.stg`) | `CallContextCodeModel` + `ContinuationCodeModel` (neu), Umbauten `TransitionCodeModel`/`WfsBaseCodeModel`/`CallCodeModel(+Builder)`/`Init|Exit|TriggerTransitionCodeModel`, Template `WFSBase.stg`. |
| Referenz-Output | `Nav.Language.Tests/Regression/Tests/WFL/generated/ConcatSampleWFSBase.generated.expected.cs` (+ `IBegin…`, `IConcat…`) und `ConcatSample.nav`. |

### Beispiel `ConcatSample.nav` (Auszug)

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

## 3. Ausgangslage: wie V1 heute generiert

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
> V1 und dem Concat-Zielbild liegt vor allem darin, **wie** die Wrapper in die Logic gelangen
> (Parameter vs. CallContext) — nicht in der Grundmechanik der Task-Aufrufe.

## 4. V2-Zielbild (Runde 2/3: konkret)

### 4.1 Durchgängiges Beispiel (Golden-Fall „Choice mit 3 Quellen + Concat")

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
    Choice_Retry --> View o-^ Msg if "Fehler";       // Concat aus der Choice heraus
    Msg:Exit     --> View;
}
```

### 4.2 CallContext = Voll-Fabrik mit opakem `Result`

Jede Logic-Methode bekommt **genau einen** Context und gibt dessen geschachtelten, opaken
`Result`-Typ zurück:

```csharp
protected abstract Init1CallContext.Result BeginLogic(string message, Init1CallContext callContext);
```

Festlegungen:

- **Contexte sind `sealed class` mit `internal` Konstruktor** (keine Records wie im concat-Branch):
  der Nutzer kann weder Context noch Result selbst konstruieren; das WFS-Feld (`_wfs`) ist nicht
  öffentlich sichtbar.
- **Pro Context ein geschachtelter `Result`** (`internal` ctor). Damit ist auch
  **Cross-Transition-Leckage** ausgeschlossen: ein aus dem `OnRetry`-Context stammendes Ergebnis
  lässt sich nicht aus `BeginLogic` zurückgeben — falscher Typ, Compile-Fehler. *(Verworfene
  Variante: EIN gemeinsamer Result-Typ je WFS — weniger Typen, aber das Leck bliebe.)*
- **`Result.Body` lebt in der Kommando-Welt, nicht in der Body-Welt (Runde 3).** In V1 gab die Logic
  einen `INavCommandBody`-Marker zurück; in V2 trägt `Result` das **fertige Framework-Kommando**
  (`IINIT_TASK` bei Init-Transitionen, `INavCommand` bei Trigger/Exit — die beiden getrennten
  Kommando-Hierarchien aus den Framework-Stubs). Die Body→Kommando-Übersetzung, die früher der
  `switch` machte, sitzt jetzt in den Context-Methoden (§4.2a).

### 4.2a Kollabierter Dispatch: Maschinerie = `.Body`-Unwrap (Runde 3)

Weil der `Result` bereits das fertige Kommando trägt, schrumpft **jede** Maschinerie-Methode auf
einen Unwrap — der hundertfache `switch` entfällt:

```csharp
public virtual IINIT_TASK Begin(string message)
    => BeginLogic(message, new Init1CallContext(this)).Body;

public virtual INavCommand OnFoo(ViewTO to) {
    to = BeforeTriggerLogic(to);                              // Trigger-Vorlauf bleibt
    return OnFooLogic(to, new OnFooCallContext(this)).Body;   // kein switch mehr
}
```

Die Context-Methoden sind expression-bodied Einzeiler, die das Framework-Kommando **eager** bauen
(die Stubs bestätigen: `GotoGUI`/`OpenModalTask`/`GotoTask`/`InternalTaskResult` sind reine
Kommando-Konstruktoren ohne Seiteneffekt; der Begin-Aufruf bleibt als `BeginTaskWrapper`-Thunk
deferred):

```csharp
public Result GotoView(ViewTO to)   => new(_wfs.GotoGUI(to));
public Result BeginB(string b1)     => new(_wfs.OpenModalTask<FooResult>(() => _wfs._b.Begin(b1), _wfs.AfterB));
```

**Residual-Ausbruch:** Der Nutzer kann noch `return null;` schreiben (`Result` ist eine Klasse) →
`.Body` würfe NRE. Wo die freundliche V1-Diagnose erhalten bleiben soll, generiert die Maschinerie
einen einzeiligen Null-Guard statt eines Switches:

```csharp
var result = BeginLogic(message, new Init1CallContext(this));
return result is null
    ? throw new InvalidOperationException(NavCommandBody.ComposeUnexpectedTransitionMessage(nameof(BeginLogic), null))
    : result.Body;
```

### 4.3 Die Context-Fläche je Kanten-Art

Der Context ist die **vollständige, benannte Übergangs-Fläche** der Transition bzw. Choice — pro
tatsächlich vorhandener Nav-Kante eine Methode:

Die Spalte „baut (eager)" ist das fertige Framework-Kommando, das der `Result` trägt (Runde 3) —
kein Zwischenmarker mehr:

| Nav-Kante der Quelle | Context-Methode | baut (eager) |
|---|---|---|
| `--> View` | `GotoView(ViewTO)` | `GotoGUI(to)` |
| `o-> View` | `OpenModalView(ViewTO)` | `OpenModalGUI(to)` |
| `==> View` | `ShowNonModalView(ViewTO)` | `StartNonModalGUI(to)` |
| `-->`/`o->`/`==>` `Task` | `Begin{Task}(…)` je Init-Überladung | `GotoTask`/`OpenModalTask`/`StartNonModalTask(() => _wfs._x.Begin(…), After{Task})` |
| `o-^ Task` (Concat) | `Show(to).Begin{Task}(…)` | `GotoGUI(to).Concat(OpenModalTask(…, After{Task}))` |
| `--> Choice` | `{Choice}({params})` | `_wfs.{Choice}Logic({params}, new(_wfs)).Body` (Forward, §4.4) |
| `--> Exit` | `Exit({result})` | `InternalTaskResult(result)` (Kommando-Cast, §4.7) |
| `--> End` | `End()` | END |
| immer | `Cancel()` | CANCEL |

Da der View-/Task-Dispatch am **Edge-Mode** hängt, macht die Voll-Fabrik die Unterscheidung
(Goto/Modal/NonModal) erstmals **im Methodennamen** sichtbar statt nur in der Maschinerie. Das
bisherige Idiom `return to;` entfällt in V2 zugunsten von `return ctx.GotoView(to);`.

**Namenskonvention (Runde 4):** View-/Task-Methoden heißen `{Mode-Verb}{NodeName}` — der Node-Name
steht **im** Methodennamen (quellenstabil, mehrere Views unterscheidbar). Das Verb-Lexikon
(`Goto`/`OpenModal`/`ShowNonModal`) ist Arbeitsname; „Goto" konkret bleibt bewusst vertagt. Die
Namen `Cancel`/`Exit`/`End`/`Show` (Member) und `Result`/`Continuation` (genestete Typen) sind
**reserviert** — ein gleichnamiger Node erzeugt eine Nav-Diagnose (§5), kein stilles Mangling.

### 4.4 Choices in C#: Context + abstrakte Logic (Runde 3: ohne Dispatch)

Eine Choice wird zu **zwei einmal generierten Bausteinen** — egal, wie viele Quellen auf sie zeigen.
Der frühere dritte Baustein (`DispatchChoice_X`) ist mit dem Kollaps (§4.2a) **entfallen**: der
Context baut die finalen Kommandos schon, die Logic gibt sie fertig zurück.

```csharp
// Baustein 1: der Choice-Context — baut die finalen Kommandos der Choice-Ausgänge.
protected sealed class Choice_RetryCallContext {

    readonly SampleWFSBase _wfs;
    internal Choice_RetryCallContext(SampleWFSBase wfs) => _wfs = wfs;

    /// Opaker Ergebnistyp: nur dieser Context kann ihn erzeugen.
    /// Body ist init-legal (IINIT_TASK), da Choice_Retry aus einem Init erreichbar ist (§4.7).
    public sealed class Result {
        internal Result(IINIT_TASK body) => Body = body;
        internal IINIT_TASK Body { get; }
    }

    // Choice_Retry --> View
    public Result GotoView(ViewTO to) => new(_wfs.GotoGUI(to));

    // Choice_Retry --> View o-^ Msg   (Concat inline, §4.5 — kein ConcatCommand/ContinueWith)
    public Continuation Show(ViewTO to) => new(_wfs, to);
    public sealed class Continuation {
        readonly SampleWFSBase _wfs; readonly ViewTO _to;
        internal Continuation(SampleWFSBase wfs, ViewTO to) { _wfs = wfs; _to = to; }
        public Result BeginMsg(string text) =>
            new(_wfs.GotoGUI(_to).Concat(_wfs.OpenModalTask<MsgResult>(() => _wfs._msg.Begin(text), _wfs.AfterMsg)));
    }

    public Result Cancel() => new(_wfs.Cancel());
}

// Baustein 2: die ENTSCHEIDUNG liegt einmal beim Nutzer:
protected abstract Choice_RetryCallContext.Result Choice_RetryLogic(
    string reason, Choice_RetryCallContext callContext);
```

**Die Delegation** ist eine Methode im Context jeder Quelle und läuft **synchron**: sie ruft die
abstrakte Choice-Logic direkt auf und **forwardet** deren fertiges Kommando (`.Body`) in den eigenen
`Result` — kein Marker, kein geteilter Dispatch mehr:

```csharp
protected sealed class Init1CallContext {
    readonly SampleWFSBase _wfs;
    internal Init1CallContext(SampleWFSBase wfs) => _wfs = wfs;

    public sealed class Result {
        internal Result(IINIT_TASK body) => Body = body;
        internal IINIT_TASK Body { get; }
    }

    // Init1 --> Choice_Retry: Choice-Logic aufrufen und Kommando durchreichen
    public Result Choice_Retry(string reason) =>
        new(_wfs.Choice_RetryLogic(reason, new(_wfs)).Body);

    public Result Cancel() => new(_wfs.Cancel());
}

// Maschinerie: nur noch Unwrap (§4.2a)
public virtual IINIT_TASK Begin(string message)
    => BeginLogic(message, new Init1CallContext(this)).Body;

protected abstract Init1CallContext.Result BeginLogic(string message, Init1CallContext callContext);
```

Der `Result`-Typ der Quelle ist bewusst ein **eigener** (nicht der Choice-`Result`): das Forwarden
re-boxt und erhält so die Leck-Verhinderung (ein `Choice_RetryCallContext.Result` lässt sich **nicht**
direkt aus `BeginLogic` zurückgeben — die Quelle *muss* durch `ctx.Choice_Retry(…)`).

Die Guards (`if "Fehler"`/`else`) an Choice-Kanten behalten ihren heutigen **Doku-Charakter** — die
Entscheidung trifft frei formulierter Nutzer-Code in der Choice-Logic, nicht der Generator.

### 4.5 Concat-Spezialform (Show → Continuation, inline)

`Show(ViewTO)` liefert eine `Continuation`, deren `Begin{Task}(…)` das **fertige** Concat-Kommando
baut: `GotoGUI(to).Concat(OpenModalTask(…, After{Task}))` (Runde 3 — kein `ConcatCommand`-Marker, kein
`ContinueWith`-Sub-Switch mehr; die Mechanik aus dem concat-Branch ist in die Context-Methode
gewandert). `.Concat(…)` ist die einzige neue Framework-API (§4.7).

Zum Start wird **nur `o-^`** unterstützt (→ `OpenModalTask`); `--^` wird per Diagnostic abgelehnt
(Leitentscheidung Runde 2, Nr. 4).

### 4.6 Nutzer-Code (der Elegance-Payoff)

```csharp
// Entscheidung EINMAL:
protected override Choice_RetryCallContext.Result Choice_RetryLogic(
        string reason, Choice_RetryCallContext ctx) {
    if (reason is null) return ctx.GotoView(CreateViewTO());
    return ctx.Show(CreateViewTO()).BeginMsg(reason);      // Messagebox obendrauf
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

### 4.7 Laufzeit-Bausteine & Rückgabetyp-Regel (Runde 3)

Durch den Dispatch-Kollaps schrumpft die neue Laufzeit-Fläche drastisch — **`ChoiceCall` und
`ConcatCommand` entfallen ganz** (waren nur Marker für den entfernten Switch/`ContinueWith`):

- **`.Concat(…)`** — einzige neue Framework-API. Signatur aus dem concat-Branch-Zielbild:
  `GOTO_GUI.Concat(<modal-task-command>)` liefert ein init-legales Kommando (im Branch aus
  `Begin(...)` mit Rückgabetyp `IINIT_TASK` returnt). Am echten Framework zu verifizieren.
- `GotoGUI`/`OpenModalTask`/`GotoTask`/`StartNonModalTask`/`OpenModalGUI`/`StartNonModalGUI` +
  `BeginTaskWrapper`-Delegat: **existieren bereits** (Stubs `FrameworkStubs.cs`) und werden von den
  Context-Methoden direkt gerufen.
- **Body↔Kommando-Brücke (verifizieren):** `InternalTaskResult<T>` gibt heute `INavCommandBody`
  zurück (Body-Welt), das V2-`Exit(…)` braucht aber ein `INavCommand`/`IINIT_TASK` (Kommando-Welt).
  Der V1-`switch` überbrückte das per Pattern-Downcast auf `TASK_RESULT`. V2 braucht entweder
  denselben Cast in der `Exit`-Context-Methode oder eine kommando-typisierte Framework-Schwester
  (`TaskResult<T>(…) : TASK_RESULT`). Analog für `CANCEL` (`ctx.Cancel()`): prüfen, wie das
  CANCEL-Kommando sauber erzeugt wird (Framework-Singleton/Factory).

**Rückgabetyp-Regel für `Result.Body`** (löst den früheren §4.8-Klärpunkt strukturell):

- **Transition-Context:** `IINIT_TASK` bei Init-Transitionen, `INavCommand` bei Trigger/Exit —
  entspricht exakt dem Maschinerie-Rückgabetyp, also `return …Logic(…).Body;` **ohne Cast**.
- **Choice-Context:** `IINIT_TASK`, sobald die Choice aus **irgendeiner** Init-Quelle erreichbar ist,
  sonst `INavCommand`. Weil `IINIT_TASK : INavCommand`, ist ein init-typisierter Choice-`Result.Body`
  auch von Trigger-/Exit-Quellen zuweisbar (Forward in §4.4). Dass die Choice-Ziele dann init-legal
  sein **müssen**, garantieren bereits die Reachability-Analyzer (**Nav0110**/**Nav0222**) — es
  braucht keine zusätzliche Prüfung im Codegen.

## 5. Syntax & Semantic Model (versionsunabhängig)

Beides ist **nicht** versionsspezifisch und wird einmal für alle Codegen-Versionen vorwärts
portiert — **nach** dem Design:

- **Syntax:** Tokens `--^`/`o-^`, Grammatik/Lexer/Parser, `ConcatTransitionSyntax`, generierte
  Visitor/Walker. **Neu (Runde 2):** `[params …]`-Klausel an der `choice`-Deklaration, analog
  `init` (Wiederverwendung `ParameterListSyntax`).
- **Semantic Model:** `IConcatTransition`/`ConcatTransition`, `IConcatableEdge`, `ContinuationCall`
  in `Call`, Edge-Mode-Behandlung, Analyzer **Nav1020/1021/1022** + **Nav0222**-Fix. **Neu
  (Runde 2):** Parameter am `IChoiceNodeSymbol`.
- **Diagnostics, versions-gated (Runde 2):** Concat-Kanten und Choice-`[params]` sind nur ab
  `#version 2` erlaubt (in V1-Units → Fehler-Diagnostic mit Verweis auf `#version`); `--^` wird
  vorerst generell abgelehnt („noch nicht unterstützt", Leitentscheidung Nr. 4).
- **Namens-Kollisions-Diagnose (Runde 4):** ein Node, dessen generierter Membername mit einem
  reservierten Context-Namen (`Cancel`/`Exit`/`End`/`Show`/`Result`/`Continuation`) kollidiert,
  wird abgelehnt (Autor benennt um). ID beim Port vergeben, zu Nav1020/1021/1022 einreihen.
  Ebenfalls versions-gated (nur relevant, wo V2-Contexte generiert werden).

## 6. Architektur-Einbettung (`feature/nav-parser`) & Anti-Bloat

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
- **EIN `CallContextEmitter`** (Context-Klasse + `Result` + Continuations). Ein separater
  Dispatch-/Switch-Emitter entfällt (Runde 3): die Maschinerie-Methode ist nur noch der `.Body`-
  Unwrap (§4.2a), ein triviales Template-Fragment. Der früher dreifach fast identische Switch-Block
  in `WfsBaseEmitter.WriteInit/Exit/TriggerTransition` **verschwindet ersatzlos**.
- `EmitterCommon` (Header/Usings/Annotations) wird V1/V2-geteilt (nach `CodeGen/Shared/` heben; V1
  referenziert weiter, byte-identisches V1-Verhalten per Regression abgesichert).
- **Anti-Bloat im generierten Code (Runde 3):** kein Dispatch-Switch mehr (weder pro Quelle noch als
  geteiltes `DispatchChoice_X`); die Choice-Logik des Nutzers existiert genau einmal, die Quellen
  forwarden als Einzeiler. Der `{Task}WFS`-One-Shot-Stub (`WfsOneShotEmitter`) generiert die neuen
  Override-Signaturen.

## 7. Offene Design-Fragen (Arbeitsvorrat)

Erledigt in Runde 2: ~~`o-^` vs `--^`~~ (nur `o-^`, Nr. 4 der Leitentscheidungen), ~~Choice-in-C#-
Form~~ (§4.4), ~~Regression-Beweis~~ (neue Snapshots, §1). Erledigt in Runde 3: ~~Framework-
Rückgabetyp des geteilten Dispatch~~ (Dispatch entfällt; Rückgabetyp-Regel §4.7). Erledigt in
Runde 4: ~~Migrationsstrategie V1→V2~~ (Default = V1, V2 opt-in via `#version 2`, Leitentscheidung
Runde 4 Nr. 2), ~~Namenskonventionen~~ (node-basiert `{Mode-Verb}{NodeName}` + reservierte Namen mit
Diagnose, Leitentscheidung Runde 4 Nr. 3/4), ~~gemeinsame Base class~~ (nein, Nr. 1).
Verbleibend/neu:

1. **Framework-Touchpoints verifizieren (§4.7)** — `.Concat(…)`-Signatur/Rückgabetyp; Body↔Kommando-
   Brücke für `Exit` (`InternalTaskResult` → `TASK_RESULT`, ggf. kommando-typisierte Schwester);
   `ctx.Cancel()` → wie das `CANCEL`-Kommando sauber erzeugt wird (Singleton/Factory).
2. **Eager-Bau bestätigen** — dass alle `Goto*/OpenModal*/StartNonModal*`-Konstruktoren wirklich
   seiteneffektfrei sind (die Stubs legen es nahe; am echten Framework absichern), damit der Aufruf
   in der Context-Methode statt in der Maschinerie unbedenklich ist.
3. **Verb-Lexikon-Detail** — ob `Goto`/`OpenModal`/`ShowNonModal` die endgültigen Verben sind
   (Namensschema selbst steht, Runde 4 Nr. 3).

## 8. Fahrplan (nach Design-Abschluss)

Jeder Umsetzungs-Step mit Review + Build/Test + gelieferter Commit-Message (kein Selbst-Commit).

1. **V2-Codegen-Design finalisieren** — verbleibende Fragen §7 entscheiden; Golden-`.nav`-Fälle
   festschreiben (CallContext-Grundform, Concat, Choice-mit-3-Quellen aus §4.1).
2. **Syntax vorwärts portieren** — Tokens/Parser/`ConcatTransitionSyntax`, **Choice-`[params]`**,
   Visitor/Walker; Parser-/Syntax-Tests.
3. **Semantic Model vorwärts portieren** — `ConcatTransition`/`IConcatableEdge`/`ContinuationCall`,
   Choice-Parameter, Analyzer Nav1020/1021/1022 + Nav0222-Fix + Versions-Gates (§5);
   Diagnostics-Fixtures.
4. **`CodeGen/V2/`-Gerüst** — CallContext-Grundform (Voll-Fabrik + opaker `Result`, Maschinerie =
   `.Body`-Unwrap, alle Transitionen, ohne Concat/Choice); Golden gegen die Grundform.
5. **V2 Concat** — `Show`/`Continuation` mit inline `.Concat(…)` (`o-^`-only); Golden gegen den
   Concat-Fall.
6. **V2 Choices in C#** — Choice-Context + `Choice_XLogic` + Forward aus den Quellen (kein Dispatch);
   Golden gegen den 3-Quellen-Fall aus §4.1.

## 9. Verifikation

- `. .\Tools\Commands\Import-NavCommands.ps1` einmalig; dann `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (beide TFMs grün).
- Codegen: **neue V2-Golden-Snapshots** (CallContext-Grundform, Concat-Fall, Choice-mit-3-Quellen)
  via Snapshot-/`nav parity`-Workflow, Vergleich nach WS-Normalisierung. Die
  concat-Branch-`.expected.cs` sind **nicht** mehr Golden-Referenz (Leitentscheidungen Runde 2).
- Diagnostics-Fixtures Nav1020/1021/1022 + Versions-Gates (mit `//==>>`-Erwartungen) als
  Semantic-Tests.
- Dispatcher-Invariante: **V1-Units bleiben byte-/verhaltensidentisch** (bestehende Regression
  unverändert grün), V2 greift nur für die neuen Fälle.

## 10. Änderungshistorie

- **Runde 1** — Erststand: Überblick `concat`-Branch, V1-Ausgangslage, V2-Zielbild (CallContext
  universell + Concat + Choices-in-C# als Richtung), offene Design-Fragen, Fahrplan.
- **Runde 2** — vier Leitentscheidungen (Choice-`[params]`, Voll-Fabrik + opaker Result, Context
  immer, Concat `o-^`-only); konkretes C#-Zielbild mit durchgängigem Beispiel (§4);
  Choice-Bausteine Context/Logic/Dispatch + `ChoiceCall`; Context-Flächen-Tabelle; Abkehr von den
  concat-Branch-Snapshots als Golden-Referenz; Anti-Bloat-Bausteine (§6); offene Fragen und
  Fahrplan aktualisiert.
- **Runde 3** — **Dispatch-`switch` eliminiert.** Erkenntnis (Stubs `FrameworkStubs.cs`): der
  hundertfache `switch` tat zweierlei — Validierung (jetzt strukturell via opakem `Result`) und
  Body→Kommando-Mapping (jetzt in den Context-Methoden, die das Framework-Kommando eager bauen).
  Maschinerie kollabiert auf `.Body`-Unwrap (§4.2a); `DispatchChoice_X`, `ContinueWith` und die
  Marker `ChoiceCall`/`ConcatCommand` **entfallen ganz**; Concat/Choice inline (§4.4/§4.5); einzige
  neue Framework-API bleibt `.Concat(…)`; Rückgabetyp-Regel für `Result.Body` (§4.7) löst den
  früheren Dispatch-Rückgabetyp-Klärpunkt; §6/§7/§8 nachgezogen.
- **Runde 4** — vier Gabelungen entschieden, §7.1/§7.4 abgeschlossen: **(1)** keine gemeinsame
  CallContext-Basisklasse (Leak-Prevention macht jedes Member context-lokal; eine Basis hätte nur
  `_wfs` + ctor); **(2)** Migration Default = V1, V2 opt-in via `#version 2` (kein Auto-Upgrade/
  Default-Flip; passt auf `VersionDispatchingCodeGenerator`); **(3)** Namenskonvention node-basiert
  `{Mode-Verb}{NodeName}` (`GotoView`…), Verb-Lexikon vertagt; **(4)** reservierte Namen
  (`Cancel`/`Exit`/`End`/`Show`/`Result`/`Continuation`) + Kollisions-Diagnose statt stillem Mangling
  (§4.3/§5). Offene Fragen §7 auf drei geschrumpft (Framework-Touchpoints, Eager-Bau, Verb-Lexikon).
