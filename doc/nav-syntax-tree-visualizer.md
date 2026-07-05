# Nav Syntax Tree Visualizer — VS-Extension-Toolfenster

Kleines VS-Toolfenster, das den **Syntax Tree** der aktiven `.nav`-Datei als Baum darstellt — Vorbild
Riders/Roslyns „Syntax Visualizer" (Knoten + Tokens + Trivia, Icons, Selektions-Sync mit dem Editor).
Stand: Konzept/Handoff nach ausführlichem Design-Grilling, **noch nicht umgesetzt**. Dieses Dokument
hält den Entscheidungsstand *und* die offenen Fragen fest, damit später nahtlos weitergearbeitet werden
kann.

## Zweck / Zielgruppe (entschieden)

**Parser-Entwickler-X-ray**, nicht Struktur-Browser für Nav-Autoren. Konsequenz: Der Tool lebt vom
Sichtbarmachen der **Error-Recovery** — synthetisierte Missing-Tokens, übersprungene Läufe
(`SkippedTokensTrivia`), fehlerhafte Direktiven (`BadDirectiveTrivia`). Ein „sauberer" Nodes-only-Baum
wäre hier das *falsche* Werkzeug, weil er genau das Interessante verbirgt.

## Entscheidungslog (aus dem Grilling)

| # | Branch | Entscheidung |
|---|---|---|
| 1 | Zielgruppe | Parser-Dev-X-ray (nicht End-User-Browser). |
| 2 | Datenquelle | **Syntax Tree** via `ParserService` (`SyntaxTreeAndSnapshot`), nicht der Semantic-Model. |
| 3 | Faithfulness | **Voll**: alle Tokens, **alle Trivia inkl. strukturierter** (Direktiven, Skipped), **Missings**. Nicht optional. |
| 4 | Missing-Tokens | Über neue **generierte Engine-API `ChildTokensIncludingMissing()`** (Variante B, s.u.) — **ausgegliederter, eigener Engine-Task**. Kein Runtime-Reflection im Visualizer, **kein** Einfädeln in den globalen Token-Strom. |
| 5 | Sync-Tiefe | Caret→Baum trifft nur **signifikante** Knoten/Tokens (bestehende `FindToken`/`FindAtPosition`). Trivia/Missings sind nur **top-down** im Baum sichtbar, nicht per Cursor anspringbar. |
| 6 | Invocation | Für auffindbare Invocation (Menü/Tastenkürzel) ist ein **`.vsct` Pflicht** — es gibt in diesem Repo keine C#-Abkürzung (kein eigenes Editor-Kontextmenü, kein Community-Toolkit). Minimieren auf **einen** Show-Command. |
| 7 | Sequenzierung | Engine-API → Baum-VM (testgetrieben, ohne Fenster) → Fensterhülle/`.vsct` zuletzt. Die Fensterhülle liegt **nicht** auf dem kritischen Pfad zum Nutzen. |

## Das Modell — warum der faithful Baum so aussehen muss

Belege im Code (`Nav.Language\Syntax\`), damit später niemand erneut recherchieren muss:

- **Kein `Kind`-Enum** — der CLR-Typ *ist* die Art. Label = `node.GetType().Name` (Suffix `Syntax`
  strippen). Für Tokens `token.Type`. (`SyntaxNode.cs`)
- **`ChildNodes()`** liefert nur Knoten; **`ChildTokens()`** = `SyntaxTree.Tokens[Extent].Where(t => t.Parent == this)`
  liefert nur die **direkt vorhandenen** Tokens dieses Knotens (inkl. un-benannter Wiederhol-Tokens wie
  Listen-Kommata). Ein Roslyn-artiges „nodes and tokens interleaved" gibt es **nicht** — muss im VM per
  Merge nach `Extent.Start` selbst gebaut werden.
- **Trivia hängt an Tokens** (`SyntaxToken.LeadingTrivia`/`TrailingTrivia`), nicht an Knoten. Im Baum sind
  Trivia also Kinder *eines Tokens*. (`SyntaxToken.cs`)
- **Strukturierte Trivia** (Direktiven, Skipped) ist Roslyn-faithful: `SyntaxTrivia.HasStructure` +
  `GetStructure()` liefert einen `StructuredTriviaSyntax`-Knoten mit eigenen `ChildNodes()`/`ChildTokens()`.
  Der faithful Baum ist damit **ein** rekursiver Walk: Knoten → Merge(ChildNodes, Tokens) nach Position;
  Token → Leading/Trailing-Trivia; Trivia → falls `HasStructure`, in `GetStructure()` rekursieren.
  (`SyntaxTrivia.cs`)
- **Missings sind NICHT im Baum/Strom.** Der Parser hält den Token-Strom bewusst = **nur real gelexte
  Tokens** (positive Breite, überlappungsfrei, `Start` als eindeutiger Identitätsschlüssel). Die Primitive
  `Tok(parent, raw, …)` fügt nichts an, wenn `raw == null` — Missings entstehen erst *on demand* über
  benannte Properties: `OpenBrace => ChildTokens().FirstOrMissing(OpenBrace)` →
  `.DefaultIfEmpty(SyntaxToken.Missing).First()`. `SyntaxToken.Missing` hat `Parent == null`,
  `Start == −1`, ist nie in `SyntaxTree.Tokens`/`ChildTokens()`. (`NavParser.cs`, `SyntaxTokenExtensions.cs`)
- **Warum nicht einfach ins Strom-Modell einfädeln (echtes Roslyn)?** Verworfen: Ein nullbreiter Missing
  (`Start == End`) bricht die dokumentierten Invarianten des frisch überarbeiteten Trivia-Systems —
  `SyntaxTokenComparer` (nur nach `Start`), `FinalizeTrivia`s `HashSet<int>`-Identität über `Start`,
  `FindAtPosition` (`[Start,End)`), Extent-Slicing. Plus Umbau der meistfrequentierten Parser-Primitive
  und Test-/Snapshot-Lawine. Geschätzt Tage–Wochen an der empfindlichsten Engine-Stelle, hohes
  Regressionsrisiko — für ein Debug-Fenster nicht vertretbar.

## Ausgegliederter Engine-Task: `ChildTokensIncludingMissing()` (Variante B)

Eigenständig commit-/planbar; nutzt auch **Diagnostics/LSP**, nicht nur den Visualizer.

- **Vertrag (Variante B):** liefert die **vollständige, quell-geordnete Vereinigung aller *direkten*
  Tokens** eines Knotens **inkl. Missings** — present Tokens (aus `ChildTokens()`, inkl. Kommata) nach
  `Start`, Missings (aus den benannten Slots) nach **Deklarationsreihenfolge** zwischen ihre
  present-Nachbarn einsortiert. Present-Tokens, die auch benannte Slots sind, per `SyntaxToken.Equals`
  deduplizieren.
- **Umsetzung:** über den **bestehenden Source-Generator** (`Nav.Visitor.SourceGenerator`, der bereits
  Walker/Visitor über die Node-Typen erzeugt) eine geordnete Slot-Liste je Node-Typ emittieren →
  compilezeit-geprüft, **kein** Runtime-Reflection, **kein** Eingriff in Strom/Parser (additiv).
- **Abzusichernde Annahme:** „Deklarationsreihenfolge der `SyntaxToken`-Properties == Quellreihenfolge"
  — per-Typ-Test über alle ~40 Node-Typen (net472 + net10.0).
- Vorschlag Signatur: `IReadOnlyList<SyntaxToken> ChildTokensIncludingMissing()` auf `SyntaxNode`
  (bzw. generiertes `partial`), analog zu `ChildNodes()`/`ChildTokens()`.

## Sequenzierung (drei unabhängige Blöcke)

1. **Engine-API** — `ChildTokensIncludingMissing()` (Variante B) per Generator + per-Typ-Tests.
   *Blockiert den faithful Baum, sonst nichts.*
2. **Baum-Aufbau + ViewModel** — `SyntaxNodeViewModel` (dreiartig: Knoten/Token/Trivia), faithful Walk
   (Merge nodes+`ChildTokensIncludingMissing` nach Position; Trivia unter Tokens; `GetStructure()`-Rekursion;
   Missings als rote Null-Breite-Slots). **Rein testgetrieben in `Nav.Language.Tests`** gegen
   `SyntaxTree.ParseText(...)` — braucht **kein** VS-Fenster. Lazy/gecachte Children, gekürzte
   Text-Previews (~100 Zeichen).
3. **Fensterhülle** — `[ProvideToolWindow(typeof(SyntaxVisualizerToolWindow))]` auf `NavLanguagePackage`,
   `ToolWindowPane` mit WPF-`TreeView` (`HierarchicalDataTemplate`, `CrispImage` + `EnvironmentColors`-Theming),
   plus das **eine** `.vsct` für „Ansicht → Weitere Fenster → Nav Syntax Tree". Erst wenn (2) sich bewährt hat.

## Fensterhülle / `.vsct` — Fakten & Fallen

- Greenfield: **kein `.vsct`** im Repo, **kein** Community.VisualStudio.Toolkit — nur rohes
  `Microsoft.VisualStudio.SDK` 17.14 + `Shell.15.0` + `VSSDK.BuildTools`.
- **`[ProvideToolWindow]` + `ShowToolWindowAsync` brauchen KEIN `.vsct`** — das `.vsct` ist ausschließlich
  für den einen Menü-/Tasten-Command, der das Fenster zeigt.
- Projekt-Split: `.vsct` + `VSCTCompile` + `Menus.ctmenu`-Zeile in `VSPackage.resx` **müssen** ins
  net472-VSIX-Projekt `Nav.Language.Extension2026` (nur dort ist `VSSDK.BuildTools`). `ToolWindowPane`,
  WPF-Control, VMs, Command-Wiring in `Nav.Language.ExtensionShared` (.shproj → kompiliert in die
  VSIX-Assembly). GUID/IDs doppelt pflegen (symbolisch im `.vsct`, C#-Konstanten in Shared).
- Erst-`.vsct`-Falle: `VSCTCompile ResourceName` = `[ProvideMenuResource("Menus.ctmenu", 1)]`-Name =
  `VSPackage.resx`-Zeile müssen exakt übereinstimmen, sonst fehlt der Command stumm. In der
  Experimental-Hive verifizieren. (Das ist der Löwenanteil der 2–3 PT — Lernkurve, nicht Komplexität.)
- Präzedenz für Toolfenster-Öffnen ohne eigenes `.vsct`: `Commands\ViewCallHierarchyCommandHandler.cs`
  (MEF `ICommandHandler`, kapert die bestehende Ctrl+K,Ctrl+T-Geste). Für „Show Syntax Tree" gibt es
  keine passende bestehende Geste → daher `.vsct` nötig.

## Wiederverwendbare Bausteine (nur konsumieren, kein Umbau)

- Aktive View: `NavLanguagePackage.GetActiveTextView()` → `IWpfTextView` → `view.TextBuffer`.
- Datenquelle/Refresh: `ParserService.GetOrCreateSingelton(buffer)` → `SyntaxTreeAndSnapshot`; Event
  `ParseResultChanged` (bereits ~200 ms debounced, Parse im Hintergrund); Abo-Muster via Ableitung von
  `ParserServiceDependent` (`OnParseResultChanged`). Dokumentwechsel: `IVsSelectionEvents`
  (`SEID_DocumentFrame`). UI-Thread via `NavLanguagePackage.Jtf`.
- Selektion/Navigation (Baum→Editor): `NavLanguagePackage.NavigateToLocation(view, pos)`,
  `TextViewExtensions.SetSelection(view, span)`, `Location.ToSnapshotSpan(snapshot)`. (Missings mit
  Breite 0 / `Start == −1` highlighten schlicht nicht — akzeptiert, sie sind top-down-only.)
- Caret→Baum (nur signifikant): `SyntaxTree.Root.FindNode(pos)` / `FindToken(pos)` → VM-Knoten
  expandieren/selektieren. Rückkopplung mit `_suppressSync`-Flag brechen; **nie** bei Caret-Bewegung
  neu parsen.
- Theming/Icons: `EnvironmentColors.*BrushKey`, `imaging:CrispImage` + `KnownMonikers`, `VsFont`
  (Muster `Margin\NavMarginControl.xaml`).

## Offene Fragen (vor Umsetzung klären)

1. **Invocation-Timing für v1:** einmaligen `.vsct`-Aufwand sofort (Standard „Ansicht → Weitere
   Fenster") — *oder* Hülle für v1 ganz zurückstellen (Baum nur im Test-Harness/Dev-Trigger sichtbar),
   `.vsct` als Block 3 später? (Grilling offen gelassen.)
2. **Sequenz-Bestätigung:** Reihenfolge Engine-API → VM → Fenster ok? Insbesondere: Ist der Engine-Task
   (Block 1) *ohnehin* für Diagnostics/LSP gewünscht? Dann fällt er aus dem Visualizer-Budget heraus und
   der Visualizer schrumpft auf „VM + Fensterhülle".
3. **Whitespace/NewLine-Trivia:** im X-ray per Default **ausblenden** (Rauschen) mit Toggle, oder immer
   zeigen? (Tendenz: ausblenden + „Show trivia"-Toggle.)
4. **Properties-Panel** (Rider-Stil: Kind/Span/FullSpan/Text des selektierten Knotens) — Teil von v1
   oder später?
5. **Node-Label-Detail:** nur Typname, oder zusätzlich Extent `[start..end)` + gekürzter Text-Slice
   inline? (Tendenz: Typname + gedimmter Extent; Text im Properties-Panel.)
6. **Icons:** Unterscheidung Knoten/Token/Trivia/Missing über `KnownMonikers` — konkrete Moniker-Wahl
   offen.

## Aufwand (grob, revidiert)

| Block | Aufwand | Risiko |
|---|---|---|
| 1 — Engine-API `ChildTokensIncludingMissing()` (Generator + Tests) | wenige PT | gering, additiv; profitiert Diagnostics/LSP |
| 2 — Baum-VM + faithful Walk (testgetrieben) | ~2 PT | gering, VS-frei testbar |
| 3 — Fensterhülle + erstes `.vsct` | 2–3 PT | mittel (Erst-`.vsct`-Lernkurve) |
| Polish — Theming/Icons/Sync/Toggles/Properties-Panel | ~1–2 PT | gering |

Der Visualizer *ohne* Block 1 (falls die Engine-API separat als Diagnostics/LSP-Feature läuft):
im Kern Block 2 + 3 + Polish ≈ 5–7 PT.

## Verifikation

- **Block 1/2:** NUnit-Tests in `Nav.Language.Tests` (net472 via `nav test`, net10.0 via
  `dotnet test … -f net10.0 --filter …`) — reiner Datenmapping-/Walk-Test gegen `SyntaxTree.ParseText`,
  inkl. Fixtures mit Missings, Skipped-Runs, Direktiven.
- **Block 3:** ganze Solution `nav build` (VSIX braucht Full-Framework-MSBuild), dann `nav install` →
  VS, `.nav` öffnen, Fenster über „Ansicht → Weitere Fenster" öffnen. Prüfen: Baum spiegelt Struktur
  inkl. Missings/Skipped/Direktiven; Tippen aktualisiert (debounced); Knoten-Klick selektiert im Editor;
  Caret-Bewegung selektiert signifikanten Knoten. Kein Nav-Doc aktiv → Leerzustand statt Absturz;
  Dokumentwechsel bindet um.
