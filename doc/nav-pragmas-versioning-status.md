# Nav-Pragmas & Sprach-Versionierung — Status & Handoff

> Dauerhafter Einstieg zum Thema Präprozessor-Direktiven (`#…`) und der darauf aufsetzenden
> **Nav-Sprach-/Schema-Version**. Ergänzt `doc/nav-kolibri.md` (Abschnitt „Präprozessor-Direktiven")
> um den konkreten Umsetzungsstand und die offenen Design-Entscheidungen. Pflichtlektüre, bevor hier
> weitergebaut wird.

## Zweck

Neue Syntax-/Codegen-Elemente sollen künftig **pro `.nav`-Datei** an eine Sprach-Generation gebunden
werden können — ohne den Bestand (~1912 Dateien, byte-identischer Codegen) zu brechen. Träger ist die
Direktive `#pragma version <N>`; Ausbleiben = Version 1 (historisches Verhalten). Dieser Schritt ist das
**Fundament** (Erkennung + Durchreichen + Gate-Mechanik) — noch **ohne** versionsabhängige Features.

## Stand — implementiert (Commit-Kandidat, Branch `feature/nav-parser`)

| Baustein | Datei |
|---|---|
| Versions-Typ (Ganzzahl, `Default`/`Latest`/`TryParse`, Vergleich) | `Nav.Language/NavLanguageVersion.cs` |
| Feature-Gate (`enum NavLanguageFeature` leer + `NavLanguageFeatures`, `Nav5000`) | `Nav.Language/NavLanguageFeature.cs` |
| Direktiv-Knoten (abstrakt) | `Nav.Language/Syntax/DirectiveTriviaSyntax.cs` |
| `#pragma version` als erster konkreter Fall | `Nav.Language/Syntax/VersionDirectiveSyntax.cs` |
| Unbekannte Direktive (nicht erkannt) | `Nav.Language/Syntax/BadDirectiveTriviaSyntax.cs` |
| Direktiv-Sub-Parser (`#`-Lauf → Knoten + lokale Token, Nav3000/Nav3002) | `Nav.Language/Syntax/NavDirectiveParser.cs` |
| Versions-Platzierung (wirksame Direktive, Nav3003/Nav3004) + `BuildTrivia` (Lauf → strukturierte `DirectiveTrivia`) | `Nav.Language/Syntax/NavParser.cs` (`ResolveLanguageVersion`, `BuildTrivia`) |
| Strukturierte Trivia (`GetStructure()`/`HasStructure`, `DirectiveTrivia`) | `Nav.Language/Syntax/SyntaxTrivia.cs`, `SyntaxTokenType.cs`, `SyntaxFacts.cs` |
| Unit-Property `LanguageVersionDirective` + `LanguageVersion` | `Nav.Language/Syntax/CodeGenerationUnitSyntax.cs` |
| `SyntaxTree.Directives()` | `Nav.Language/Syntax/SyntaxTree.cs` |
| Semantik-Durchreiche `LanguageVersion` | `Nav.Language/SemanticModel/CodeGenerationUnit.cs` |
| Codegen-Durchreiche in die Templates | `Nav.Language/CodeGen/CodeGeneratorContext.cs` (+ Aufruf in `CodeGenerator.cs`) |
| Diagnosen `Nav3002` (malformed) / `Nav3003` (nicht ganz oben) / `Nav3004` (doppelt) / `Nav5000` (Gate) | `Diagnostic/DiagnosticId.cs`, `…Descriptors.Syntax.cs`, `…Descriptors.Semantic.cs` |
| **Rumpf-Tokenisierung im Lexer** (Directive-Mode: Wort→`PreprocessorKeyword`, Ziffern→`PreprocessorNumber`, Rest→`PreprocessorText`) | `Syntax/NavLexer.cs` (`ScanPreprocessor`), neuer Typ `Syntax/SyntaxTokenType.cs` |
| **Klassifikation rein typ-getrieben** (`PreprocessorNumber`→`NumberLiteral`) | `Syntax/NavParser.cs` (`TryClassifyNonSignificant`, `IsHidden`), `Text/SyntaxTokenClassification.cs` |
| Editor-Anbindung (beide Hosts, geteilt) | LSP: `SemanticTokensBuilder.cs` (Legende `number`); VS: `Classification/ClassificationType{Names,Definitions}.cs` (`NavNumber`); VS-Code-Client-Fallback: `vscode-nav-lsp/syntaxes/nav.tmLanguage.json` |
| Tests | `Nav.Language.Tests/Syntax/LanguageVersionTests.cs`, Fixtures `Syntax/Tests/VersionPragma(.Invalid).nav` |

**Verifiziert:** net10 1146/0, net472 1146/0; Engine + CodeAnalysis/LSP/MCP/CLI bauen; Default ohne Pragma =
Version 1 ⇒ Bestand bit-identisch. Direktiven sind seit dem Weg-B-Umbau **strukturierte Trivia**
(`doc/nav-weg-b-structured-trivia.md`) — `.tokens`/`.tree`/`.trivia`-Golden entsprechend neu, `.diag`
byte-identisch, kein Korpus-`.expected.cs` verändert. (VSIX/`nav build` für den VS-Klassifizierungspfad.)

## Design-Entscheidungen (mit dem Nutzer geklärt — nicht ohne Grund umwerfen)

- **Syntax:** `#pragma version <int>` — generisches, extensibles Pragma-Muster `#pragma <name> <wert>`.
- **Schema:** monotone **Ganzzahl** (`1`, `2`, `3` …), entkoppelt von der git-abgeleiteten Assembly-Version.
- **Gate:** permissiv parsen + **semantische** Diagnose (Roslyn-Stil); ohne Pragma = Version 1.
- **Repräsentation (Weg B, umgesetzt):** Die Direktive ist **strukturierte Trivia** nach Roslyn-Vorbild —
  ein `SyntaxTokenType.DirectiveTrivia`-Stück am Folge-Token, dessen `SyntaxTrivia.GetStructure()` den
  `DirectiveTriviaSyntax`-Knoten liefert. Der Knoten hält seine Präprozessor-Token in einer **eigenen,
  lokalen** `SyntaxTokenList` (nicht im flachen `SyntaxTree.Tokens`-Strom) und ist **kein** Kindknoten der
  Wurzel mehr. **Jede** `#pragma version` → `VersionDirectiveSyntax` (wirksam ist nur die erste ganz oben,
  s.u.); jede andere, nicht erkannte Direktive → `BadDirectiveTriviaSyntax`. (Vorstufe war **Weg A** —
  Direktive als Kindknoten mit flachen Token; siehe `doc/nav-weg-b-structured-trivia.md` für den Umbau.)
- **Direktiv-Sub-Parser (`NavDirectiveParser`) statt Hand-Scan:** Der Hauptparser-Cursor sieht Präprozessor-
  Token nicht (sie sind Trivia). Der cursor-basierte `NavDirectiveParser` (`Nav.Language/Syntax/
  NavDirectiveParser.cs`) erkennt pro `#`-Lauf generisch per **Keyword-Dispatch über die Token-Art**: der
  Lexer erkennt die Direktiv-Schlüsselwörter bereits tabellengesteuert als eigene Token (`PragmaKeyword`,
  `VersionKeyword`; kein `Substring`-Vergleich mehr im Sub-Parser). `At(PragmaKeyword)` mit `VersionKeyword`-
  Subjekt und genau einem `PreprocessorNumber`-Argument (validiert via `NavLanguageVersion.TryParse`) →
  `VersionDirectiveSyntax` (`Nav3002` bei fehlend/ungültig/mehrfach), alles andere → `BadDirectiveTriviaSyntax`
  (`Nav3000`). Der **Lexer** erzwingt die Zeilenanfang-Regel (`#` nur als erstes Nicht-Whitespace der Zeile,
  sonst `Unknown`/`Nav0000`) —
  ein eigenes `Nav3001` gibt es **nicht mehr**. Die **Platzierungs-Semantik** (welche Versions-Direktive
  wirksam ist, `Nav3003`/`Nav3004`) liegt separat in `NavParser.ResolveLanguageVersion` (aus den erzeugten
  Läufen, nicht im generischen Sub-Parser). `BuildTrivia` faltet den Lauf zu einem `DirectiveTrivia`-Stück und
  hängt es als Leading-Trivia des Folge-Tokens (bzw. `EndOfFile`) an. Der `switch`-Dispatch ist der Andockpunkt
  für weitere Direktiven (z.B. `#pragma warning disable` → `case`-Zweig + `WarningDirectiveSyntax`).

## Zulässige Versionsnummern — Stand & offene Entscheidung

**Heute (bewusst minimal):**
- `NavLanguageVersion.TryParse` akzeptiert eine **reine, nicht-negative Ganzzahl** (kein Vorzeichen, keine
  `1.0`, kein Text). Fehlparse ⇒ `Nav3002`, Rückfall auf `Default` (= 1).
- Es gibt **keine** Prüfung gegen `Latest` (= 1). `#pragma version 7` parst heute **klaglos** zu Version 7,
  obwohl die Engine nur Version 1 kennt. Auch `#pragma version 0` wird akzeptiert.

**Offen — zu entscheiden, bevor die erste echte v2 kommt:**
1. **Untergrenze:** `version 0` (und alles < 1) sollte ungültig sein — Versionen beginnen bei 1.
   → Empfehlung: als malformed behandeln (`Nav3002`) **oder** eigene „unsupported"-Diagnose (s.u.).
2. **Obergrenze / unbekannte Zukunft:** Eine Datei, die eine **höhere** Version als `Latest` deklariert
   (alter Toolstand liest neue Datei), darf nicht still falsch generieren. Analogie C#: `<LangVersion>99`
   ⇒ „not a recognized language version".
   → **Empfehlung:** neue Diagnose **`Nav5001`** „Nav language version {N} is not supported by this
   toolset (latest supported: {Latest})", **semantisch** (permissiv geparst), Severity **Error**
   (Codegen-Korrektheit nicht garantierbar). Verankern in der Semantik (`CodeGenerationUnitBuilder`), nicht
   im Parser.
3. **Ort der Prüfung:** `Nav3002` = **syntaktisch** (Wert fehlt/keine Ganzzahl, im Parser). Die
   Unterstützungs-Prüfung (bekannt/zu neu/< 1) ist **semantisch** → eigener Deskriptor (`Nav5001`), damit
   die Trennung „Syntax vs. Bedeutung" sauber bleibt.
4. **`Latest` pflegen:** Beim Anheben der Sprache `NavLanguageVersion.Latest` erhöhen (derzeit `1`). Das ist
   die eine Stelle, gegen die Support-/Completion-Logik prüft.
5. **Doppel-/Platzierung — erledigt:** `#pragma version` wird an **jeder** Stelle strukturell als
   `VersionDirectiveSyntax` erkannt; wirksam ist nur die **erste** und nur, wenn ihr ausschließlich Trivia
   vorausgeht (ganz oben). Eine weiter unten stehende meldet **`Nav3003`**, eine doppelte **`Nav3004`** (das
   erste gewinnt) — beide bleiben **eigenständige `VersionDirectiveSyntax`-Knoten** (in
   `SyntaxTree.Directives()`), aber unwirksam; ihre Token normal eingefärbt. „Ganz oben" = nur Trivia davor;
   selbst eine andere Direktive davor verletzt die Regel. Die Auswahl trifft `NavParser.ResolveLanguageVersion`;
   `CodeGenerationUnitSyntax.LanguageVersionDirective` ist der so bestimmte wirksame Knoten (kein
   `Directives().First()` mehr).

## Code-Completion — Plan (noch nicht umgesetzt)

Einstieg ist der geteilte `NavCompletionService` (`Nav.Language/Completion/`), der über
`NavCompletionContext.Classify(unit, position)` → `NavCompletionContextKind` dispatcht (VS + LSP teilen ihn).
Für Pragmas sind **neue Kontext-Arten** nötig:

- **`#` am Zeilenanfang** (bzw. leere `#`-Direktive) → Vorschlag `pragma` (+ künftige Direktiven-Keywords).
- **hinter `#pragma `** → Vorschlag `version` (+ künftige Pragma-Namen wie `warning`).
- **hinter `#pragma version `** → Vorschlag der **unterstützten** Versionsnummern `1 … Latest`
  (heute nur `1`). **Das ist die kanonische Antwort auf „welche Versionsnummern sind zulässig":** die
  Completion bietet genau die von der Engine unterstützten an.

**Gotchas (wichtig):**
- `NavCompletionContext.Classify` arbeitet heute über den **signifikanten** Syntaxbaum. Direktiv-Token sind
  seit Weg B **strukturierte Trivia** (nicht mehr im flachen Strom); die fertige Direktive ist ein Knoten
  über `SyntaxTrivia.GetStructure()` (`VersionDirectiveSyntax`/`BadDirectiveTriviaSyntax`), bei unfertiger
  Eingabe nur die `DirectiveTrivia` selbst. Die Klassifikation muss also die **Präprozessor-Zeile** an der
  Position über die Trivia erkennen (`SyntaxTree.FindTrivia(position)`/`Directives()`), nicht nur den
  Token-Baum.
- Der Fallstrick aus [[nav-completion-context]] gilt: `SyntaxToken.PreviousToken()` ist parent-lokal →
  globalen Token-Strom + Binärsuche nutzen.
- Beim Tippen ist die Zeile oft unvollständig (`#pragma versi`); Completion muss auf Präfixen robust sein.

## Weitere offene Punkte / Roadmap

- **Erstes echtes v2-Feature einziehen:** Wert in `NavLanguageFeature` ergänzen, Mindestversion in
  `NavLanguageFeatures.RequiredVersion` eintragen, `ReportIfUnavailable(...)` im Semantik-Lauf aufrufen
  (Parser bleibt permissiv). `Nav5000` wird damit erstmals real ausgelöst.
- **Editor-Klassifikation verfeinern — erledigt (C#-treu, im Lexer):** `#`, `pragma` **und** `version` sind
  `PreprocessorKeyword`, die Versionszahl ist ein numerisches Literal (`TextClassification.NumberLiteral`).
  Umsetzung wie Roslyns Directive-Mode: das `#` schaltet `ScanPreprocessor` in den Präprozessor-Modus, der den
  Rumpf **in ganzen Läufen** ausgibt — Wort→`PreprocessorKeyword`, reine Ziffern→neuer `PreprocessorNumber`,
  Rest (Zwischenraum/Satzzeichen)→`PreprocessorText`. Die **Klassifikation** folgt dann **allein aus dem
  Token-Typ** (`TryClassifyNonSignificant`: `PreprocessorNumber`→`NumberLiteral`) — **keine** direktiven-
  spezifische Färbe-Logik (kein Extent-Abgleich). Erkennung + Diagnosen der Direktiven liegen dagegen im
  Direktiv-Sub-Parser, der Färbung unberührt lässt (die Direktiv-Token-Spans emittieren die Host-Färber aus
  der strukturierten Trivia). Der ungültige Versionswert (`Nav3002`) ist ein Wort, also
  Keyword-, nicht Zahl-gefärbt. Beide Hosts ziehen automatisch nach (LSP-Token-Typ `number`; VS `NavNumber` auf
  Basis C#-`NumericLiteral`); der VS-Code-Client hat zusätzlich statisches TextMate-Fallback für `#pragma version <N>`.
  Nebeneffekt (gewollt): der Rumpf **jeder** `#`-Direktive ist jetzt sauber tokenisiert (z.B. `#if DEBUG` → `DEBUG`
  ein Wort-Token statt Einzelzeichen).
- **`#pragma warning disable <NavXXXX>` (heißer Kandidat):** dank Direktiv-Sub-Parser jetzt eine kleine
  Ergänzung — ein `WarningDirectiveSyntax` (analog `VersionDirectiveSyntax`), im Sub-Parser-Dispatch auf das
  Subjekt `warning` erkannt, plus eine **Diagnose-Filterschicht**, die gemeldete Diagnostics gegen die
  aktiven Suppressions siebt. Datei-weite Suppression (am Kopf) passt ins bestehende Modell; echte
  Regionen-Semantik (`disable … restore` mitten im Code) ist mit dem Weg-B-Trivia-Modell (Direktive kann an
  **jedem** Token hängen) jetzt strukturell möglich, aber weiterhin ungebaut (Placement-Regel bewusst „nur
  oben").
- **QuickInfo/Hover** auf der Direktive; **Code-Fix** zu `Nav3002`/`Nav3003`/`Nav5001` (gültige/`Latest`-Version
  einsetzen bzw. Direktive an den Dateikopf verschieben).
- **Lexer-LF-Fallstrick:** Direktiven terminieren im Textmodus **nur bei `\r\n`** (Alt-Grammatik; einzelnes
  `\n` bleibt `PreprocessorText` und verschluckt den Rest bis `\r\n`/EOF). Gilt für **alle** `#`-Direktiven.
  `.nav` sind CRLF, daher praktisch maskiert. Option: Lexer so ändern, dass auch einzelnes `\n` terminiert
  (macht LF-Dateien robust, ist aber eine bewusste Abkehr vom Alt-Verhalten → Golden prüfen).
- **Generische Direktiven** (`#region`, `#if`): das Weg-B-Fundament (strukturierte Trivia via
  `SyntaxTrivia.GetStructure()`, positions-basierte `SyntaxTokenList`-Konsumenten reconnectet) **steht**.
  Offen ist nur noch, die Placement-Regel zu lockern (Direktiven **überall** zulassen) und je Direktive einen
  eigenen Sub-Parser-Zweig samt Knotentyp zu ergänzen.
- **LSP/MCP:** `nav_diagnostics`/Push liefern `Nav3002`/`Nav5000`/(`Nav5001`) automatisch. Optional
  `LanguageVersion` in `nav_outline`/`nav_workspace` ausweisen.

## Fallstricke (bereits gelernt)

- **CRLF-Pflicht** für `#pragma`-Zeilen in Fixtures/Tests (s.o. LF-Fallstrick). Test-Strings mit `\r\n`.
- **Encoding:** `.nav`-Bestand ist teils **Windows-1252** (Einzelbyte-Umlaute); niemals blind über
  UTF-8-`ReadAllText`/`WriteAllText` umschreiben (macht Umlaute zu permanentem U+FFFD). Immer UTF-8 **mit
  BOM**. Siehe [[nav-utf8-bom-discipline]].
- **`[SampleSyntax]`-Knoten müssen `partial` sein** — der `SyntaxVisitorWalkerGenerator` erzeugt ihre
  `Accept`/`Walk`-Hälfte. Neue konkrete `*Syntax`-Typen werden automatisch in `AllRules.nav`-Abdeckungstests
  (`TestAllSyntaxesPresent`, `WalkReachesEveryNodeType`) verlangt → Beispiel in `AllRules.nav` aufnehmen und
  den Typ-Zähler pflegen.

## Verifikation (Wiederholrezept)

- Engine: `dotnet build Nav.Language\Nav.Language.csproj`.
- Tests: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` **und** net472 via
  NUnit-Console (`Build\nunit.consolerunner\3.8.0\tools\nunit3-console.exe … bin\Debug\Nav.Language.Tests.dll`).
- Golden neu: `[Explicit] SyntaxGoldenTests.UpdateGolden`, danach `git status` — es dürfen **nur** neue
  Dateien erscheinen, kein Bestands-Golden sich ändern.
