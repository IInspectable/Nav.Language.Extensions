# Nav Syntax Tree Visualizer — VS-Extension-Toolfenster

Überblick über Aufwand und nötige Bausteine für ein kleines VS-Toolfenster, das den **Syntax Tree**
der aktiven `.nav`-Datei als Baum darstellt — Vorbild ist Riders/Roslyns „Syntax Visualizer"
(Knoten + Tokens, Icons, Selektions-Sync mit dem Editor). Stand: Konzept/Backlog, **noch nicht
umgesetzt**.

## Kurzfazit

Der Datenteil ist praktisch geschenkt — die Engine liefert alles fertig. Der eigentliche Aufwand
steckt im **VSIX-Toolfenster-Gerüst**, das es im Repo noch **gar nicht** gibt (kein `.vsct`, keine
`ToolWindowPane`, kein `[ProvideToolWindow]`). Das ist Greenfield und der größte Risikoposten.

| Umfang | Aufwand |
|---|---|
| **MVP** (Toolfenster + Baum, nur Knoten, Klick-auf-Knoten → Editor-Selektion, Refresh bei Edit) | ~4–5 PT |
| **Vollausbau** (Tokens/Trivia, Zwei-Wege-Sync mit Caret, Properties-Panel, Icons/Polish) | ~8–10 PT |

## Was schon da ist (wird nur konsumiert)

**Syntax-Modell (`Nav.Language`, netstandard2.0):** exakt passend für einen Baum-Renderer —
- `SyntaxTree.ParseText(text, filePath, ct)` → `.Root` (`SyntaxNode`), `.Tokens`, `.SourceText`
  (`Nav.Language\Syntax\SyntaxTree.cs`).
- Rekursion generisch über `SyntaxNode.ChildNodes()` (+ `ChildTokens()` für Tokens). Kein
  `Kind`-Enum — **Label = `node.GetType().Name`** (Suffix `Syntax` abschneiden, z.B.
  `TaskDefinitionSyntax` → `TaskDefinition`). Für Tokens `token.Type`.
- Editor-Highlight & Rück-Navigation: jeder Knoten/Token hat `Extent`/`FullExtent`,
  `GetLocation()` (`Nav.Language\Common\Location.cs`, mit Start/EndLine/Character), und der
  umgekehrte Weg Caret→Knoten via `Root.FindNode(pos)` / `Root.FindToken(pos)`.
- Ein generierter Walker (`SyntaxNodeWalker` / `ISyntaxNodeVisitor`) existiert, für den Renderer ist
  aber die simple `ChildNodes()`-Rekursion ausreichend.

**Datenanbindung in der Extension (fertig, nur nutzen):**
- Aktive View: `NavLanguagePackage.GetActiveTextView()` → `IWpfTextView` → `view.TextBuffer`.
- Pro-Buffer-Singleton `ParserService.GetOrCreateSingelton(buffer)` (`ParserService\ParserService.cs`)
  → `SyntaxTreeAndSnapshot` (enthält den `SyntaxTree`), Event `ParseResultChanged` (bereits ~200 ms
  debounced, Parse im Hintergrund). Abo-Muster: von `ParserServiceDependent` ableiten und
  `OnParseResultChanged` überschreiben.
- Navigation/Selektion: `NavLanguagePackage.NavigateToLocation(view, pos)`,
  `TextViewExtensions.SetSelection(view, span)`, `Location.ToSnapshotSpan(snapshot)`.
- Threading: `NavLanguagePackage.Jtf` (`SwitchToMainThreadAsync`), `ThreadHelper.ThrowIfNotOnUIThread`.

## Projekt-Split (die tragende Randbedingung)

- **`.vsct` + `VSCTCompile` MÜSSEN in `Nav.Language.Extension2026` (net472 VSIX)** liegen — nur dort
  ist `Microsoft.VSSDK.BuildTools` referenziert. Ebenso die `Menus.ctmenu`-Zeile in `VSPackage.resx`
  (`MergeWithCTO=true` ist dort schon gesetzt).
- **Alles reine C#/XAML** (`ToolWindowPane`, `UserControl`, ViewModels, Command-Wiring) kommt in
  **`Nav.Language.ExtensionShared` (.shproj)** — kompiliert in die VSIX-Assembly; XAML-`<Page>`-Einträge
  laufen dort schon (z.B. `NavMarginControl.xaml`).
- **GUIDs/IDs doppelt pflegen** (symbolisch im `.vsct`, als C#-Konstanten in Shared) — dieselben
  Literale, sonst fehlt der Menübefehl kommentarlos.

## Phasen

### Phase 1 — Toolfenster-Gerüst (MVP, riskantester Teil)

Neue Dateien:
- `Nav.Language.Extension2026\NavSyntaxVisualizer.vsct` — Command-Table: `<Group>` unter
  `guidSHLMainMenu:IDG_VS_WNDO_OTRWNDWS0` (**Ansicht → Weitere Fenster**), ein `<Button>` „Nav Syntax
  Tree". `<Symbols>`: `guidNavPackage` = bestehende `PackageGuidString`, neuer CmdSet-GUID +
  `IDSymbol`s (Group `0x1020`, Command `0x0100`).
- `Nav.Language.ExtensionShared\SyntaxVisualizer\SyntaxVisualizerCommands.cs` — C#-Spiegel der IDs.
- `Nav.Language.ExtensionShared\SyntaxVisualizer\SyntaxVisualizerToolWindow.cs` —
  `[Guid(...)] sealed class SyntaxVisualizerToolWindow : ToolWindowPane`, `Caption`,
  `Content = new SyntaxVisualizerControl(...)`.

Änderungen:
- `NavLanguagePackage.cs` — Attribute
  `[ProvideToolWindow(typeof(SyntaxVisualizerToolWindow), Style=VsDockStyle.Tabbed, Orientation=ToolWindowOrientation.Right)]`
  + `[ProvideMenuResource("Menus.ctmenu", 1)]`; in `InitializeAsync` den `MenuCommand` an
  `OleMenuCommandService` hängen, Handler ruft
  `ShowToolWindowAsync(typeof(SyntaxVisualizerToolWindow), 0, true, DisposalToken)` via `Jtf.RunAsync`.
- `Nav.Language.Extension2026.csproj` —
  `<VSCTCompile Include="NavSyntaxVisualizer.vsct"><ResourceName>Menus.ctmenu</ResourceName></VSCTCompile>`.
- `VSPackage.resx` — `Menus.ctmenu`-MergeWithCTO-Zeile.
- `NavLanguagePackage.Guids.cs` — neue CmdSet-/ToolWindow-GUID-Konstanten dazu.

APIs: `ToolWindowPane`, `[ProvideToolWindow]`/`[ProvideMenuResource]`,
`AsyncPackage.ShowToolWindowAsync`/`FindToolWindow`, `OleMenuCommandService`/`MenuCommand`/`CommandID`.

### Phase 2 — Baum-Control + ViewModel (MVP)

Neue Dateien (Shared, unter `SyntaxVisualizer\`):
- `SyntaxNodeViewModel.cs` — `INotifyPropertyChanged`, Wrapper über `SyntaxNode`/`SyntaxToken`.
  `Label` (Typname ohne `Syntax`-Suffix bzw. `token.Type`), `ExtentText` (`[start..end)`),
  `TextPreview` (`ToString()`, **auf ~100 Zeichen/1 Zeile gekürzt**), `Icon` (`KnownMonikers`,
  Knoten vs. Token vs. Trivia), **lazy** gecachte `Children` aus `ChildNodes()`, bindbare
  `IsExpanded`/`IsSelected`, Rückverweis auf `TextExtent` für Selektion.
- `SyntaxVisualizerControl.xaml`/`.xaml.cs` — `UserControl` (Muster `NavMarginControl.xaml`):
  `TreeView` mit `HierarchicalDataTemplate` (`CrispImage` + Label + gedimmter Extent),
  `ItemContainerStyle` bindet `IsExpanded`/`IsSelected` bidirektional. Theming über
  `EnvironmentColors.*BrushKey`, `VsFont`. Toolbar: „Sync mit Caret"-`ToggleButton`
  (+ optional „Tokens anzeigen"/„Trivia anzeigen"). Enhancement: unteres Properties-Panel
  (Kind/Span/FullSpan/Text) per `GridSplitter`, Rider-Stil.
- `.projitems` — neue `<Page>`/`<Compile>`-Einträge registrieren.

### Phase 3 — Datenquelle + Refresh (MVP)

- `SyntaxVisualizerViewModel.cs` — aktive View auflösen (`GetActiveTextView` → `ParserService`),
  Root-VM aus `SyntaxTreeAndSnapshot`. Abo `ParseResultChanged` (bzw. `ParserServiceDependent`) →
  Root neu bauen, UI-Thread via `Jtf`. Dokumentwechsel: `IVsSelectionEvents.OnElementValueChanged`
  (`SEID_DocumentFrame`) → neu binden, altes Abo lösen. Kein aktives Nav-Doc / falscher Content-Type
  → Leerzustand („Kein Nav-Dokument aktiv"). Beim Schließen des Fensters Abos/Advise sauber lösen.

### Phase 4 — Zwei-Wege-Selektions-Sync (Enhancement)

- **Baum → Editor:** `SelectedItemChanged` → `Extent`/`GetLocation()` → `Location.ToSnapshotSpan` +
  `NavigateToLocation` + `SetSelection`.
- **Editor → Baum** (per Toggle): `view.Caret.PositionChanged` → `Root.FindNode(pos)` → Pfad von der
  Wurzel expandieren, Ziel `IsSelected` + in Sicht bringen.
- Rückkopplung mit `_suppressSync`-Flag brechen. **Nie** bei Caret-Bewegung neu parsen — Baum nur bei
  `ParseResultChanged` neu bauen.

## Risiken

1. **`.vsct`-Gerüst ist neu im Repo** (größtes Zeitrisiko): `VSCTCompile ResourceName` =
   `[ProvideMenuResource]`-Name = `VSPackage.resx`-Zeile müssen exakt übereinstimmen, sonst fehlt der
   Menübefehl stumm. In der Experimental-Hive verifizieren.
2. **`.shproj`+net472-Split:** `.vsct`/`VSCTCompile` können nicht in Shared (kein VSSDK.BuildTools dort).
3. **Thread-Affinität:** VS-Interop/WPF nur auf dem Main-Thread; Events kommen ggf. off-thread → immer
   über `Jtf` hoppen.
4. **Performance bei großen Dateien:** nur bei Parse-Änderungen neu bauen (upstream schon debounced),
   Children lazy + gecacht, Previews gekürzt, `FindNode` O(Tiefe).
5. **Leaks:** `ParseResultChanged`-Abo und Selection-Advise beim Fenster-Dispose lösen.

## Aufwandsschätzung (grob, Personentage)

| Phase | Aufwand |
|---|---|
| Phase 1 — Gerüst (erstes `.vsct` im Repo) | 2–3 PT |
| Phase 2 — Control + VM | 2 PT |
| Phase 3 — Datenquelle + Refresh | 1–1,5 PT |
| Phase 4 — Zwei-Wege-Sync | 2 PT |
| Theming/Icons/Leerzustände/Properties-Panel-Polish | 1 PT |

MVP (Phasen 1–3, nur Knoten, Baum→Editor-Selektion): ~4–5 PT. Vollausbau: ~8–10 PT.

## Verifikation

- **Bauen:** Ganze Solution via `nav build` (VSIX braucht Full-Framework-MSBuild).
- **Installieren/Ausprobieren:** `nav install` → VS starten, `.nav`-Datei öffnen, **Ansicht → Weitere
  Fenster → Nav Syntax Tree**. Prüfen: Baum spiegelt Struktur; Tippen aktualisiert (debounced);
  Knoten anklicken selektiert den Bereich im Editor; (Vollausbau) Caret-Bewegung selektiert den Knoten.
- Kein Nav-Doc aktiv → Leerzustand statt Absturz. Dokumentwechsel bindet um.
- Reines Datenmapping (`SyntaxNode.ChildNodes()`/Label/Extent) ggf. als kleiner NUnit-Test in
  `Nav.Language.Tests` (net472 via `nav test`, net10.0 via `dotnet test … -f net10.0`) — das
  Toolfenster selbst ist manuell in VS zu verifizieren.
