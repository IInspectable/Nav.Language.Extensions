# Call Hierarchy für Visual Studio — Implementierungsanleitung

> **Zweck dieses Dokuments:** Die Aufrufhierarchie (Call Hierarchy) gibt es bereits im LSP-Server.
> Sie soll zusätzlich in der **VS-2026-Extension** verfügbar werden — über VS' eingebautes
> Call-Hierarchy-Toolfenster (Ctrl+K, Ctrl+T), so wie Roslyn es für C# nutzt. Dieses Dokument ist
> die **Step-für-Step-Anleitung**: jeder Step ist so beschrieben, dass er in einer **eigenen Session**
> ohne Vorwissen abgearbeitet werden kann. Vor jedem Step zuerst den Abschnitt
> [„Gemeinsamer Kontext"](#gemeinsamer-kontext) lesen.

---

## Leitidee (in einem Satz)

Es kommt **keine neue Sprachlogik** dazu — der vorhandene, VS-freie Engine-Kern
`NavCallHierarchyService` wird 1:1 so aufgerufen wie im LSP; neu ist nur eine **VS-Adapterschicht**,
die VS' fertiges Call-Hierarchy-Toolfenster bedient.

---

## Gemeinsamer Kontext

### Was schon existiert (NICHT anfassen)

**Engine-Kern** — `Nav.Language\CallHierarchy\NavCallHierarchyService.cs` (host-agnostisch, fertig):

| Methode | Liefert |
|---|---|
| `PrepareCallHierarchy(CodeGenerationUnit unit, int position)` → `ITaskDefinitionSymbol?` | Die Task-Definition, deren Block die (0-basierte) Zeichen-Position enthält. Anker der Hierarchie. |
| `GetOutgoingCalls(ITaskDefinitionSymbol task)` → `IReadOnlyList<OutgoingCall>` | Aufgerufene Tasks (gruppiert nach Ziel-Deklaration). `OutgoingCall { ITaskDeclarationSymbol Target; IReadOnlyList<Location> CallSites; }` |
| `GetIncomingCallsAsync(ITaskDefinitionSymbol task, NavSolution solution, CancellationToken ct)` → `Task<IReadOnlyList<IncomingCall>>` | Solution-weit alle Tasks, die `task` aufrufen. `IncomingCall { ITaskDefinitionSymbol Caller; IReadOnlyList<Location> CallSites; }` |

Die Aufrufstellen (`CallSites`) sind `Location`s der TaskNode-Bezeichner in der aufrufenden Task.

### Die exakte Vorlage: der LSP-Server

Das VS-Verhalten ist **identisch** zum LSP, nur in-process statt über JSON-RPC. Die LSP-Implementierung
ist die maßgebliche Referenz — beim Bauen jedes Steps danebenlegen:

- `Nav.Language.Lsp\NavLanguageServer.cs`, Methoden `PrepareCallHierarchy` (~Zeile 659),
  `CallHierarchyIncomingCalls` (~682), `CallHierarchyOutgoingCalls` (~709), `ResolveCallHierarchyTask` (~739).
- `Nav.Language.Lsp\CallHierarchy\CallHierarchyBuilder.cs` — baut aus Task-Symbolen die LSP-Items
  (`FromDefinition` für Definitionen/Aufrufer, `FromDeclaration` für ausgehende Ziele). **Pendant** in VS
  wird `NavCallHierarchyItemFactory`.

**LSP↔VS-Abbildung:**

| LSP-Schritt | VS-Entsprechung |
|---|---|
| `textDocument/prepareCallHierarchy` | Command-Handler baut Root-`ICallHierarchyMemberItem` aus `PrepareCallHierarchy(unit, caretOffset)` |
| `callHierarchy/incomingCalls` | `ICallHierarchySearchCommand` der Kategorie **CallsToMember** → `GetIncomingCallsAsync` |
| `callHierarchy/outgoingCalls` | `ICallHierarchySearchCommand` der Kategorie **CallsFromMember** → `GetOutgoingCalls` |
| Re-Resolve via `{Uri, Offset}` (`ResolveCallHierarchyTask`) | Jeder Knoten trägt `{FilePath, Offset}` und löst bei Expansion frisch via `PrepareCallHierarchy` auf |

### Das VS-Vorbild: wie die Extension VS-Features baut

- **Commanding ist modern** (`ICommandHandler<TArgs>` aus `Microsoft.VisualStudio.Text.Editor.Commanding.Commands`),
  exakt wie Roslyn. Vorlage zum Abkupfern: `Nav.Language.ExtensionShared\Commands\FindReferencesCommandHandler.cs`
  und `GoToDefinitionCommandCommandHandler.cs` (MEF-Export `[Export(typeof(ICommandHandler))]`,
  `[ContentType(NavLanguageContentDefinitions.ContentType)]`, `[Name(...)]`).
- **Caret → Task:** `SemanticModelService.GetOrCreateSingelton(buffer).UpdateSynchronously()` liefert
  `CodeGenerationUnitAndSnapshot` (siehe `FindReferencesCommandHandler.GetCodeGenerationUnit`); mit dem
  Caret-Offset → `NavCallHierarchyService.PrepareCallHierarchy(unit, offset)`. **Kein** `TryFindSymbolUnderCaret` nötig.
- **Solution holen:** `await NavLanguagePackage.GetSolutionAsync(ct)` (liefert `NavSolution`).
- **Navigation zu einer `Location`:** vorhandener `GoToLocationService` (im GoToDefinition-Handler injiziert)
  bzw. `Nav.Language.ExtensionShared\Common\LocationExtensions.cs`. Cross-file/`taskref`-Ziele inklusive.
- **Icons:** `Nav.Language.ExtensionShared\Images\ImageMonikers.cs` → `ImageMonikers.TaskDefinition`
  (= `KnownMonikers.Task`). VS' Call-Hierarchy-API will i.d.R. ein WPF-`ImageSource`, nicht `ImageMoniker`
  → per Imaging-SDK (`IVsImageService2` / `ImageMoniker.ToImageSource(...)`) konvertieren.

### Wo die neuen Dateien hingehören

**Alles** nach `Nav.Language.ExtensionShared` (`.shproj`/`.projitems`, von der VSIX
`Nav.Language.Extension2026` geteilt). **Keine** Änderung an `Nav.Language` (Engine). **Kein**
eigenes Toolfenster und **keine** Package-Registrierung — das Toolfenster liefert VS.

```
Nav.Language.ExtensionShared\
  Commands\ViewCallHierarchyCommandHandler.cs   -- ICommandHandler<ViewCallHierarchyCommandArgs>
  Commands\CommandHandlerNames.cs               -- (bestehende Datei: Namens-Konstante ergänzen)
  CallHierarchy\NavCallHierarchyItemFactory.cs  -- Symbol -> ICallHierarchyMemberItem (Pendant zu CallHierarchyBuilder)
  CallHierarchy\NavCallHierarchyMemberItem.cs   -- ICallHierarchyMemberItem (ein Task-Knoten)
  CallHierarchy\NavCallHierarchySearchCommand.cs-- ICallHierarchySearchCommand (CallsTo / CallsFrom)
  CallHierarchy\NavCallHierarchyDetail.cs        -- CallHierarchyDetail je Aufrufstelle (ggf. nur Hilfsklasse)
```

> **Hinweis `.projitems`:** Wenn neue Dateien in `Nav.Language.ExtensionShared` nicht automatisch
> kompiliert werden, müssen sie evtl. in `Nav.Language.ExtensionShared.projitems` als `<Compile Include=…>`
> eingetragen werden (so wie die bestehenden Dateien dort gelistet sind).

### Verfügbare Assemblies (kein neues NuGet nötig)

`Nav.Language.Extension2026.csproj` referenziert das volle `Microsoft.VisualStudio.SDK`-Metapaket plus
`Microsoft.VisualStudio.LanguageServices` (Roslyns VS-Schicht). Damit sind verfügbar:
- `Microsoft.VisualStudio.Language.CallHierarchy` (das Toolfenster + Interfaces),
- Roslyns Referenz-Implementierung (`CallHierarchyProvider`, `CallHierarchyItem`, …) zum Spicken.

### Build / Test

- Bauen der VSIX braucht **Full-Framework MSBuild** → `n build` (nicht `dotnet build`; die VS-Extension
  baut `dotnet build` nicht). Debug bevorzugen (`n publish`/`n build` bauen Debug).
- Manueller Test: VSIX in die experimentelle VS-Instanz deployen (`n install` bzw. F5-Debug der VSIX),
  eine `.nav`-Datei öffnen, Cursor in eine Task, Ctrl+K, Ctrl+T.
- Sprache: **echte Umlaute** in Code-Kommentaren/Texten (Projektkonvention).
- **Nicht committen** — am Ende jedes Steps nur Review + Build + fertige Commit-Message als Text liefern.

### Die VS-Call-Hierarchy-API (Kontrakt, den wir bedienen)

| Typ | Herkunft | Unsere Rolle |
|---|---|---|
| `ViewCallHierarchyCommandArgs` | VS-Commanding | Geste „View Call Hierarchy" — `ICommandHandler<>` dranhängen |
| `ICallHierarchyPresenter` | **von VS exportiert** | per `[ImportMany] IEnumerable<ICallHierarchyPresenter>` holen, `PresentHierarchy(root)` → öffnet Toolfenster |
| `ICallHierarchyMemberItem` | **wir** | ein Knoten = eine Nav-Task |
| `ICallHierarchySearchCommand` | **wir** | je Richtung ein Such-Kommando (CallsTo / CallsFrom) |
| `ICallHierarchySearchCallback` | von VS übergeben | wir rufen `AddResult(...)` / `SearchSucceeded()` |
| `CallHierarchyDetail` | **wir** füllen | die Aufrufstellen (TaskNodes) eines Knotens |
| `CallHierarchyPredefinedSearchCategoryNames` | VS | Kategorie-Strings `CallsToMember` (eingehend) / `CallsFromMember` (ausgehend) |

**Rekursion = der eigentliche Mehrwert:** Jedes per `callback.AddResult(...)` gelieferte Kind ist
wieder ein vollwertiges `ICallHierarchyMemberItem` mit **eigenen** Such-Kommandos → das Toolfenster
lässt beliebig tief auf-/absteigend expandieren.

### Stolpersteine (vorab geklärt)

1. **Threading:** `ICallHierarchySearchCommand.StartSearch` darf nicht blockieren. Engine-Aufruf auf
   `TaskScheduler.Default`, Ergebnisse per `callback.AddResult(...)`, am Ende `callback.SearchSucceeded()`.
   UI-Marshalling nur, wo die API es verlangt.
2. **Freshness:** Knoten **nicht** ein `ITaskDefinitionSymbol` über mehrere Expansions festhalten
   (Solution kann sich ändern), sondern `{FilePath, Offset}` tragen und je Expansion frisch
   `PrepareCallHierarchy` aufrufen — exakt das LSP-`ResolveCallHierarchyTask`-Muster.
3. **Icon:** `ImageMoniker` → `ImageSource` konvertieren (Imaging-SDK).
4. **Leere Ergebnisse / nicht aufgelöste `taskref`:** `Declaration == null` überspringen (wie LSP/Engine).
5. **`CommandState`-Gating:** Command nur auf `IWpfTextView` mit Nav-ContentType aktiv (wie FindReferences).

---

## Step 0 — API-Signaturen verifizieren *(eigene Session, ~½ Tag)*

**Ziel:** Die exakten Member-Signaturen der VS-Interfaces sichern, damit Steps 1–3 nicht raten müssen.
**Das ist der einzige echte Unsicherheitsfaktor** — die API ist alt und dünn dokumentiert.

**Aufgaben:**
1. `Microsoft.VisualStudio.Language.CallHierarchy.dll` inspizieren (Object Browser in VS, oder ILSpy/dotPeek).
   Vollständige Signaturen notieren von:
   - `ICallHierarchyMemberItem` (Properties wie `MemberName`, `SortText`, `ContainingTypeName`,
     `ContainingNamespaceName`, Glyph/`ImageSource`, `Details`, `SupportedSearchCommands`/`SupportedSearchCategories`,
     `IsRoot`, `CanNavigateTo`, `NavigateTo()` — exakte Namen/Typen prüfen!),
   - `ICallHierarchySearchCommand` (`DisplayName`, `SearchCategory`, `StartSearch(callback)`, `CancelSearch()`),
   - `ICallHierarchySearchCallback` (`AddResult`, `SearchSucceeded`, `SearchFailed`, `ReportProgress`, `CancellationToken`),
   - `ICallHierarchyPresenter` (`PresentHierarchy(...)`-Signatur),
   - `CallHierarchyDetail` (Konstruktor/Properties: zugehöriges Item, Span/Location, `Text`, `NavigateTo`),
   - `CallHierarchyPredefinedSearchCategoryNames` (exakte Konstanten-Namen für eingehend/ausgehend).
2. Roslyns Referenz-Implementierung danebenlegen (in `Microsoft.VisualStudio.LanguageServices` bzw.
   `Microsoft.CodeAnalysis.Editor`): `CallHierarchyProvider`, `CallHierarchyItem`, `CallHierarchySearchCommand`,
   `CallHierarchyDetail`, der `CallHierarchyCommandHandler` (wie wird `ICommandHandler<ViewCallHierarchyCommandArgs>`
   verdrahtet, wie wird der Presenter geholt und `PresentHierarchy` aufgerufen?).
3. Bestätigen, dass `ViewCallHierarchyCommandArgs` in `Microsoft.VisualStudio.Text.Editor.Commanding.Commands`
   existiert und welche Properties es trägt (`TextView`, `SubjectBuffer`).

**Ergebnis dieser Session:** Diese Anleitung unten im Abschnitt
[„Step 0 — Befunde"](#step-0--befunde) mit den realen Signaturen ergänzen. Kein Code.

**Definition of Done:** Alle obigen Signaturen schriftlich festgehalten; offene Abweichungen zum hier
angenommenen Kontrakt markiert.

---

## Step 1 — Command-Handler + Root-Item (Prepare) *(eigene Session)*

**Voraussetzung:** Step 0 abgeschlossen (Signaturen bekannt).

**Ziel:** Die Geste Ctrl+K, Ctrl+T auf einer Task öffnet das Call-Hierarchy-Toolfenster mit dem
**Wurzelknoten** (Kinder noch leer/nicht expandiert).

**Aufgaben:**
1. `Commands\CommandHandlerNames.cs`: Konstante `ViewCallHierarchyCommandHandler` ergänzen.
2. `Commands\ViewCallHierarchyCommandHandler.cs`: `ICommandHandler<ViewCallHierarchyCommandArgs>`,
   MEF-Attribute wie bei `FindReferencesCommandHandler`. Im Konstruktor `[ImportMany] IEnumerable<ICallHierarchyPresenter>`
   importieren. `GetCommandState`: `Available` nur bei `IWpfTextView`. `ExecuteCommand`:
   - `CodeGenerationUnitAndSnapshot` holen (wie `FindReferencesCommandHandler.GetCodeGenerationUnit`),
   - Caret-Offset bestimmen,
   - `NavCallHierarchyService.PrepareCallHierarchy(unit, offset)` → Task; bei `null` Info-Meldung,
   - Root-`ICallHierarchyMemberItem` via `NavCallHierarchyItemFactory.FromDefinition(task)`,
   - `presenter.PresentHierarchy(root)` auf dem UI-Thread.
3. `CallHierarchy\NavCallHierarchyItemFactory.cs`: `FromDefinition(ITaskDefinitionSymbol)` und
   `FromDeclaration(ITaskDeclarationSymbol)` (Pendant zu `CallHierarchyBuilder`) — erzeugen
   `NavCallHierarchyMemberItem` mit `{FilePath, Offset}`, Name, Icon. Bei Deklarationen ohne Syntax
   die Bezeichner-`Location` verwenden (wie `CallHierarchyBuilder.FromDeclaration`).
4. `CallHierarchy\NavCallHierarchyMemberItem.cs`: Grundgerüst — Properties (Name, Containing*, Glyph,
   `IsRoot`, `NavigateTo` via `GoToLocationService`/`LocationExtensions`). `SupportedSearchCommands` darf
   in diesem Step noch leer sein (Such-Kommandos kommen in Step 2/3).

**Definition of Done:** Build grün (`n build`); in der exp. VS-Instanz öffnet Ctrl+K,Ctrl+T auf einer
Task das Toolfenster mit korrekt benanntem Wurzelknoten; Doppelklick navigiert zur Task.
Commit-Message als Text liefern.

---

## Step 2 — Ausgehende Aufrufe (Calls From) *(eigene Session)*

**Voraussetzung:** Step 1 abgeschlossen.

**Ziel:** Der Wurzelknoten lässt sich zu den **aufgerufenen** Tasks expandieren, inkl. Aufrufstellen.

**Aufgaben:**
1. `CallHierarchy\NavCallHierarchySearchCommand.cs`: `ICallHierarchySearchCommand` mit
   `SearchCategory = CallsFromMember`. `StartSearch(callback)`:
   - auf `TaskScheduler.Default` wechseln,
   - Task per `{FilePath, Offset}` frisch auflösen (`NavLanguagePackage.GetSolutionAsync` →
     `PrepareCallHierarchy`),
   - `NavCallHierarchyService.GetOutgoingCalls(task)`,
   - je `OutgoingCall` ein Kind-`NavCallHierarchyMemberItem` via `Factory.FromDeclaration(call.Target)`
     bauen, dessen `Details`/`CallSites` aus `call.CallSites` füllen, `callback.AddResult(child)`,
   - am Ende `callback.SearchSucceeded()`.
2. `NavCallHierarchyMemberItem.SupportedSearchCommands` um das CallsFrom-Kommando ergänzen.
3. `CallHierarchy\NavCallHierarchyDetail.cs` (falls eigene Klasse nötig): eine Aufrufstelle =
   `Location` + Anzeigetext + `NavigateTo`.

**Definition of Done:** Build grün; Wurzel expandiert zu den aufgerufenen Tasks; Aufrufstellen
navigierbar; cross-file/`taskref`-Ziele landen richtig. Commit-Message als Text.

---

## Step 3 — Eingehende Aufrufe (Calls To) + echte Rekursion *(eigene Session)*

**Voraussetzung:** Step 2 abgeschlossen.

**Ziel:** Knoten lassen sich auch zu ihren **Aufrufern** expandieren, und **jeder** Kindknoten trägt
selbst wieder beide Kategorien → mehrstufiges Auf-/Absteigen.

**Aufgaben:**
1. Zweites `ICallHierarchySearchCommand` mit `SearchCategory = CallsToMember`. `StartSearch`:
   wie Step 2, aber `await NavCallHierarchyService.GetIncomingCallsAsync(task, solution, ct)`; je
   `IncomingCall` ein Kind via `Factory.FromDefinition(call.Caller)`.
2. **Sicherstellen, dass die in Step 2/3 erzeugten Kindknoten dieselbe `SupportedSearchCommands`-Logik
   tragen** (beide Kategorien) — sonst keine Tiefenexpansion. (Wenn Factory einheitlich baut, ist das
   automatisch der Fall.)
3. `CancellationToken` aus dem Callback an `GetIncomingCallsAsync` durchreichen; `CancelSearch()` verdrahten.

**Definition of Done:** Build grün; eingehende Aufrufe erscheinen; ein Kindknoten lässt sich erneut
in beide Richtungen expandieren (auch über Dateigrenzen); Abbruch funktioniert. Commit-Message als Text.

---

## Step 4 — Feinschliff & manueller Test *(eigene Session)*

**Voraussetzung:** Steps 1–3 abgeschlossen.

**Aufgaben:**
- Icons/Glyphs korrekt (`ImageMonikers.TaskDefinition` → `ImageSource`).
- Anzeigetexte: `MemberName` = Task-Name, `ContainingTypeName`/Namespace sinnvoll (z.B. Dateiname/Pfad).
- Aufrufstellen-Details mit lesbarem Text (analog LSP-`FromRanges`).
- Leere Ergebnisse, nicht aufgelöste `taskref`, Caret außerhalb jeder Task: sauberes Verhalten.
- `CommandState`-Gating final prüfen.
- Manueller Smoke-Test in der exp. VS-Instanz über mehrere Dateien/Includes; Vergleich mit dem
  LSP-Verhalten (gleiche `.nav`-Solution in VS Code öffnen).

**Definition of Done:** Feature in VS funktional gleichwertig zum LSP. Commit-Message als Text.
Abschließend in `doc\nav-lsp-status.md` / Memory den neuen Stand vermerken.

---

## Step 0 — Befunde

> *(Dieser Abschnitt wird in der Step-0-Session mit den real verifizierten Signaturen gefüllt.
> Bis dahin gelten die Annahmen aus „Die VS-Call-Hierarchy-API" oben.)*

---

## Aufwand (Richtwert)

| Step | Aufwand |
|---|---|
| Engine | 0 (vorhanden) |
| Step 0 (API-Klärung) | ½ Tag |
| Steps 1–3 (Kern) | 2–3 Tage |
| Step 4 (Feinschliff/Test) | ½–1 Tag |
| **Gesamt** | **3–4 Tage** |

Das Risiko steckt fast vollständig in Step 0/1 (erstes korrektes `PresentHierarchy`). Danach sind
Steps 2–3 mechanisch — sie rufen nur die getesteten Engine-Methoden und spiegeln das LSP-Verhalten.
