# Nav — Code-Keywords als kontextuelle Keywords (Status & Umsetzungsplan)

## Ziel

Die 10 **Code-Keywords** der `[ … ]`-Deklarationen (`result`, `params`, `base`, `namespaceprefix`,
`using`, `code`, `generateto`, `notimplemented`, `abstractmethod`, `donotinject`) sollen **kontextuelle
Keywords** werden: Sie sind nur noch als **Leader-Token** direkt hinter `[` ein Schlüsselwort, an jeder
anderen syntaktischen Position ein ganz normaler `Identifier`. Konkreter Auslöser: `[params ErrorBoxResult
result]` scheitert heute mit `unexpected input 'result'`, weil `result` als hartes Keyword gelext wird und
in der Parameter-**Namens**position kein `Identifier` mehr sein kann. Nach der Umsetzung parst dieser Fall
sauber; der Rest des Sprachverhaltens bleibt unverändert.

## Ausgangsproblem (verifiziert)

- Der Lexer mappt `result` **bedingungslos** auf `SyntaxTokenType.ResultKeyword` — `NavLexer.cs:557–583`
  (`Keywords`-Dictionary), Lookup in `ScanIdentifierOrKeyword` (`NavLexer.cs:303–328`). Keine Kontext-Gate.
- Der Parser verlangt an der Parameter-Namensposition ein `Identifier`-Token —
  `ParseParameter` (`NavParser.cs:1874–1884`): `var name = At(SyntaxTokenType.Identifier) ? Eat(...) : null;`.
- Ergebnis: `result` (= `ResultKeyword`) passt nicht, `name` wird `null`, das `]` fehlt an der erwarteten
  Stelle → `unexpected input 'result'`.
- `ErrorBoxResult` funktioniert als **Typ**, weil es ein einziges `Identifier`-Token ist (die
  Groß-/Kleinschreibung trifft das reine `result` nicht).

## Kernentscheidung: „Retype an der Parser-Grenze" (mit Begründung)

**Was gilt:** Der Lexer emittiert für die 10 Code-Keywords künftig `Identifier`. Der Parser **stuft das
Leader-Token in der Deklarations-Position (direkt nach `[`) per Retype wieder auf den jeweiligen
Keyword-Token-Typ hoch** (`RawToken with { Type = keyword }`).

**Warum:** Der finalisierte Syntaxbaum wird dadurch **byte-identisch** zu heute — der Leader trägt exakt
denselben Keyword-Token-Typ wie bisher. Damit bleibt **alles Downstream unangetastet**: Classification,
QuickInfo (`SyntaxFacts.GetKeywordDescription`), Completion-Angebot (`CodeBlockFacts`), Formatter
(`GapRules`/`AlignmentMapBuilder`), Outlining. Der Blast-Radius liegt praktisch nur in Lexer + Parser.
`ParseParameter` braucht **keine** Änderung — `result` lext in der Namensposition jetzt als `Identifier`
und wird von der bestehenden Zeile konsumiert.

**Warum das die Roslyn-Idee ist:** Roslyn lext contextual keywords als `IdentifierToken` und hebt sie im
Parser über einen `ContextualKind` kontextabhängig zum Keyword. Nav hat kein `ContextualKind`-Feld — das
Token-Modell ist ein flaches `SyntaxTokenType` auf `RawToken` (`readonly record struct`, `NavLexer.cs:20`).
Der Retype ist die adäquate Übertragung: „Promotion" = Umtypung des `RawToken` statt eines Parallel-Felds.

**Verworfene Alternativen (nicht erneut aufrollen):**

- **Option A — Parser-Fix nur an der Namensposition** (Keyword-Token als Name akzeptieren und zu Identifier
  retypen): Halbfix. Das Wort bliebe an der Namensposition ein Keyword-*Token* → falsche Einfärbung, stört
  Completion/QuickInfo. Verworfen zugunsten der sauberen kontextuellen Lösung.
- **Option C — Verbatim-Escape `@result`** (wie C#): neue Sprachoberfläche, für dieses eng umrissene Problem
  Overkill. Verworfen.
- **Echtes `ContextualKind`-Feld am Token** (die wörtliche Roslyn-Form): berührt `RawToken`, `SyntaxToken`
  (Bit-Packing, `SyntaxToken.cs:115`) und jede Konsumentenstelle — deutlich mehr Aufwand **ohne**
  Zusatznutzen, weil der Retype ohnehin identische Bäume liefert. Verworfen.

## Zweite Entscheidung: B-konservativ (mit Begründung)

`SyntaxFacts.Keywords` (Autorität für `IsValidIdentifier`, `SyntaxFacts.cs:159` + `:515`) enthält heute auch
die Code-Keywords → `result` ist als Bezeichner **überall** verboten (Task-Namen, Knoten-Namen). Nach der
Lexer-Umstellung würde `task result;` syntaktisch parsen.

**Was gilt (empfohlen): B-konservativ** — die Code-Keywords **bleiben** in `SyntaxFacts.Keywords`, also
bleibt `IsValidIdentifier("result") == false`. Folge: `task result;` parst syntaktisch, wird aber von
`Nav2000IdentifierExpected` (`SemanticAnalyzer/Nav2000IdentifierExpected.cs`) weiterhin sauber als Fehler
gemeldet.

**Warum:** Die Parameter-Namensposition (das gemeldete Problem) ist ein reiner syntaktischer
`Identifier`-Slot und fragt `IsValidIdentifier` **nie** — sie funktioniert unabhängig von dieser
Entscheidung. Top-Level-Namen konservativ zu halten (syntaktisch Identifier, semantisch via Nav2000
validiert) ist exakt der Roslyn-Split und hält die Änderung klein und risikoarm.

**Verworfen für jetzt: B-vollständig** (Code-Keywords aus `SyntaxFacts.Keywords` entfernen → `task result;`
legal). Möglicher Folge-Schritt, falls das je gewünscht wird; berührt zusätzlich `Nav2000IdentifierExpected`,
`SyntaxFactsTest`, `NavGrammarConsistencyTests`. **Nicht** Teil dieses Plans.

## Verifizierte Fakten (Pfad:Zeile)

- **Lexer-Keyword-Tabelle:** `NavLexer.cs:557–583`. Zu entfernende Einträge: `result`(573), `params`(574),
  `base`(575), `namespaceprefix`(576), `using`(577), `code`(578), `generateto`(579), `notimplemented`(580),
  `abstractmethod`(581), `donotinject`(582). Bleiben: die Struktur-Keywords (`task`…`do`).
- **Längen-Vorfilter:** `NavLexer.cs:549–552`. `MinKeywordLength = 2`, `MaxKeywordLength = 15`. Reine
  Perf-Vorfilterung vor dem Dict-Lookup, **keine** Korrektheitsbedingung. Nach dem Entfernen ist das längste
  verbleibende Keyword `spontaneous` (11) → `MaxKeywordLength` auf **11** senken; `Min` bleibt 2. Kommentar
  bei `:549` mitziehen (die aufgezählten Beispiele nennen aktuell `namespaceprefix = 15`).
- **Leader-Erkennung:** `AtCodeDeclaration(SyntaxTokenType keyword)` — `NavParser.cs:2076–2087`. Nutzt heute
  `PeekType(1) == keyword` plus einen Präfix-Zweig `PeekType(1) == Identifier && IsKeywordPrefix(...)`.
- **Präfix-Test:** `IsKeywordPrefix` — `NavParser.cs:2095–2103`; verlangt `text.Length < keywordText.Length`
  → ein **exakter** Treffer zählt nie als Präfix (wichtig: exakt und Präfix kollidieren nicht).
- **Leader-Konsum:** `EatKeywordOrSkip(SyntaxTokenType keyword)` — `NavParser.cs:2248–2265`. Heute:
  `At(keyword)` → `Eat`; sonst `ReportMissing` + Identifier-Skip (Präfix-Rescue beim Tippen).
- **Token-Store ist mutierbar:** `_raw` ist ein Array von `RawToken` (`readonly record struct(SyntaxTokenType
  Type, TextExtent Extent)`, `NavLexer.cs:20`). Index-Zuweisung `_raw[_pos] = _raw[_pos] with { Type = keyword }`
  ist zulässig. `At0`/`At` lesen `_raw[_pos].Type`, `Eat` liest `var token = _raw[_pos]` **nach** einer
  etwaigen Umtypung (`NavParser.cs:2221–2234`) → Retype-dann-Eat funktioniert.
- **Retype-Zieltext:** `SyntaxFacts.GetKeywordText(type)` — `SyntaxFacts.cs:492`, liest aus `KeywordTexts`
  (`SyntaxFacts.cs:474`). Enthält die Code-Keyword-Typen **weiterhin** (wird **nicht** angefasst) →
  `GetKeywordText(ResultKeyword) == "result"` bleibt gültig, obwohl der Eintrag aus der Lexer-`Keywords`-
  Tabelle verschwindet.
- **Aktueller Text am Cursor:** `CurrentText` — `NavParser.cs:2354`.
- **`ParseParameter`:** `NavParser.cs:1874–1884` — bleibt unverändert.
- **`SyntaxTokenType.ResultKeyword` etc.** (`SyntaxTokenType.cs`) bleiben erhalten — die retypten Leader
  nutzen sie, alles Downstream keyt darauf.
- **Konsumenten-Inventar** (26 Dateien referenzieren die Code-Keyword-Typen, u.a.):
  - `CodeBlockFacts.cs` (erlaubte Keywords je Host; Autorität für Completion + Parser-Recovery),
  - `Completion/NavCompletionService.cs` (`:376–389`, bietet Code-Keywords im Leader-Slot),
  - `SyntaxFacts.cs` (`KeywordDescriptions` ab `:173`, QuickInfo),
  - `Formatting/GapRules.cs`, `Formatting/AlignmentMapBuilder.cs`,
  - `ExtensionShared/Outlining/OutlineTagger/CodeUsingDirectiveOutlineTagger.cs`,
    `…/CodeNamespaceDeclarationOutlineTagger.cs`,
  - die `Code*DeclarationSyntax`-Knoten (`ResultKeyword => ChildTokens().FirstOrMissing(...)`).
  Alle sehen dank Retype weiterhin den Keyword-Token-Typ am Leader → **kein** Eingriff nötig (nur
  Verifikation).

## Fallen

- **`nav test` baut nicht selbst** — vor `nav test` (net472, gebündelter NUnit-Runner) erst `nav build`.
  net10-Tests via `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` bauen bei Bedarf
  selbst. Beide TFMs müssen grün sein.
- **`dotnet test -f net472` läuft ins Leere (0 Tests)** — für net472 ausschließlich `nav test`.
- **UTF-8 mit BOM** für alle bearbeiteten Dateien (CLAUDE.md). `Edit`/`Write` hinterlassen kein BOM →
  Kodierung nach dem Schreiben prüfen/nachziehen; **keine** Windows-1252-Altlast bloß mit BOM „reparieren"
  (siehe CLAUDE.md).
- **`KeywordTexts`/`SyntaxTokenType` NICHT anfassen** — nur die Lexer-`Keywords`-Tabelle wird beschnitten.
  Der Retype braucht `GetKeywordText` weiterhin.
- **Exakt-vor-Präfix ist automatisch konfliktfrei**, weil `IsKeywordPrefix` echte Präfixe (kürzer) verlangt;
  trotzdem im Code die Reihenfolge exakt-ODER-Präfix so lassen wie in der Skizze.
- **Classification ist der einzige echte Unsicherheitspunkt** (siehe Step 3): kein rein-lexikalischer
  `SyntaxTokenClassifier` gefunden — Classification läuft baumgetrieben auf dem finalisierten Baum, in den
  der Retype eingeht. Muss dennoch an einer Semantic-Tokens-Probe bestätigt werden.

## Plan

### Step 1 — Lexer + Parser umstellen (der tragende Eingriff)

**Dateien:** `Nav.Language\Syntax\NavLexer.cs`, `Nav.Language\Syntax\NavParser.cs`.

1a. `NavLexer.cs:557–583`: die 10 Code-Keyword-Einträge aus dem `Keywords`-Dictionary entfernen.
1b. `NavLexer.cs:551–552`: `MaxKeywordLength` von 15 auf **11** senken, Kommentar `:549` aktualisieren.
1c. `AtCodeDeclaration` (`NavParser.cs:2076`) — Leader per exaktem Text ODER Präfix erkennen:

```csharp
bool AtCodeDeclaration(SyntaxTokenType keyword) {
    if (!At(SyntaxTokenType.OpenBracket)) {
        return false;
    }

    if (PeekType(1) == SyntaxTokenType.Identifier) {
        var text = PeekText(1);
        return text == SyntaxFacts.GetKeywordText(keyword)   // exakt: kontextuelles Keyword
            || IsKeywordPrefix(keyword, text);               // Präfix: beim Tippen unvollständig
    }

    return PeekType(1) == keyword;                           // Rest-Fall (weiterhin hart gelexte)
}
```

1d. `EatKeywordOrSkip` (`NavParser.cs:2248`) — Happy-Path „Retype-dann-Eat" vor dem bestehenden Präfix-Skip:

```csharp
RawToken? EatKeywordOrSkip(SyntaxTokenType keyword) {
    if (At(keyword)) {
        return Eat(keyword);
    }

    // Kontextuelles Code-Keyword: als Identifier gelext, Text == kanonisches Literal. In der
    // Leader-Position zum Keyword-Token hochstufen → finalisierter Baum byte-identisch zum früher
    // hart gelexten Keyword (Classification/QuickInfo/Formatter unverändert).
    if (At(SyntaxTokenType.Identifier) && CurrentText == SyntaxFacts.GetKeywordText(keyword)) {
        _raw[_pos] = _raw[_pos] with { Type = keyword };
        return Eat(keyword);
    }

    ReportMissing(Describe(keyword));
    // … bestehender Präfix-Skip unverändert …
}
```

**Definition of Done:** `nav build` grün; bestehende Suite grün (net472 via `nav test`, net10 via `dotnet
test … -f net10.0`); `nav snapshot` neu gezogen und **ohne Diff** (starker Beleg für „byte-identisch").
`[params ErrorBoxResult result]` parst ohne Diagnose, `result` ist an der Namensposition ein `Identifier`.

### Step 2 — Tests

**Dateien:** `Nav.Language.Tests\` (passende vorhandene Test-Klassen erweitern; Fixtures als **Raw-Strings**,
siehe CLAUDE.md).

- Positiv: `[params Foo result]`, `[result Foo params]`, `[base Foo using]` parsen; das Nicht-Leader-Wort ist
  `Identifier` und bekommt `TextClassification.ParameterName`.
- Unverändert: QuickInfo auf dem Leader (`NavHoverServiceTests`), Completion bietet Code-Keywords im
  Leader-Slot weiter an (`NavCompletionServiceTests:890` + `:986`).
- Regression: `SyntaxFactsTest`, `SyntaxTokenTests`, `SyntaxTreeAllRulesTests`, `NavGrammarConsistencyTests`
  auf **beiden** TFMs grün.

**Definition of Done:** neue Tests grün auf net472 **und** net10; keine Regression in der Bestandssuite.

### Step 3 — Semantic-Tokens-/Classification-Probe (Verifikation des Restrisikos)

- Prüfen, dass der Leader (`result` in `[result …]`) weiterhin Keyword-Farbe trägt und `result` als
  Parameter-Name `ParameterName`-Farbe bekommt — an einer LSP-Semantic-Tokens-Probe (`nav.lsp` per stdio)
  und/oder der VS-Extension.
- Falls hier ein rein-lexikalischer Klassifizierungspfad auftaucht (nicht erwartet), diesen an die
  kontextuelle Sicht angleichen.

**Definition of Done:** Leader = Keyword-Farbe, Name = Parameter-Farbe, an mindestens einem Host belegt.

## Stand

- **Noch nichts umgesetzt.** Reine Analyse-/Planungs-Session. Arbeitsbaum sauber, Branch `master`, HEAD
  `83db0983`.
- **Nächster Schritt: Step 1.**
- Offene Design-Frage bewusst entschieden: **B-konservativ** (B-vollständig ist nicht Teil dieses Plans).

## Verifikation (Kommandos)

- `nav build` — Solution bauen (MSBuild.exe; nötig, weil `nav test` nicht selbst baut).
- `nav test` — net472-Suite (gebündelter NUnit-Runner).
- `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0 --filter "FullyQualifiedName~Syntax"`
  — net10-Suite (Filter nach Bedarf, z.B. `~Completion`, `~QuickInfo`).
- `nav snapshot` — Regression-Snapshots neu erzeugen; danach `git status`/`git diff` auf **kein** Diff prüfen.
