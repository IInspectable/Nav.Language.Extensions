# NavV2NavigationDemo

Ein kleines Demo-Projekt (unter `Demos/` im Engine-Repo), um die **Begin↔After-GoTo-Navigation**
mit dem **V2-Codegen** (`#version 2`) in Visual Studio zu testen. Der Nav-Build-Step ist bereits
eingehängt: beim Bauen wird aus `Demo.nav` der C#-Code generiert.

Die Demo liegt bewusst **außerhalb** der Haupt-Solution (`Nav.Language.Extensions.slnx`) und hat eine
eigene `NavV2NavigationDemo.slnx`. Zwei lokale `Directory.Build.props`/`.targets` klemmen die
Build-Infrastruktur der Engine ab (git-Versionsstempel, Quellgeneratoren, Central Package Management),
sodass die Demo dieselbe minimale Sicht hat wie ein externes Team, das nur die Build Tools konsumiert.

## Was steckt drin?

| Datei | Rolle |
|---|---|
| `Demo.nav` | V2-Workflow (`#version 2`): View `Home` setzt via Continuation in zwei Sub-Tasks fort — `o-^ Warn` (modal) und `--^ Drill` (Goto). Zusätzlich eine **Choice** `Choice_Retry` (Trigger `OnDecide` delegiert dorthin) mit drei Ausgängen: zurück auf Home (plain), Home + modal Warn (Continuation → `BeginWarn`) und Abbruch (`Exit`). |
| `WFL/ContinuationFlowWFS.cs` | **Handgeschriebener Logic-Teil** — hier stehen die `AfterWarnLogic`/`AfterDrillLogic`-Overrides, die `Choice_RetryLogic`/`OnDecideLogic`-Overrides und die `callContext.ShowHome(to).BeginWarn(...)`/`.BeginDrill(...)`-Aufrufe. **Das ist das Testobjekt.** |
| `WFL/generated/`, `IWFL/generated/` | Beim Build erzeugter Code (WFSBase, CallContexts, Interfaces) — mit den `<NavExit>`/`<NavInitCall>`-Annotationen. Nicht eingecheckt. |
| `Framework/XTplusFrameworkStub.cs` | Minimaler **Kompilier-Stub** des XTplus-Frameworks (`StandardWFS`, `GotoGUI`/`OpenModalTask`/`GotoTask`/`.Concat`, `IINIT_TASK`/`INavCommand`). Keine echte Laufzeit. |
| `Stubs/DemoContracts.cs` | Stub-Kontrakte, die real woanders generiert würden: `HomeTO`, `MsgResult`/`DetailResult`, `IBeginMsgWFS`/`IBeginDetailWFS`. |

> **Stub-Hinweis:** Das Framework ist bewusst nur eine Attrappe, damit der generierte Code **fehlerfrei
> kompiliert** und Roslyn ein vollständiges Semantik-Modell hat — nur so greifen die GoTo-Features
> zuverlässig. Der Workflow ist **nicht** lauffähig; es geht ausschließlich um Editor-Navigation.

## Woher die Build Tools kommen

Die Demo checkt **keine** Build-Tools-Binaries ein. Der Codegen bezieht Task-DLL, Targets und die
self-contained `nav.exe` frisch aus `<Repo-Root>\deploy\Build Tools`. Dieses Verzeichnis füllt
**`nav publish`**: der Solution-Build legt Task-DLL + Targets ab, `Publish-Cli` ergänzt die `nav.exe`.
So testet die Demo immer die **aktuell gebaute** Engine, nicht einen eingefrorenen Schnappschuss.

> **Voraussetzung:** einmal **`nav publish`** im Engine-Repo, bevor die Demo baubar ist. Fehlen die
> Build Tools, meldet der Build das mit einer klaren Fehlermeldung (Guard in `.csproj`/`build.ps1`).

## Voraussetzungen

1. **Visual Studio 2026** mit installierter **Nav-Language-Extension** (`nav install`).
2. Einmal **`nav publish`** (füllt `deploy\Build Tools`).
3. Gebaut wird mit **MSBuild.exe** (VS, `nav demo` oder `build.cmd`) — **nicht** `dotnet build`. Der
   Nav-Task ist net472 und läuft im MSBuild-Prozess.

## Bauen

- **Engine-Repo-Command:** `nav demo` (löst MSBuild.exe + Repo-Root auf und prüft die Build Tools).
- **Kommandozeile (standalone):** `.\build.ps1` (löst MSBuild.exe via vswhere auf, gleicher Guard).
- **Visual Studio:** `NavV2NavigationDemo.slnx` öffnen → Projekt bauen. Der Codegen läuft automatisch
  vor dem Kompilieren.

Nach dem ersten Build liegt der generierte Code in `WFL/generated` bzw. `IWFL/generated` und ist Teil
des Roslyn-Workspaces (für IntelliSense/GoTo).

---

## Test-Checkliste — Begin↔After-Navigation

> Alle Sprünge werden über die **kleinen GoTo-Glyphen** am linken Rand (bzw. Kontextmenü **„Gehe zu …“**)
> der annotierten Methoden ausgelöst. Vorher **einmal bauen**, damit der generierte Code existiert.

### A) Rückrichtung: `AfterTaskLogic` → alle `callContext.Begin…()`-Aufrufe  ← *die neue Frage*

- [ ] `WFL/ContinuationFlowWFS.cs` öffnen, Cursor/Glyph auf **`AfterWarnLogic`** setzen.
- [ ] GoTo auslösen → die Liste enthält **alle drei** `BeginWarn(...)`-Aufrufstellen (klassenweite
      Sammlung über verschiedene Methoden): in **`OnShowWarnLogic`**, in **`OnReWarnLogic`** und in
      **`Choice_RetryLogic`** (letztere steckt in einer **Choice-Logic**, nicht in einem Trigger — genau
      das zeigt, dass die Sammlung nicht am Methoden-Typ hängt). Alle Ziele durchspringen.
- [ ] Analog **`AfterDrillLogic`** → **`BeginDrill(...)`-Aufrufstelle** in `OnDrillDownLogic`
      (nur eine — es gibt nur einen Drill-Trigger).

### B) Hinrichtung: `Begin…()`-Aufruf → `After…`-Methode (und Begin-Logic des Sub-Tasks)

- [ ] Cursor auf den **`BeginWarn`**-Aufruf in `OnShowWarnLogic` setzen.
- [ ] GoTo → Ziel **`AfterWarn`** (die Rücksprung-Methode) ist erreichbar.
- [ ] (Falls vorhanden) zweites Ziel „BeginLogic“ des Sub-Tasks wird angeboten.

### C) Grund-Navigation (soll unter V2 unverändert funktionieren)

- [ ] `.nav` → C#: In `Demo.nav` auf einen Task/Init/Exit/Trigger → GoTo in den generierten Code.
- [ ] C# → `.nav`: Aus einer annotierten Methode (`<NavTask>`/`<NavExit>`/`<NavTrigger>`) zurück in `Demo.nav`.
- [ ] **Choice:** In `Demo.nav` auf `Choice_Retry` → GoTo zur generierten `Choice_RetryLogic`; die
      Delegation `callContext.Choice_Retry("warn")` in `OnDecideLogic` führt ebenfalls dorthin.
- [ ] QuickInfo / Rename / Find References an einem Nav-Symbol.

### Ergebnis festhalten

- [ ] A funktioniert? (Kernfrage: `AfterTaskLogic` → `Begin…`-Aufrufe)
- [ ] B funktioniert?
- [ ] C funktioniert?
- [ ] Auffälligkeiten (Glyph fehlt, Liste unvollständig, falsches Ziel) notieren.

---

## Warum das der richtige Testfall ist

Unter V2 steht der Sub-Task-Einstieg als `callContext.ShowHome(to).BeginWarn(text)` (Member-Zugriff) im
Logic-Code — nicht mehr als bloßer Bezeichner wie unter V1. Der generierte CallContext trägt die
`<NavInitCall>`-Annotation vor `BeginWarn`/`BeginDrill`, die `<NavExit>`-Annotation sitzt (auch) auf den
abstrakten `After…Logic`-Methoden. Genau dieses Zusammenspiel prüft die Checkliste.
