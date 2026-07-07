# V2-Codegen-Design: CallContext, Concat & Choices in C#

> **Status: lebendes Dokument, Runde 1.** Dieses Dokument wird über mehrere Runden weiter ausgefeilt.
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

## 2. Referenz: der `concat`-Branch

Auf dem Remote-Branch **`concat`** wurde beides bereits angefangen — allerdings **alt**: Merge-Base
(`f44b91a3`) liegt vor 145 master-Commits und vor der gesamten **Codegen-Versionierung** auf
`feature/nav-parser`. Der Branch codiert Concat noch in der alten **StringTemplate-(`.stg`)-Welt**.

→ Der Branch ist **Referenz-Zielbild**, wird **nicht** gemergt/cherry-gepickt. Umgesetzt wird in der
heutigen Emitter/`CodeBuilder`-Welt als neues `CodeGen/V2/`.

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

## 4. V2-Zielbild

### 4.1 Universeller CallContext (ersetzt Wrapper-Parameter)

Statt Wrapper-Parametern bekommt jede Logic-Methode **einen** generierten CallContext:

```csharp
protected abstract INavCommandBody BeginLogic(string message, Init2CallContext callContext);
```

Der CallContext ist ein `sealed record`, der die aufrufbaren Tasks als Methoden trägt (verschoben von
den bisherigen `protected Begin{Node}`-Hilfen der Base) — je Task pro Init-Überladung eine Methode:

```csharp
protected sealed record Init2CallContext(WfsBase wfs) {
    public INavCommandBody BeginA()          => new TaskCall(ANodeName, () => wfs._a.Begin());
    public INavCommandBody BeginA(string a1) => new TaskCall(ANodeName, () => wfs._a.Begin(a1));
    // … nur die von DIESER Transition erreichbaren Tasks
}
```

Der Rest (Felder `_a`, `switch(body)`, `OpenModalTask`) bleibt wie in V1. Nutzen: keine langen
Wrapper-Parameterlisten mehr; die aufrufbare Task-Fläche ist pro Transition sauber gekapselt/benannt.

### 4.2 Concat-Spezialform (Show → Continuation → ConcatCommand)

Für Transitionen mit Concat-Targets erweitert sich der CallContext um `Show(ViewTO)`, das eine
`Continuation` liefert; deren `BeginX(...)` liefern statt `TaskCall` ein **`ConcatCommand`**
(View + fortzusetzender TaskCall). Referenz: `ConcatSampleWFSBase.generated.expected.cs`.

```csharp
protected sealed record Init2CallContext(WfsBase wfs) {
    internal Continuation Show(ViewTO to) => new(wfs, to);
    internal sealed record Continuation(WfsBase wfs, ViewTO to) {
        public INavCommandBody BeginA(string a1) => new ConcatCommand {
            TO = to, Continuation = new TaskCall(ANodeName, () => wfs._a.Begin(a1)),
        };
    }
}
```

Die Maschinerie-Methode ergänzt:

```csharp
case ConcatCommand concatCommand when concatCommand.TO is ViewTO to:
    return GotoGUI(to).Concat(ContinueWith(concatCommand.Continuation));
// …
ITASK_BOUNDARY ContinueWith(INavCommandBody continuationBody) {
    switch (continuationBody) {
        case TaskCall taskCall when taskCall.NodeName == ANodeName:
            return OpenModalTask<FooResult>(taskCall.BeginWrapper, AfterA);
        // …
    }
}
```

Vorausgesetzte/zu ergänzende Engine-Laufzeit-Bausteine: `ConcatCommand`, `TaskCall(NodeName,
BeginWrapper)`, `.Concat(...)`, `ITASK_BOUNDARY ContinueWith` — im concat-Branch bereits verwendet.

### 4.3 Choices in C# (Kern der Design-Frage)

**Problem:** Choices werden heute an jeder Quelle eingefaltet. Bei mehreren Quellen dupliziert das die
Entscheidungslogik; mit Concat wird die Einfaltung zusätzlich komplex (Guards + Concat-Targets).

**Empfohlene Richtung (Runde 1, noch zu schärfen):** Eine Choice wird zu **eigenem generiertem C#** —
ein Konstrukt pro Choice, an das die Quell-Transitionen **delegieren** statt es zu duplizieren.
Konkret als eigener **Choice-CallContext / Choice-Methode**, die
- die von der Choice erreichbaren Ziel-Aufrufe als benannte Methoden exponiert (analog CallContext,
  inkl. Concat-`Show(...)` wo nötig) und
- von jeder Quell-Transition, die auf die Choice zeigt, **einmal** aufgerufen wird.

So liegt die Choice-Entscheidung an **einer** Stelle im generierten Code und wird geteilt.

**Offene Sub-Fragen (nächste Runden):**
- Choice als `record`-Context **oder** als eigene abstrakte `…Logic`-Methode (bekommt der Nutzer die
  Choice-Logik als eigene zu implementierende Methode, oder bleibt sie rein maschinell/deklarativ)?
- Wie erhält die Choice Zugriff auf die Task-Aufruffläche (eigener CallContext an die Choice
  gereicht)?
- Verhalten der bestehenden `if "…"`-Guards und von `IntroduceChoiceCodeFix` in der neuen Form.

## 5. Syntax & Semantic Model (versionsunabhängig)

Beides ist **nicht** versionsspezifisch und wird einmal für alle Codegen-Versionen vorwärts
portiert — **nach** dem Design:

- **Syntax:** Tokens `--^`/`o-^`, Grammatik/Lexer/Parser, `ConcatTransitionSyntax`, generierte
  Visitor/Walker.
- **Semantic Model:** `IConcatTransition`/`ConcatTransition`, `IConcatableEdge`, `ContinuationCall`
  in `Call`, Edge-Mode-Behandlung, Analyzer **Nav1020/1021/1022** + **Nav0222**-Fix.

## 6. Architektur-Einbettung (`feature/nav-parser`)

Versionierungs-Infrastruktur steht bereits:

- `CodeGen/Shared/` — `CodeBuilder`, `CodeInfo/*`, `Facts/` (`CodeGenInvariants`, `ICodeGenFacts`,
  `NavCodeGenFacts`).
- `CodeGen/V1/` — `CodeGeneratorV1`, `CodeModel/*`, `Emitters/*`.
- `CodeGen/VersionDispatchingCodeGenerator.cs` — Versions-Weiche.

→ **Codegen** ist versionsspezifisch: neues **`CodeGen/V2/`** (eigene `CodeModel/*` + `Emitters/*` im
`CodeBuilder`-Stil), über den Dispatcher geschaltet. Vieles aus V1 ableitbar (Emitter-Struktur,
`ReachableCalls`-Traversierung) plus die neuen Bausteine `CallContextCodeModel`/`ContinuationCodeModel`
(Vorlage: `CodeGen/CodeModel/CallContextCodeModel.cs` im concat-Branch) und ein Choice-CodeModel.

## 7. Offene Design-Fragen (Arbeitsvorrat)

1. **`o-^` vs `--^`** — genaue modal/non-modal-Semantik und Mapping auf `OpenModalTask` bzw. eine
   Alternative (im concat-Branch generiert sichtbar nur `OpenModalTask`). Für V2 verbindlich
   festzulegen.
2. **Choice-in-C#-Form** — die drei Sub-Fragen aus §4.3.
3. **Migrationsstrategie V1→V2** — V2 zunächst nur für neue/`#version`-markierte Units, V1 bleibt
   Default? (Bindet an die vorhandene `#version`-/Dispatcher-Mechanik.)
4. **Regression-Beweis** — dienen die concat-Branch-`.expected.cs` als Golden-Referenz für V2, oder
   weicht das V2-Zielbild (CallContext universell) bewusst davon ab (dann neue Snapshots)?

## 8. Fahrplan (nach Design-Abschluss)

Jeder Umsetzungs-Step mit Review + Build/Test + gelieferter Commit-Message (kein Selbst-Commit).

1. **V2-Codegen-Design finalisieren** — offene Fragen §7 entscheiden; Zielbild hier festschreiben;
   Golden-`.nav`-Fälle definieren (CallContext-Grundform, Concat, Choice-an-mehreren-Quellen).
2. **Syntax vorwärts portieren** — Tokens/Parser/`ConcatTransitionSyntax`, Visitor/Walker;
   Parser-/Syntax-Tests.
3. **Semantic Model vorwärts portieren** — `ConcatTransition`/`IConcatableEdge`/`ContinuationCall`,
   Analyzer Nav1020/1021/1022 + Nav0222-Fix; Diagnostics-Fixtures (im Branch vorhanden).
4. **`CodeGen/V2/`-Gerüst** — Dispatch + CallContext-Grundform (alle Transitionen, ohne Concat);
   Golden gegen die CallContext-Grundform.
5. **V2 Concat** — `Show`/`Continuation`/`ConcatCommand`/`ContinueWith`; Golden gegen `ConcatSample`.
6. **V2 Choices in C#** — Choice-Konstrukt + Delegation von mehreren Quellen; Golden mit
   Mehrfach-Quelle.

## 9. Verifikation

- `. .\Tools\Commands\Import-NavCommands.ps1` einmalig; dann `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (beide TFMs grün).
- Codegen: Golden-/Regression-Snapshots (`ConcatSample.nav` u.a.) via Snapshot-/`nav parity`-Workflow,
  Vergleich nach WS-Normalisierung.
- Diagnostics-Fixtures Nav1020/1021/1022 (mit `//==>>`-Erwartungen) als Semantic-Tests.
- Dispatcher-Invariante: **V1-Units bleiben byte-/verhaltensidentisch** (bestehende Regression
  unverändert grün), V2 greift nur für die neuen Fälle.

## 10. Änderungshistorie

- **Runde 1** — Erststand: Überblick `concat`-Branch, V1-Ausgangslage, V2-Zielbild (CallContext
  universell + Concat + Choices-in-C# als Richtung), offene Design-Fragen, Fahrplan.
