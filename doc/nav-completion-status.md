# Nav-Code-Completion — Status, Überblick & Arbeitsliste

> Dauerhafter Einstieg zur **Code-Completion** (IntelliSense) der Nav-Sprache. Beschreibt den
> heutigen (guten) Stand, die identifizierten Schwachstellen und die geplante Weiterentwicklung als
> **session-übergreifende Arbeitsliste**. Pflichtlektüre, bevor hier weitergebaut wird.

## Zweck & Leitidee

Die Completion ist **syntaxbaum-getrieben** und über den VS-freien Engine-Service
`NavCompletionService` (`Nav.Language/Completion/`) zwischen **VS-Extension und LSP-Server geteilt** —
die richtige „eine Engine"-Architektur. Ein Host übersetzt nur Protokoll/UI, die Sprachlogik liegt in
der Engine. Jede Engine-Verbesserung wirkt daher **sofort in beiden Hosts**.

Wichtig als Ausgangsbefund: Der oft vermutete Schwachpunkt „nach `:` in einer Exit-Edge sofort die
Exits vorschlagen" **funktioniert bereits** — `:` ist in beiden Hosts Trigger-Char, und
`NavCompletionContext.Classify` klassifiziert die Position als `ExitConnectionPoint` (sortiert sogar
unverbundene Exits zuerst).

## Heutiger Stand (Referenz)

**Engine-Kern** (`Nav.Language/Completion/`):
- `NavCompletionService.cs` — öffentliche API `GetCompletions(unit, position, solution?)`; wählt je
  Kontext die Kategorie und erzeugt die `NavCompletionItem`. Enthält auch `GetPathCompletions`
  (Pfad-Vervollständigung in `taskref "…"`).
- `NavCompletionContext.cs` — `Classify(unit, position)` bestimmt aus dem Baum die grammatische
  Situation. Kontext-Anker ist das signifikante Token **links** der Position (Binärsuche über den
  flachen, nach `Start` sortierten `SyntaxTree.Tokens`-Strom — **nicht** `SyntaxToken.PreviousToken()`,
  das nur parent-lokal navigiert).
- `NavCompletionItem.cs` — neutrales Item-Modell (`Label`, `Kind`, `InsertText`, `ReplacementExtent`,
  `Detail`, `Symbol`) + `NavCompletionItemKind` (`Keyword, Task, ConnectionPoint, Choice, GuiNode,
  Node, File, Folder`).

Behandelte Kontexte (`NavCompletionContextKind`): `MemberLevel, TaskNodeName, StatementStart,
EdgeSlot, TargetSlot, ExitConnectionPoint, AfterTarget, AfterTrigger, AfterCondition, Suppress,
Fallback`.

**VS-Extension** (`Nav.Language.ExtensionShared/Completion/`): vier Async-Completion-Quellen mit je
eigenem Provider und gemeinsamer Basis `AsyncCompletionSource.cs`:
- `NavCompletionSource` — Haupt-Quelle; Trigger auf Buchstaben + `:`; ruft die Engine, zeigt alles
  **außer** Edge-Keywords.
- `EdgeCompletionSource` — Trigger auf Buchstaben + `-`; zeigt **nur** die Edge-Keywords (eigener
  Ersetzungsbereich via `GetStartOfEdge`).
- `CodeCompletionSource` — Trigger auf Buchstaben + `[`; zeigt `SyntaxFacts.CodeKeywords` **an der
  Engine vorbei** (die Engine unterdrückt Code-Blöcke).
- `PathCompletionSource` — Trigger auf Buchstaben + `"` + Pfadtrenner; Pfad-Completion (dupliziert die
  Engine-Pfadlogik).
- Commit: `CompletionCommitManager.cs` (`PotentialCommitCharacters`, per-Item-Replacement über
  `ReplacementTrackingSpanProperty`).

**LSP-Server** (`Nav.Language.Lsp/NavLanguageServer.cs`): `CompletionOptions.TriggerCharacters`
(`:`, `-`, `"`, `/`, `\`) und der Handler `Completion(...)`, der stumpf `NavCompletionService.
GetCompletions` aufruft und Reihenfolge über `SortText` erhält. **Profitiert automatisch** von allen
Engine-Verbesserungen.

**Direktiv-Bausteine** (Grundlage für `#version`-Completion):
`Nav.Language/Syntax/{DirectiveTriviaSyntax, VersionDirectiveSyntax, BadDirectiveTriviaSyntax,
NavDirectiveParser}.cs`; Versions-Autorität `Nav.Language/NavLanguageVersion.cs`
(`SupportedVersions`, aktuell nur `Version1`). Zugriff am Baum: `SyntaxTree.FindTrivia(position)`,
`SyntaxTree.Directives()`, `SyntaxTrivia.GetStructure()`. Siehe `doc/nav-pragmas-versioning-status.md`.

## Identifizierte Baustellen

1. **`#version` ist ein blinder Fleck.** `Classify` inspiziert nie Direktiv-Trivia; `#` ist in keinem
   Host Trigger-Char; und weil Direktiven *strukturierte Trivia* sind (nicht im flachen
   `tree.Tokens`-Strom), fallen diese Positionen auf `MemberLevel`/`Fallback`. Kein Vorschlag für
   `version` nach `#`, keiner für die gültigen Versionsnummern.
2. **Trigger-/Commit-Chars driften** und liegen pro VS-Quelle verstreut; ein
   `// TODO … Sinnhaftigkeit prüfen` in `CompletionCommitManager.cs` steht offen.
3. **VS-Seite zersplittert:** vier Quellen mit fragiler Quer-Koordination (Nav filtert Edge-Keywords
   heraus, Edge fügt sie wieder hinzu), eigene Code-Block-Keyword-Liste an der Engine vorbei,
   duplizierte Pfad-Logik. Zudem nutzt sogar die **Engine** in `Classify` noch einen zeilenbasierten
   `IsInQuotation`/`IsInTextBlock`-Scan (der „nur aktuelle Zeile"-TODO) statt des Baums.

## Getroffene Entscheidungen (fix)

- **`pragma` wird NICHT als Completion angeboten** — solange kein konkretes Pragma existiert
  (`ParsePragma` erzeugt heute stets `BadDirectiveTriviaSyntax` + `Nav3001`), führt der Vorschlag in
  eine Sackgasse. Es wird ausschließlich `version` angeboten. `pragma` erst aktivieren, wenn ein
  echtes Pragma existiert.
- **C4 (baumbasierte Suppression) wird erst entschieden, wenn wir dort ankommen** — Umfang/Machbarkeit
  hängen an der Baum-Repräsentation von Code-Blöcken/Strings; nicht vorab festlegen.

## Arbeitsliste (session-übergreifend)

Jeder Schritt ist bewusst so geschnitten, dass er einzeln baubar, testbar und committbar ist.
Fortschritt über die Checkboxen führen.

### Workstream A — `#version`-Completion (Engine-Kern; höchster Nutzen)

- [ ] **A1 — Direktiv-Kontexte in `NavCompletionContext.Classify`.** Vor der Token-Binärsuche prüfen,
  ob die Position in einer Direktiv-Trivia liegt (`tree.FindTrivia(position)`, dann
  `trivia.HasStructure && trivia.GetStructure() is DirectiveTriviaSyntax d`). Aus der lokalen
  Token-Liste (`d.ChildTokens()` / `d.HashToken` / `VersionKeyword`) ableiten, wo der Cursor steht,
  und zwei neue `NavCompletionContextKind` liefern:
  - `DirectiveKeyword` — direkt hinter `#` → Vorschlag **nur `version`** (kein `pragma`).
  - `DirectiveVersionValue` — hinter `#version ` → Vorschlag der gültigen Versionsnummern.
  Fallstrick (analog `ContextToken`): `FindTrivia` nutzt Halbintervall `[Start, End)` — steht der Caret
  exakt am Trivia-Ende (gerade `#version ` getippt), liefert `FindTrivia(position)` `default`; den
  an/knapp-vor der Position endenden Direktiv-Trivia-Fall explizit behandeln.
- [ ] **A2 — Item-Erzeugung in `NavCompletionService.GetCompletions`.** Zwei `case`-Zweige:
  `DirectiveKeyword` → Keyword-Item `version` (Konstante aus `SyntaxFacts`); `DirectiveVersionValue`
  → je ein Item pro `NavLanguageVersion.SupportedVersions` (heute `1`), Label = `Value.ToString()`.
  **Single Source of Truth**: dieselbe Tabelle, die `Nav5001` validiert — kein hartkodierter Wert.
- [ ] **A3 — Item-Kind.** Prüfen, ob ein eigenes `NavCompletionItemKind` (`Directive`/`Version` für
  Icon) nötig ist oder `Keyword` genügt. Empfehlung: `Keyword` wiederverwenden — kleinster Eingriff.
- [ ] **A4 — Tests** (`Nav.Language.Tests/Completion/NavCompletionServiceTests.cs`, net472 **und**
  net10): Cursor nach `#`, nach `#v`, nach `#version `, ungültige Position; leere/Default-Datei ohne
  Direktive.

### Workstream B — Trigger- & Commit-Chars in einer Autorität

- [ ] **B1 — Kanonische Trigger-Char-Menge im Engine-Kern** (z.B. `NavCompletionService.
  TriggerCharacters` oder in `SyntaxFacts`): Vereinigung der heutigen Zeichen plus **`#`** (Direktiven)
  und `[` (Code-Block-Keywords). Beide Hosts konsumieren dieselbe Menge — LSP speist
  `CompletionOptions.TriggerCharacters` daraus, VS leitet `ShouldTriggerCompletionOverride` daraus ab
  statt je Quelle eigene Listen.
- [ ] **B2 — Commit-Chars.** Den `// TODO … Sinnhaftigkeit prüfen` in `CompletionCommitManager.cs`
  auflösen: Commit-Char-Menge bewusst festlegen und aus derselben Autorität beziehen (VS + LSP
  konsistent).

### Workstream C — VS-Quellen-Konsolidierung (engine-getrieben)

Zielbild: **eine** `IAsyncCompletionSource` + **ein** Provider. Die Engine bleibt alleinige Instanz
für das *Was* (inkl. Code-Block-Keywords, Pfade, Direktiven); die VS-Quelle macht nur noch Mapping,
Icons/Filter und Ersetzungsbereich.

**Kern-Herausforderung — Ersetzungsbereiche.** Die Async-Completion-API hat **einen**
`applicableToSpan` pro Session; Edge-Keywords (`-->`) und Pfade brauchen andere Spans als ein
Identifier. Lösung: `NavCompletionItem.ReplacementExtent` (heute nur von Pfad-Items genutzt)
verallgemeinern — die Engine liefert `ReplacementExtent` auch für Edge-Keywords; die VS-Quelle
honoriert `item.ReplacementExtent`, wenn gesetzt (per-Item-Replacement über den vorhandenen
`ReplacementTrackingSpanProperty`-Pfad), sonst den Identifier-Span der Session.

- [ ] **C1 — Code-Block-Keywords in die Engine.** Kontext `CodeBlock` in `Classify` erkennen und die
  Code-Keywords aus dem Service liefern; `CodeCompletionSource(+Provider)` entfällt.
- [ ] **C2 — Pfade über die Engine.** VS-Quelle auf `GetPathCompletions` umstellen;
  `PathCompletionSource(+Provider)` + Duplikat-Logik entfällt.
- [ ] **C3 — Edge in die Nav-Quelle mergen.** Mit per-Item-`ReplacementExtent` entfällt
  `EdgeCompletionSource` und das fragile `IsEdgeKeyword`-Heraus-/Wieder-Hinzufügen.
- [ ] **C4 — Baumbasierte Suppression (Umfang erst bei Ankunft entscheiden).** Idee: den
  zeilenbasierten `IsInQuotation`/`IsInTextBlock`-Scan in der Engine (`Classify`) und in der
  VS-`ShouldProvideCompletions` durch baumbasierte Erkennung ersetzen. **Zuerst** prüfen, wie
  Code-Blöcke (`do [ … ]`) und String-Literale im Baum repräsentiert sind; trägt das nicht sauber,
  C4 als optionalen Folgeschritt behandeln, nicht den Umbau blockieren.

**Sequenz:** C1 → C2 → C3 (jeder Schritt entfernt eine Quelle und ist einzeln prüfbar), C4 separat
(Regressionsgefahr bei Suppression).

## Kritische Dateien

- Engine: `Nav.Language/Completion/{NavCompletionContext, NavCompletionService, NavCompletionItem}.cs`;
  Referenz `Nav.Language/NavLanguageVersion.cs`, `Nav.Language/Syntax/SyntaxTree.cs`
  (`FindTrivia`/`Directives`), `…/Syntax/*DirectiveTriviaSyntax.cs`.
- VS: `Nav.Language.ExtensionShared/Completion/NavCompletionSource.cs` (+ Provider) bleibt/wird die
  eine Quelle; `Edge/Code/PathCompletionSource(+Provider).cs` entfallen; `AsyncCompletionSource.cs`
  (Mapping/Replacement), `CompletionCommitManager.cs` (Commit-Chars).
- LSP: `Nav.Language.Lsp/NavLanguageServer.cs` (`CompletionOptions.TriggerCharacters` + Handler).
- Tests: `Nav.Language.Tests/Completion/NavCompletionServiceTests.cs`, `NavCompletionPathTests.cs`.

## Verifikation

1. **Engine-Unit-Tests** (`NavCompletionServiceTests`) auf **net472 + net10** grün — inkl. neuer
   Direktiv-, Code-Block- und (bei C4) Suppression-Fälle. Beachte: `nav test` baut nicht selbst →
   erst `nav build`, für net10 `dotnet test … -f net10.0`.
2. **LSP-stdio-Smoke** gegen die laufende `nav.lsp`: `textDocument/completion` an Positionen nach `#`,
   `#version `, `:` (Exit), nach Quellknoten (Edge), in `taskref "…"` (Pfad); Trigger-Char-Registrierung
   (`#`, `[`) im `initialize`-Ergebnis kontrollieren.
3. **VS-Extension** (`nav build` inkl. VSIX, MSBuild.exe; `nav install`): die Situationen manuell im
   Editor durchspielen; nach dem Merge sicherstellen, dass Edge-Keywords weiterhin den korrekten
   Bereich ersetzen (`-->`), Pfade weiterhin den String-Inhalt ersetzen und keine Doppel-Vorschläge
   entstehen.
