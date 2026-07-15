# Weg B — Präprozessor-Direktiven als strukturierte Trivia (Plan & Handoff)

> Selbsttragender Einstieg für eine **frische Session ohne Gesprächskontext.** Ergänzt
> `doc/nav-pragmas-versioning-status.md` (Direktiven-Ist-Stand) und `doc/nav-kolibri.md`
> (Abschnitte „Präprozessor-Direktiven" + „architektonischer Gabelpunkt"). Quelle der Wahrheit
> bleibt der Code; Pfade/Zeilen sind Einstieg (zum Erhebungszeitpunkt verifiziert).
>
> **Status: umgesetzt** (net10 1145/0, net472 grün; `.diag` byte-identisch). Steps 1–5 erledigt —
> siehe „Umsetzung" am Ende. Der folgende Plan bleibt als Begründung/Nachschlagewerk stehen; die
> geltende Quelle ist der Code.
>
> **Syntax-Vereinfachung (aktuell):** Die Versionsdirektive heißt inzwischen `#version <N>` (eigene Direktive,
> kein Pragma-Subjekt). `#pragma …` bleibt als Form erhalten, meldet aber „Unknown pragma" (`Nav3001` — wieder
> in Gebrauch). Die unten durchgehend verwendete Schreibweise `#pragma version` ist historisch; maßgeblich sind
> `doc/nav-pragmas-versioning-status.md` und `doc/nav-directive-subparser.md`.
>
> **Nachfolge-Umbau (siehe `doc/nav-directive-subparser.md`):** Der hier als „Region in `NavParser`"
> skizzierte Direktiv-Sub-Parser wurde inzwischen in die eigene Klasse `NavDirectiveParser` extrahiert; die
> Zeilenanfang-Regel erzwingt jetzt der **Lexer**, und **jede** `#version`
> wird strukturell `VersionDirectiveSyntax` (deplatzierte/doppelte bleiben Knoten, sind aber unwirksam —
> `Nav3003`/`Nav3004` liefert `NavParser.ResolveLanguageVersion`). Die unten stehenden Erwähnungen von
> `Nav3001` bzw. „andere Direktive → `BadDirectiveTriviaSyntax`" sind entsprechend überholt.

## Warum

Der Direktiven-Vorlauf `NavParser.ParseDirectives()` (`Nav.Language\Syntax\NavParser.cs:1872`) erkennt
`#pragma version` per **roher Index-Arithmetik über das `_raw`-Token-Array** (`DirectiveEnd`,
`VersionSubjectIndex`, `i = end - 1`), weil die Präprozessor-Token `IsHidden` sind und der Parser-Cursor
sie nicht sieht. Das ist bewusst **„Weg A"** (flaches Token-Modell, Direktiv-Knoten hängt als erster
Kindknoten der Wurzel und **besitzt** flache Token im globalen Strom). Zielbild der Doku ist **„Weg B"** —
Roslyn-Stil: eine Direktive ist **strukturierte Trivia** (`SyntaxTrivia` mit `GetStructure()` →
`DirectiveTriviaSyntax`), ihre Token verlassen den flachen `SyntaxTree.Tokens`-Strom
(`doc/nav-kolibri.md:235-246, 282-305`; `doc/nav-pragmas-versioning-status.md:136-138`).

Ziel: den handgeschriebenen Vorlauf durch einen **cursor-basierten Präprozessor-Sub-Parser** ersetzen, der
**jede** `#`-Direktive strukturell zu einem Knoten parst, und Direktiven als **strukturierte Trivia**
modellieren.

## Geklärte Entscheidungen (nicht ohne Grund umwerfen)

- **Voller Sub-Parser**: `#pragma version` → `VersionDirectiveSyntax`; **jede andere** Direktive
  (`#if`, `#pragma warning`, …) → **neuer** generischer `BadDirectiveTriviaSyntax`, der weiterhin
  Nav3000/Nav3001 meldet.
- **Nur Darstellung umstellen**: Direktive wird intern zu strukturierter Trivia (technisch an jedem
  Folge-Token möglich); die geltende Platzierungsregel bleibt (`#pragma version` **nur ganz oben**,
  Nav3003/Nav3004). **Kein** neues „Direktiven überall"-Verhalten.
- **Verhalten nach außen unverändert**: Nav3000/3001/3002/3003/3004 gleich (Locations + Reihenfolge
  Nav3001→Nav3000), Codegen byte-identisch. `.tokens`/`.tree`/`.trivia`-Golden ändern sich
  erwartungsgemäß; **`.diag` bleibt identisch** (Diagnose-Parität ist die Leitplanke).

## Ist-Zustand (aus Code-Erkundung — was schon da ist)

- **Angehängte Trivia existiert bereits** (Schritt 5 des Kolibri-Umbaus): `SyntaxToken` trägt
  `LeadingTrivia`/`TrailingTrivia` (`Nav.Language\Syntax\SyntaxToken.cs:74,80`); alle Token teilen **ein**
  `_allTrivia`-Array via `SyntaxTriviaList` (`SyntaxTriviaList.cs`); Aufbau in `NavParser.BuildTrivia`
  (`NavParser.cs:2228`), Zuordnung per `LookupTrivia` (`:2188`). Trivia liegt **nur** angehängt vor, nicht
  im flachen Strom.
- **`SyntaxTrivia`** (`Nav.Language\Syntax\SyntaxTrivia.cs`) ist `readonly struct` mit nur `Type`+`Extent`,
  **ohne** Parent/Struktur. Doku dort nennt `GetStructure()` bereits als geplante Erweiterung → der
  natürliche Andockpunkt.
- **Direktive heute = Kindknoten + flache Token**: `VersionDirectiveSyntax : DirectiveTriviaSyntax`
  (`VersionDirectiveSyntax.cs`, `DirectiveTriviaSyntax.cs`), als **erster Kindknoten** der
  `CodeGenerationUnitSyntax` (`CodeGenerationUnitSyntax.cs:30`); Token via `MaterializeDirectiveTokens`
  in den globalen `_tokens` (`NavParser.cs:2032`), im Sweep per `consumedStarts` übersprungen (`:2075,2090`);
  Wurzel-Extent wird vorgezogen (`:262-264`). `ChildTokens()` liest `SyntaxTree.Tokens[Extent]`
  (`SyntaxNode.cs:107`).
- **Versions-Durchreiche**: `CodeGenerationUnitSyntax.LanguageVersion` (`:47`) → `CodeGenerationUnit.
  LanguageVersion` (`SemanticModel\CodeGenerationUnit.cs:53`) → `CodeGeneratorContext.LanguageVersion`
  (`CodeGen\CodeGeneratorContext.cs:19`) → StringTemplates.
- **`Directives()`** = `Root.DescendantNodes().OfType<DirectiveTriviaSyntax>()` (`SyntaxTree.cs:74`).
- **Positions-Konsumenten** (aus Erkundung): **indifferent/bereits robust** — GoTo/References/Rename
  (symbol-basiert via `SymbolPosition`), `BraceMatching` (`FindAtPosition`), `GetClassifiedText`,
  `CodeFixContext.FindTokens`, `ChildTokens` (Parent-basiert). **Betroffen** — Einfärbung
  (`Nav.Language.Lsp\SemanticTokensBuilder.cs` `CollectSpans`;
  `Nav.Language.ExtensionShared\Classification\SyntacticClassificationTagger.cs` `GetTags`) färbt
  Präprozessor-Token heute aus dem **flachen Strom**; Completion-Binärsuche
  (`Nav.Language\Completion\NavCompletionContext.cs:207-244`) läuft über `tree.Tokens`.
- **Golden/Tests**: `SyntaxGoldenTests` (`.tokens`/`.tree`/`.diag`/`.trivia` + `RoundTrip`,
  `ParsesAndRoundTripsAllTypingPrefixes`); `TokenTriviaTests.PreprocessorTokensAreSeparatorsWithoutAttachedTrivia`;
  `SyntaxTreeAllRulesTests` (Knotentyp-Zähler, jeder Typ muss in `AllRules.nav` vorkommen; aktuell 47).
  `UpdateGolden` ist `[Explicit]`.
- **Walker/Visitor** werden vom Roslyn-Quellgenerator erzeugt (`Build\SourceGenerators\
  Nav.Visitor.SourceGenerator`); jede konkrete, nicht-abstrakte `*Syntax`-Klasse bekommt Accept/Walk
  automatisch. `SyntaxNode.FindToken`-Doku (`SyntaxNode.cs:210-211`) behauptet noch „Nav führt keine
  strukturierte Trivia" → beim Umbau aktualisieren.

## Zielmodell

- **`SyntaxTrivia.cs`**: optionaler Struktur-Verweis (`DirectiveTriviaSyntax`) + `HasStructure`/
  `GetStructure()`. Bleibt Struct (nur ein Referenzfeld mehr).
- Neuer Trivia-Typ `SyntaxTokenType.DirectiveTrivia`: eine **ganze** Direktivzeile (`#…` bis **vor** ihr
  Zeilenende) ist **ein** strukturiertes Trivia-Stück im `_allTrivia` → fließt als Leading-Trivia des
  nächsten im Strom verbleibenden Tokens (bzw. `EndOfFile`). Das terminierende `\r\n` bleibt `NewLine`-Trivia.
- **`DirectiveTriviaSyntax`** hält seine Token **lokal** (eigene kleine `SyntaxTokenList`), **nicht** im
  globalen `SyntaxTree.Tokens`; `ChildTokens()` für Direktiv-Knoten auf die lokale Liste überschreiben.
- Direktiv-Knoten sind **keine** `ChildNodes` der Wurzel mehr; erreichbar über die Trivia.

## Steps (alle erledigt — siehe „Umsetzung")

Arbeitsweise (User-Vorgabe): große Aufgabe in Steps; nach **jedem** Step Code-Review + Build/Tests, dann
fertige **Commit-Message als Text** — **nie** selbst committen. Neue Dateien UTF-8 **mit BOM**, echte
Umlaute (ä ö ü ß). Nicht auf „Steps" in dauerhafter Code-Doku verweisen.

### Step 1 — Strukturierte-Trivia-Mechanik (additiv, byte-identisch)
- `SyntaxTrivia.cs`: Feld `SyntaxNode _structure` (nullbar), Ctor-Overload, `HasStructure`, `GetStructure()`.
- `SyntaxTokenType.cs`: neuer Typ `DirectiveTrivia`. `SyntaxFacts.IsTrivia(SyntaxTokenType)`
  (`SyntaxFacts.cs:196`) um `DirectiveTrivia` erweitern.
- `DirectiveTriviaSyntax.cs`: lokale Token-Ablage + `ChildTokens()`-Override (Basis für Version/Bad).
- **Kein** Produzent strukturierter Trivia ⇒ Build + alle Tests grün, Golden unverändert.

### Step 2 — Direktiv-Sub-Parser (cursor-basiert)
- Neuer `NavDirectiveParser` (bzw. Region in `NavParser.cs`): kleiner Reader über einen Präprozessor-Lauf,
  ersetzt `ParseDirectives`/`DirectiveEnd`/`VersionSubjectIndex`/`AcceptVersionDirective`/
  `MaterializeDirectiveTokens`/`ReportDirectiveDiagnostics` (`NavParser.cs:1872-2058`).
- `#pragma version <int>` → `VersionDirectiveSyntax` (Form unverändert, Token lokal); alles andere → **neuer**
  `Nav.Language\Syntax\BadDirectiveTriviaSyntax.cs : DirectiveTriviaSyntax` (`[SampleSyntax]`,
  `sealed partial`). Nav3000/3001 (bad), Nav3002 (malformed), Nav3003/3004 (Platzierung/Duplikat) **exakt**
  wie heute; Location über `DirectiveLocation` (`NavParser.cs:1964`).

### Step 3 — `BuildTrivia`: Präprozessor-Läufe zu Direktiv-Trivia falten
- `NavParser.BuildTrivia` (`:2228`) + `AttachNonSignificantTokens` (`:2071`): Präprozessor-Token nicht mehr
  als „hidden separator" in `_tokens`; stattdessen Sub-Parser-Output als **ein** strukturiertes
  `DirectiveTrivia`-Stück ins `_allTrivia`, mit umgebendem Whitespace/Newline in die Leading-Trivia des
  Folge-Tokens. `consumedStarts`-Pfad (`:2075`) und Wurzel-Extent-Vorziehung (`:262-264`) entfallen.

### Step 4 — Konsumenten reconnecten
- `SyntaxTree.Directives()` (`SyntaxTree.cs:74`): über
  `DescendantTrivia().Where(HasStructure).Select(GetStructure).OfType<DirectiveTriviaSyntax>()`.
- `CodeGenerationUnitSyntax` (`:19-47`): `LanguageVersionDirective`/`LanguageVersion` aus der ersten
  strukturierten Versions-Direktive der Trivia (kein Ctor-Kindparameter mehr). Durchreiche in
  `CodeGenerationUnit.cs:53` und `CodeGeneratorContext.cs:19` **unverändert**.
- **Einfärbung** (Präprozessor-Token nicht mehr im Strom): `SemanticTokensBuilder.CollectSpans` und
  `SyntacticClassificationTagger.GetTags` zusätzlich Spans der strukturierten Direktiv-Token emittieren
  (über `DescendantTrivia`/`GetStructure().ChildTokens()`); Klassifikationen unverändert
  (`PreprocessorKeyword`/`NumberLiteral`).
- Completion-Binärsuche (`NavCompletionContext.cs:207-244`): läuft mit Direktiv-Token-freiem Strom
  natürlich weiter (Direktiven sind Trivia) — verifizieren.
- Abdeckung: `SyntaxTreeAllRulesTests` so anpassen, dass Direktiv-Knoten über Trivia erreicht werden;
  Zähler +1 (`BadDirectiveTriviaSyntax`). `SyntaxNode.FindToken`-Doku (`:210-211`) aktualisieren.

### Step 5 — Golden, Tests, Doku
- `SyntaxGoldenTests.UpdateGolden` neu: `.tokens`/`.tree`/`.trivia` ändern sich (Direktiv-Token verlassen
  `.tokens`, erscheinen als strukturierte `.trivia`), **`.diag` byte-identisch** prüfen.
- `TokenTriviaTests.PreprocessorTokensAreSeparatorsWithoutAttachedTrivia` neu schreiben.
- Neue Tests: `GetStructure()` liefert `VersionDirectiveSyntax`/`BadDirectiveTriviaSyntax`; Round-Trip über
  strukturierte Trivia; Nav3000-3004-Parität; Versions-Durchreiche unverändert.
- `AllRules.nav`: `#pragma version 1` behalten + eine unbekannte Direktive für `BadDirectiveTriviaSyntax`.
- `doc/nav-kolibri.md`, `doc/nav-pragmas-versioning-status.md` und dieses Dokument fortschreiben.

## Risiken
- **Round-Trip**: Whitespace-vor-`#` + Direktive + Zeilenende müssen vollständig in die Leading-Trivia des
  Folge-Tokens fließen, sonst bricht `TokenStreamRoundTrips`/`…AllTypingPrefixes`.
- **Codegen-Parität**: Versions-Durchreiche identisch (byte-identische `.expected.cs`).
- **LF-Fallstrick**: Direktiven terminieren nur bei `\r\n` (`NavLexer.ScanPreprocessor`) — beibehalten.
- **VS-Extension** (net472/VSIX): Einfärbung erst mit `nav build` verifizierbar (kein `dotnet build`).

## Verifikation
- Engine: `dotnet build Nav.Language\Nav.Language.csproj`.
- Tests: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` **und** net472 via
  NUnit-Console (`Build\nunit.consolerunner\...\nunit3-console.exe … bin\Debug\Nav.Language.Tests.dll`).
- Golden: `[Explicit] SyntaxGoldenTests.UpdateGolden`, dann `git diff` — nur `.tokens`/`.tree`/`.trivia`
  ändern sich, `.diag` unverändert, kein Korpus-`.expected.cs` verändert.
- LSP-stdio-Smoke: `#pragma version` weiterhin farblich klassifiziert.
- `nav build` (VSIX) für den VS-Klassifizierungspfad.

## Umsetzung (erledigt)

Weg B ist implementiert; Direktiven sind jetzt **strukturierte Trivia** nach Roslyn-Vorbild. Committed in
`d79aaaf5` (Step 1) und `3a8caa8a` (Steps 2–5). Verifiziert: net10 1145/0, net472 grün, `.diag`
byte-identisch, kein Korpus-`.expected.cs` verändert.

**Was tatsächlich entstand:**
- **`SyntaxTrivia`** trägt einen optionalen `SyntaxNode _structure` samt `HasStructure`/`GetStructure()`;
  bleibt `readonly struct` (`Nav.Language\Syntax\SyntaxTrivia.cs`). Neuer Trivia-Typ
  `SyntaxTokenType.DirectiveTrivia`, in `SyntaxFacts.IsTrivia` aufgenommen.
- **`DirectiveTriviaSyntax`** hält seine Token in einer **lokalen** `SyntaxTokenList` (`SetLocalTokens`,
  `ChildTokens()`-Override), **nicht** im flachen `SyntaxTree.Tokens`-Strom. Neuer konkreter Typ
  **`BadDirectiveTriviaSyntax`** für jede nicht-wirksame Direktive (unbekannt → Nav3000/3001, deplatziert
  → Nav3003, doppelt → Nav3004); `VersionDirectiveSyntax` bleibt die einzige wirksame Ausprägung.
- **`NavParser`**: Direktiv-Sub-Parser ersetzt den früheren Index-Arithmetik-Vorlauf; `BuildTrivia` faltet
  jeden Präprozessor-Lauf zu **einem** strukturierten `DirectiveTrivia`-Stück und hängt es als
  Leading-Trivia des Folge-Tokens (bzw. `EndOfFile`) an. `consumedStarts`-Skip und Wurzel-Extent-Vorziehung
  entfielen.
- **Konsumenten:** `SyntaxTree.Directives()` liest über `DescendantTrivia().Where(HasStructure)…`;
  `CodeGenerationUnitSyntax.LanguageVersionDirective`/`LanguageVersion` ziehen die erste
  `VersionDirectiveSyntax` aus den Trivia (kein Ctor-Kindparameter mehr). Einfärbung in
  `SemanticTokensBuilder.CollectSpans` (LSP) und `SyntacticClassificationTagger.GetTags` (VS) emittiert die
  Direktiv-Token-Spans aus den strukturierten Trivia. Durchreiche in `CodeGenerationUnit`/
  `CodeGeneratorContext` unverändert.

**Abweichungen vom Plan (bewusst):**
- **`AllRules.nav` bleibt fehlerfrei** — statt dort eine unbekannte Direktive einzufügen, wird
  `BadDirectiveTriviaSyntax` in `SyntaxTreeAllRulesTests` über ein **eigenes Fehler-Schnipsel** abgedeckt
  (Knotentyp-Zähler 47 → 48). `AllRules.nav` trägt weiterhin nur `#pragma version 1`.
- `SyntaxNode.FindToken`-Doku ist auf `GetStructure()` umgeschrieben (die Behauptung „Nav führt keine
  strukturierte Trivia" entfernt).
