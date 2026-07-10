# Nav Activity-Diagram — Status & Design

**Stand:** 2026-07-10 · **Status:** Konzept/Design abgestimmt, noch nicht implementiert.
Eine lauffähige HTML-Demo existiert (siehe „Demo-Artefakt").

## 1. Ziel (Wow-Feature)

Die Nav-Version ist weitgehend feature-complete; gesucht war ein Feature mit Wow-Effekt.
Ergebnis: eine **interaktive Visualisierung des `.nav`-Graphen als Activity-Diagram** in einem
**VS-Code-Webview** — **klickbar und navigierbar**:

- Klick auf Knoten/Kante → Sprung an die passende Stelle im `.nav`-Editor.
- **Live-Sync**: Diagramm re-rendert beim Tippen.
- **Rück-Highlight**: Cursor im Editor hebt den zugehörigen Knoten im Diagramm hervor.
- **Umschaltbare Layouts** (Orientierung, Routing) und **Stile** (modern/sketchy).

## 2. Grundentscheidungen

- **Kein Mermaid.** Mermaid erzeugt für unsere Graphen unpassende Layouts; wir brauchen Kontrolle
  über das Layout. → Eigener Renderer über ein neutrales Modell.
- **Renderer-Stil: „Modern schlicht" als Standard, „Sketchy" optional.** Modern = klare Karten,
  dünne Akzent-Ränder, dezente Schatten, System-Font, ruhige Palette (theme-aware Light/Dark).
  Sketchy = handgezeichnet via rough.js. Beides ist derselbe Stil-Layer über demselben Modell/Layout
  — im Feature nur ein Renderer-Flag. (Sketchy war der erste Entwurf, wurde als „nicht jedermanns
  Sache" zugunsten von Modern zurückgestuft, bleibt aber als Option erhalten.)
- **Neutrales Graph-Modell aus der Engine; Layout im Client.** Siehe §3/§4.
- **Layout-Engine: `elkjs`** (Eclipse Layout Kernel) — geschichtetes Activity-Layout, orthogonales
  Routing, `elk.direction` liefert die Orientierungs-Umschaltung. (Alternative `dagre`: leichter,
  schwächeres Routing.)
- **Self-contained:** elkjs + rough.js werden lokal ins Webview-Bundle eingebettet (esbuild) — keine
  CDN-/Netz-Abhängigkeit.

## 3. Rollenteilung Server ↔ Client

**Server (Engine + LSP) liefert nur Daten, kein Layout.** Ein layout-freies Modell — keine
Koordinaten, keine Pixel, keine Formen:

```
nav/diagramModel { uri } →
{
  tasks: [{
    name,
    nodes: [ { id, kind, label, location } ],            // kind = init|gui|choice|tasknode|exit|end
    edges: [ { id, source, target, mode, label, guard,   // mode = modal|goto|nonmodal|continuation
               isContinuation, location } ]
  }]
}
```

**Client (VS-Code-Webview) übernimmt Layout + Rendering + Interaktion.** Warum das Layout im Client:
Orientierung umschalten, Zoom/Pan, Re-Layout beim Tippen und Rück-Highlight laufen so **ohne
Server-Roundtrip** flüssig. Nach dem einmaligen Modell-Fetch ist fast alles clientseitig — die
`location` je Element reist im Modell mit; nur der eigentliche Editor-Sprung geht per `postMessage`
zurück an die Extension.

| | Layout im Client (elkjs) — **gewählt** | Layout in C# (eigener Sugiyama) |
|---|---|---|
| Orientierung umschalten | sofort, ohne Roundtrip | jedes Mal Server fragen |
| Live-Sync / Zoom | flüssig | träger, mehr Protokoll |
| Aufwand | fertige Lib | Sugiyama + Routing selbst bauen |
| Nachteil | nicht direkt für Server-/CLI-HTML nutzbar | — |

Deshalb ist das Modell bewusst **layout-frei** — hält den optionalen Standalone-HTML-Export offen
(dort würde elk mit ins HTML gebündelt oder ein Server-Layout ergänzt).

## 4. Architektur / Datenfluss

```
Engine (Nav.Language, VS-frei)
  NavDiagramModel.From(CodeGenerationUnit)  ── neutrales DTO (Knoten/Kanten/Locations)
        │  (rein datengetrieben; snapshot-testbar ohne Host)
        ▼
LSP-Server (nav.lsp)  ── custom Request  nav/diagramModel { uri }  →  NavDiagramModel (JSON)
        │                 (Pull, wie GoTo/Refs; kein Server-State)
        ▼
VS-Code-Client (vscode-nav-lsp)
  ├─ WebviewPanel „Nav Diagram" (Command nav.showDiagram)
  ├─ Modell per client.sendRequest('nav/diagramModel', {uri})
  ├─ elkjs-Layout + SVG-Render (Stil-Layer modern/sketchy)
  ├─ Klick → postMessage → Extension → showTextDocument + revealRange (via Location)
  ├─ Live-Sync: onDidChangeTextDocument (debounced) → Modell neu → re-render
  └─ Rück-Highlight: onDidChangeTextEditorSelection → Offset→Knoten (aus Locations) → highlight
```

## 5. Layout-/Ansichts-Optionen (Toolbar, alle über elk-Parameter)

1. **Orientierung**: Top-Down / Bottom-Up / Left-Right / Right-Left (`elk.direction`).
2. **Kanten-Routing**: orthogonal ↔ polyline ↔ spline.
3. **Dichte**: kompakt ↔ luftig (`spacing.*`).
4. **Swimlanes pro Task**: `TaskDefinition` als elk-Container (Multi-Task-Dateien).
5. **Unerreichbares dimmen**: `INodeSymbol.IsReachable() == false` ausgrauen.
6. **Pfad-Highlight ab Init** (Vorlage `EdgeExtensions.GetReachableCalls`, clientseitig).
7. **Zoom-to-fit / Pan-Zoom** (+ optional Minimap).

## 6. Erkenntnisse aus der Engine-/Host-Exploration

**Graph-API der Engine (VS-frei, `Pharmatechnik.Nav.Language`)** — das Modell ist bereits vollständig
vorhanden, ein Renderer konsumiert nur Interfaces:

- Einstieg: `SemanticModelProvider.Default.GetSemanticModel(path)` → `CodeGenerationUnit`.
- Pro Datei: `CodeGenerationUnit.TaskDefinitions` (ein Activity-Graph je `ITaskDefinitionSymbol`).
- Pro Task: `NodeDeclarations` (`INodeSymbol`) + `Edges()` (alle Transitionen) sowie typisierte
  Listen `InitTransitions`/`ChoiceTransitions`/`TriggerTransitions`/`ExitTransitions`.
- Knoten-Arten: `IInitNodeSymbol`, `IExitNodeSymbol`, `IEndNodeSymbol`, `IChoiceNodeSymbol`,
  `IViewNodeSymbol`/`IDialogNodeSymbol` (`IGuiNodeSymbol`), `ITaskNodeSymbol`; Konnektivität via
  `ISourceNodeSymbol.Outgoings` / `ITargetNodeSymbol.Incomings`.
- Kante `IEdge`: Endpunkte `SourceReference?.Declaration` / `TargetReference?.Declaration`,
  `EdgeMode` (`enum EdgeMode { Modal, NonModal, Goto }`, dazu `IsContinuation` für `o-^`/`--^`),
  `IEdgeModeSymbol.DisplayName`/`Description` als fertige Labels.
- Trigger: `ITriggerTransition.Triggers` (`ITriggerSymbol`). Guards/Do stehen **nur am Syntaxknoten**:
  `ITransition.Syntax.ConditionClause` / `.DoClause`.
- Klassifikation wiederverwendbar: `Nav.Language.Mcp/Tools/NavSymbolKind.cs`
  (`init|exit|end|choice|gui|tasknode|node|task`) — in die Engine hochziehen und teilen.
- Navigation: jedes Symbol trägt `Location` (`FilePath`, 0-basiert `StartLine`/`StartCharacter`,
  Offsets `Start`/`End`). Für Anzeige `+1`.
- Graph-Walker als Vorlage: `EdgeExtensions.GetReachableCalls`/`GetDirectCalls`, `Call`.

**Host-Terrain (HTML/SVG ist überall Greenfield):**

- **VS-Code-Client** (`vscode-nav-lsp/`): heute dünn — genau **ein** Command (`nav.showReferences`,
  Server→Client-Roundtrip), **kein** Webview. Natürlicher Ort fürs Diagramm; Webview-/`commands`-/
  `menus`-Plumbing + esbuild-Asset-Pipeline sind neu.
- **LSP-Server** (`Nav.Language.Lsp/`): Präzedenz für **custom JSON-RPC** ohne Protokoll-Paket-Support
  = **Call Hierarchy** (`CallHierarchy/`, literale Methodennamen + `NavServerCapabilities`).
  Blaupause für `nav/diagramModel`. „LSP gibt das nicht her" gilt nur für Stock-LSP.
- **Emit-Primitive**: `CodeBuilder` (`Nav.Language/CodeGen/Shared/CodeBuilder.cs`) ist
  inhaltsneutral (konfigurierbarer Indent, parametrierbares `Block(open, close)`) — taugt für
  HTML/JSON-Emit; Emitter-Muster wie `CodeGen/…/Emitters/` (`static Emit(...)` über `CodeBuilder`).
  (StringTemplate/`.stg` ist längst raus; Emitter nutzen Raw-String-Literale.)
- **MCP** (`nav_outline`): läuft bereits `TaskDefinitions → NodeDeclarations` ab (nur Knoten, keine
  Kanten) und liefert lean-JSON — Vorlage für ein optionales `nav_diagram`.
- **Snapshot-Harness**: `Nav.Language.Tests/Regression/RegressionTests.cs` (`.expected`-Goldens,
  `nav snapshot`) als Muster für Modell-Snapshots (net472 **und** net10).
- **Voll-VS-Tool-Window**: teuer (erstes `.vsct`, `ToolWindowPane`, WebView2) — siehe
  `doc/nav-syntax-tree-visualizer.md`; bewusst Phase 2.

## 7. Umsetzungs-Steps (Vorschlag, noch nicht begonnen)

- **S1 — Engine-Modell:** `Nav.Language/Diagram/` mit `NavDiagramModel` + `NavDiagramNode/Edge`
  (DTOs). `From(CodeGenerationUnit)`/`From(ITaskDefinitionSymbol)` füllt Knoten (Kind via geteilter
  `NavSymbolKind`-Logik) + Kanten (Endpunkte/Modus/Trigger/Guard/Continuation) + `Location`.
  Knoten-`Id` = task-qualifizierter Name. Golden-Snapshot-Tests (net472 + net10).
- **S2 — LSP:** custom Request `nav/diagramModel` nach `CallHierarchy/`-Muster (DTOs, Handler in
  `NavLanguageServer.cs`, Flag in `NavServerCapabilities`, URI→Pfad via `NavUri.ToFilePath`).
  `doc/nav-lsp-status.md` fortschreiben.
- **S3 — VS-Code-Webview:** `package.json` (Command `nav.showDiagram`, Kontextmenü/Icon; Dev-Deps
  `elkjs`/`roughjs`; esbuild-Entry `media/diagram.js`; `.vscodeignore`). `extension.js`:
  `createWebviewPanel`, Modell per `sendRequest` holen, per `asWebviewUri` + CSP-`nonce` laden.
  Webview: elk-Layout + SVG (Stil modern/sketchy), Knotenformen je Kind, Kantenstil je Modus.
- **S4 — Volle Interaktivität:** Klick→`goto` (`showTextDocument`+`revealRange`), Live-Sync
  (`onDidChangeTextDocument`, debounced), Rück-Highlight (`onDidChangeTextEditorSelection`).

## 8. Optionale Erweiterungen

- **Standalone-HTML-Export** über `nav.exe`-Verb (self-contained `.html`, elk+rough inline; nutzt
  `FileGenerator`) — teilbar + zweites Test-Bett.
- **MCP-Tool `nav_diagram`** — Modell/HTML als String (trivial, sobald S1 steht).
- **Voll-VS WebView2-Tool-Window** (Phase 2, teuer).

## 9. Kritische Dateien

- Neu (Engine): `Nav.Language/Diagram/NavDiagramModel.cs` (+ Node/Edge-DTOs); `NavSymbolKind`-Logik
  aus `Nav.Language.Mcp/Tools/NavSymbolKind.cs` hochziehen.
- Neu (Tests): `Nav.Language.Tests/Diagram/…` (Golden-Harness + `TestDataDirectory`).
- LSP: `Nav.Language.Lsp/Diagram/`, `NavLanguageServer.cs`, `NavServerCapabilities.cs`, `NavUri.cs`.
- VS Code: `vscode-nav-lsp/extension.js`, `package.json`, neu `media/diagram.js` (+ CSS), esbuild/
  `.vscodeignore`.

## 10. Verifikation (wenn implementiert)

1. Engine/Modell: `nav test` (net472) + `dotnet test … -f net10.0 --filter Diagram` — Goldens grün.
2. LSP: Debug-Build, stdio-Smoke `initialize` → `nav/diagramModel {uri}`, JSON gegen Erwartung.
3. Webview end-to-end: `npm install`/`npm run esbuild`, F5-Dev-Host, `.nav` → `nav.showDiagram`;
   Orientierung umschalten; Klick→Sprung; Tippen→Update; Cursor→Highlight. (Dev-Host vor
   LSP-Rebuild schließen — DLL-Lock.)

## 11. Offene Entscheidungen

- Sketchy nur als Option behalten oder ganz streichen? (aktuell: behalten)
- elkjs vs. dagre endgültig; Minimap ja/nein.
- Standalone-HTML-Export im Kern-Scope oder später?

## 12. Demo-Artefakt

Eigenständige HTML-Demo (außerhalb des Repos, Scratchpad):
`…/scratchpad/nav-diagram-demo.html`. Zeigt Modell → Layout → Stil an einem Login-Workflow;
Toolbar: Stil (modern/sketchy), Orientierung (TB/LR/BT/RL), Routing, Theme, Einpassen; Klick→
Sprung-Toast, Hover-/Auswahl-Highlight, Pan/Zoom. **Einschränkungen ggü. Feature:** Graph
hartkodiert (statt Engine-Modell); Layout = vereinfachter Longest-Path-Layerer (statt elkjs);
rough.js per CDN (statt lokal gebündelt).
