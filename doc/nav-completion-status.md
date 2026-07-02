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
TransitionStart, EdgeSlot, TargetSlot, ExitConnectionPoint, AfterTarget, AfterTrigger, AfterCondition,
Suppress, Fallback`. Der Task-Body ist grammatisch geordnet (erst Knoten-Deklarations-Block, dann
Transitions-Block); entsprechend trennt die Completion den Satzanfang: `StatementStart` (Deklarations-Block
— Deklarations-Keywords **und** quellfähige Knoten) vs. `TransitionStart` (Transitions-Block — nur
quellfähige Knoten plus `init`, **keine** Deklarations-Keywords). Knoten-Referenzen werden zudem auf ihre
grammatische Rolle gefiltert: Quell-Slots bieten nur `ISourceNodeSymbol`, der Ziel-Slot nur
`ITargetNodeSymbol`. Auch der **Code-Block** (`CodeBlock`) ist wirt-sensitiv: der `CodeBlockHost` (Datei-Kopf,
`taskref`, `task`-Kopf, `init`-Knoten, `task`-Knoten) entscheidet über die zulässige Keyword-Teilmenge (z.B.
Datei-Ebene nur `using`/`namespaceprefix`), statt pauschal aller `SyntaxFacts.CodeKeywords`. Der Wirt wird am
Token **links** des `[` bestimmt (verlässlich auch beim leeren, noch nicht geparsten `[]`).

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

- [x] **A1 — Direktiv-Kontexte in `NavCompletionContext.Classify`.** Vor der Token-Binärsuche prüft
  `DirectiveContext(tree, position)`, ob die Position in einer Direktive liegt, und liefert zwei neue
  `NavCompletionContextKind`:
  - `DirectiveKeyword` — direkt hinter `#` (bzw. im ersten Wort-Slot, auch beim getippten Präfix `#v`)
    → Vorschlag **nur `version`** (kein `pragma`).
  - `DirectiveVersionValue` — hinter `#version ` → Vorschlag der gültigen Versionsnummern.

  Umsetzung wich vom Skizzen-Vorschlag ab: statt `tree.FindTrivia(position)` (Halbintervall
  `[Start, End)` — liefert am Trivia-Ende `default`) enumeriert `DirectiveAt` `tree.Directives()` und
  matcht den Direktiv-Extent **inklusive Endposition**. So wird der frisch getippte Fall `#version ` mit
  Caret am Trivia-Ende sauber erfasst; der Direktiv-Extent endet vor dem Zeilenende, daher keine
  Kollision mit der Folgezeile. Slot-Entscheidung über `VersionKeyword.End` bzw. das erste
  Wort-Token (`PreprocessorKeyword`/`PragmaKeyword`/`VersionKeyword`); tiefer in einer nicht erkannten
  Direktive (`#pragma foo …`) → `Suppress` statt Fallback.
- [x] **A2 — Item-Erzeugung in `NavCompletionService.GetCompletions`.** `DirectiveKeyword` →
  Keyword-Item `version` (neue Konstante `SyntaxFacts.VersionDirectiveKeyword`, von der der Lexer sein
  `PreprocessorKeywords`-Literal jetzt bezieht → SSOT); `DirectiveVersionValue` → `VersionValueItems()`,
  je ein Item pro `NavLanguageVersion.SupportedVersions` (heute `1`), Label = `v.ToString()`.
- [x] **A3 — Item-Kind.** `NavCompletionItemKind.Keyword` wiederverwendet — kein eigenes Kind nötig.
- [x] **A4 — Tests** (`NavCompletionServiceTests`, net472 **und** net10): Cursor nach `#`
  (nur `version`, kein `pragma`/Sprach-Keyword), nach `#v` (Präfix), nach `#version ` (Werte-Slot am
  Trivia-Ende → Versionsnummern), `#pragma foo …` (Subjekt-Slot → nichts). net10 1159/0, net472-Service
  14/14.

### Workstream B — Trigger- & Commit-Chars in einer Autorität

- [x] **B1 — Kanonische Trigger-Char-Menge im Engine-Kern.** `NavCompletionService.TriggerCharacters`
  (`IReadOnlyList<char>`) + `IsTriggerCharacter(char)` sind jetzt die eine Autorität: Vereinigung der
  bisherigen Zeichen (`:`, `-`, `[`, `"`, `/`, `\`) **plus `#`** (Direktiven). `#` bezieht die Menge aus
  der neuen SSOT `SyntaxFacts.Hash` (auch der Lexer nutzt sie jetzt). Buchstaben sind bewusst nicht Teil
  der Menge (lösen client-/`char.IsLetter`-seitig ohnehin aus). Beide Hosts konsumieren sie: der
  LSP-Server speist `CompletionOptions.TriggerCharacters` daraus (gewinnt `#` und `[`); die vier
  VS-Quellen haben ihre eigenen `ShouldTriggerCompletionOverride` verloren — die **eine** Implementierung
  in der Basis `AsyncCompletionSource` leitet aus `IsTriggerCharacter` ab (die echte Spezialisierung je
  Quelle bleibt `ShouldProvideCompletions`). Damit löst insbesondere `#` jetzt auch in VS die
  Direktiv-Completion aus. Tests: `TriggerCharacters_ContainAllContextDelimiters`,
  `IsTriggerCharacter_MatchesTriggerCharacters` (net10 1161/0, net472 1169/0).
- [x] **B2 — Commit-Chars.** Der `// TODO … Sinnhaftigkeit prüfen` in `CompletionCommitManager.cs` ist
  aufgelöst: `NavCompletionService.CommitCharacters` ist jetzt die eine Autorität mit einer bewusst
  festgelegten Menge — `{ Leerzeichen, `,`, `;`, `:`, `"`, `[`, `]`, `/`, `\` }` (Trenner,
  Connection-Point-Doppelpunkt, Zeichenketten-/Code-Block-Begrenzer, Pfadtrenner). Der **Punkt bleibt
  bewusst draußen** (in Nav ein gültiges Bezeichner-Zeichen — ein Commit darauf würde qualifizierte Namen
  zerreißen), ebenso das frühere `'` (kein Nav-Konstrukt). VS bezieht daraus
  `PotentialCommitCharacters`, der LSP-Server `CompletionOptions.AllCommitCharacters` (vorher gar keine)
  → beide Hosts konsistent. Test: `CommitCharacters_AreTheDeliberateSet` (net10 17/17, net472 1170/0).

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

- [x] **C1 — Code-Block-Keywords in die Engine.** Neuer Kontext `NavCompletionContextKind.CodeBlock`:
  `Classify` erkennt im Code-Block (`[ … ]`) den **Schlüsselwort-Slot direkt hinter `[`** (Kontext-Anker
  `contextToken.Type == OpenBracket`) und liefert darüber `CodeBlockKeywordItems()` aus dem Service
  (`SyntaxFacts.CodeKeywords` ohne versteckte); im C#-Inhalt dahinter weiterhin `Suppress`. Die
  `ContextToken`-Berechnung wanderte dafür **vor** die `IsInTextBlock`-Prüfung. Die frühere separate
  `CodeCompletionSource(+Provider)` ist entfernt; `NavCompletionSource.ShouldProvideCompletions`
  unterdrückt Code-Blöcke nicht mehr selbst, sondern überlässt der Engine die Entscheidung (leere Liste
  im C#-Inhalt). Mehrzeilige Blöcke bleiben die bekannte „nur aktuelle Zeile"-Grenze (→ C4). Tests:
  `InCodeBlockKeywordSlot_OffersCodeKeywords` (net10 1163/0, net472 1171/0).
- [x] **C2 — Pfade über die Engine.** `NavCompletionSource` bezieht Pfade jetzt aus
  `NavCompletionService.GetCompletions(unit, position, solution)`: im String-Kontext holt sie die
  `NavSolution` (cached, via `NavLanguagePackage.GetSolutionAsync`) und reicht sie an die Engine; der
  Applicable-/Filter-Bereich ist der getippte **Dateiname-Teil** (`GetStartOfFileNamePart`), den
  Ersetzungsbereich (gesamter String-Inhalt) trägt jedes Item über `ReplacementExtent`. Neues Mapping
  `AsyncCompletionSource.CreatePathCompletion` bildet ein File-Item ab (Icon, `filterText`=Dateiname,
  `insertText`=relativer Pfad, per-Item-Replacement über den `ReplacementTrackingSpanProperty`-Pfad,
  QuickInfo-`FileInfo` aus dem relativen Pfad rekonstruiert). `PathCompletionSource(+Provider)` sowie die
  jetzt toten Base-Helfer (`CreateFileInfoCompletion`, `CreateDirectoryInfoCompletion`, der
  `DirectoryInfo`-QuickInfo-Zweig, `DirectoryInfoPropertyName`) sind entfernt.
  **Bewusster Verhaltenswechsel:** Die VS-Seite navigiert **nicht mehr** durchs Dateisystem
  (`..`, Unterordner, `[verzeichnis]\`-Scoping); stattdessen — wie schon der LSP — **solution-weit alle
  erreichbaren `*.nav`, gefiltert über den Dateinamen** (tippt man „Messageb", findet das auch ein tief
  verschachteltes „MessageBoxes.nav"). Das ist die „eine Engine"-Vereinheitlichung. Engine-Pfadlogik
  (`GetPathCompletions`) blieb unverändert; ihre Tests (`NavCompletionPathTests`) weiterhin grün
  (net10 1163/0, net472 1171/0).
- [x] **C3 — Edge in die Nav-Quelle mergen.** Die Engine setzt jetzt auch für Edge-Keywords einen
  `ReplacementExtent`: neuer Helfer `NavCompletionService.EdgeReplacementExtent(source, position)` (Port des
  VS-`GetStartOfEdge` — Rückwärtslauf über die neue SSOT `SyntaxFacts.EdgeCharacters`/`IsEdgeCharacter` bis zum
  Zeilenanfang) speist `VisibleEdgeKeywordItems(replacement)` im `EdgeSlot`- **und** im `Fallback`-Fall. Ist
  nichts Edge-artiges vorgetippt, ist der Bereich leer (reines Einfügen an der Position); der eine reale
  Nicht-leer-Fall ist das getippte `o` (Beginn von `o->`) — Buchstabe **und** Edge-Zeichen, das als Wort-Präfix
  behandelt wird, sodass der Kontext der Quellknoten (EdgeSlot) bleibt und der Bereich das `o` einschließt.
  Die VS-Quelle honoriert den Extent jetzt kind-unabhängig: `AsyncCompletionSource.ToCompletionItem(item,
  snapshot, navDirectory)` dispatcht Pfad→Datei-Item, sonst Symbol-/Keyword-Item, und hängt bei gesetztem
  `ReplacementExtent` per-Item den `ReplacementTrackingSpanProperty` an (extrahierter Helfer
  `ApplyReplacementExtent`, von `CreatePathCompletion` mitgenutzt). `NavCompletionSource` filtert **nichts** mehr
  heraus — die frühere `EdgeCompletionSource(+Provider)`, das `IsEdgeKeyword`-Heraus-/Wieder-Hinzufügen, der
  `[Order]`-Bezug in `NavCompletionSourceProvider` sowie die toten `TextSnaphotLineExtensions.GetStartOfEdge`/
  `IsEdgeChar` sind entfernt. Der LSP profitiert automatisch (sein Handler mappt `ReplacementExtent` bereits
  generisch auf einen `TextEdit`) — Edge-Keywords bekommen dort jetzt einen präzisen Ersetzungsbereich.
  **Bewusst hingenommen:** nach dem Merge hat die eine VS-Session **einen** `applicableToSpan` (Identifier-Span);
  bei getipptem `-` filtert die Vorschlagsliste nicht mehr über das `-` (der Commit bleibt über den per-Item-Extent
  korrekt). Tests: `EdgeSlot_EdgeItemsCarryReplacementExtent`, `EdgeSlot_ReplacementExtentCoversTypedEdgeCharacters`
  (net10 1165/0, net472 1173/0).
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
