# Handoff: Direktiv-Sub-Parser (`NavDirectiveParser`) + Lexer-Gate + Versions-Semantik

> **Selbsttragender Einstieg für eine frische Session ohne Gesprächskontext.** Branch `feature/nav-parser`.
> Quelle der Wahrheit ist der Code; die Zeilennummern sind Einstieg (zum Erhebungszeitpunkt verifiziert, können
> durch frühere Edits driften). Verwandte Handoffs: `doc/nav-weg-b-structured-trivia.md`,
> `doc/nav-pragmas-versioning-status.md`, `doc/nav-kolibri.md`.
> **Konventionen (Pflicht):** neue Dateien UTF-8 **mit BOM**; echte Umlaute (ä ö ü ß); **keine** Plan-„Step"-Verweise
> in Code/XML-Doku; **nie selbst committen** (nach jedem Step Review + Build/Tests, dann Commit-Message als Text
> liefern); Tests auf **net472 UND net10.0** grün. Bei Abschluss diese Doku + `MEMORY.md` fortschreiben.

## Warum

Präprozessor-Direktiven sind bereits **strukturierte Trivia** (Weg B): ein `DirectiveTrivia`-Stück, dessen
`GetStructure()` einen `DirectiveTriviaSyntax`-Knoten mit **lokalen** Token liefert. Das *Parsen* ist aber noch
**rohe Index-Arithmetik** über `_raw` in `NavParser.ParseDirectives()`, und dieser Vorlauf vermischt **zwei
unterschiedliche Platzierungsregeln**, die beide nicht in einen generischen Direktiv-Parser gehören:

- **lexikalisch/allgemein** — Direktive nur mit Whitespace bis Zeilenanfang (heute Parser-Diagnose **Nav3001**,
  obwohl der Lexer jedes `#` als Direktive lext);
- **semantisch/version-spezifisch** — `#pragma version` muss *ganz oben* stehen (Nav3003/Nav3004, laufende Flags).

## Fixierte Entscheidungen (nicht neu aufrollen)

1. **Eigene Klasse `NavDirectiveParser`** (nicht Region in `NavParser`), cursor-basiert, spiegelt die
   Hauptparser-Idiome.
2. **Generisch/erweiterbar**: Keyword-Dispatch-Seam als Fundament für spätere `#if`/`#region`/`#pragma warning`
   (jetzt **nicht** implementiert — nur der Seam).
3. **Lexer-Gate wie C#**: `#` beginnt eine Direktive **iff** es das erste Nicht-Whitespace-Zeichen seiner Zeile
   ist. `#` mitten im Code → `Unknown` (Nav0000), **keine** Direktive. **Nav3001 entfällt** (unerreichbar).
4. **Versions-Platzierung aus der Struktur**: **jedes `#pragma version` wird strukturell `VersionDirectiveSyntax`**;
   ein separater, version-spezifischer Schritt wählt die *wirksame* und meldet Nav3003/Nav3004. Kehrt die
   Weg-B-Entscheidung „deplatziert/doppelt = Bad" um.

## Enshrined Invarianten (in Stein)

- **Lexikalisch (Lexer):** `#` = Direktivbeginn **iff** erstes Nicht-Whitespace der Zeile (Einrückung durch
  Whitespace erlaubt), sonst `Unknown`/Nav0000. Direktiven dürfen auf **jeder** Zeile stehen.
- **Semantisch (nur `#pragma version`):** nur *ganz oben* (nur Trivia davor) wirksam; sonst Nav3003, Duplikat Nav3004.

---

## Ist-Zustand (verifiziert, mit Zeilen)

**Lexer** `Nav.Language\Syntax\NavLexer.cs`:
- Haupt-Dispatch `:131`: `if (c == '#') { ScanPreprocessor(); return; }` — **bei jedem** `#`, ohne Zeilen-Check.
- `ScanPreprocessor()` `:264-327`: fügt `HashToken`; danach ein optionales `PreprocessorKeyword` (reine Buchstaben)
  als Direktiv-Keyword (`inTextMode=true`); Rumpf in Läufen: `PreprocessorNumber` (Ziffern), `PreprocessorKeyword`
  (Wort), `PreprocessorText` (Whitespace/Satzzeichen); Ende bei `\r\n` (immer `PreprocessorNewLine`) bzw.
  1-Zeichen-Newline nur **vor** dem Keyword. Ein einzelnes `\n` **nach** dem Keyword bleibt `PreprocessorText`
  (Lauf endet dann ohne `PreprocessorNewLine`). Hilfen: `IsWhitespace`, `IsLetterCharacter`, `NewLineLength`.

**Parser** `Nav.Language\Syntax\NavParser.cs`:
- Ctor `:82-99`: `_raw = NavLexer.Lex(...)`; `_directiveRuns = ParseDirectives();` (`:92`); dann
  `_allTrivia = BuildTrivia(_raw, _directiveRuns, …)` (`:94`); `_pos=0; SkipHidden();`.
  Felder: `_sourceText` (`SourceText`), `_raw` (`ImmutableArray<RawToken>`), `_diagnostics`
  (`ImmutableArray<Diagnostic>.Builder`), `_eofPos` (`int`), `_firstSignificantStart` (`int?`).
- `ParseCodeGenerationUnit` Ende `:262-282`: `rootStart = _firstSignificantStart ?? _eofPos`; `root = new
  CodeGenerationUnitSyntax(rootExtent, …)` (`:267`); `AttachNonSignificantTokens(root)` (`:271`);
  `syntaxTree = new SyntaxTree(… diagnostics: _diagnostics.ToImmutable())` (`:273-276`);
  `root.FinalConstruct(…)` (`:278`); `FinalizeDirectives(syntaxTree, root)` (`:279`).
- `FinalizeDirectives` `:291-295`: `foreach (run in _directiveRuns) run.Node.FinalConstruct(syntaxTree, root);`.
- Cursor-Primitiven `:1638-1743`: `At0` (`_pos<_raw.Length ? _raw[_pos].Type : EndOfFile`), `At(type)`,
  `AtEof`, `PeekType(n)` (überspringt `IsHidden`), `Eat(type)→RawToken?` (Missing-Recovery), `TryEat(type, out
  RawToken)`, `SkipHidden()`, `Recover(Func<bool>)`, `CurrentStart/CurrentEnd`.
- **Direktiven-Vorlauf `:1885-2114`** (wird durch `NavDirectiveParser` ersetzt):
  - `ParseDirectives() → List<DirectiveRun>` `:1885`: Schleife über `_raw`; `SyntaxFacts.IsTrivia` → skip;
    Nicht-`HashToken`-signifikant → `atHead=false; sawCode=true`; bei `HashToken`: `end=DirectiveEnd(i)`,
    `subject=VersionSubjectIndex(i,end)`; wenn Version-förmig: `if(sawCode) Nav3003` / `else if(versionDirective!=null)
    Nav3004` / `else if(!atHead) Nav3003` / `else accept`; sonst `ReportDirectiveDiagnostics(i,end)`; `runs.Add(MakeRun(i,end,node))`;
    `atHead=false; i=end-1;`.
  - `MakeRun` `:1957`, `DirectiveEnd` `:1968` (Lauf-Ende inkl. terminierendem `PreprocessorNewLine`),
    `DirectiveExtent` `:1992` (`#`…letztes Inhalts-Token, ohne Newline), `DirectiveLocation` `:2004`
    (`_sourceText.GetLocation(DirectiveExtent(…))` — echte LineRange, **nicht** `LexicalLocation`).
  - `VersionSubjectIndex` `:2013`: `#`+1 muss `PreprocessorKeyword`=="pragma"; Subjekt = erstes
    `PreprocessorKeyword|PreprocessorNumber` danach, nur Whitespace zwischen `pragma` und Subjekt; Subjekt=="version".
  - `AcceptVersionDirective` `:2049`: Argument = Text hinter Subjekt bis Newline; `NavLanguageVersion.TryParse` →
    sonst **Nav3002** + `Default`; `new VersionDirectiveSyntax(DirectiveExtent, version)`; `PopulateDirectiveTokens`.
  - `BuildBadDirective` `:2070`, `PopulateDirectiveTokens` `:2083` (lokale Token via
    `TryClassifyNonSignificant` + `SyntaxTokenFactory.CreateToken(extent, type, classification, node, Empty, Empty)`,
    ohne terminierendes `PreprocessorNewLine`; `node.SetLocalTokens(...)`).
  - `ReportDirectiveDiagnostics` `:2104`: **Nav3001** wenn `!_sourceText.SliceFromLineStartToPosition(start).IsWhiteSpace()`,
    dann **Nav3000**.
- `AttachNonSignificantTokens` `:2127`: überspringt Trivia + `IsPreprocessorToken`; hängt Rest an Wurzel;
  nutzt `TryClassifyNonSignificant` (`:2201`, heute `private static`) auch hier (`:2155`).
- `BuildTrivia` `:2292`: faltet je `DirectiveRun` (keyed `RawStart`) ein `SyntaxTrivia(DirectiveTrivia, ContentExtent,
  Node)` + optionales `NewLine` (`:2323-2330`, `if(!NewLineExtent.IsMissing)`), überspringt die Roh-Token des Laufs.
- `IsHidden` `:2398` (u.a. alle Präprozessor-Typen), `IsPreprocessorToken` `:2421`
  (`HashToken|PreprocessorKeyword|PreprocessorText|PreprocessorNumber|PreprocessorNewLine`).
- `DirectiveRun` (private nested `readonly struct`) `:2435-2450`: `RawStart, RawEnd, ContentExtent, NewLineExtent, Node`.

**Knoten** `Nav.Language\Syntax\`:
- `DirectiveTriviaSyntax` (abstrakt): `SetLocalTokens`, `ChildTokens()`-Override, `HashToken`.
- `VersionDirectiveSyntax(TextExtent, NavLanguageVersion)` `[SampleSyntax("#pragma version 1")]`, `sealed partial`;
  Props `PragmaKeyword`, `Version`.
- `BadDirectiveTriviaSyntax(TextExtent)` `[SampleSyntax("#unknown")]`, `sealed partial`. Doku nennt Nav3003/3004 als
  Bad-Beispiele → **aktualisieren** (nur noch unbekannt = Bad).
- `[SampleSyntax]`-Typen müssen `sealed partial` sein (Visitor-Quellgenerator).

**Konsumenten** (nur über strukturierte Trivia — vom Sub-Parser-Mechanismus entkoppelt):
- `SyntaxTree.Directives()` `SyntaxTree.cs:76`: `DescendantTrivia().Where(HasStructure).Select(GetStructure).OfType<DirectiveTriviaSyntax>()`.
- `CodeGenerationUnitSyntax` `CodeGenerationUnitSyntax.cs:40/46`:
  `LanguageVersionDirective => Directives().OfType<VersionDirectiveSyntax>().FirstOrDefault();`
  `LanguageVersion => LanguageVersionDirective?.Version ?? NavLanguageVersion.Default;`.
- Einfärbung: `Nav.Language.Lsp\SemanticTokensBuilder.cs` `CollectSpans` (iteriert `Directives().ChildTokens()`),
  `Nav.Language.ExtensionShared\Classification\SyntacticClassificationTagger.cs` `GetTags`.

**`NavLanguageVersion.TryParse(string, out)`**: Trim, reine Ziffern, kein Vorzeichen (`[TestCase]` in `LanguageVersionTests`).

**Diagnosen** `Nav.Language\Diagnostic\{DiagnosticId.cs, DiagnosticDescriptors.Syntax.cs}` + `doc\Errors.md`:
`Nav3000` (unbekannt), `Nav3001` (nicht Zeilenanfang — **wird entfernt**), `Nav3002` (Versionswert),
`Nav3003` (Version nicht oben), `Nav3004` (Version doppelt). `SliceFromLineStartToPosition` in
`Nav.Language\Text\SourceTextExtensions.cs` (+ eigener `SourceTextTests`) — bleibt, nur die Direktiven-Nutzung entfällt.

**Tests/Golden** `Nav.Language.Tests\`:
- `Syntax\LanguageVersionTests.cs` — deckt Nav3000/3002/3003/3004, Default, Klassifikation, RoundTrip, `TryParse` ab.
- `SyntaxGoldenTests` (`.tokens/.tree/.diag/.trivia` + RoundTrip + `ParsesAndRoundTripsAllTypingPrefixes`;
  `UpdateGolden` `[Explicit]`), `TokenTriviaTests`, `SyntaxTreeAllRulesTests.TestAllSyntaxesPresent`
  (Knotenzahl `==48`; `BadDirectiveTriviaSyntax` via `"#unknown\r\ntask A{}"`-Schnipsel; `AllRules.nav` trägt nur
  `#pragma version 1`).
- Fixture `Syntax\Tests\PreprocessorNotAtLineStart.nav` = `task A{…}\r\n} #directive`; `.diag` heute:
  `Nav3001` + `Nav3000` über `(6,3,6,13)`.

---

## Ziel-Umsetzung

### Step 1 — `DirectiveRun` extrahieren
`DirectiveRun` aus `NavParser.cs:2435` in neue Datei `Nav.Language\Syntax\DirectiveRun.cs` (BOM) als
`internal readonly struct` (Felder/Ctor/Doku unverändert). Nur `NavParser` referenziert es (grep). **Verifikation:**
Build + net10, **null** Golden-Diff.

### Step 2 — `TryClassifyNonSignificant` teilen
Von `private static` (`NavParser.cs:2201`) zu `internal static` (in `Nav.Language\Internal\SyntaxTokenFactory.cs`
oder neue `Nav.Language\Internal\PreprocessorClassification.cs`), beide Aufrufstellen (`:2090`, `:2155`) umbiegen.
**Verifikation:** Build + net10, null Diff.

### Step 3 — Lexer-Gate + Nav3001 entfernen
- `NavLexer.cs:131`: `ScanPreprocessor()` nur, wenn Zeilen-Präfix bis `#` reiner Whitespace ist. Prüfung per
  Rückwärts-Scan auf `_text`: `for (var i=_pos-1; i>=0 && _text[i]!='\n'; i--) if (_text[i]!='\r' && !IsWhitespace(_text[i])) → kein Zeilenanfang`.
  Sonst wie bisher: `#` als **einzelnes `Unknown`-Token** anhängen und um 1 vorrücken (der bestehende
  Unerwartetes-Zeichen-/Fallback-Zweig); der Zeilenrest lext normal.
- `ReportDirectiveDiagnostics` (bzw. dessen Nachfolger im Sub-Parser) meldet **kein Nav3001** mehr. `Nav3001` aus
  `DiagnosticId.cs`, `DiagnosticDescriptors.Syntax.cs`, `doc\Errors.md` entfernen; `BadDirectiveTriviaSyntax`-Doku anpassen.
- Fixture `PreprocessorNotAtLineStart.*` regenerieren (neu: `}` , `#`=`Unknown`/Nav0000, `directive`=Identifier
  → am Top-Level übersprungen ⇒ Unerwartet-Diagnose statt Nav3001/Nav3000). Fixture ggf. umbenennen
  (`PreprocessorMidLineIsUnknown`). **Beim Golden-Review die exakten neuen `.diag`-Zeilen bestätigen.**
**Verifikation:** Build + Tests; `git diff` Golden = **nur** diese Fixture.

### Step 4 — `NavDirectiveParser` Gerüst + Cursor
Neue Datei `Nav.Language\Syntax\NavDirectiveParser.cs` (BOM), `sealed class`:
```
NavDirectiveParser(ImmutableArray<RawToken> raw, SourceText sourceText, ImmutableArray<Diagnostic>.Builder diagnostics)
List<DirectiveRun> Parse()            // Einstieg
```
Cursor über einen `#`-Lauf `[hashIndex, _end)`: `int _pos`; `SyntaxTokenType At0`; `bool At(type)`;
`bool AtRunEnd => _pos>=_end`; `RawToken Current`; `bool TryEat(type, out RawToken)`; `void SkipPreprocessorText()`.
`int RunEnd(int hashIndex)` (Logik aus `DirectiveEnd`). **Keine** Missing-/Recover-Mechanik. `Parse()` zunächst leere Liste.
**Verifikation:** Build.

### Step 5 — Generische Knotenproduktion (ohne Platzierung)
In `NavDirectiveParser` portieren: `DirectiveExtent`, `DirectiveLocation`, `MakeRun`, `PopulateLocalTokens`
(nutzt geteiltes `TryClassifyNonSignificant`), Nav3000-Meldung. Äußerer Scan über `_raw`: skip Trivia; bei
`HashToken`: `end=RunEnd(i)`; Keyword-Text = `_raw[i+1]` iff `PreprocessorKeyword` direkt hinter `#`; Dispatch:
- `"pragma"` → `ParsePragma`: Subjekt (erstes `PreprocessorKeyword|PreprocessorNumber` nach `pragma`, nur
  `PreprocessorText` dazwischen). `"version"` + Argument → **immer** `VersionDirectiveSyntax` (Nav3002 bei
  fehlend/ungültig, Rückfall `Default`). Sonst (`#pragma warning`, kein Subjekt) → `BadDirectiveTriviaSyntax` + **Nav3000**.
- `default` (`#foo`, `#`) → `BadDirectiveTriviaSyntax` + **Nav3000**.
`runs.Add(MakeRun(i, end, node)); i = end-1;`. **Kein** `atHead`/`sawCode`/`versionDirective`, **kein** Nav3003/3004.
**Verifikation:** Build.

### Step 6 — Versions-Semantik-Schritt + `CodeGenerationUnitSyntax`
Neue `NavParser`-Methode, aufgerufen in `ParseCodeGenerationUnit` **nach** der Member-Schleife (wenn
`_firstSignificantStart` endgültig) und **vor** `_diagnostics.ToImmutable()` (`:276`) / Wurzel-Ctor (`:267`):
```
VersionDirectiveSyntax ResolveLanguageVersion() {
    VersionDirectiveSyntax effective = null;
    bool sawAnyDirective = false;
    foreach (var run in _directiveRuns) {                 // _directiveRuns ist in Quelltext-Reihenfolge
        if (run.Node is VersionDirectiveSyntax v) {
            bool codeBefore = run.ContentExtent.Start >= (_firstSignificantStart ?? _eofPos);
            if (codeBefore)            _diagnostics.Add(Nav3003 @ DirectiveLocation(run));
            else if (effective != null) _diagnostics.Add(Nav3004 @ DirectiveLocation(run));
            else if (sawAnyDirective)   _diagnostics.Add(Nav3003 @ DirectiveLocation(run));
            else                        effective = v;
        }
        sawAnyDirective = true;                            // gilt für JEDE Direktive (Version wie Bad)
    }
    return effective;                                      // Nav3000 kommt bereits aus dem Sub-Parser
}
```
(Location: `_sourceText.GetLocation(run.ContentExtent)`.) `CodeGenerationUnitSyntax`-Ctor um
`VersionDirectiveSyntax languageVersionDirective` erweitern; `LanguageVersionDirective` = gespeicherter Wert
(kein `Directives().First()` mehr), `LanguageVersion = languageVersionDirective?.Version ?? Default`; Doku korrigieren.
**Verifikation:** Build.

### Step 7 — Verdrahten + alte Helfer löschen
Ctor `:92`: `_directiveRuns = new NavDirectiveParser(_raw, _sourceText, _diagnostics).Parse();`. Wurzel-Ctor
mit `ResolveLanguageVersion()` speisen. Aus `NavParser.cs` löschen: `ParseDirectives`, `MakeRun`, `DirectiveEnd`,
`DirectiveExtent`, `DirectiveLocation`, `VersionSubjectIndex`, `AcceptVersionDirective`, `BuildBadDirective`,
`PopulateDirectiveTokens`, `ReportDirectiveDiagnostics` (`:1885-2114`). **Bleiben:** `BuildTrivia`,
`FinalizeDirectives`, `IsHidden`, `IsPreprocessorToken`, `DirectiveRun` (extern). `<see cref="ParseDirectives"/>`-Doku
→ `NavDirectiveParser`. **Verifikation:** Engine-Build.

### Step 8 — Tests + Golden
- `LanguageVersionTests.cs`:
  - `DuplicatePragma_ReportsNav3004_FirstWins` (`:130`) und `DuplicatePragma_AfterMember_ReportsNav3003_NotNav3004`
    (`:148`): `OfType<VersionDirectiveSyntax>().Count()` **1 → 2**; Kommentare „wird kein eigener Knoten" streichen.
  - `DuplicatePragma_MidLineAfterCode_ReportsNav3003_SpanningWholeDirective` (`:176`): die **mittige** zweite
    `#pragma version 1` ist keine Direktive mehr (Lexer-Gate) → **kein Nav3003**; Test umschreiben (mid-line `#`
    → Nav0000, keine Direktive, `Directives()` enthält nur die erste).
  - Unverändert grün: `WellFormedPragma…`, `NoPragma…`, `LeadingTriviaBeforePragma…`, `Missing/NonInteger…Nav3002`,
    `OtherPragma…Nav3000`, `MisplacedPragma_AfterMember…Nav3003` (zeilen-getrennt), `PragmaAfterOtherDirective…Nav3003`
    (+Nav3000), `DirectiveDiagnostic_SpansWholeDirective` (zeilen-getrennt), `MisplacedPragma_StillClassifiesNumberValue`,
    Klassifikations-/RoundTrip-/`TryParse`-Tests. `LanguageVersionDirective`/`LanguageVersion` = wirksame Direktive.
- `SyntaxTreeAllRulesTests`: Knotenzahl bleibt **48** (beide Direktiv-Typen weiter erzeugt).
- `[Explicit] SyntaxGoldenTests.UpdateGolden`, dann `git diff`: erwartet **nur** `PreprocessorNotAtLineStart.*`
  (bewusst). Jeder weitere `.tokens/.tree/.trivia/.diag`-Diff = ungewollt → untersuchen.
**Verifikation:** net10 + net472 grün.

### Step 9 — Doku
`doc\nav-weg-b-structured-trivia.md`, `doc\nav-kolibri.md`, `doc\nav-pragmas-versioning-status.md` fortschreiben
(Lexer-Gate, Invarianten, Knotentyp-Umkehr, `ParseDirectives` → `NavDirectiveParser`). Diese Doku
(`doc/nav-directive-subparser.md`) auf „umgesetzt" fortschreiben; `MEMORY.md` +
`nav-weg-b-structured-trivia`-Memory aktualisieren.

---

## Verifikation (Kommandos)
- Engine: `dotnet build Nav.Language\Nav.Language.csproj`
- net10: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`
- net472: NUnit-Console-Runner (`Build\nunit.consolerunner\...\nunit3-console.exe … bin\Debug\net472\Nav.Language.Tests.dll`)
- Golden: `[Explicit] SyntaxGoldenTests.UpdateGolden` + `git diff` (nur `PreprocessorNotAtLineStart.*`)
- VS-Färbepfad: `nav build` (VSIX, net472 — validiert `ExtensionShared`/`Lsp` gegen den unveränderten Trivia-Seam)

## Risiken / Randfälle
- **Lexer-Gate:** `#` an Datei-/Zeilenanfang (Präfix leer → Direktive); eingerückt (`    #pragma …` → Direktive);
  `\r\n` im Rückwärts-Scan korrekt als Zeilengrenze; mid-line-`#` erzeugt Nav0000 **plus** ggf. Folge-Diagnose für
  den Zeilenrest — beim Golden-Review bestätigen.
- **Effektiv-Auswahl exakt** (an allen `LanguageVersionTests`-Fällen prüfen): `codeBefore`-Vergleich,
  `sawAnyDirective` für **jede** Direktive, Zweig-Reihenfolge Nav3003 vor Nav3004.
- **Ordering:** `ResolveLanguageVersion` vor `_diagnostics.ToImmutable()` und vor dem Wurzel-Ctor.
- **Round-Trip / LF-nach-Keyword / fehlendes EOF-Newline:** `RunEnd`/`MakeRun`/`DirectiveExtent` verbatim aus
  `DirectiveEnd`/`MakeRun`/`DirectiveExtent`; lokale Token ohne terminierendes `PreprocessorNewLine`; `NewLineExtent.Missing`
  am EOF (`BuildTrivia` prüft `!IsMissing`).
- **`DirectiveRun`-Umzug:** keine `NavParser.DirectiveRun`-Referenz mehr (grep).

## Nachgelagert (nicht Teil dieser Arbeit)
Auf dem Keyword-Dispatch-Seam: `#region`/`#endregion` bzw. `#if`/`#elif`/`#else`/`#endif` (Rumpf zunächst nur
lokale Token; `DirectiveStack`/Konditionalauswertung separat) — je ein switch-case + ein
`sealed partial : DirectiveTriviaSyntax`-Knoten mit `[SampleSyntax]`. Ebenfalls offen: `Nav5001` unzulässige
Version, Direktiven-Completion.
