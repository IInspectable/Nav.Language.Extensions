# Test-Abdeckung vor dem Parser-Austausch — Step-für-Step-Plan

> **Zweck dieses Dokuments:** Bevor der ANTLR-basierte Lexer/Parser durch einen handgeschriebenen
> ersetzt wird (siehe [`nav-handwritten-parser.md`](./nav-handwritten-parser.md)), soll die
> **Test-Abdeckung der Syntax-Schicht maximiert** werden. Ziel ist **nicht** „mehr Tests" allgemein,
> sondern ein **differentielles Sicherheitsnetz**, das den exakt beobachtbaren Output des *heutigen*
> Parsers einfriert, damit der neue Parser Byte für Byte dagegen diffbar ist.
>
> Jeder Step ist so beschrieben, dass er in einer **eigenen Session** ohne Vorwissen abgearbeitet
> werden kann. Vor jedem Step zuerst [„Gemeinsamer Kontext"](#gemeinsamer-kontext) lesen.

---

## Leitidee (in einem Satz)

Der ANTLR-Parser ist die **Golden Reference**: Wir frieren seinen vollständigen Syntax-Output
(Token-Strom, Knotenbaum, Diagnostics, Round-Trip) über einen Korpus als Snapshot ein — beabsichtigte
spätere Verhaltensänderungen erscheinen dann als **reviewbare Diffs** an den Golden-Dateien.

---

## Gemeinsamer Kontext

### Wo die Sprach-Engine und ihre Tests liegen

- Engine-Kern: `Nav.Language\` (Assembly `Pharmatechnik.Nav.Language`, `netstandard2.0`).
  Pipeline: `Syntax/` (heute ANTLR) → `SemanticModel/` → `Generator/`.
- Tests: `Nav.Language.Tests\` (**multi-target `net472;net10.0`** — neue Tests müssen auf **beiden**
  TFMs grün sein). Framework ist **NUnit**.
- Test-Daten: `Nav.Language.Tests\Resources\` (eingebettet, u.a. `Resources.AllRules` — eine `.nav`,
  die jede Grammatikregel mindestens einmal nutzt), `Regression\Tests\` (echte `.nav` + `.expected.cs`),
  `Diagnostics\Tests\` (`.nav` mit Inline-Diagnose-Annotationen).

### Tests ausführen

- **net472:** `n test` (gebündelter NUnit-Console-Runner).
- **.NET 10:** `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`.
- Beim Hinzufügen neuer Golden-Dateien als eingebettete Ressourcen / Test-Daten daran denken, dass
  sie via Copy-to-Output bzw. `TestDataDirectory.Resolve(...)` gefunden werden müssen (siehe wie
  `RegressionTests`/`DiagnosticTests` das machen).

### Die zentralen Einstiegspunkte zum Parsen (Vorlage)

- `SyntaxTree.ParseText(string text, string filePath = null, …)` → ganze Datei.
- `Syntax.ParseXxx(string text, …)` (generiert, `Syntax.Generated.cs`) → **jede einzelne Grammatik-
  Regel** als eigener Einstiegspunkt (z.B. `Syntax.ParseTransitionDefinition`, `Syntax.ParseCodeType`).
  Diese Methoden bleiben auch nach dem Parser-Tausch bestehen → ideal für regelgranulare Tests.
- Ergebnis: `SyntaxTree { SyntaxNode Root; SyntaxTokenList Tokens; ImmutableArray<Diagnostic> Diagnostics; SourceText SourceText; }`.
- `SyntaxToken { TextExtent Extent; SyntaxTokenType Type; TextClassification Classification; bool IsMissing; SyntaxNode Parent; }` — `ToString()` liefert den Quelltext-Ausschnitt.
- `SyntaxTokenList` ist eine **flache, sortierte Liste *aller* Token inkl. Trivia** (Whitespace,
  NewLine, Kommentare) — nicht Roslyns angehängte Leading/Trailing-Trivia. Wichtig fürs Snapshotting.

### Was schon existiert (NICHT neu bauen, nur kennen)

| Datei | Deckt ab |
|---|---|
| `Generated Tests\SyntaxTest.cs` | Jede Regel parst ihr `SampleSyntax` → **0 Diagnostics, kein Missing-Token** (Positiv-Smoke je Regel). |
| `Generated Tests\ParseEmptyStringTests.cs` | Jede Regel auf `""` → alle erwarteten Token `IsMissing` (Missing-Token-Verhalten je Regel). |
| `SyntaxTreeAllRulesTests.cs` | Tiefe Struktur-Assertions über `Resources.AllRules` (Typen, Extents, Classifications, Verschachtelung). `TestAllSyntaxesPresent` prüft, dass jede Knotenklasse vorkommt. |
| `SyntaxStressTests.cs` | Inkrementelles Tippen (Präfix-Kürzung), Random-Shuffle → keine Null-Children, keine Crashes. |
| `SyntaxNodeTriviaTests.cs` | Trivia-Verhalten — in Step 4 **erschöpfend** ausgebaut (siehe „Stand"-Tabelle), TODO aufgelöst. |
| `SyntaxErrorTests.cs` | Nur „`AllRules` → 0 Diagnostics" — **dünn**. |
| `Diagnostics\DiagnosticTests.cs` | `.nav`-Korpus mit Inline-Annotation `// ^----^ NavId` + `FixTests`-Golden-Update. Geht über das **semantische** Modell (`CodeGenerationUnit`). |
| `Regression\RegressionTests.cs` | End-to-End-Golden der generierten `.cs` (`.expected.cs`). |
| `SyntaxTokenTests.cs`, `SyntaxFactsTest.cs`, `SyntaxTreeNavigationTests.cs`, `DescendantNodesTests.cs`, `ExtentTests.cs` | Token-Modell, Navigation, Extents. |

### Stand: Steps 1–5 sind erledigt (das Sicherheitsnetz steht)

| Artefakt | Inhalt / wiederzuverwenden |
|---|---|
| `Syntax\Tests\*.nav` | **Kuratierter Korpus** (valider Voll-Feature-Fall + Edge/Torture: halbe Transition, fehlendes Target/Semikolon/Brace, kaputte Generics, Unexpected Char, Präprozessor — am Zeilenanfang (Nav3000) **und** nicht am Zeilenanfang (Nav3001) —, leerer Block, nur Whitespace/Kommentar, leere Datei, vorzeitiges EOF). Step 5 hat den Korpus bewusst **nicht** erweitert — die Unicode-/Lex-Kanten liegen inline in `SyntaxLexerTests` (git würde LF/CR in einer `.nav` normalisieren). Neue `.nav` hier ⇒ `UpdateGolden` erzeugt alle drei Golden automatisch; bestehende nicht neu erfinden. |
| `Syntax\SyntaxGoldenTests.cs` | Golden-Fixture mit **drei Strängen**: `DumpTokens`→`.tokens`, `DumpTree`→`.tree`, `DumpDiagnostics`→`.diag`; dazu Full-Fidelity-Round-Trip und Struktur-Invarianten (Parent-Zuordnung, Kind-in-Parent-Extent, überlappungsfreie Geschwister). Ein gemeinsamer `[Explicit] UpdateGolden` schreibt alle drei; `GetCorpusFiles()` (TestCaseSource über `Syntax\Tests`), `Normalize()` (EOL-tolerant). |
| `Syntax\Tests\*.nav.tokens` / `*.nav.tree` / `*.nav.diag` | Die drei Golden je `.nav`. `.diag` = reine **Syntax**-Diagnostics (direkt aus `SyntaxTree.Diagnostics`, ohne semantisches Modell, im `UnitTestDiagnosticFormatter`-Format); leere `.diag` pinnt „keine Syntaxfehler". |
| `Syntax\SyntaxNewLineTests.cs` | Inline-NL-Tests (`\n`/`\r`/`\r\n` + NEL/LS/PS = `U+0085`/`U+2028`/`U+2029`), NL-Sequenzen als `(char)0x…`-Casts. Pinnt u.a. die CRLF-Asymmetrie im SingleLineComment-Split. Im selben Inline-Stil ergänzt `SyntaxLexerTests` die übrigen Unicode-/Lex-Kanten — **nicht** datei-basiert (git würde LF/CR in `.nav` sonst zu CRLF normalisieren). |
| `Syntax\SyntaxLexerTests.cs` | **Lexer-Direkt-Golden + Unicode** (Step 5): parametrisierte Inline-Fälle über `SyntaxTree.ParseText(...).Tokens` — exakte Token-Sequenz + Round-Trip je Fall. Deckt Umlaut-/Punkt-/Ziffern-Identifier, Keyword-Längstmatch (`task` vs. `tasks`), Kanten-Keywords `o->`/`-->`/`==>` gegen ihre Präfixe (`-`/`=`/`->`/`==` → `Unknown`/`GreaterThan`), Nicht-Nav-Letter (`é` → `Unknown`), Zs-Whitespace + Tab/VT/FF, String-Literale mit/ohne Abschluss (inkl. NEL-Asymmetrie: U+0085 bleibt im Literal) und entartete Eingaben. NL-Varianten bewusst nur in `SyntaxNewLineTests`. |
| `SyntaxNodeTriviaTests.cs` | **Erschöpfende Trivia-Tests**: Leading/Trailing-Extents (inkl. Dateiränder + `onlyWhiteSpace`), Trivia-Parent = Root, SingleLineComment-/EOL-Split, mehrzeiliger Kommentar, führendes BOM, Unicode-Zs-Whitespace. Das alte `// TODO Weitere Tests für Trivias` ist aufgelöst. |
| `Syntax\Tests\.gitattributes` | `*.nav` / `*.tokens` / `*.tree` / `*.diag` je `-text` → friert Zeilenenden ein (Working-Tree-Bytes == committete Bytes), damit die **absoluten Offsets** der Golden checkout-stabil bleiben. **Neue Golden-Endung ⇒ hier `-text` ergänzen.** |
| `Nav.Language.Tests.csproj` | Korpus + Golden via vier Einträgen `<Content Include="Syntax\Tests\**\*.nav` … `*.tokens` … `*.tree` … `*.diag" />` registriert. **Neue Golden-Endung ⇒ hier Eintrag ergänzen.** |

**Verbindliche Konventionen (Steps 1–5):** Golden-Offsets sind absolut → Korpus-`.nav` bleiben CRLF und per `-text` eingefroren; Golden-Vergleiche laufen über `Normalize()`; neue `.cs`/`.nav` als UTF-8 **mit BOM**; Tests müssen auf **net472 und net10.0** grün sein; Goldens werden ausschließlich über den `[Explicit]`-Update-Test erzeugt (nie von Hand editiert); **keine literalen Sonderzeichen im Code** (BOM/Zs/NL als `\u….`-Escapes bzw. `(char)0x…`-Casts); **keine „Step"-Verweise in Code-/XML-Doku** (siehe `CLAUDE.md`).

### Bewährte Muster zum Wiederverwenden

- **Golden-Update-Schalter:** `RegressionTests.GenerateFiles` (`[Test, Explicit]`) und
  `DiagnosticTests.FixTests` zeigen das Muster „ein `Explicit`-Test schreibt die Golden-Dateien neu".
  Jede neue Golden-Harness bekommt **denselben** Mechanismus.
- **File-Test-Cases:** `[Test, TestCaseSource(nameof(GetTestCases))]` + `Directory.EnumerateFiles(..., "*.nav")`
  (siehe `DiagnosticTests`/`RegressionTests`) → ein Test pro Korpus-Datei, gut sichtbar im Runner.
- **Inline-Diagnose-Annotation:** `DiagnosticTests.ParseDiagnostics` liest `// ^---^ NavId`-Zeilen aus
  der `.nav` selbst; `UnitTestDiagnosticFormatter` formatiert Ist-Diagnostics ins selbe Format.

### Wichtige Konventionen

- **Echte Umlaute** überall (ä, ö, ü, ß).
- **Niemals selbst committen** — nach jedem Step Code-Review + Build/Test, dann fertige
  Commit-Message als Text liefern.
- Golden-Dateien sind Teil des Reviews: bei beabsichtigten Verhaltensänderungen werden ihre Diffs
  bewusst geprüft und mit-committed.

---

## Die Lücken, die dieser Plan schließt

1. **Kein Golden-Master des vollständigen Token-Stroms** — heute nur punktuelle „dieses Token hat Typ X".
2. **Kein Round-Trip / Full-Fidelity-Test** — niemand prüft, dass die Token den Quelltext lückenlos abdecken.
3. **Syntax-Diagnostics quasi ungepinnt** — Recovery-Fälle (missing edge/target, `Nav0000`, Präprozessor) fehlen.
4. **Trivia nur rudimentär** — genau der Bereich, der sich ändern soll.
5. **Keine Lexer-Direkt-Tests** — alles läuft über den Parser; Unicode-Edge-Cases ungetestet.

---

## Step 1 — Token-Stream-Golden + Round-Trip (höchster Hebel)

**Ziel:** Das wichtigste Netz. Vollständige Serialisierung des Token-Stroms je Korpus-Datei +
Full-Fidelity-Invariante.

**Aufgaben:**

1. Neue Fixture `SyntaxGoldenTests.cs`.
2. **Korpus** zusammenstellen: alle `.nav` aus `Regression\Tests` und `Diagnostics\Tests` +
   `Resources.AllRules`. In neuem Ordner `Syntax\Tests\` als Test-Daten ablegen (oder per
   `TestDataDirectory.Resolve` auf die vorhandenen Orte zeigen).
3. **Serializer** `string DumpTokens(SyntaxTree tree)`: je Token eine Zeile mit
   `Start`, `Length`, `Type`, `Classification`, `IsMissing`, **Parent-Knotentyp** (`token.Parent?.GetType().Name`).
   Stabil sortiert (die Liste ist bereits sortiert). Trivia **mit** ausgeben.
4. `[Test, TestCaseSource]` pro Datei: `DumpTokens` gegen `<datei>.tokens`-Golden vergleichen.
5. `[Test, Explicit] UpdateGolden()` schreibt alle `.tokens` neu (Muster: `RegressionTests.GenerateFiles`).
6. **Round-Trip-Test** (separat, kein Golden nötig): für jede Korpus-Datei
   `String.Concat(tree.Tokens ohne EndOfFile, je token.ToString()) == originaltext`.
   Achtung: `EndOfFile`-Token und evtl. Missing-Token (Länge 0) korrekt behandeln.
7. Round-Trip **zusätzlich** in die Stress-Harness einklinken: in `SyntaxStressTests` bei jedem
   Präfix zusätzlich Round-Trip prüfen (tausende Fälle gratis).

**Akzeptanz:** Beide TFMs grün; `.tokens`-Golden eingecheckt; Round-Trip über gesamten Korpus + alle Präfixe grün.

---

## Step 2 — Knotenbaum-Golden (Struktur)

**Ziel:** Den *Baum* (nicht nur Token) als Snapshot festhalten — fängt Strukturfehler, die der
Token-Strom allein nicht zeigt (falsche Verschachtelung, fehlende optionale Knoten).

**Aufgaben:**

1. `string DumpTree(SyntaxNode root)`: rekursiv, eingerückt, je Knoten `GetType().Name` + `Extent`
   (+ optional die `ChildTokens`-Typen). Nutzt `ChildNodes()`.
2. `[Test, TestCaseSource]` gegen `<datei>.tree`-Golden, plus `Explicit`-Update.
3. Invarianten-Tests (kein Golden): jeder Knoten/Token hat `Parent != null` (außer Root);
   `child.Extent` liegt in `parent.Extent`; Geschwister-Extents überlappen nicht. (Teilweise schon
   in `SyntaxTreeAllRulesTests` punktuell — hier systematisch über den ganzen Korpus.)

**Akzeptanz:** Beide TFMs grün; `.tree`-Golden eingecheckt; Invarianten über gesamten Korpus grün.

---

## Step 3 — Syntax-Diagnostics-Korpus (Recovery festnageln)

**Ziel:** Das Error-Recovery-Verhalten pinnen — der Bereich, in dem der neue Parser am ehesten abweicht.

**Aufgaben:**

1. Neuer Ordner `Syntax\Diagnostics\` mit absichtlich kaputten `.nav`, je Datei ein Fokus:
   - fehlende Edge (`A ;`), fehlendes Target (`A --> ;`)
   - fehlendes Semikolon, fehlende `}` / `]`
   - kaputte Generics (`T2<T3,`), kaputte Params
   - unerwartetes Zeichen → `Nav0000` (`Nav0000UnexpectedCharacter`)
   - Präprozessor-Zeilen → `Nav3000`/`Nav3001`
   - leerer Block, nur Whitespace, nur Kommentar, vorzeitiges EOF
2. Verfahren von `DiagnosticTests` wiederverwenden, aber **syntax-only**: Diagnostics direkt aus
   `SyntaxTree.ParseText(...).Diagnostics` (nicht über `CodeGenerationUnit`), als Golden serialisieren
   (`Id`, `Location.Start/Length` bzw. Zeile/Spalte, Message). Inline-Annotation `// ^---^ NavId`
   oder separate `.diag`-Golden — am `DiagnosticTests`-Muster orientieren.
3. Explizit die zwei `NotifyErrorListeners`-Fälle aus der heutigen Grammatik abdecken
   („missing edge", „missing target node" in `transitionDefinition`).

**Akzeptanz:** Beide TFMs grün; jede Recovery-Erwartung als Golden festgehalten.

> **Hinweis:** Diese Golden pinnen die *ANTLR*-Recovery. Der handgeschriebene Parser wird hier
> bewusst teils **bessere** Meldungen liefern → erwartete, reviewte Diffs. Das ist beabsichtigt
> (siehe `nav-handwritten-parser.md`, Abschnitt Error-Recovery).

---

## Step 4 — Trivia erschöpfend

**Ziel:** Das `// TODO Weitere Tests für Trivias` auflösen. Genau der Bereich, der sich beim Umbau
auf strukturierte/angehängte Trivia ändern soll → muss vorher dicht gepinnt sein.

**Aufgaben (erweitert `SyntaxNodeTriviaTests`):**

1. Leading/Trailing-Trivia-Extents an jeder Position: Datei-Anfang, zwischen Knoten, Datei-Ende,
   `onlyWhiteSpace`-Variante (vorhandene Tests als Muster).
2. **Parent-Zuordnung** der Trivia-Token (heute hängen Trivia laut `PostprocessTokens` am Root —
   `// TODO: hier evtl. den echten Parent herausfinden`; das aktuelle Verhalten **pinnen**, damit eine
   spätere Änderung sichtbar wird).
3. **SingleLineComment-/EOL-Split**: heute wird ein `//`-Kommentar in Kommentar-Token **ohne** EOL +
   separates `NewLine`-Token zerlegt (`SplitSingleLineCommenTokens`). Exakt testen.
4. **CRLF vs. LF vs. CR** und die exotischen Zeilenenden (``, ` `, ` `).
5. **BOM** am Dateianfang, MultiLineComment über mehrere Zeilen, Whitespace-Klasse `Zs`.

**Akzeptanz:** Beide TFMs grün; Trivia-Verhalten an allen Rändern festgehalten.

---

## Step 5 — Lexer-Direkt-Golden + Unicode

**Ziel:** Lex-Regressionen isoliert sichtbar machen (unabhängig vom Parser) und Unicode-Edge-Cases pinnen.

**Aufgaben:**

1. Token-Sequenz direkt prüfen — bis der handgeschriebene Lexer existiert, geht das über
   `SyntaxTree.ParseText(...).Tokens` (kleine Strings, exakte Sequenz asserten). Sobald der neue Lexer
   da ist, zusätzlich direkt gegen ihn.
2. Unicode-/Knifflig-Fälle als parametrisierte Tests:
   - Umlaut-Identifier (`ä`, `Ö`, `ß`), Identifier mit `.`/`_`/Ziffern
   - `o->` (ModalEdge) vs. Identifier `oXyz`; `-->` vs. `-` + `->`; `==>` vs. `=`
   - Zs-Whitespace (` `, ` `, …), die drei NL-Varianten
   - StringLiteral mit/ohne schließendes `"`, leerer String `""`
   - leerer Input, nur Whitespace, nur Kommentar

**Akzeptanz:** Beide TFMs grün; Lexer-Verhalten und Unicode-Kanten festgehalten.

---

## Korpus — Anforderungen

Je größer und schräger, desto besser. Mindestens:

- **Real:** alle `.nav` aus `Regression\Tests`, `Diagnostics\Tests`, `Resources.AllRules`.
- **Torture (neu):** halbe Transitionen, fehlende Klammern/Semikola, kaputte Generics, leere Blöcke,
  nur Whitespace, nur Kommentar, exotische Zeilenenden, Präprozessor-Zeilen, sehr lange Identifier,
  tief verschachtelte Generics/Arrays.

---

## Reihenfolge & Definition of Done

1. **Step 1** zuerst (Token-Golden + Round-Trip) — größter Hebel, kleinster Aufwand.
2. Step 2 (Baum-Golden), Step 3 (Diagnostics), dann Step 4/5 (Trivia, Lexer).

**Gesamt-DoD:** Vollständiger Syntax-Output (Token, Baum, Diagnostics) je Korpus-Datei als Golden
eingecheckt; Round-Trip + Struktur-Invarianten über Korpus **und** alle Tipp-Präfixe grün; beide TFMs
grün. Ab dann kann der Parser-Tausch beginnen — jeder Golden-Diff ist entweder ein Bug oder eine
bewusste, dokumentierte Änderung.

> **Status:** Alle fünf Steps sind erledigt, die DoD ist erfüllt (Suite auf net10.0 **und** net472
> grün). Das Sicherheitsnetz steht — der Parser-Tausch (siehe `nav-handwritten-parser.md`) kann
> beginnen. Neue Tests entstehen ab hier nur noch reaktiv, falls der handgeschriebene Parser
> zusätzliche Fälle aufdeckt.
