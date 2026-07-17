# Nav-Pragmas & Sprach-Versionierung — Status & Handoff

> Dauerhafter Einstieg zum Thema Präprozessor-Direktiven (`#…`) und der darauf aufsetzenden
> **Nav-Sprach-/Schema-Version**. Ergänzt `doc/nav-kolibri.md` (Abschnitt „Präprozessor-Direktiven")
> um den konkreten Umsetzungsstand und die offenen Design-Entscheidungen. Pflichtlektüre, bevor hier
> weitergebaut wird.

## Zweck

Neue Syntax-/Codegen-Elemente sollen künftig **pro `.nav`-Datei** an eine Sprach-Generation gebunden
werden können — ohne den Bestand (~1912 Dateien, byte-identischer Codegen) zu brechen. Träger ist die
Direktive `#version <N>`; Ausbleiben = Version 1 (historisches Verhalten). Dieser Schritt ist das
**Fundament** (Erkennung + Durchreichen + Gate-Mechanik) — noch **ohne** versionsabhängige Features.

## Stand — implementiert (Commit-Kandidat, Branch `feature/nav-parser`)

| Baustein | Datei |
|---|---|
| Versions-Typ (Ganzzahl, `Default`/`Latest`/`TryParse`, Vergleich) | `Nav.Language/NavLanguageVersion.cs` |
| Feature-Gate (`enum NavLanguageFeature` leer + `NavLanguageFeatures`, `Nav5000`) | `Nav.Language/NavLanguageFeature.cs` |
| Direktiv-Knoten (abstrakt) | `Nav.Language/Syntax/DirectiveTriviaSyntax.cs` |
| `#version` als erster konkreter Fall | `Nav.Language/Syntax/VersionDirectiveSyntax.cs` |
| Unbekannte Direktive / unbekanntes Pragma (nicht erkannt) | `Nav.Language/Syntax/BadDirectiveTriviaSyntax.cs` |
| Direktiv-Sub-Parser (`#`-Lauf → Knoten + lokale Token, Nav3000/Nav3001/Nav3002) | `Nav.Language/Syntax/NavDirectiveParser.cs` |
| Versions-Platzierung (wirksame Direktive, Nav3003/Nav3004) + `BuildTrivia` (Lauf → strukturierte `DirectiveTrivia`) | `Nav.Language/Syntax/NavParser.cs` (`ResolveLanguageVersion`, `BuildTrivia`) |
| Strukturierte Trivia (`GetStructure()`/`HasStructure`, `DirectiveTrivia`) | `Nav.Language/Syntax/SyntaxTrivia.cs`, `SyntaxTokenType.cs`, `SyntaxFacts.cs` |
| Unit-Property `LanguageVersionDirective` + `LanguageVersion` | `Nav.Language/Syntax/CodeGenerationUnitSyntax.cs` |
| `SyntaxTree.Directives()` | `Nav.Language/Syntax/SyntaxTree.cs` |
| Semantik-Durchreiche `LanguageVersion` | `Nav.Language/SemanticModel/CodeGenerationUnit.cs` |
| Codegen-Durchreiche in die Templates | `Nav.Language/CodeGen/CodeGeneratorContext.cs` (+ Aufruf in `CodeGenerator.cs`) |
| Diagnosen `Nav3001` (unbekanntes Pragma) / `Nav3002` (malformed) / `Nav3003` (nicht ganz oben) / `Nav3004` (doppelt) / `Nav5000` (Gate) | `Diagnostic/DiagnosticId.cs`, `…Descriptors.Syntax.cs`, `…Descriptors.Semantic.cs` |
| **Rumpf-Tokenisierung im Lexer** (Directive-Mode: Wort→`PreprocessorKeyword`, Ziffern→`PreprocessorNumber`, Rest→`PreprocessorText`) | `Syntax/NavLexer.cs` (`ScanPreprocessor`), neuer Typ `Syntax/SyntaxTokenType.cs` |
| **Klassifikation rein typ-getrieben** (`PreprocessorNumber`→`NumberLiteral`) | `Syntax/NavParser.cs` (`TryClassifyNonSignificant`, `IsHidden`), `Text/SyntaxTokenClassification.cs` |
| Editor-Anbindung (beide Hosts, geteilt) | LSP: `SemanticTokensBuilder.cs` (Legende `number`); VS: `Classification/ClassificationType{Names,Definitions}.cs` (`NavNumber`); VS-Code-Client-Fallback: `vscode-nav-lsp/syntaxes/nav.tmLanguage.json` |
| Tests | `Nav.Language.Tests/Syntax/LanguageVersionTests.cs`, Fixtures `Syntax/Tests/VersionDirective(Invalid).nav` |

**Verifiziert:** net10 1155/0, net472 1163/0; Engine + CodeAnalysis/LSP/MCP/CLI bauen; Default ohne Direktive =
Version 1 ⇒ Bestand bit-identisch. Direktiven sind seit dem Weg-B-Umbau **strukturierte Trivia**
(`doc/nav-weg-b-structured-trivia.md`) — `.tokens`/`.tree`/`.trivia`-Golden entsprechend neu, `.diag`
byte-identisch, kein Korpus-`.expected.cs` verändert. (VSIX/`nav build` für den VS-Klassifizierungspfad.)

## Design-Entscheidungen (mit dem Nutzer geklärt — nicht ohne Grund umwerfen)

- **Syntax:** `#version <int>` — eine eigene Direktive (kein Pragma-Subjekt). Das `#pragma`-Muster
  `#pragma <name> <wert>` bleibt als Erweiterungspunkt erhalten, hat aber **keine** bekannten Subjekte mehr:
  jedes `#pragma …` (auch `#pragma version`) meldet `Nav3001` („Unknown pragma"). Vorgeschichte: bis zur
  Vereinfachung war die Versionsdirektive `#pragma version <int>`.
- **Schema:** monotone **Ganzzahl** (`1`, `2`, `3` …), entkoppelt von der git-abgeleiteten Assembly-Version.
- **Gate:** permissiv parsen + **semantische** Diagnose (Roslyn-Stil); ohne Pragma = Version 1.
- **Repräsentation (Weg B, umgesetzt):** Die Direktive ist **strukturierte Trivia** nach Roslyn-Vorbild —
  ein `SyntaxTokenType.DirectiveTrivia`-Stück am Folge-Token, dessen `SyntaxTrivia.GetStructure()` den
  `DirectiveTriviaSyntax`-Knoten liefert. Der Knoten hält seine Präprozessor-Token in einer **eigenen,
  lokalen** `SyntaxTokenList` (nicht im flachen `SyntaxTree.Tokens`-Strom) und ist **kein** Kindknoten der
  Wurzel mehr. **Jede** `#version` → `VersionDirectiveSyntax` (wirksam ist nur die erste ganz oben,
  s.u.); jede andere, nicht erkannte Direktive → `BadDirectiveTriviaSyntax`. (Vorstufe war **Weg A** —
  Direktive als Kindknoten mit flachen Token; siehe `doc/nav-weg-b-structured-trivia.md` für den Umbau.)
- **Direktiv-Sub-Parser (`NavDirectiveParser`) statt Hand-Scan:** Der Hauptparser-Cursor sieht Präprozessor-
  Token nicht (sie sind Trivia). Der cursor-basierte `NavDirectiveParser` (`Nav.Language/Syntax/
  NavDirectiveParser.cs`) erkennt pro `#`-Lauf generisch per **Keyword-Dispatch über die Token-Art**: der
  Lexer erkennt die Direktiv-Schlüsselwörter bereits tabellengesteuert als eigene Token (`PragmaKeyword`,
  `VersionKeyword`; kein `Substring`-Vergleich mehr im Sub-Parser). `At(VersionKeyword)` unmittelbar hinter dem
  `#` mit genau einem `PreprocessorNumber`-Argument (validiert via `NavLanguageVersion.TryParse`) →
  `VersionDirectiveSyntax` (`Nav3002` bei fehlend/ungültig/mehrfach). `At(PragmaKeyword)` → `ParsePragma`: es
  gibt keine bekannten Pragmas mehr, ein Subjekt hinter `pragma` meldet `Nav3001` („Unknown pragma"), ein
  `#pragma` ohne Subjekt `Nav3000`; jedes unbekannte Direktiv-Schlüsselwort ebenfalls `Nav3000`. Alle drei
  Nicht-Versions-Fälle ergeben eine `BadDirectiveTriviaSyntax`. Der **Lexer** erzwingt die Zeilenanfang-Regel
  (`#` nur als erstes Nicht-Whitespace der Zeile, sonst `Unknown`/`Nav0000`). Die **Platzierungs-Semantik**
  (welche Versions-Direktive wirksam ist, `Nav3003`/`Nav3004`) liegt separat in
  `NavParser.ResolveLanguageVersion` (aus den erzeugten Läufen, nicht im generischen Sub-Parser). `BuildTrivia`
  faltet den Lauf zu einem `DirectiveTrivia`-Stück und hängt es als Leading-Trivia des Folge-Tokens (bzw.
  `EndOfFile`) an. Der Keyword-Dispatch ist der Andockpunkt für weitere Direktiven; der `#pragma`-Zweig ist der
  Andockpunkt für spätere Pragma-Subjekte (z.B. `#pragma warning disable` → `WarningDirectiveSyntax`).

## Zulässige Versionsnummern — Stand (umgesetzt)

**Zentrale Autorität:** `NavLanguageVersion` führt die unterstützten Versionen als benannte Konstanten
(`Version1` …) und in `SupportedVersions` (aufsteigend) — es gibt **keine** „magischen" Versionszahlen im
übrigen Code. Daraus abgeleitet: `Default` (= `Version1`), `Latest` (= letzte in `SupportedVersions`) und die
Instanz-Prüfung `IsSupported` (Mitgliedschaft in `SupportedVersions`). Eine neue Version freizuschalten heißt:
eine weitere `VersionN`-Konstante anlegen und in `SupportedVersions` aufnehmen — die eine Stelle, gegen die
Support-/Completion-Logik prüft.

> **net472-Fallstrick (verankert im Code):** `SupportedVersions` darf **kein** statisches Feld vom Typ
> `ImmutableArray<NavLanguageVersion>` **in der Struktur selbst** sein — ein Wertetyp mit einem statischen Feld
> einer Generic-Instanz über sich selbst lädt der .NET-Framework-Typlader nicht (`TypeLoadException`, empirisch
> auf net472 verifiziert; .NET 10 ist toleranter). Die Liste liegt daher in einer separaten (Referenz-)Klasse
> `SupportedVersionTable`; die öffentliche API bleibt unverändert. Ein statisches Feld des eigenen Struct-Typs
> (`Version1`) ist dagegen unkritisch (wie `TimeSpan.Zero`).

**Syntaktisch (`Nav3002`, im Parser):** `NavLanguageVersion.TryParse` akzeptiert eine reine, nicht-negative
Ganzzahl (kein Vorzeichen, keine `1.0`, kein Text). Fehlt der Wert oder ist er keine Ganzzahl ⇒ `Nav3002`,
Rückfall auf `Default`. Das ist die reine Token-Form (siehe `NavDirectiveParser.ParseVersion`).

**Semantisch (`Nav5001`, im Analyzer):** Eine syntaktisch wohlgeformte, aber **nicht unterstützte** Version
(`#version 99`, ebenso `#version 0`) meldet `Nav5001NavLanguageVersionNotSupported` (Kategorie
`Semantic`, Severity `Error`) — parallel zum Feature-Gate `Nav5000`. Der Parser bleibt bewusst permissiv (er
kennt stets die volle Syntax); ob die Engine die Version **kennt**, ist eine reine Bedeutungsfrage. Geprüft wird
nur die **wirksame** `LanguageVersionDirective` (eine deplatzierte Direktive ist bereits `Nav3003`, bekommt kein
zusätzliches `Nav5001`). So bleibt „Syntax vs. Bedeutung" sauber getrennt, und `version 0`/`version < 1` fällt
ohne Sonderregel unter dieselbe Mitgliedschaftsprüfung (nicht in `SupportedVersions` ⇒ `Nav5001`).

**Erledigt — Doppel-/Platzierung:** `#version` wird an **jeder** Stelle strukturell als
   `VersionDirectiveSyntax` erkannt; wirksam ist nur die **erste** und nur, wenn ihr ausschließlich Trivia
   vorausgeht (ganz oben). Eine weiter unten stehende meldet **`Nav3003`**, eine doppelte **`Nav3004`** (das
   erste gewinnt) — beide bleiben **eigenständige `VersionDirectiveSyntax`-Knoten** (in
   `SyntaxTree.Directives()`), aber unwirksam; ihre Token normal eingefärbt. „Ganz oben" = nur Trivia davor;
   selbst eine andere Direktive davor verletzt die Regel. Die Auswahl trifft `NavParser.ResolveLanguageVersion`;
   `CodeGenerationUnitSyntax.LanguageVersionDirective` ist der so bestimmte wirksame Knoten (kein
   `Directives().First()` mehr).
5. **Doppel-/Platzierung — erledigt:** `#version` wird an **jeder** Stelle strukturell als
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

- **`#` am Zeilenanfang** (bzw. leere `#`-Direktive) → Vorschlag `version` (+ `pragma` als Erweiterungspunkt
  und künftige Direktiven-Keywords).
- **hinter `#version `** → Vorschlag der **unterstützten** Versionsnummern `1 … Latest`
  (heute nur `1`). **Das ist die kanonische Antwort auf „welche Versionsnummern sind zulässig":** die
  Completion bietet genau die von der Engine unterstützten an.
- **hinter `#pragma `** → derzeit keine Vorschläge (keine bekannten Pragmas); Andockpunkt für künftige
  Pragma-Namen wie `warning`.

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

- **Erstes echtes v2-Feature einziehen — erledigt (V2-Codegen S4a):** Die Features `Continuation`
  (`o-^`/`--^`) und `ChoiceParameters` (`choice X [params …]`) sind in `NavLanguageFeature` samt
  Mindestversion in `NavLanguageFeatures.RequiredVersion` eingetragen; das Gate feuert real über den
  auto-discoverten Analyzer `Nav5000FeatureRequiresNavLanguageVersion` (der die Diagnose per privatem
  `Gate(...)` je Continuation-Kante/Choice-`[params]`-Klausel **yieldet** — die Analyzer-Pipeline reicht
  keinen Diagnose-Builder herein, daher return- statt append-Form). Der Parser bleibt permissiv.
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
  Basis C#-`NumericLiteral`); der VS-Code-Client hat zusätzlich statisches TextMate-Fallback für `#version <N>`.
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
- **Lexer-LF-Fallstrick — erledigt:** Früher terminierten Direktiven im Textmodus (hinter dem Keyword) **nur
  bei `\r\n`**; ein einzelnes `\n` blieb Rumpf und verschluckte den Rest bis `\r\n`/EOF. `ScanPreprocessor`
  beendet jetzt bei **jedem** Zeilenende (Längstmatch `\r\n`, sonst Einzelzeichen: LF, lone CR, NEL, LS, PS) —
  reine LF-Dateien terminieren zeilengenau, für **alle** `#`-Direktiven. Die frühere `inTextMode`-Sonderfall-
  Unterscheidung entfiel dabei (Vereinfachung). `.nav` sind CRLF, waren also nie real betroffen; die Änderung
  ist verhaltensneutral für CRLF (Golden unverändert). Tests: `LanguageVersionTests.LfTerminatedDirective_*`.
- **Generische Direktiven** (`#region`, `#if`): das Weg-B-Fundament (strukturierte Trivia via
  `SyntaxTrivia.GetStructure()`, positions-basierte `SyntaxTokenList`-Konsumenten reconnectet) **steht**.
  Offen ist nur noch, die Placement-Regel zu lockern (Direktiven **überall** zulassen) und je Direktive einen
  eigenen Sub-Parser-Zweig samt Knotentyp zu ergänzen.
- **LSP/MCP:** `nav_diagnostics`/Push liefern `Nav3002`/`Nav5000`/(`Nav5001`) automatisch. `nav_outline`
  weist die effektive Sprachversion aus (`languageVersion` + `hasVersionDirective`); dasselbe für
  `nav_workspace` bleibt optional offen (Parse-Kosten je Datei vs. Paging-/Token-Budget).

## Fallstricke (bereits gelernt)

- **CRLF/LF für `#`-Direktiv-Zeilen:** seit dem LF-Fix (s.o.) terminiert auch einzelnes `\n` die Direktive —
  Test-Strings dürfen LF verwenden. Bestandsfixtures bleiben CRLF; neue Tests können bewusst LF setzen, um die
  Terminierung zu prüfen.
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
