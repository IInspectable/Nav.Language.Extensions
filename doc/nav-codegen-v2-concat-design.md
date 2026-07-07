# V2-Codegen-Design: CallContext, Concat & Choices in C#

> **Status: lebendes Dokument, Runde 8.** Dieses Dokument wird über mehrere Runden weiter ausgefeilt.
> Offene Punkte sind als solche markiert; die „Offenen Design-Fragen" sind der Arbeitsvorrat für die
> nächsten Runden. Framework-Verifikation der §4.7-Touchpoints: `doc/WFS-Spracherweiterung —
> Framework-Verifikation.md`.

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
   **bewusst vertagtes** Lexikon-Detail (kein Blocker). *(Runde 7: für **View**-Kanten ersetzt durch
   EIN mode-freies `Show{Node}` — der Anzeige-Modus ist edge-bestimmt, nicht Autor-Wahl; `Begin{Task}`
   bleibt. §4.3.)*
4. **Namens-Kollision: reservierte Namen + Diagnostic.** Die fixen Context-Member
   `Cancel`/`Exit`/`End`/`Show` und die genesteten Typnamen `Result`/`Continuation` sind
   **reserviert**. Ein Node, dessen generierter Membername damit kollidiert — insbesondere ein
   **Choice-Forward** `{Choice}(…)`, der den **bloßen** Node-Namen nutzt (z.B. Choice namens
   `Show`/`Cancel`) — erzeugt eine **Nav-Diagnose** (Autor benennt um). Kein stilles Namens-Mangling.
   *(Runde 7 revidiert die Liste: `Show` wird Verb-Präfix und fällt als fixes Member weg — §4.3/§5.)*

### Leitentscheidungen (Runde 5): Framework-Verifikation eingearbeitet

Die fünf `§4.7`-Unbekannten wurden am **realen** Framework (`Framework.NavigationEngine`, nicht Stubs)
verifiziert — Details + Quellen: `doc/WFS-Spracherweiterung — Framework-Verifikation.md`. Ergebnis:
drei Annahmen bestätigt, **zwei Design-Prämissen korrigiert**.

1. **`.Concat` bestätigt** — öffentliche Instanzmethode auf `GOTO_GUI` (keine Extension), Parameter
   sind die **Tagging-Interfaces** `INOT_A_TASK_BOUNDARY`/`ITASK_BOUNDARY`. `GotoGUI(to).Concat(
   OpenModalTask(…))` liefert `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : IINIT_TASK, INavCommand` →
   für `Begin` **und** Trigger/Exit zuweisbar.
2. **Exit ohne Cast** — `InternalTaskResult<T>` liefert real `TASK_RESULT<T>` (vereint Body- **und**
   Kommando-Welt: `INavCommandBody, IINIT_TASK, ITASK_BOUNDARY`). Die `ctx.Exit`-Fabrik konkret als
   `TASK_RESULT<T>` typisieren → **statischer Upcast, kein Cast**. Keine kommando-typisierte Schwester
   nötig.
3. **`ctx.Cancel()` = `_wfs.Cancel()`** — echte Factory-Methode (`new CANCEL()`); `CANCEL` ist
   `IINIT_TASK` **und** `INavCommandBody`. Kein Singleton/Property. (Doc-Annahme war korrekt.)
4. **Init-Legalität = Typsystem verbietet die Kante (Auflösung b).** `OPEN_MODAL_TASK`/`OPEN_MODAL_GUI`/
   `START_NONMODAL_TASK`/`END` sind **bewusst nicht** `IINIT_TASK` (Framework-Regel: „a task can only
   start with GOTO_TASK, GOTO_GUI or TASK_RESULT"). Der opake `Result.Body` ist damit **nicht** immer
   `IINIT_TASK`-typisierbar → das **Semantic Model muss Init-Ausgangskanten auf die IINIT_TASK-Menge
   beschränken** (`GotoGUI`/`GotoTask`/`TASK_RESULT`/`CANCEL`/`GotoGUI(…).Concat(…)`); Modal/Nonmodal/
   Modal-GUI nur *innerhalb* eines Tasks. Analyzer-Anforderung → §5.
5. **Eager-Bau ist NICHT seiteneffektfrei → `Result` trägt den Befehl deferred (Thunk).** Die
   Konstruktoren von `GOTO_GUI`, `OPEN_MODAL_GUI` und `.Concat(ITASK_BOUNDARY)` feuern **Seiteneffekte**
   (GUI-Navigation bzw. `ExecuteCallResult`) — die Round-3-Prämisse „reine Konstruktoren, eager bauen"
   gilt nur für die feld-speichernden Commands. Auflösung: `Result` kapselt einen `Func<…>`; die
   Konstruktion feuert erst beim `.Body`-Unwrap in der Maschinerie — **exakt V1-Timing** und robust
   gegen „Fabrik aufrufen, aber nicht zurückgeben". Der Dispatch-Kollaps (kein `switch`) bleibt
   unberührt. Details §4.2a/§4.7.

### Leitentscheidungen (Runde 6): Init-Legalität empirisch geklärt + Nav0118

Gegen die realen Framework-Quellen (`END.cs`, `WfsBaseEmitter.cs`) verifiziert: `END` trägt **kein**
`IINIT_TASK`; V1 erzeugt für `init --> End` bereits **nicht-kompilierenden** Code (`return
EndNonModal();` in `IINIT_TASK Begin()` → CS0266). **Nav0110** deckt nur den Edge-Mode-Teil ab
(`o->`/`==>` aus Init-Reichweite), **nicht** `--> End` (Goto-Mode-Kante, aber `END ∉ IINIT_TASK`).
→ neuer, versionsUNabhängiger Analyzer **Nav0118** implementiert: aus einem Init per Goto erreichbare
End-Knoten werden abgelehnt (Details §4.7/④a, §5). Lehre: `nav.exe`-Codegen-Erfolg ≠ Kompilierbarkeit
— erst `csc` gegen das Framework ist das Gate.

### Leitentscheidungen (Runde 7): EIN mode-freies `Show{Node}` + typ-erzwungener Concat

Auslöser: der Anzeige-Modus einer View/Dialog wird im **Nav via Edge** festgelegt (`-->`/`o->`/`==>`),
**nie** vom Autor im C#-Code durch Methodenwahl. Ein mode-tragender Methodenname suggeriert also eine
Wahl, die es nicht gibt. Und der erste Schritt eines Concat ist **derselbe `GotoGUI(to)`** wie ein
schlichter `--> View`-Goto (§4.5). Daraus fünf Entscheidungen:

1. **Ein View-Verb `Show{Node}` für alle drei Anzeige-Modi.** Der Modus lebt nur im generierten Body
   (`GotoGUI`/`OpenModalGUI`/`StartNonModalGUI`), nicht im Namen. Das **löst die letzte offene Frage
   §7.1 (Verb-Lexikon) auf** — statt `Goto`/`OpenModal`/`ShowNonModal` gibt es nur `Show`. Der
   Node-Name bleibt als Suffix (`ShowView`, quellenstabil — Runde 4). Modus optional per XML-Doc am
   Member dokumentierbar. `Begin{Task}` (Task-Kanten) bleibt unverändert.
2. **Concat wird durch den Rückgabetyp kodiert, nicht durch den Namen.** `Show{Node}(to)` liefert
   `Result` (direkt returnbar) ohne Concat, `Continuation` (mit `.Begin{Task}(…)`) bei Concat. Damit
   erfüllt sich die Nutzer-Kernforderung **strukturell**: bei erzwungenem Concat ist
   `return ctx.Show{Node}(to);` ein Compile-Fehler; und **symmetrisch** kann kein Concat hinzugefügt
   werden, den das Nav nicht definiert. Das Typsystem spiegelt die Nav-Definition exakt.
3. **Choices tragen Continuations** (Beispiel §4.1/§4.4: `Choice_Retry --> View o-^ Msg`). Der
   Choice-Context bekommt dieselbe `Show{Node}`+Continuation-Fläche wie Transition-Contexte.
4. **Union pro Ziel-Knoten** (nicht pro Kante). Hat eine Quelle **plain UND Concat** zur selben View
   (`--> View` und `--> View o-^ Msg`), bündelt **eine** `Show{View}`-Methode beide Behandlungen: der
   Rückgabetyp trägt einen impliziten `Result`-Operator (weil eine plain-Kante existiert) **und** je
   Concat-Kante ein `.Begin{Task}(…)`. „Erzwungener Concat" heißt damit präzise **concat-only** (keine
   plain-Schwesterkante) — nur dort fehlt der implizite Operator und erzwingt `.Begin{Task}`. Die
   Guards (`if/else`) sind in V2 Doku-Charakter; die Union ist genau die Menge der vom Nav deklarierten
   legalen Ausgänge (§4.3/§4.5).
5. **Continuation-Verb bleibt `Begin{Task}`** (das Pattern existiert in V1 schon); der Rücksprung aus
   dem angehängten Task erfolgt wie gehabt in `After{Task}`. **Rest-Kollision** (separat): zwei Kanten
   zum selben Ziel mit **unterschiedlichem Anzeige-Modus** (goto vs. modal, beide ohne Concat) →
   gleiche `Show{Node}(ViewTO)`-Signatur, nur Body-Modus verschieden → echte C#-Kollision, **nicht**
   über Rückgabetyp lösbar. Fachlich vermutlich ohnehin sinnlos/illegal → **eigene Diagnose** deckt es
   auf (§5), kein Codegen-Sonderfall.

### Leitentscheidungen (Runde 8): Guard-Zentralisierung, notimplemented/donotinject, Diagnose-IDs, Choice-Ketten, Exit-Bestätigung

Sieben Gabelungen im Grill-Durchgang entschieden — alle gegen Code/Korpus (`d:\tfs\Main`, ~1800 `.nav`)
verifiziert. Damit sind die zuvor offenen §7-Punkte geschlossen.

1. **Null-Guard immer — zentralisiert als `Guard<T>` je `{Task}WFSBase`.** Der V1-`switch`-`default:`
   fing `null` **und** unerwartete Marker mit `NavCommandBody.ComposeUnexpectedTransitionMessage` ab; in
   V2 ist der Marker-Fall strukturell unmöglich, übrig bleibt nur `return null;`. Statt pro Transition
   (die eigentliche 1000-fach-Duplikation) einen Guard zu emittieren, generiert V2 **einmal je
   `{Task}WFSBase`** einen rein null-werfenden Helfer und lässt die Maschinerie auf einen geschützten
   Einzeiler kollabieren:
   ```csharp
   protected static T Guard<T>(T? result, string logicMethodName) where T : class
       => result ?? throw new InvalidOperationException(
              NavCommandBody.ComposeUnexpectedTransitionMessage(logicMethodName, null));

   public virtual IINIT_TASK Begin(string message)
       => Guard(BeginLogic(message, new Init1CallContext(this)), nameof(BeginLogic)).Body;
   ```
   Kein Interface, keine gemeinsame Basis (verträgt sich mit Runde-4-Nr.-1): `Guard<T>` fasst `.Body`
   nicht an — der Zugriff erfolgt am Call-Site in der Basisklasse, die (als umschließender Typ) auch
   `private` genestete Member ihrer `Result`-Typen erreicht. **`Result.Body` darf damit `private`
   bleiben** → maximale Leck-Hygiene. Platzierung bewusst **pro Klasse (Nx identische 3-Zeiler)**, nicht
   assembly-weit (1x): eine geteilte Generat-Datei wäre neue Infrastruktur mit Inkrement-Build-Risiko
   (Manifest-Zeitstempel), ein Framework-Helfer verstieße gegen „minimale neue Framework-API". Die Nx
   Kopien sind kosmetisch; die relevante Duplikation (pro Transition) ist eliminiert. §4.2a nachziehen.

2. **`[notimplemented]` und `[donotinject]` werden in V2 unterstützt (korpus-real, kein opt-out).**
   Beide kommen produktiv vor (`notimplemented`: 5, `donotinject`: 6 echte `.nav`), also **kein**
   „in `#version 2` verboten" — sonst entstünde eine nie migrierbare V1-Insel.
   - **`[notimplemented]`:** `ctx.Begin{Task}(…)` liefert einen `Result`, dessen **Thunk**
     `NotImplementedException` wirft — V1-Timing exakt (Wurf beim `.Body`-Unwrap, wie V1s `switch`):
     `public Result BeginFoo(/*args*/) => new(() => throw new NotImplementedException("Task Foo is specified as [notimplemented]"));`.
   - **`[donotinject]`:** `ctx.Begin{Task}` nimmt den Wrapper als **expliziten Parameter** — der
     **originalgetreue** V1-Port (`WfsBaseEmitter`/generiert: `BeginDoSomething(IBeginShowSomethingWFS wfs)`).
     Fachlicher Grund: donotinject steht für eine **Familie konkreter Implementierungen, laufzeit-selektiert**
     (belegt in `DublettenMischenWFS.cs`: `_valueEditorList.SingleOrDefault(c => editor.IsAssignableFrom(c.GetType()))`) —
     es *gibt* nichts zu injizieren; der explizite Parameter ist die ehrliche Signatur. Die Laufzeit-Auswahl
     bleibt unverändert im `…Logic(args, ctx)`-Override.
   - Beide: eigener Abschnitt in §4.3-Tabelle + je ein Golden-Snapshot-Fall.

3. **Union `plain + concat`: implizite Konvertierung bleibt, mit Doc-Politur.** Die
   `static implicit operator Result(ShowViewContinuation)`-Lösung (§4.4) wird **nicht** umgebaut. Ihre
   einzige Schwäche — der unsichtbare plain-Pfad und der generische `CS0029` im concat-only-Fall — wird
   billig geheilt: sprechender Continuation-Typname + XML-Doc am Member („mit `.Begin{Task}(…)` fortsetzen"),
   plus ein Ergonomie-/Golden-Test, der den concat-only-`CS0029` als erwartetes Verhalten festschreibt.
   Der unsichtbare Pfad ist der harmlose (plain); `.Begin{Task}` ist auf dem Rückgabewert sichtbar.

4. **Diagnose-IDs: Cluster Nav0120–Nav0124 im 01xx-Strukturband.** `Nav1xxx` ist in diesem Repo
   **strikt DeadCode/Warning** — die concat-Branch-Nummern **Nav1020/1021/1022** (Error-Semantik) sind
   dort ein Konventionsbruch. Da die Concat-Feature hier **nicht** ausgeliefert ist (concat-Branch = reines
   Referenz-Zielbild), gibt es **keine** Kompatibilitätsbindung → frei umnummerieren:
   - **Nav0120** Concat-Quelle muss GUI/View-Knoten sein (concat-Branch 1020) · **Nav0121** Concat-Ziel
     muss Task-Knoten sein (1021) · **Nav0122** verschiedene Views in einer Concatenation nicht unterstützt
     (1022) · **Nav0123** `--^` (Goto-Concat) noch nicht unterstützt.
   - **Nav0124 = EINE generische Member-Kollisions-Diagnose** (Error), berechnet aus der generierten
     Member-Menge, an der `.nav`-Deklaration/-Kante verankert. Sie **ersetzt** die zuvor getrennt geplanten
     „Anzeige-Modus-Kollision" und „reservierte Namen" (**Nav0125 entfällt**) und deckt einheitlich ab:
     reservierte Namen (`Cancel`/`Exit`/`End`/`Result` als Choice), Präfix-Klasch (`Show{X}`/`Begin{X}`-Choice
     trifft gleichnamigen Knoten) **und** Modus-Kollision. Ihr Eigenwert: der **still kompilierende Overload**
     (unterschiedliche Signaturen), den `csc` **nicht** meldet — genau dort liefert die Nav-Diagnose die frühe,
     auf die `.nav`-Stelle zeigende Fehlermeldung (ein `csc`-Fehler im *generierten* Code ist kaum
     rückführbar). Zukunftssicher (keine enumerierte Sonderfall-Liste). Nur bare-name Choice-Forwards sind
     überhaupt Kollisions-Vektor; Views/Tasks sind `Show`/`Begin`-präfixt.
   - Versions-Gate für Concat-Kanten + Choice-`[params]`: **kein** neuer Code — bestehendes **Nav5000**.
   - **Korpus: 0 Kollisionen** (Choices namens `Cancel`/`Exit`/`End`/`Result`: 0/5637; `Show*`/`Begin*`-Choices
     existieren zwar zahlreich, kollidieren aber mit keinem gleichnamigen Knoten). Die Diagnose ist also
     frühwarnende Versicherung, kein häufiger Fall — der Wert liegt in der Rückführbarkeit, nicht der Frequenz.

5. **Verschachtelte Choices (`Choice --> Choice`): rekursives Forwarding.** In Nav legal und von der
   Reachability rekursiv aufgelöst (`EdgeExtensions.GetReachableCallsImpl`, mit Zyklenschutz). V2 faltet
   **nicht** platt (das würde die von V2 gekillte Duplikation für Ketten wiedereinführen), sondern
   forwarded: `Choice_A`s Context bekommt `ctx.Choice_B(params)` → `Choice_BLogic(…).Body` deferred —
   dieselbe Mechanik wie Transition→Choice, eine Ebene tiefer. **Anti-Bloat bleibt transitiv** (jede
   `Choice_XLogic` existiert einmal), Init-Legalitäts-Typisierung greift automatisch (Choice_B ist mit
   Choice_A init-erreichbar). Choice→Choice explizit in §4.3-Tabelle. Ein Choice-**Zyklus** ergäbe sich
   gegenseitig referenzierende Context-Methoden (kompiliert); ob er zur Laufzeit kreist, entscheidet die
   Nutzer-Logik — kein Codegen-Problem.

6. **Fixes `Exit(result)` bestätigt (kein `Exit{Node}`).** Herausforderung: Tasks *können* mehrere
   Exit-Knoten deklarieren (`Wizard`: `exit Done; exit Esc;`). Aber der Task-Result ist **einwertig**
   (`CodeTaskResult` pro Task), und das reale Framework `InternalTaskResult<TResult>(TResult result)`
   (`BaseWFService.cs:236`) trägt **keine** Exit-Identität → mehrere Exit-Knoten kollabieren auf dasselbe
   `TASK_RESULT<T>`; V1 unterscheidet sie am Child-Ende nicht. `--> Done`/`--> Esc` bilden beide auf das
   fixe `Exit(result)` ab — V1-treu. Multi-Exit-Tasks: **0 Produktion / 1 Framework-Test** → kein
   Ergonomie-Verlust. §4.3 hält.

7. **`Logic`-Suffix, gemeinsame Basis, Deferred-Thunk** — allesamt in früheren Runden entschieden und im
   Grill **bestätigt**, keine Änderung. Die Deferred-Thunk-Allokation (`Result` + `Func<>` je Transition)
   ist unkritisch: Navigation läuft auf Interaktions-, nicht Schleifentempo; V1 allokierte Marker + Thunks
   vergleichbar.

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
  einen `INavCommandBody`-Marker zurück; in V2 liefert `Result.Body` das **fertige Framework-Kommando**
  (`IINIT_TASK` bei Init-Transitionen, `INavCommand` bei Trigger/Exit). Die Body→Kommando-Übersetzung,
  die früher der `switch` machte, sitzt jetzt in den Context-Methoden (§4.2a).
- **`Result` kapselt das Kommando *deferred* (`Func<…>`), gebaut erst beim `.Body`-Zugriff
  (Runde 5).** Grund: die Konstruktoren von `GOTO_GUI`/`OPEN_MODAL_GUI`/`.Concat(ITASK_BOUNDARY)`
  haben **Seiteneffekte** (Framework-verifiziert, §4.7). Würde die Fabrikmethode eager bauen, feuerte
  der Effekt schon beim Aufruf — auch wenn das Ergebnis nie zurückgegeben wird. Der Thunk verschiebt
  ihn auf den `.Body`-Unwrap in der Maschinerie (V1-Timing).

### 4.2a Kollabierter Dispatch: Maschinerie = `.Body`-Unwrap (Runde 3/5)

Weil der `Result` das Kommando (deferred) trägt, schrumpft **jede** Maschinerie-Methode auf einen
Unwrap — der hundertfache `switch` entfällt. Der `.Body`-Zugriff wertet den Thunk aus und feuert damit
die Kommando-Konstruktion (inkl. etwaiger Seiteneffekte) an genau der Stelle, an der V1 den `switch`
lief — **nach** der Logic:

```csharp
public virtual IINIT_TASK Begin(string message)
    => Guard(BeginLogic(message, new Init1CallContext(this)), nameof(BeginLogic)).Body;

public virtual INavCommand OnFoo(ViewTO to) {
    to = BeforeTriggerLogic(to);                                                  // Trigger-Vorlauf bleibt
    return Guard(OnFooLogic(to, new OnFooCallContext(this)), nameof(OnFooLogic)).Body;  // kein switch mehr
}
```

Der `Guard(…)`-Aufruf ist der **immer** emittierte Null-Guard (Runde 8, s.u.) — ein geschützter
Einzeiler ersetzt den `switch` vollständig.

Die Context-Methoden sind expression-bodied Einzeiler, die den Kommando-Bau in einen `Func<…>`
kapseln (Runde 5 — der Thunk verschiebt den Seiteneffekt der `GOTO_GUI`/`OPEN_MODAL_GUI`/`Concat`-
Konstruktoren auf den `.Body`-Unwrap; der Begin-Aufruf des Sub-Tasks bleibt zusätzlich als
`BeginTaskWrapper`-Thunk deferred):

```csharp
public Result ShowView(ViewTO to)   => new(() => _wfs.GotoGUI(to));   // plain-only: direkt Result (Runde 7)
public Result BeginB(string b1)     => new(() => _wfs.OpenModalTask<FooResult>(() => _wfs._b.Begin(b1), _wfs.AfterB));

// … mit dem geschachtelten Result-Typ (Body bleibt private, Runde 8 — nur die Basisklasse
// erreicht ihn als umschließender Typ):
public sealed class Result {
    readonly Func<IINIT_TASK> _command;
    internal Result(Func<IINIT_TASK> command) => _command = command;
    IINIT_TASK Body => _command();               // feuert die Konstruktion beim Unwrap
}
```

**Null-Guard immer, zentralisiert (Runde 8).** Der Nutzer kann noch `return null;` schreiben (`Result`
ist eine Klasse) → nacktes `.Body` würfe eine kryptische NRE tief in der Maschinerie. Der V1-`switch`-
`default:` fing `null` **und** unerwartete Marker mit `ComposeUnexpectedTransitionMessage` ab; in V2 ist
der Marker-Fall strukturell unmöglich, übrig bleibt nur `null`. Statt den Guard **pro Transition** (die
eigentliche 1000-fach-Duplikation) zu emittieren, generiert V2 **einmal je `{Task}WFSBase`** einen rein
null-werfenden Helfer — die Maschinerie ruft ihn als geschützten Einzeiler (s.o.):

```csharp
protected static T Guard<T>(T? result, string logicMethodName) where T : class
    => result ?? throw new InvalidOperationException(
           NavCommandBody.ComposeUnexpectedTransitionMessage(logicMethodName, null));
```

`Guard<T>` fasst `.Body` **nicht** an — der Zugriff erfolgt am Call-Site in der Basisklasse, die (als
umschließender Typ) auch `private` genestete Member ihrer `Result`-Typen erreicht. Deshalb **kein
Interface / keine gemeinsame Basis** (verträgt sich mit Runde-4-Nr.-1), und `Result.Body` darf `private`
bleiben (maximale Leck-Hygiene). Platzierung bewusst **pro Klasse (Nx identische 3-Zeiler)** statt
assembly-weit (1x): eine geteilte Generat-Datei wäre neue Infrastruktur mit Inkrement-Build-Risiko
(Manifest-Zeitstempel), ein Framework-Helfer verstieße gegen „minimale neue Framework-API". Die Nx
Kopien sind kosmetisch; die relevante Duplikation (pro Transition) ist eliminiert.

### 4.3 Die Context-Fläche je Kanten-Art

Der Context ist die **vollständige, benannte Übergangs-Fläche** der Transition bzw. Choice — pro
tatsächlich vorhandener Nav-Kante eine Methode:

Die Spalte „baut (deferred)" ist das Framework-Kommando, das der `Result`-Thunk beim `.Body`-Unwrap
konstruiert (Runde 5, §4.2a) — kein Zwischenmarker mehr:

| Nav-Kante der Quelle | Context-Methode | baut (deferred im Thunk) |
|---|---|---|
| `-->` / `o->` / `==>` **GUI-Knoten** (View **oder** Dialog) | `Show{Node}(ViewTO)` — **mode-frei** (Runde 7) | `GotoGUI` / `OpenModalGUI` / `StartNonModalGUI` je Edge-Mode; Modal/Nonmodal nur im Task-Kontext (§4.7/④) |
| `--> View o-^ Task` (Concat) | `Show{View}(to).Begin{Task}(…)` — **selber Einstieg**, Rückgabetyp `Continuation` | `GotoGUI(to).Concat(OpenModalTask(…, After{Task}))` |
| `-->`/`o->`/`==>` `Task` | `Begin{Task}(…)` je Init-Überladung | `GotoTask`/`OpenModalTask`/`StartNonModalTask(() => _wfs._x.Begin(…), After{Task})` |
| `-->`/`o->`/`==>` `Task` **`[notimplemented]`** (Runde 8) | `Begin{Task}(…)` (existiert weiter) | `throw new NotImplementedException("Task {Task} is specified as [notimplemented]")` im Thunk — V1-Timing (s. Absatz unten) |
| `-->`/`o->`/`==>` `Task` **`[donotinject]`** (Runde 8) | `Begin{Task}(IBegin{Task}WFS wrapper, …)` — **expliziter** Wrapper-Parameter | `…{mode}Task(() => wrapper.Begin(…), After{Task})` — Wrapper vom Nutzer laufzeit-selektiert (s. Absatz unten) |
| `--> Choice` (auch **Choice→Choice**, Runde 8) | `{Choice}({params})` | `_wfs.{Choice}Logic({params}, new(_wfs)).Body` (Forward, §4.4; rekursiv bei Choice-Ketten) |
| `--> Exit` | `Exit({result})` | `InternalTaskResult(result)` → `TASK_RESULT<T>`, castfrei (§4.7/②) |
| `--> End` | `End()` | `EndNonModal()` → `END` |
| immer | `Cancel()` | `Cancel()` → `CANCEL` |

**`[notimplemented]`/`[donotinject]` (Runde 8, beide korpus-real — 5 bzw. 6 echte `.nav`, kein opt-out).**
`[notimplemented]`: der Ziel-Task bleibt begin-bar, scheitert aber beim `.Body`-Unwrap mit
`NotImplementedException` — exakt V1s Laufzeitverhalten, nur ins Thunk-Modell überführt.
`[donotinject]`: der Wrapper wird **nicht** injiziert (kein `_wfs._x`-Feld), also nimmt `ctx.Begin{Task}`
ihn als **expliziten Parameter** — der originalgetreue V1-Port (`BeginDoSomething(IBeginShowSomethingWFS wfs)`).
Fachlich steht `[donotinject]` für eine **Familie konkreter Implementierungen, laufzeit-selektiert**
(`_valueEditorList.SingleOrDefault(c => editor.IsAssignableFrom(c.GetType()))`) — es *gibt* nichts zu
injizieren; die Laufzeit-Auswahl bleibt unverändert im `…Logic(args, ctx)`-Override.

**Mehrere Exit-Knoten (Runde 8):** `Exit({result})` ist **ein fixes** Member auch bei Tasks mit mehreren
`exit`-Knoten. Der Task-Result ist einwertig, und das reale `InternalTaskResult<T>(result)` trägt **keine**
Exit-Identität → `--> Done`/`--> Esc` kollabieren auf dasselbe `TASK_RESULT<T>` (V1 unterscheidet sie am
Child-Ende ebenfalls nicht). Kein `Exit{Node}`; Korpus: 0 Produktion / 1 Framework-Test.

Die `Begin{Task}`-Überladung folgt dem Edge-Mode: `-->` → `GotoTask` (init-legal aus jedem Kontext),
`o->`/`==>` → `OpenModalTask`/`StartNonModalTask` (**nur** im Task-Kontext, da nicht `IINIT_TASK`;
§4.7/④). Aus einem Init sind nur `GotoTask`/`Show{View}` (Goto-Mode)/`Exit`/`Cancel`/`Show{View}(…).Begin…`
(Concat) zulässig. Das bisherige Idiom `return to;` entfällt in V2 zugunsten von `return ctx.Show{View}(to);`.

**Task-Namensschema ist V1-Präzedenz (Runde 7):** `Begin{Node}` ist **nicht neu** — V1 emittiert bereits
`{BeginMethodPrefix}{TaskNodeName}` (`WfsBaseEmitter.cs:280`), aus dem lokalen `taskNode.Name`
(`BeginWrapperCodeModel.cs`), überladen über die Init-Transitionen des Ziel-Tasks. V2 ändert nur den
**Rückgabetyp** (V1: `TaskCall`-Marker + Switch-Mapping pro Kante auf `GotoTask`/`OpenModalTask`; V2:
opaker `Result`, Modus wandert **in** `ctx.Begin{Node}`). Weil der **CallContext pro Quelle** ist,
kollidiert das nicht: erreichen zwei *verschiedene* Quellen denselben Task-Knoten mit verschiedenem
Modus, sind das zwei Context-Klassen mit je eigenem `Begin{Node}`; die Anzeige-Modus-Kollision (§5)
entsteht nur, wenn *eine* Quelle zwei verschieden-modale Kanten zum selben Knoten hat.

**Namenskonvention View-Kanten (Runde 7, ersetzt Runde 4 Nr. 3 für Views):** EIN mode-freies Verb
`Show{NodeName}`. Der Anzeige-Modus (`GotoGUI`/`OpenModalGUI`/`StartNonModalGUI`) ist im Nav-Edge
festgelegt und lebt nur im generierten Body, **nicht** im Namen — der Autor hat keine Wahl, also gibt
es kein Verb-Lexikon. Der Node-Name bleibt als Suffix (quellenstabil, mehrere GUI-Knoten unterscheidbar).

**`Show{Node}` deckt beide GUI-Knoten-Arten ab (Runde 7):** `IViewNodeSymbol` **und**
`IDialogNodeSymbol` sind beide `IGuiNodeSymbol` und bauen dieselben GUI-Kommandos. View vs. Dialog ist
eine **Aussehens-**, keine Verhaltens-Unterscheidung → dasselbe Verb `Show`, der Node-Name trägt den
Unterschied (`ShowHomeView`, `ShowLoginDialog`). Die **Modalität ist eine Eigenschaft des Aufrufers
(der Kante), nicht des Knotens** — genauer: des aufgerufenen Sub-Workflows („starte modal / nicht
modal"), unabhängig davon, ob er ein Fenster hochschaltet. Deshalb ersetzt die Knoten-Art den Edge-Mode
**nicht**, und die „mode-frei"-These hält auch an der Dialog-Kante. Empirischer Anker: `o->`/`==>` ist
aus `init` verboten (Modus = Aufruf-Eigenschaft, ein Init ruft nicht modal auf) — genau das erzwingt
**Nav0110** bereits (§4.7/④).

**Union pro Ziel-Knoten (Runde 7):** hat eine Quelle mehrere Kanten zur *selben* View, bündelt **eine**
`Show{View}`-Methode deren Behandlungen. Der Rückgabetyp entsteht pro Ziel aus der Union:

| Kanten Quelle→View | `Show{View}(to)` liefert | `return ctx.Show{View}(to);` |
|---|---|---|
| nur plain | direkt `Result` | ✓ |
| nur Concat (**erzwungen**) | `Continuation` (kein `Result`) | ✗ → `.Begin{Task}(…)` erzwungen |
| plain **und** Concat | Typ mit implizitem `Result` **und** `.Begin{Task}(…)` | ✓ plain / `.Begin{Task}` Concat |

Der implizite `Result`-Operator wird genau dann emittiert, wenn eine plain-Kante existiert; je
Concat-Kante ein `.Begin{Task}`. So spiegelt der Typ exakt die Nav-Definition („erzwungener Concat" =
concat-only, keine plain-Schwesterkante). Die Guards (`if/else`) sind in V2 Doku-Charakter — die Union
ist genau die Menge der vom Nav deklarierten legalen Ausgänge.

**Reservierte Namen (Runde 7 revidiert, Runde 8 zusammengefasst):** fixe Member `Cancel`/`Exit`/`End`
und der genestete Typ `Result` bleiben reserviert; `Show` ist nun **Verb-Präfix** (nie bloß) und fällt
als fixes Member weg; die Continuation-Typen sind node-suffigiert (`Show{View}Continuation`). Weil nur
der **bare-name Choice-Forward** `{Choice}(…)` überhaupt kollidieren kann (Views/Tasks sind
`Show`/`Begin`-präfixt), fällt die Kollisionsprüfung in Runde 8 mit der Modus-Kollision zu **einer
generischen Member-Kollisions-Diagnose (Nav0124, §5)** zusammen — kein eigener reservierte-Namen-Analyzer,
kein stilles Mangling. Sie deckt reservierte Namen, Präfix-Klasch (`Show{X}`/`Begin{X}`-Choice trifft
Knoten `X`) und Modus-Kollision einheitlich ab; ihr Eigenwert ist der **still kompilierende Overload**,
den `csc` verschweigt. Korpus: 0 Kollisionen jeder Art.

### 4.4 Choices in C#: Context + abstrakte Logic (Runde 3: ohne Dispatch)

Eine Choice wird zu **zwei einmal generierten Bausteinen** — egal, wie viele Quellen auf sie zeigen.
Der frühere dritte Baustein (`DispatchChoice_X`) ist mit dem Kollaps (§4.2a) **entfallen**: der
Context baut die finalen Kommandos schon, die Logic gibt sie fertig zurück.

```csharp
// Baustein 1: der Choice-Context — baut die finalen Kommandos der Choice-Ausgänge.
// Choice_Retry hat ZWEI Kanten zur selben View (--> View  und  --> View o-^ Msg) → EINE
// mode-freie Show-Methode mit Union-Rückgabetyp (Runde 7, §4.3).
protected sealed class Choice_RetryCallContext {

    readonly SampleWFSBase _wfs;
    internal Choice_RetryCallContext(SampleWFSBase wfs) => _wfs = wfs;

    /// Opaker Ergebnistyp: nur dieser Context kann ihn erzeugen; das Kommando wird deferred
    /// gebaut (Thunk, §4.2a). Body ist init-legal (IINIT_TASK), da Choice_Retry aus einem Init
    /// erreichbar ist und das Semantic Model init-legale Ausgänge erzwingt (§4.7/④).
    public sealed class Result {
        readonly Func<IINIT_TASK> _command;
        internal Result(Func<IINIT_TASK> command) => _command = command;
        internal IINIT_TASK Body => _command();
    }

    // Beide Choice_Retry --> View-Kanten (plain + o-^ Msg) → EINE Methode, Union der Behandlungen.
    public ShowViewContinuation ShowView(ViewTO to) => new(_wfs, to);
    public sealed class ShowViewContinuation {
        readonly SampleWFSBase _wfs; readonly ViewTO _to;
        internal ShowViewContinuation(SampleWFSBase wfs, ViewTO to) { _wfs = wfs; _to = to; }

        // plain-Kante (--> View) existiert → direkt als Result returnbar:
        public static implicit operator Result(ShowViewContinuation v)
            => new(() => v._wfs.GotoGUI(v._to));

        // Concat-Kante (--> View o-^ Msg) existiert → Continuation (§4.5, kein ConcatCommand):
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
abstrakte Choice-Logic direkt auf und **forwardet** deren fertiges Kommando (`.Body`) in den eigenen
`Result` — kein Marker, kein geteilter Dispatch mehr:

```csharp
protected sealed class Init1CallContext {
    readonly SampleWFSBase _wfs;
    internal Init1CallContext(SampleWFSBase wfs) => _wfs = wfs;

    public sealed class Result {
        readonly Func<IINIT_TASK> _command;
        internal Result(Func<IINIT_TASK> command) => _command = command;
        internal IINIT_TASK Body => _command();
    }

    // Init1 --> Choice_Retry: Choice-Logic aufrufen und Kommando durchreichen (deferred:
    // die Choice-Entscheidung UND der Kommando-Bau feuern erst beim .Body-Unwrap)
    public Result Choice_Retry(string reason) =>
        new(() => _wfs.Choice_RetryLogic(reason, new(_wfs)).Body);

    public Result Cancel() => new(() => _wfs.Cancel());
}

// Maschinerie: nur noch Unwrap mit Null-Guard (§4.2a, Runde 8)
public virtual IINIT_TASK Begin(string message)
    => Guard(BeginLogic(message, new Init1CallContext(this)), nameof(BeginLogic)).Body;

protected abstract Init1CallContext.Result BeginLogic(string message, Init1CallContext callContext);
```

Der `Result`-Typ der Quelle ist bewusst ein **eigener** (nicht der Choice-`Result`): das Forwarden
re-boxt und erhält so die Leck-Verhinderung (ein `Choice_RetryCallContext.Result` lässt sich **nicht**
direkt aus `BeginLogic` zurückgeben — die Quelle *muss* durch `ctx.Choice_Retry(…)`).

Die Guards (`if "Fehler"`/`else`) an Choice-Kanten behalten ihren heutigen **Doku-Charakter** — die
Entscheidung trifft frei formulierter Nutzer-Code in der Choice-Logic, nicht der Generator.

**Verschachtelte Choices `Choice_A --> Choice_B` (Runde 8).** In Nav legal; die Reachability löst
Choice-Ketten rekursiv auf (`EdgeExtensions.GetReachableCallsImpl`, mit Zyklenschutz). V2 faltet **nicht**
platt (das brächte die von V2 gekillte Duplikation für Ketten zurück), sondern forwardet **eine Ebene
tiefer**: `Choice_A`s Context bekommt `{Choice_B}({params})` → `_wfs.Choice_BLogic({params}, new(_wfs)).Body`
(deferred) — dieselbe Mechanik wie Transition→Choice. **Anti-Bloat bleibt transitiv** (jede
`Choice_XLogic` existiert einmal), und die Init-Legalitäts-Typisierung greift automatisch (ist `Choice_A`
init-erreichbar, ist `Choice_B` es transitiv auch → beider `Result.Body` ist `IINIT_TASK`). Ein
Choice-**Zyklus** ergäbe sich gegenseitig referenzierende Context-Methoden (kompiliert sauber); ob er zur
Laufzeit kreist, entscheidet allein die Nutzer-Logik — kein Codegen-Problem.

### 4.5 Concat-Spezialform (Runde 7: derselbe `Show{View}`-Einstieg, Rückgabetyp `Continuation`)

Der Concat ist **kein eigener Einstieg** mehr (Runde 7): dieselbe `Show{View}`-Methode liefert statt
`Result` eine `Continuation`, sobald die Kante einen Concat trägt. Deren `Begin{Task}(…)` baut das
Concat-Kommando **deferred** im `Result`-Thunk: `GotoGUI(to).Concat(OpenModalTask(…, After{Task}))`
(Runde 3/5 — kein `ConcatCommand`-Marker, kein `ContinueWith`-Sub-Switch mehr; die Mechanik aus dem
concat-Branch ist in die Context-Methode gewandert). Der Rücksprung aus dem angehängten Task erfolgt
wie in V1 über `After{Task}`. Wichtig: `GotoGUI` **und** `.Concat(ITASK_BOUNDARY)` haben
Konstruktor-Seiteneffekte (§4.7/⑤) — daher zwingend im Thunk, nicht eager. `OpenModalTask` →
`OPEN_MODAL_TASK : ITASK_BOUNDARY` wählt am Framework die Überladung `Concat(ITASK_BOUNDARY)` →
`TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : IINIT_TASK`. `.Concat(…)` ist die einzige neue Framework-API
(§4.7).

**Rückgabetyp pro Ziel-View (Union, §4.3):** existiert **nur** eine Concat-Kante zu dieser View (keine
plain-Schwester), fehlt der implizite `Result`-Operator → `return ctx.Show{View}(to);` ist ein
Compile-Fehler, der Autor **muss** `.Begin{Task}(…)` anhängen. Existiert zusätzlich eine plain-Kante,
ist beides zulässig (Nutzer-Beispiel `Choice_Retry`, §4.4). So spiegelt der Typ exakt die
Nav-Definition — „erzwungener Concat" = concat-only.

Zum Start wird **nur `o-^`** unterstützt (→ `OpenModalTask`); `--^` wird per Diagnostic abgelehnt
(Leitentscheidung Runde 2, Nr. 4).

### 4.6 Nutzer-Code (der Elegance-Payoff)

```csharp
// Entscheidung EINMAL:
protected override Choice_RetryCallContext.Result Choice_RetryLogic(
        string reason, Choice_RetryCallContext ctx) {
    if (reason is null) return ctx.ShowView(CreateViewTO());          // plain (implizit → Result)
    return ctx.ShowView(CreateViewTO()).BeginMsg(reason);             // Messagebox obendrauf (Concat)
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

### 4.7 Laufzeit-Bausteine & Rückgabetyp-Regel (Runde 3, verifiziert Runde 5)

Durch den Dispatch-Kollaps schrumpft die neue Laufzeit-Fläche drastisch — **`ChoiceCall` und
`ConcatCommand` entfallen ganz** (waren nur Marker für den entfernten Switch/`ContinueWith`). Alle
fünf früheren Unbekannten sind am **realen** Framework verifiziert (`doc/WFS-Spracherweiterung —
Framework-Verifikation.md`):

- **① `.Concat(…)`** — einzige neue Framework-API, öffentliche **Instanzmethode** auf `GOTO_GUI`
  (keine Extension; überladen auch auf `OPEN_MODAL_GUI`/`TWO_STEP_IINIT_TASK`). Parameter sind die
  **Tagging-Interfaces** `INOT_A_TASK_BOUNDARY`/`ITASK_BOUNDARY` (nicht `INavCommand` allgemein).
  `GotoGUI(to).Concat(OpenModalTask(…))` → `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : TWO_STEP, IINIT_TASK`
  → `IINIT_TASK` **und** `INavCommand`.
- **② Exit ohne Cast** — `InternalTaskResult<T>` liefert real `TASK_RESULT<T>` (ein Objekt vereint
  `INavCommandBody` **und** `IINIT_TASK, ITASK_BOUNDARY, NavCommand`). Die `ctx.Exit`-Fabrik konkret
  als `TASK_RESULT<T>` typisieren → **statischer Upcast, kein Cast, keine Schwester nötig**. (Nur an
  einem `INavCommandBody`-typisierten Zwischenwert wäre `(TASK_RESULT)…` nötig — dann laufzeitsicher.)
- **③ `ctx.Cancel()`** — `_wfs.Cancel()` ist eine echte **Factory-Methode** (`new CANCEL()`); `CANCEL`
  ist `IINIT_TASK` **und** `INavCommandBody`. Kein Singleton/Property/`EscapeTask`.
- **⑤ Seiteneffekte in Konstruktoren → Thunk zwingend:** `GOTO_GUI`, `OPEN_MODAL_GUI` und
  `.Concat(ITASK_BOUNDARY)` feuern im **Konstruktor** Seiteneffekte (GUI-Navigation bzw.
  `ExecuteCallResult`); nur die feld-speichernden Commands (`OPEN_MODAL_TASK`/`START_NONMODAL_TASK`/
  `TASK_RESULT`/`CANCEL`/`END`/`GOTO_TASK`) sind rein. Deshalb kapselt `Result` den Bau **deferred**
  (§4.2a) — der Effekt feuert erst beim `.Body`-Unwrap, wie in V1.

**Rückgabetyp-Regel für `Result.Body`** (löst den früheren §4.8-Klärpunkt strukturell):

- **Transition-Context:** `IINIT_TASK` bei Init-Transitionen, `INavCommand` bei Trigger/Exit —
  entspricht exakt dem Maschinerie-Rückgabetyp, also `return …Logic(…).Body;` **ohne Cast**.
- **Choice-Context:** `IINIT_TASK`, sobald die Choice aus **irgendeiner** Init-Quelle erreichbar ist,
  sonst `INavCommand`. Weil `IINIT_TASK : INavCommand`, ist ein init-typisierter Choice-`Result.Body`
  auch von Trigger-/Exit-Quellen zuweisbar (Forward in §4.4).
- **④ Init-Legalität ist eine echte Einschränkung, keine Selbstverständlichkeit.** Das Framework macht
  `IINIT_TASK` **gezielt selektiv**: `OPEN_MODAL_TASK`/`OPEN_MODAL_GUI`/`START_NONMODAL_TASK`/`END` sind
  **nicht** `IINIT_TASK` (nur `GOTO_GUI`/`GOTO_TASK`/`TASK_RESULT`/`CANCEL`/`GotoGUI(…).Concat(…)`). Ein
  init-typisierter `Result.Body` ist also nur baubar, wenn **alle** aus einem Init erreichbaren Ausgänge
  in dieser Menge liegen. Das **muss das Semantic Model erzwingen** (§5).
- **④a `--> End` aus Init ist die konkrete Lücke — verifiziert (Runde 6).** `END : NavCommand,
  ITASK_BOUNDARY, INavCommandBody` (bestätigt an `END.cs`) trägt **kein** `IINIT_TASK`. Der V1-Generator
  emittiert für `init --> end` aber `public virtual IINIT_TASK Begin() { … case END _: return
  EndNonModal(); }` (`WfsBaseEmitter.cs:194`+`:333–337`), und `EndNonModal()` liefert `END` → die
  Zuweisung `END → IINIT_TASK` ist **CS0266**. V1 erzeugt für diesen (im Korpus offenbar nicht
  vorkommenden) Fall also bereits **nicht-kompilierenden** Code. **Nav0110 fängt es *nicht*:** `--> End`
  ist eine **Goto-Mode**-Kante und passiert Nav0110 (das nur `EdgeMode != Goto` in Init-Reichweite
  verbietet) — die `IINIT_TASK`-Mitgliedschaft ist ein *anderes* Kriterium als der Edge-Mode, und für
  `End` fallen beide auseinander. Der §5-Analyzer braucht daher **zusätzlich** eine Regel gegen `--> End`
  in Init-Reichweite (bzw. eine Nav0110-Erweiterung von „Edge-Mode" auf „`IINIT_TASK`-Zielkommando"). Ein
  reiner `nav.exe`-Codegen-Erfolg beweist die Kompilierbarkeit hier **nicht** — erst `csc` gegen das
  Framework ist das maßgebliche Gate (Roslyn/IDE-verifiziert: `CS0266: Cannot implicitly convert type
  '…IWFL.END' to '…IWFL.IINIT_TASK'` an `return EndNonModal();`). **Umgesetzt als Analyzer `Nav0118`
  (Runde 6, versionsUNabhängig)** — ein eigener Analyzer, Nav0110 unverändert.

## 5. Syntax & Semantic Model (versionsunabhängig)

Beides ist **nicht** versionsspezifisch und wird einmal für alle Codegen-Versionen vorwärts
portiert — **nach** dem Design:

- **Syntax:** Tokens `--^`/`o-^`, Grammatik/Lexer/Parser, `ConcatTransitionSyntax`, generierte
  Visitor/Walker. **Neu (Runde 2):** `[params …]`-Klausel an der `choice`-Deklaration, analog
  `init` (Wiederverwendung `ParameterListSyntax`).
- **Semantic Model:** `IConcatTransition`/`ConcatTransition`, `IConcatableEdge`, `ContinuationCall`
  in `Call`, Edge-Mode-Behandlung, Analyzer **Nav0120/0121/0122** (Runde 8 umnummeriert aus den
  concat-Branch-Nummern 1020/1021/1022 — `Nav1xxx` ist hier **strikt DeadCode/Warning**, Error-Semantik
  gehört ins 01xx-Band; kein Kompatibilitätszwang, da nicht ausgeliefert) + **Nav0222**-Fix. **Neu
  (Runde 2):** Parameter am `IChoiceNodeSymbol`.
- **Init-Legalitäts-Analyzer (Runde 5, aus Framework-Verifikation ④).** Aus einem Init erreichbare
  Ausgangskanten dürfen nur Kommandos der **`IINIT_TASK`-Menge** erzeugen (`GotoGUI`/`GotoTask`/
  `TASK_RESULT`/`CANCEL`/`GotoGUI(…).Concat(…)`); `o->`/`==>` direkt aus einem Init (→ `OPEN_MODAL_GUI`/
  `OPEN_MODAL_TASK`/`START_NONMODAL_TASK`, **nicht** `IINIT_TASK`) sowie `--> End` aus init-Reichweite
  müssen abgelehnt werden — sonst ist der `IINIT_TASK`-typisierte `Result.Body` nicht baubar (§4.7).
  **Port-Klärung erledigt (Runde 6):** **Nav0110** deckt den *Edge-Mode*-Teil bereits ab (`o->`/`==>`
  aus Init-Reichweite = `EdgeMode != Goto` → Fehler), **nicht** aber `--> End` (Goto-Mode-Kante, `END`
  ist kein `IINIT_TASK` → CS0266, §4.7/④a). **Nav0222** trägt nichts bei (nur Edge-Mode-Konsistenz).
  Es ist also eine neue Regel nötig — **umgesetzt (Runde 6) als eigener versionsUNabhängiger Analyzer
  `Nav0118` (`Nav0118EndNode0NotAllowedBecauseReachableFromInit1`, Severity Error)**: aus einem Init per
  Goto erreichbare End-Knoten (direkt oder über Choices) werden abgelehnt. Nav0110 blieb dabei
  unangetastet. Modal/Nonmodal/Modal-GUI nur *innerhalb* eines Tasks (erst `GotoGUI`, dann `.Concat(…)`).
- **Init-Signatur-Eindeutigkeit (Runde 7, versionsUNabhängig, V1-geerbt).** Zwei Init-Knoten desselben
  Tasks dürfen keine **identische Parameter-Typ-Signatur** haben. `Begin{Node}` (V2) bzw.
  `IBegin{Task}WFS.Begin` (V1, `IBeginWfsEmitter.cs`) wird über die Init-Transitionen **überladen** — der
  Init-Knotenname landet nur als Annotation, nicht im Methodennamen. Zwei signaturgleiche Inits →
  `Begin(sig)` doppelt → **CS0111** (dupliziertes Member): ein latenter, nicht-kompilierender Fall wie
  `--> End` (§4.7/④a), gegen den heute **keine** Regel schützt. **Korpus-Beweis: 0 Verstöße** (1913
  `.nav`, 2804 task/taskref-Blöcke, 3487 Init-Knoten, 419 Blöcke mit >1 Init — kein einziger mit
  doppelter Signatur) → ein neuer Analyzer (Severity **Error**, Sibling von **Nav0118**) ist
  **korpussicher** und bricht keine bestehende `.nav`. Kann — wie Nav0118 — **sofort und unabhängig
  von V2** implementiert werden. **Umgesetzt als Analyzer `Nav0119`
  (`Nav0119InitNode0HasSameSignatureAsInitNode1`, Severity Error, versionsUNabhängig):** Signatur =
  geordnete Parameter-Typen (Whitespace entfernt, `List<int>` ≡ `List< int >`; Namen irrelevant); pro
  Task erste Signatur = Referenz, jede weitere Kollision wird am Identifier des Duplikats gemeldet.
  Greift auch für **edge-lose** Inits (der V1-Generator emittiert `Begin()` für *jeden* Init-Knoten,
  `CodeModelBuilder.GetInitTransitions`). Struktur = Klon von Nav0118, Auto-Discovery. Nav0118
  unangetastet.
- **Diagnostics, versions-gated (Runde 2, IDs Runde 8):** Concat-Kanten und Choice-`[params]` sind nur
  ab `#version 2` erlaubt (in V1-Units → Fehler über das **bestehende Nav5000** „requires Nav language
  version {1}", **keine** neue ID); `--^` (Goto-Concat) wird vorerst generell abgelehnt („noch nicht
  unterstützt", Leitentscheidung Runde 2 Nr. 4) — **Nav0123**.
- **Generische Member-Kollisions-Diagnose Nav0124 (Runde 8, ersetzt die zuvor getrennt geplanten
  Namens- und Anzeige-Modus-Kollisionen).** Berechnet aus der **generierten Member-Menge** einer Quelle,
  verankert an der `.nav`-Deklaration/-Kante des Verursachers; Severity **Error**, versions-gated (nur wo
  V2-Contexte entstehen). Deckt einheitlich ab:
  - **Reservierte Namen:** ein Choice-Forward `{Choice}(…)` (der einzige **bare-name** Member — Views/
    Tasks sind `Show`/`Begin`-präfixt) namens `Cancel`/`Exit`/`End`/`Result`.
  - **Präfix-Klasch:** ein Choice namens `Show{X}`/`Begin{X}`, der auf den präfixten Member eines
    gleichnamigen GUI-/Task-Knotens `X` derselben Quelle trifft.
  - **Anzeige-Modus-Kollision:** eine Quelle mit zwei Kanten zum **selben** Ziel bei
    **unterschiedlichem Anzeige-Modus** (goto vs. modal vs. nonmodal, beide ohne Concat) → gleiche
    `Show{Node}(ViewTO)`- bzw. `Begin{Node}(…)`-Signatur, nicht über Rückgabetyp lösbar (anders als
    plain+concat, §4.3-Union — das ist **keine** Kollision).

  Ihr **Eigenwert** ist der **still kompilierende Overload** (unterschiedliche Signaturen), den `csc`
  **nicht** meldet — die harten Fälle (CS0102/CS0111) fängt der Compiler zwar, aber ein `csc`-Fehler im
  *generierten* Code ist kaum auf die `.nav`-Stelle rückführbar; die Nav-Diagnose ist die **frühe,
  zeigende** Meldung. **Korpus: 0 Kollisionen** (Choices namens `Cancel`/`Exit`/`End`/`Result`: 0/5637;
  `Show*`/`Begin*`-Choices existieren, kollidieren aber mit keinem gleichnamigen Knoten) → frühwarnende
  Versicherung, kein häufiger Fall. **Kein** eigener reservierte-Namen-Blocklist-Analyzer (Nav0125 verworfen).

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
Diagnose, Leitentscheidung Runde 4 Nr. 3/4), ~~gemeinsame Base class~~ (nein, Nr. 1). Erledigt in
Runde 5 (Framework-Verifikation, `doc/WFS-Spracherweiterung — Framework-Verifikation.md`):
~~Framework-Touchpoints (§4.7)~~ (`.Concat` = Instanzmethode auf `GOTO_GUI`; Exit castfrei via
`TASK_RESULT<T>`; `ctx.Cancel()` = Factory `_wfs.Cancel()`), ~~Eager-Bau~~ (**nicht**
seiteneffektfrei → `Result`-Thunk, §4.2a/⑤). Erledigt in Runde 6 (empirisch, gegen `END.cs` +
`WfsBaseEmitter.cs`): ~~Port-Klärung Init-Legalität~~ — **`END ∉ IINIT_TASK` bestätigt**, Nav0110
deckt nur den Edge-Mode-Teil ab, `--> End` aus Init braucht eine **neue/erweiterte** Regel (§4.7/④a,
§5). **Erledigt in Runde 6 (implementiert): ~~Init-Legalitäts-Regel~~** — als eigener,
versionsUNabhängiger Analyzer **Nav0118** (`End node '{0}' not allowed here because it's reachable
from init node '{1}'`, Severity Error) umgesetzt: aus einem Init per Goto erreichbare End-Knoten werden
abgelehnt. Zusammen mit **Nav0110** (Non-Goto aus Init) ist die `IINIT_TASK`-Menge für den V1-Command-
Satz damit vollständig abgedeckt (View→`GOTO_GUI`, Task→`GOTO_TASK`, Exit→`TASK_RESULT`, Cancel→`CANCEL`
sind init-legal; nur `--> End`→`END` war die Lücke). Erledigt in Runde 7: ~~Verb-Lexikon-Detail~~ —
**aufgelöst**, weil der Anzeige-Modus edge-bestimmt ist (nie Autor-Wahl): EIN mode-freies View-Verb
`Show{Node}` (Leitentscheidungen Runde 7). Ebenfalls erledigt in Runde 7: ~~`Logic`-Suffix an den
Override-Methoden weglassen?~~ — **entschieden: behalten.** Zwar bildet die Logic-Methode durch ihren
zusätzlichen `callContext`-Parameter eine saubere Überladung neben der Maschinerie-Methode (compiler-
eindeutig, keine Kollision), aber der Suffix trägt drei menschliche Vorteile: (a) er trennt beim
**Choice** die Rolle **call** (`ctx.Choice_X(…)`, an der Quelle) von **implement**
(`Choice_XLogic(…)`, die Entscheidung) — ohne Suffix hieße beides `Choice_X`; (b) die Maschinerie liest
nicht als Selbst-Rekursion (`Begin(m) => BeginLogic(m, ctx).Body` statt scheinbar `Begin(m) =>
Begin(m, ctx)`); (c) klarere Fehlerdiagnose bei Signatur-Tippfehlern im Override. Der Gewinn
„sauberere Namen" wiegt das nicht auf, da die Override-Methoden die Haupt-Berührungsfläche des Nutzers
sind. Verbleibend/neu:

1. ~~**Kollisions-Diagnose-IDs** (Anzeige-Modus-Kollision für GUI-/Task-Ziele, Namens-Kollision) beim
   Semantic-Model-Port vergeben und einreihen (§5).~~ **Erledigt (Runde 8):** Cluster **Nav0120–Nav0124**
   im 01xx-Band; die beiden Kollisions-Diagnosen zu **einer generischen Member-Kollision (Nav0124)**
   zusammengefasst, Nav0125 entfällt (Leitentscheidungen Runde 8 Nr. 4). Damit ist §7 leer — der
   Arbeitsvorrat wandert in den Fahrplan (§8, Syntax-/Semantic-Model-/Codegen-Port).
2. ~~**Analyzer Init-Signatur-Eindeutigkeit** (versionsUNabhängig, Error, Sibling von Nav0118) —
   korpussicher (0/419), kann sofort implementiert werden (§5). Nur noch ID + Umsetzung.~~
   **Erledigt: als `Nav0119` implementiert (§5).**

## 8. Fahrplan (nach Design-Abschluss)

Jeder Umsetzungs-Step mit Review + Build/Test + gelieferter Commit-Message (kein Selbst-Commit).

1. **V2-Codegen-Design finalisieren** — verbleibende Fragen §7 entscheiden; Golden-`.nav`-Fälle
   festschreiben (CallContext-Grundform, Concat, Choice-mit-3-Quellen aus §4.1).
2. **Syntax vorwärts portieren** — Tokens/Parser/`ConcatTransitionSyntax`, **Choice-`[params]`**,
   Visitor/Walker; Parser-/Syntax-Tests.
3. **Semantic Model vorwärts portieren** — `ConcatTransition`/`IConcatableEdge`/`ContinuationCall`,
   Choice-Parameter, Analyzer **Nav0120/0121/0122** (Concat-Struktur) + **Nav0123** (`--^`) + **Nav0124**
   (generische Member-Kollision) + Nav0222-Fix + Versions-Gate über bestehendes **Nav5000** (§5);
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
- Diagnostics-Fixtures **Nav0120–Nav0124** + Versions-Gate (Nav5000) (mit `//==>>`-Erwartungen) als
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
- **Runde 5** — **Framework-Verifikation eingearbeitet** (am realen `Framework.NavigationEngine`,
  `doc/WFS-Spracherweiterung — Framework-Verifikation.md`). Drei Annahmen bestätigt: **①** `.Concat`
  = Instanzmethode auf `GOTO_GUI` (Tagging-Interface-Parameter, Ergebnis `IINIT_TASK`+`INavCommand`);
  **②** Exit castfrei via `TASK_RESULT<T>` (keine Schwester); **③** `ctx.Cancel()` = Factory
  `_wfs.Cancel()`. Zwei Prämissen korrigiert: **④** `IINIT_TASK` ist selektiv → Semantic Model muss
  Init-Ausgänge auf die `IINIT_TASK`-Menge beschränken (neuer Analyzer-Bedarf §5; frühere
  „Nav0110/0222 garantieren das"-Annahme in §4.7 auf „beim Port zu verifizieren" abgeschwächt); **⑤**
  `GOTO_GUI`/`OPEN_MODAL_GUI`/`.Concat(ITASK_BOUNDARY)` haben **Konstruktor-Seiteneffekte** → die
  Round-3-„eager"-Prämisse fällt: `Result` kapselt das Kommando **deferred** (`Func<…>`), Konstruktion
  feuert erst beim `.Body`-Unwrap (V1-Timing; §4.2a/§4.7). Dispatch-Kollaps bleibt unberührt. §4.2–
  §4.5-Beispiele auf Thunk-Form umgestellt; §5/§7 nachgezogen.
- **Runde 6** — **Init-Legalität empirisch geklärt + Analyzer Nav0118.** Gegen `END.cs`/
  `WfsBaseEmitter.cs`: `END ∉ IINIT_TASK`, V1 erzeugt für `init --> End` CS0266-Code; **Nav0110** deckt
  nur den Edge-Mode-Teil ab, nicht `--> End` (Goto-Mode, aber `END` kein `IINIT_TASK`). Neuer
  versionsUNabhängiger Analyzer **Nav0118** implementiert (aus Init per Goto erreichbare End-Knoten →
  Error). §4.7/④a + §5 + §7 nachgezogen. Lehre: `nav.exe`-Erfolg ≠ Kompilierbarkeit (csc ist das Gate).
- **Runde 7** — **EIN mode-freies `Show{Node}` + typ-erzwungener Concat.** Auslöser: Anzeige-Modus ist
  edge-bestimmt, nie Autor-Wahl → **löst §7.1 (Verb-Lexikon) auf** (statt `Goto`/`OpenModal`/
  `ShowNonModal` nur `Show`, Modus im Body). Concat über **Rückgabetyp** kodiert (`Result` vs.
  `Continuation`) statt über den Namen → `return ctx.Show(to);` bei erzwungenem Concat = Compile-Fehler,
  symmetrisch kein ungewollter Concat. **Choices tragen Continuations**; **Union pro Ziel-Knoten** bei
  plain+concat zur selben View (impliziter `Result`-Operator gdw. plain-Kante existiert, `.Begin{Task}`
  je Concat-Kante); „erzwungener Concat" = concat-only. Continuation-Verb bleibt `Begin{Task}`,
  Rücksprung via `After{Task}`. Neue **View-Anzeige-Modus-Kollisions-Diagnose** (goto vs. modal zum
  selben Ziel, §5). **`Logic`-Suffix an Override-Methoden: behalten** (Rollentrennung call/implement
  bei Choices, keine Selbstrekursions-Lesart der Maschinerie, klarere Override-Fehler). §4.3/§4.4/§4.5/
  §4.6/§5/§7 + Header überarbeitet. Offen nur noch: Kollisions-Diagnose-IDs beim Port (§7).
- **Nachtrag Runde 7** — **Analyzer `Nav0119` (Init-Signatur-Eindeutigkeit) implementiert.**
  Versions­UNabhängig, Error, Klon-Struktur von Nav0118: zwei Init-Knoten desselben Tasks mit
  identischer Parameter-Typ-Signatur → doppelte `Begin`-Überladung → CS0111. Signatur =
  Parameter-Typen ohne Whitespace, Namen irrelevant; greift auch für edge-lose Inits. `DiagnosticId`
  + Descriptor + Analyzer + 3 Fixtures + `Errors.md` (Zeile 34). net10.0 1352/0, net472 1360/0 grün.
  §5/§7 erledigt-markiert. Damit ist die Init-Analyzer-Familie (Nav0110/0118/0119) komplett; offen
  bleiben nur die V2-Kollisions-Diagnose-IDs beim Semantic-Model-Port.
- **Runde 8** — **Grill-Durchgang, sieben Gabelungen gegen Code/Korpus verifiziert; §7 geschlossen.**
  **(1)** Null-Guard immer, zentralisiert als `Guard<T>` je `{Task}WFSBase` (kein Interface, `.Body`
  bleibt `private`, Maschinerie = geschützter Einzeiler; Platzierung pro Klasse, nicht assembly-weit —
  Inkrement-Build-/Framework-API-Kosten). **(2)** `[notimplemented]` (Thunk wirft) und `[donotinject]`
  (expliziter Wrapper-Parameter, V1-treu; Wrapper laufzeit-selektiert aus nutzer-verwalteter Kollektion)
  werden in V2 unterstützt — beide korpus-real (5/6 `.nav`), kein opt-out. **(3)** Union `plain+concat`:
  implizite Konvertierung bleibt, geheilt per Doc-Politur + Ergonomie-Test. **(4)** Diagnose-IDs
  **Nav0120–0124** im 01xx-Band (concat-Struktur 1020/1021/1022→0120/0121/0122 umnummeriert, kein
  Kompatibilitätszwang; `--^`=0123); **eine generische Member-Kollision Nav0124** ersetzt Modus-Kollision
  + reservierte Namen (**Nav0125 entfällt**), Wert = still kompilierender Overload den `csc` verschweigt;
  Versions-Gate via bestehendem Nav5000. Korpus: 0 Kollisionen. **(5)** Verschachtelte Choices =
  rekursives Forwarding (Anti-Bloat transitiv), Choice→Choice in §4.3-Tabelle. **(6)** Fixes `Exit(result)`
  bestätigt: reales `InternalTaskResult<T>(result)` trägt keine Exit-Identität, Multi-Exit kollabiert
  (0 Produktion/1 Framework-Test) → kein `Exit{Node}`. **(7)** `Logic`-Suffix/gemeinsame Basis/
  Deferred-Thunk (inkl. Allokation) bestätigt, keine Änderung. Detail-Folding **nachgezogen**:
  §4.2a (`Guard<T>`-Zentralisierung + Maschinerie-Einzeiler), §4.3 (Kanten-Tabelle um
  notimplemented/donotinject/Choice→Choice + Multi-Exit-Note + reservierte Namen auf Nav0124),
  §4.4 (Choice→Choice-Forward), §5 (IDs Nav0120–0124, Nav0125 verworfen, Gate via Nav5000), §7/§8/§9.
