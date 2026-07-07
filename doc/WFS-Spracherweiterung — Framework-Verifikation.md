# WFS-Spracherweiterung — Framework-Verifikation der 5 Unbekannten

> Verifiziert am realen Framework (`Framework.NavigationEngine`), nicht aus Stubs/V1-Branch geschlossen.
> Alle Command-Typen der `…IWFL`-Familie (nicht `…IWFL.ClientNavCommands`).
> Die sechste Verifikation ⑥ (`--^`/Goto-Concat) ist im Design dokumentiert:
> `doc/nav-codegen-v2-concat-design.md`, §3.8/⑥.

## ⚠️ Vorab: Zwei gleichnamige Command-Familien — die richtige treffen

| Namespace | Ort | Rolle | Interfaces |
|---|---|---|---|
| `…NavigationEngine.IWFL` | `IWFL\NavCommands\*.cs` | **echte** WFS-Commands (Server, `ServerExecutionContext`) | `: INavCommand` + Tagging-Ifaces |
| `…NavigationEngine.IWFL.ClientNavCommands` | `IWFL\ClientNavCommands\*.cs` | Legacy Client-Side-Commands | **keine** |

`BaseWFService.GotoGUI/OpenModalTask/…` liefern durchweg die **`…IWFL.*`**-Typen (`return new GOTO_GUI(...)` → `IWFL.GOTO_GUI`, `BaseWFService.cs:113`). Roslyns erste `get_class_hierarchy`-Auflösung zeigte auf die *ClientNavCommands*-Variante (daher dort „`interfaces:[]`") — **falsche Familie**. Stubs/V1 müssen `…IWFL.GOTO_GUI` referenzieren, nicht `…IWFL.ClientNavCommands.GOTO_GUI`.

---

## ① `.Concat(…)` — Instanzmethode, Parameter = Tagging-Interfaces, Ergebnis ist IINIT_TASK ✓

`Concat` ist eine **öffentliche Instanzmethode auf `GOTO_GUI`** (`NavCommands\GOTO_GUI.cs:18/27`), **keine Extension**. Überladen auch auf `OPEN_MODAL_GUI`, `TWO_STEP_IINIT_TASK`.

```csharp
public TWO_STEP_IINIT_TASK                  Concat(INOT_A_TASK_BOUNDARY next)  // GOTO_GUI.cs:18
public TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY Concat(ITASK_BOUNDARY next)        // GOTO_GUI.cs:27
```

Parametertyp ist **nicht** `INavCommand` allgemein und **nicht** „das Ergebnis von OpenModalTask", sondern die **Tagging-Interfaces** `INOT_A_TASK_BOUNDARY` / `ITASK_BOUNDARY`.

`GotoGUI(to).Concat(OpenModalTask(…))`: `OpenModalTask` → `OPEN_MODAL_TASK : NavCommand, ITASK_BOUNDARY` (`OPEN_MODAL_TASK.cs:7`) → Überladung `Concat(ITASK_BOUNDARY)` → **`TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY`**.

`TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : TWO_STEP, IINIT_TASK` (`…:7`); `TWO_STEP : NavCommand : INavCommand`. → **sowohl `IINIT_TASK` (Begin) als auch `INavCommand` (Trigger/Exit) zuweisbar. Branch-Schluss am Framework bestätigt.** Beide Concat-Ergebnistypen sind `IINIT_TASK`.

> **Aber (→ ⑤):** `Concat(ITASK_BOUNDARY)`-Ctor ruft `BaseWFService.ExecuteCallResult(next)` (`TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY.cs:12`) — **Seiteneffekt**, nicht rein.

---

## ② Exit / Body↔Kommando — keine Schwester nötig; Cast ist Stub-Artefakt

Realer Rückgabetyp ist **nicht** `INavCommandBody` (Stub-Vereinfachung), sondern:

```csharp
protected TASK_RESULT<TResult> InternalTaskResult<TResult>(TResult result)   // BaseWFService.cs:236
    => new TASK_RESULT<TResult>(result);

public abstract class TASK_RESULT : NavCommand, IINIT_TASK, ITASK_BOUNDARY, INavCommandBody  // TASK_RESULT.cs:9
public class TASK_RESULT<ResultType> : TASK_RESULT                                            // TASK_RESULT.cs:18
```

Ein **einziges** Objekt vereint beide Welten: `INavCommandBody` (Body) UND `TASK_RESULT`/`NavCommand`/`IINIT_TASK`/`ITASK_BOUNDARY` (Kommando). Der V1-Switch `case TASK_RESULT taskResult` greift, weil das reale Objekt statisch bereits `TASK_RESULT<T>` ist.

**Entscheidung:** Keine kommando-typisierte Schwester `TaskResult<T>(…) : TASK_RESULT` nötig — sie existiert de facto (`InternalTaskResult<T>` → `TASK_RESULT<T>`).
- V2-`ctx.Exit`-Fabrikmethode konkret als `TASK_RESULT<T>`/`TASK_RESULT` typisieren → **kein Cast** (statischer Upcast).
- Nur bei `INavCommandBody`-typisiertem Zwischenwert (wie `TaskResultFactory.Weiter<T>`, `TaskResultFactory.cs:27–28`, Kommentar *„der Compiler castet nicht :-("*) ist `(TASK_RESULT)…` nötig — dann **laufzeitsicher** (Objekt ist immer `TASK_RESULT<T>`).

Framework-Kommentar (`BaseWFService.cs:230`): *„The compiler will, unfortunately, not find out the correct type - thus, it must be specified manually."* → In V2 konkreten Typ führen, dann castfrei.

---

## ③ CANCEL — Factory-Methode `_wfs.Cancel()` (Doc korrekt)

```csharp
public CANCEL Cancel() => new CANCEL();          // BaseWFService.cs:254  (public, [SuppressInterceptors])
public class CANCEL : NavCommand, IINIT_TASK, INavCommandBody   // CANCEL.cs:5  (Execute → context.Cancel())
```

**Kein Singleton, keine Property, kein EscapeTask** — echte Factory-Methode, 20 reale Aufrufer. `ctx.Cancel()` → `_wfs.Cancel()` → `new CANCEL()`. `CANCEL` ist `IINIT_TASK` **und** `INavCommandBody` → als Init-Rückgabe und als Body legal.

(`EscapeTask` = Anwendungscode: `KPAnwendungWFS.EscapeTask` ruft `InternalTaskResult(to)`, kein Framework-CANCEL-Produzent.)

---

## ④ Init-Legalitäts-Matrix — das Typsystem verbietet die Kante bewusst

`IINIT_TASK` ist **gezielt selektiv** implementiert:

| Command | Producer | Interfaces | `IINIT_TASK`? |
|---|---|---|:---:|
| `GOTO_GUI` | `GotoGUI(to)` | `IINIT_TASK, INOT_A_TASK_BOUNDARY` | ✅ |
| `GOTO_TASK` (abstr.) | `GotoTask<…>(…)` | `IINIT_TASK, ITASK_BOUNDARY` | ✅ |
| `TASK_RESULT<T>` | `InternalTaskResult<T>(…)` | `IINIT_TASK, ITASK_BOUNDARY, INavCommandBody` | ✅ |
| `CANCEL` | `Cancel()` | `IINIT_TASK, INavCommandBody` | ✅ |
| `TWO_STEP_IINIT_TASK` | `GotoGUI(…).Concat(INOT_A_TASK_BOUNDARY)` | `IINIT_TASK` | ✅ |
| `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY` | `GotoGUI(…).Concat(ITASK_BOUNDARY)` | `IINIT_TASK` | ✅ |
| **`OPEN_MODAL_TASK`** | `OpenModalTask<…>(…)` | `ITASK_BOUNDARY` | ❌ |
| **`OPEN_MODAL_GUI`** | `OpenModalGUI(to)` | `INOT_A_TASK_BOUNDARY` | ❌ |
| **`START_NONMODAL_TASK`** | `StartNonModalGUI(to)` | `ITASK_BOUNDARY` | ❌ |
| **`END`** | `EndNonModal()` | `ITASK_BOUNDARY, INavCommandBody` | ❌ |

Design-Grund wörtlich im Framework (`TWO_STEP.cs`, Header): *„…we suddenly undermine the rule that a task can only start with GOTO_TASK, GOTO_GUI or TASK_RESULT."* Deshalb ist `TWO_STEP` gesplittet; nur der `GOTO_GUI`-Intro trägt `IINIT_TASK`.

**Konsequenz V2:** Zeigt eine Init-Transition (`o->`/`==>`) **direkt** auf Modal-Task/Non-Modal-Task/Modal-GUI, entsteht `OPEN_MODAL_TASK`/`START_NONMODAL_TASK`/`OPEN_MODAL_GUI` (**nicht** `IINIT_TASK`) → `IINIT_TASK`-typisierter `Result.Body` = **Compile-Fehler**. Das ist die beabsichtigte Einschränkung. Der opake `Result.Body` ist **nicht** immer `IINIT_TASK`-typisierbar. Semantic Model **muss** Init-Ausgangskanten auf die `IINIT_TASK`-Menge beschränken (`GOTO_GUI`, `GOTO_TASK`, `TASK_RESULT`, `CANCEL`, `GOTO_GUI(…).Concat(…)`). Modal/Nonmodal/Modal-GUI nur *innerhalb* eines Tasks (erst `GOTO_GUI`, dann `.Concat(…)`). Trägt die Init-Legalitäts-Regel des Designs (§3.8/④, umgesetzt als Analyzer Nav0110+Nav0118, Design §4).

---

## ⑤ Eager-Bau **nicht** seiteneffektfrei

| Producer / Command | Ctor-Seiteneffekt? | Beleg |
|---|---|---|
| **`GotoGUI` → `GOTO_GUI`** | **JA** — Ctor ruft `context.GotoGui(wfs, to)` | `GOTO_GUI.cs:8–11` (TODO *„Nicht schön"*) |
| **`OpenModalGUI` → `OPEN_MODAL_GUI`** | **JA** — Ctor ruft `context.OpenModalGui(wfs, to)` | `OPEN_MODAL_GUI.cs:8–11` |
| **`.Concat(ITASK_BOUNDARY)`** | **JA** — Ctor ruft `BaseWFService.ExecuteCallResult(next)` | `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY.cs:8–13` |
| `OpenModalTask → OPEN_MODAL_TASK` | nein — Felder speichern; Effekt in `Execute` | `OPEN_MODAL_TASK.cs:12–21` |
| `StartNonModalGUI → START_NONMODAL_TASK` | nein — Effekt in `Execute` | `START_NONMODAL_TASK.cs:15–30` |
| `InternalTaskResult → TASK_RESULT<T>` | nein | `TASK_RESULT.cs:24` |
| `Cancel → CANCEL`, `EndNonModal → END`, `GotoTask → GOTO_TASK_*` | nein | `CANCEL.cs`, `END.cs:7`, `GOTO_TASK.cs:21/41` |

Stubs (Rückgabe `null`) verschleiern das. Das Framework selbst vermeidet den Eager-Effekt per Thunk: `StartNonModalGUI(to) => StartNonModalTask(() => GotoGUI(to))` (`BaseWFService.cs:153`); `BeginTaskWrapper` (`INavCommand.cs:6`) ist der Verzögerungsmechanismus.

**Konsequenz V2:** `GotoGUI`/`OpenModalGUI`/`Concat(ITASK_BOUNDARY)` aus dem `BeginTaskWrapper`-Thunk in eine **eager** Context-Fabrikmethode zu verschieben feuert GUI-Navigation bzw. `ExecuteCallResult` **zur Konstruktionszeit statt beim kontrollierten Ausführen** → Verhaltensänderung. Das Design fängt das ab: der `Result`-Thunk baut **alle** Kommandos deferred (Design §3.2) — feld-speichernde Commands *dürften* zwar eager gebaut werden, diese drei aber keinesfalls.

---

## Zusammenfassung

- **①** Instanzmethode auf `GOTO_GUI`; Param = `INOT_A_TASK_BOUNDARY`/`ITASK_BOUNDARY`; Ergebnis `TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY` ist `IINIT_TASK`+`INavCommand` ✓ (Concat-Seiteneffekt beachten).
- **②** Keine Schwester nötig — `InternalTaskResult<T>` liefert `TASK_RESULT<T>`; konkret typisieren → castfrei; sonst Cast laufzeitsicher.
- **③** `_wfs.Cancel()` → `new CANCEL()` (Factory-Methode) — Doc korrekt.
- **④** Semantic Model muss die Kante verbieten; Command-Typen sind bewusst nicht alle `IINIT_TASK`.
- **⑤** **Nicht** seiteneffektfrei: `GOTO_GUI`, `OPEN_MODAL_GUI`, `Concat(ITASK_BOUNDARY)` haben Konstruktor-Seiteneffekte → müssen im Thunk bleiben.

## Quellen (Datei:Zeile)

- `framework/src/Framework.NavigationEngine/WFL/BaseWFService.cs` — GotoGUI:112, OpenModalGUI:121, StartNonModalTask:132/142, StartNonModalGUI:152, EndNonModal:160, GotoTask:167ff, OpenModalTask:202–224, InternalTaskResult:236 (Kommentar:230), CloseModal:246, Cancel:254, ExecuteCallResult:263
- `framework/src/Framework.NavigationEngine/IWFL/NavCommands/` — GOTO_GUI.cs, OPEN_MODAL_GUI.cs, OPEN_MODAL_TASK.cs, START_NONMODAL_TASK.cs, TASK_RESULT.cs, CANCEL.cs, END.cs, GOTO_TASK.cs, TWO_STEP.cs, TWO_STEP_IINIT_TASK.cs, TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY.cs
- `framework/src/Framework.NavigationEngine/IWFL/` — INavCommand.cs, INavCommandBody.cs, IINIT_TASK.cs, ITASK_BOUNDARY.cs, INOT_A_TASK_BOUNDARY.cs
- `XTplusApplication/src/Application.Common/WFL/TaskResultFactory.cs:27–28`
