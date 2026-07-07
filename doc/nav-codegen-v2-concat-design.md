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

**Konsequenz für die Referenz-Snapshots:** Das V2-Zielbild weicht damit bewusst vom
`concat`-Branch-Output ab (dort: CallContext nur als Concat-Vehikel, `INavCommandBody`-Rückgabe,
Records mit public `wfs`, Choices weiterhin an jeder Quelle eingefaltet). Offene Frage §7.4 aus
Runde 1 ist entschieden: es gibt **neue Golden-Snapshots**; die concat-Branch-`.expected.cs` bleiben
nur konzeptionelle Referenz.

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

## 4. V2-Zielbild (Runde 2: konkret)

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
- **Pro Context ein geschachtelter `Result`** (`internal` ctor, trägt intern das
  `INavCommandBody`). Damit ist auch **Cross-Transition-Leckage** ausgeschlossen: ein aus dem
  `OnRetry`-Context stammendes Ergebnis lässt sich nicht aus `BeginLogic` zurückgeben — falscher
  Typ, Compile-Fehler. *(Verworfene Variante: EIN gemeinsamer Result-Typ je WFS — weniger Typen,
  aber das Leck bliebe und würde erst zur Laufzeit im `default`-Zweig auffallen.)*
- Die Maschinerie entpackt via `.Body` und dispatcht wie in V1 per `switch`; der `default`-Zweig
  bleibt als Wächter bestehen (kann bei korrektem Generator praktisch nicht mehr auftreten).

### 4.3 Die Context-Fläche je Kanten-Art

Der Context ist die **vollständige, benannte Übergangs-Fläche** der Transition bzw. Choice — pro
tatsächlich vorhandener Nav-Kante eine Methode:

| Nav-Kante der Quelle | Context-Methode | erzeugt intern |
|---|---|---|
| `--> View` | `GotoView(ViewTO)` | `ViewTO` (→ `GotoGUI`) |
| `o-> View` | `OpenModalView(ViewTO)` | `ViewTO` (→ `OpenModalGUI`) |
| `==> View` | `ShowNonModalView(ViewTO)` | `ViewTO` (→ `StartNonModalGUI`) |
| `-->`/`o->`/`==>` `Task` | `Begin{Task}(…)` je Init-Überladung | `TaskCall` (→ `GotoTask`/`OpenModalTask`/`StartNonModalTask`) |
| `o-^ Task` (Concat) | `Show(to).Begin{Task}(…)` | `ConcatCommand` |
| `--> Choice` | `{Choice}({params})` | `ChoiceCall` (Delegation, §4.4) |
| `--> Exit` | `Exit({result})` | TASK_RESULT (heute `InternalTaskResult`) |
| `--> End` | `End()` | END |
| immer | `Cancel()` | CANCEL |

Da der View-/Task-Dispatch am **Edge-Mode** hängt, macht die Voll-Fabrik die Unterscheidung
(Goto/Modal/NonModal) erstmals **im Methodennamen** sichtbar statt nur in der Maschinerie. Das
bisherige Idiom `return to;` entfällt in V2 zugunsten von `return ctx.GotoView(to);`.

### 4.4 Choices in C#: Context + abstrakte Logic + geteilter Dispatch

Eine Choice wird zu **drei einmal generierten Bausteinen** — egal, wie viele Quellen auf sie zeigen:

```csharp
protected sealed class Choice_RetryCallContext {

    readonly SampleWFSBase _wfs;
    internal Choice_RetryCallContext(SampleWFSBase wfs) => _wfs = wfs;

    /// Opaker Ergebnistyp: nur dieser Context kann ihn erzeugen.
    public sealed class Result {
        internal Result(INavCommandBody body) => Body = body;
        internal INavCommandBody Body { get; }
    }

    // Choice_Retry --> View
    public Result GotoView(ViewTO to) => new(to);

    // Choice_Retry --> View o-^ Msg   (Concat, §4.5)
    public Continuation Show(ViewTO to) => new(_wfs, to);
    public sealed class Continuation {
        readonly SampleWFSBase _wfs; readonly ViewTO _to;
        internal Continuation(SampleWFSBase wfs, ViewTO to) { _wfs = wfs; _to = to; }
        public Result BeginMsg(string text) => new(new ConcatCommand {
            TO           = _to,
            Continuation = new TaskCall(MsgNodeName, () => _wfs._msg.Begin(text)),
        });
    }

    public Result Cancel() => new(/* CANCEL wie bisher erzeugt */);
}

// 1. Die ENTSCHEIDUNG liegt einmal beim Nutzer:
protected abstract Choice_RetryCallContext.Result Choice_RetryLogic(
    string reason, Choice_RetryCallContext callContext);

// 2. Die AUSWERTUNG der Choice-Ausgänge liegt einmal in der Maschinerie
//    (statt Switch-Union pro Quelle wie in V1):
INavCommand DispatchChoice_Retry(INavCommandBody body) {
    switch (body) {
        case ViewTO viewTO:
            return GotoGUI(viewTO);
        case ConcatCommand concatCommand when concatCommand.TO is ViewTO to:
            return GotoGUI(to).Concat(ContinueWith(concatCommand.Continuation));
        case CANCEL cancel:
            return cancel;
        default:
            throw new InvalidOperationException(…);
    }
    ITASK_BOUNDARY ContinueWith(INavCommandBody continuationBody) {
        switch (continuationBody) {
            case TaskCall taskCall when taskCall.NodeName == MsgNodeName:
                return OpenModalTask<MsgResult>(taskCall.BeginWrapper, AfterMsg);
            default:
                throw new InvalidOperationException(…);
        }
    }
}
```

**Die Delegation** ist eine Methode im Context jeder Quelle und läuft **synchron**: sie ruft die
abstrakte Choice-Logic direkt auf und verpackt deren Ergebnis in einen `ChoiceCall`-Marker, damit die
Quell-Maschinerie an den geteilten Dispatch weiterreichen kann:

```csharp
protected sealed class Init1CallContext {
    readonly SampleWFSBase _wfs;
    internal Init1CallContext(SampleWFSBase wfs) => _wfs = wfs;

    public sealed class Result { /* wie oben */ }

    // Init1 --> Choice_Retry
    public Result Choice_Retry(string reason) =>
        new(new ChoiceCall(Choice_RetryNodeName,
                           _wfs.Choice_RetryLogic(reason, new(_wfs)).Body));

    public Result Cancel() => …;
}

public virtual IINIT_TASK Begin(string message) {
    var body = BeginLogic(message, new Init1CallContext(this)).Body;
    switch (body) {
        case ChoiceCall choiceCall when choiceCall.NodeName == Choice_RetryNodeName:
            return DispatchChoice_Retry(choiceCall.Body);
        case CANCEL cancel:
            return cancel;
        default:
            throw new InvalidOperationException(…);
    }
}

protected abstract Init1CallContext.Result BeginLogic(string message, Init1CallContext callContext);
```

Die Guards (`if "Fehler"`/`else`) an Choice-Kanten behalten ihren heutigen **Doku-Charakter** — die
Entscheidung trifft frei formulierter Nutzer-Code in der Choice-Logic, nicht der Generator.

### 4.5 Concat-Spezialform (Show → Continuation → ConcatCommand)

Mechanik wie im concat-Branch, eingebettet in die Voll-Fabrik: `Show(ViewTO)` liefert eine
`Continuation`, deren `Begin{Task}(…)` ein `ConcatCommand` (View + fortzusetzender `TaskCall`) in
einen `Result` verpackt. Die Maschinerie behandelt `ConcatCommand` mit
`GotoGUI(to).Concat(ContinueWith(…))` und lokaler `ContinueWith`-Funktion (§4.4).

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

### 4.7 Laufzeit-Bausteine (XTplus-Framework / Engine)

- `ConcatCommand`, `TaskCall(NodeName, BeginWrapper)`, `.Concat(…)`, `ITASK_BOUNDARY ContinueWith` —
  wie im concat-Branch bereits verwendet (Runde 1, unverändert).
- **Neu: `ChoiceCall(NodeName, Body)`** — Marker für „Ergebnis kam durch Choice X", damit die
  Quell-Maschinerie an den geteilten `DispatchChoice_X` weiterreichen kann.

### 4.8 Klärpunkt: Framework-Rückgabetypen des geteilten Dispatch

`DispatchChoice_X` wird von Init-Maschinerie (Rückgabetyp `IINIT_TASK`) **und** Trigger-/Exit-
Maschinerie (`INavCommand`) aufgerufen. Am echten Framework zu klären: gibt es einen gemeinsamen
konkreten Rückgabetyp (`GotoGUI(…)` wird heute schon in beiden Kontexten returnt)? Fallbacks, falls
nicht: (a) zwei Dispatch-Varianten je Rückgabetyp generieren (max. 2), oder (b) Switch-Union pro
Quelle inlinen (V1-Stil — dann nur Maschinerie-, kein Nutzercode-Bloat). An der Nutzer-Fläche ändert
sich in keinem Fall etwas.

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
  Choice-Delegation/Exit/End/Cancel) beschreibt Transitions- **und** Choice-Kontexte — beide sind
  „Aufruffläche einer Kanten-Quelle" und unterscheiden sich nur in Namensquelle und Parametern.
- **EIN `CallContextEmitter`** (Context-Klasse + Result + Continuations) und **EIN
  `DispatchSwitchEmitter`** (Maschinerie-`switch` aus derselben Callable-Liste). Der Switch-Code
  steht heute schon fast identisch dreimal in `WfsBaseEmitter.WriteInit/Exit/TriggerTransition` —
  V2 verallgemeinert ihn statt ihn zu vervielfachen.
- `EmitterCommon` (Header/Usings/Annotations) wird V1/V2-geteilt (nach `CodeGen/Shared/` heben; V1
  referenziert weiter, byte-identisches V1-Verhalten per Regression abgesichert).
- **Anti-Bloat im generierten Code:** geteilter `DispatchChoice_X` statt Switch-Union pro Quelle;
  die Choice-Logik des Nutzers existiert genau einmal. Der `{Task}WFS`-One-Shot-Stub
  (`WfsOneShotEmitter`) generiert die neuen Override-Signaturen.

## 7. Offene Design-Fragen (Arbeitsvorrat)

Erledigt in Runde 2: ~~`o-^` vs `--^`~~ (nur `o-^`, Nr. 4 der Leitentscheidungen), ~~Choice-in-C#-
Form~~ (§4.4), ~~Regression-Beweis~~ (neue Snapshots, §1). Verbleibend/neu:

1. **Migrationsstrategie V1→V2** — V2 zunächst nur für neue/`#version`-markierte Units, V1 bleibt
   Default? (Bindet an die vorhandene `#version`-/Dispatcher-Mechanik.)
2. **Framework-Rückgabetyp des geteilten Dispatch** (§4.8) — am echten XTplus-Framework klären.
3. **CANCEL-Erzeugung im Context** — wie entsteht heute das `CANCEL`-Objekt im Nutzer-Code, und wie
   liefert `ctx.Cancel()` es sauber (Framework-Singleton vs. Factory)?
4. **Namenskonventionen im Detail** — `GotoView(…)` generisch vs. je View-Node benannt;
   Kollisionsregeln Node-Name ↔ generierter Membername (z.B. Task namens `Show` oder `Cancel`);
   Node-Name-Konstanten für Choices (`Choice_RetryNodeName`).

## 8. Fahrplan (nach Design-Abschluss)

Jeder Umsetzungs-Step mit Review + Build/Test + gelieferter Commit-Message (kein Selbst-Commit).

1. **V2-Codegen-Design finalisieren** — verbleibende Fragen §7 entscheiden; Golden-`.nav`-Fälle
   festschreiben (CallContext-Grundform, Concat, Choice-mit-3-Quellen aus §4.1).
2. **Syntax vorwärts portieren** — Tokens/Parser/`ConcatTransitionSyntax`, **Choice-`[params]`**,
   Visitor/Walker; Parser-/Syntax-Tests.
3. **Semantic Model vorwärts portieren** — `ConcatTransition`/`IConcatableEdge`/`ContinuationCall`,
   Choice-Parameter, Analyzer Nav1020/1021/1022 + Nav0222-Fix + Versions-Gates (§5);
   Diagnostics-Fixtures.
4. **`CodeGen/V2/`-Gerüst** — Dispatch + CallContext-Grundform (Voll-Fabrik + opaker Result, alle
   Transitionen, ohne Concat/Choice); Golden gegen die Grundform.
5. **V2 Concat** — `Show`/`Continuation`/`ConcatCommand`/`ContinueWith` (`o-^`-only); Golden gegen
   den Concat-Fall.
6. **V2 Choices in C#** — Choice-Context/-Logic/-Dispatch + `ChoiceCall` + Delegation; Golden gegen
   den 3-Quellen-Fall aus §4.1.

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
