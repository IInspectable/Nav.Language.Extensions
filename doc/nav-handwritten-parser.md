# Handgeschriebener Lexer/Parser für Nav — Erkenntnisse & Entwurf

> **Zweck dieses Dokuments:** Festhalten der Analyse zur Frage, ob/wie der **ANTLR-basierte**
> Lexer/Parser in `Nav.Language` durch einen **handgeschriebenen** ersetzt werden kann. Es ist
> Grundlage für die spätere Umsetzung und ergänzt den Test-Plan
> [`nav-parser-test-plan.md`](./nav-parser-test-plan.md), der **vor** dem Umbau abgearbeitet wird.
>
> Stand der Analyse: Juni 2026. Quelle der Wahrheit bleibt der Code — Pfade/Zeilen unten als Einstieg.

---

## Motivation

Zwei Schmerzpunkte mit ANTLR:

1. **Doppeltes Objektmodell:** Das mutable ANTLR-Parse-Tree muss in den immutable `SyntaxNode`-Baum
   transformiert werden (Visitor). Zwei Repräsentationen, eine davon nur Durchgangsstation.
2. **Trivia:** ANTLR wirft Whitespace/Kommentare auf separate Channels. Sie werden heute **nachträglich**
   rekonstruiert (`PostprocessTokens`), statt sie natürlich als Leading/Trailing-Trivia zu führen.

Ein eigener Parser löst beides: ein Durchlauf, der direkt die immutable Nodes baut **und** alle Token
inkl. Trivia in einem Strom liefert.

---

## Kernbefund (in einem Satz)

**Der Parser selbst ist einfach** (die Grammatik ist winzig, LL(1), ohne Linksrekursion/Präzedenz) —
Aufwand und Risiko liegen im **Lexer-Detailkram** (Modes/Unicode-Trivia) und im **exakten Reproduzieren**
des heutigen Outputs (Token-Classifications + Diagnostics + Snapshot-Tests), nicht im Parsen.

Realistische Schätzung: Lexer 1–2 Tage, Parser ~1 Tag, Error-Recovery + Diagnostics-Parität +
Tests-grün 2–4 Tage.

---

## Ausgangslage im Code

### Grammatik (winzig, ~25 Regeln)

- `Nav.Language\Grammar\NavGrammar.g4` — Parser-Grammatik. **LL(1)**, keine Linksrekursion, keine
  Präzedenz. Lookahead nur an **drei** trivialen Stellen:
  1. `memberDeclaration`: `taskref` + `StringLiteral` → Include; `taskref` + `Identifier` →
     TaskDeclaration; `task` → TaskDefinition (LA(2) nach `taskref`).
  2. `transitionDefinition` vs. `exitTransitionDefinition`: letzteres ist `Identifier ':' …`
     (nach Identifier auf `:` schauen).
  3. `codeType`: `simpleType`/`genericType`/`arrayType` starten alle mit `Identifier`; `<` ⇒ generic,
     `[` ⇒ array (LA(1)).
- `Nav.Language\Grammar\NavTokens.g4` — Lexer-Grammatik. ~30 Keywords + Einzelzeichen-Punctuation +
  drei Mehrzeichen-Operatoren (`-->`, `o->`, `==>`). Identifier inkl. Umlauten (`Ä Ö Ü ä ö ü ß`),
  `_`, Ziffern, `.`. Trivia auf `TriviaChannel`, Präprozessor auf `PreprocessorChannel`.
  Lexer-**Modes**: `DEFAULT`, `PreprocessorMode`, `PreprocessorTextMode`. NL-Varianten:
  `\r\n \r \n     `. Whitespace: Unicode-Klasse `Zs` + Tab/VT/FF.

### Heutige Pipeline (ANTLR → immutable)

- `Nav.Language\Syntax\SyntaxTree.cs` — `ParseText(...)`: Lexer/Parser-Setup, dann
  `NavGrammarVisitor.Visit(tree)` (baut Nodes + signifikante Token), dann **`PostprocessTokens`**:
  gleicht die Visitor-Token gegen `cts.AllTokens` ab und ergänzt Trivia/Unknown. Diese
  Reconciliation (`-T---TTT-`-Mechanik) **entfällt** beim eigenen Parser.
- `Nav.Language\Internal\NavGrammarVisitor.cs` — **die De-facto-Spezifikation des neuen Parsers.**
  Jede `VisitXxx` zeigt 1:1: welche Node-Klasse mit welchen Kindern entsteht und welches Token welche
  `TextClassification` bekommt (derselbe `Identifier` wird kontextabhängig `TaskName`, `GuiNode`,
  `TypeName`, `ParameterName`, `Identifier` …). Diese Logik wandert in die jeweilige Parse-Methode.
- `Nav.Language\Internal\SyntaxTokenFactory.cs`, `TextExtentFactory.cs` — Token/Extent-Erzeugung.
- Immutable Nodes: `Nav.Language\Syntax\*.cs` (Konstruktoren nehmen Kinder + Extent, `AddChildNode`,
  `FinalConstruct`). Generiert: `Syntax\Generated\Syntax.Generated.cs` (die `Syntax.ParseXxx`-Einstiege),
  `SyntaxNodeVisitor.Generated.cs`, `SyntaxNodeWalker.Generated.cs`.

### Token-Modell (wichtig!)

`Nav.Language\Syntax\SyntaxTokenList.cs` ist eine **flache, sortierte Liste *aller* Token**
(signifikant + Trivia gemischt), jedes Token mit `Parent`-Zeiger; Knoten finden ihre Token per
Extent-Überlappung (`ChildTokens()`). **Das ist *nicht* Roslyns angehängte Leading/Trailing-Trivia.**
Trivia sind heute gleichberechtigte Geschwister im Strom (Parent = Root, mit `// TODO echten Parent`).

---

## Entwurf: Lexer

Ein Durchlauf liefert **alle** Token als flache Liste, inkl. Trivia + genau einem `EndOfFile`.

```csharp
readonly record struct RawToken(SyntaxTokenType Type, TextExtent Extent) {
    public bool IsTrivia => Type is SyntaxTokenType.Whitespace or SyntaxTokenType.NewLine
                                  or SyntaxTokenType.SingleLineComment or SyntaxTokenType.MultiLineComment;
}
```

Scan-Reihenfolge je Position: Trivia (Whitespace/NL/Kommentare) → **Mehrzeichen-Operatoren vor
Identifier** (`-->`, `==>`, `o->` — sonst frisst der Identifier-Scanner das `o`) → Einzelzeichen-
Punctuation → `#` (Präprozessor, bis EOL) → `"` (StringLiteral) → Identifier (Buchstabe inkl. Umlaute,
`_`, Ziffer, `.`; danach Keyword-Lookup per Dictionary) → sonst `Unknown`.

Zwei Feinheiten, die heute Sonderbehandlung brauchen und hier **natürlich** reinfallen:

- **SingleLineComment ohne EOL:** Der Kommentar-Scanner stoppt vor dem Zeilenende; das `NewLine` wird
  als eigenes Token gelext. → ersetzt `SplitSingleLineCommenTokens`.
- **`o->` vs. Identifier `oXyz`:** Mehrzeichen-Operator-Check steht vor dem Identifier-Scan.

Mehr Arbeit als der Parser: Unicode-`Zs`, die NL-Varianten, der Präprozessor-Mode.

---

## Entwurf: Parser (Recursive Descent)

Läuft über die rohe Token-Liste, hält einen Index aufs nächste **signifikante** Token, sammelt beim
Konsumieren signifikante (klassifizierte) Token **und** übersprungene Trivia in die Output-Liste.
Helfer:

- `PeekType(n)` — n-tes signifikantes Token (Trivia überspringend), Identifier→Keyword gemappt.
- `Eat(expected, classification, parent)` — flusht Leading-Trivia, konsumiert das erwartete Token
  (klassifiziert anhängen) oder synthetisiert ein **zero-width Missing-Token** + Diagnose (Roslyn-Stil,
  **kein** Vorrücken).
- `SkipTo(parent, …sync)` — Panic-Mode, überzählige Token als `Skiped`-Trivia anhängen.

Am Ende: `SyntaxTokenList.AttachSortedTokens(tokens)` (unverändert). Jede der ~25 Parse-Methoden
entspricht 1:1 einer `VisitXxx`. Die immutable Nodes werden über **einen** Konstruktor mit gesammelten
Kindern gebaut.

---

## Error-Recovery (Detail, weil hier das meiste Abweichungsrisiko sitzt)

**Leitregel:** Kein Zeichen wird je weggeworfen — jedes Token landet in einem Node oder als
`Skiped`-Trivia. Der Baum deckt **immer** den ganzen Text ab.

Zwei Mechanismen:

1. **Insertion / Missing-Token** (erwartet, fehlt): zero-width `SyntaxToken.Missing` + Diagnose, nicht vorrücken.
2. **Deletion / Skipped-Token** (da, nicht erwartet): als `Skiped`-Trivia konsumieren + Diagnose, retry.

**Wiedereinstieg über Sync-/Anchor-Token.** Die Nav-Grammatik ist dafür ideal:

| Sync-Token | Bedeutung |
|---|---|
| `;` | beendet **jede** Node-/Transition-Deklaration |
| `}` | schließt Block (`nodeDeclarationBlock`, `task{}`) |
| `task`, `taskref` | Start neues Top-Level-Member |
| `init`,`exit`,`end`,`choice`,`dialog`,`view` | Start neue Node-Deklaration |

Implementiert in den **Listen-Schleifen** (`*`-Wiederholungen) mit zwei Invarianten:

1. **Garantierter Fortschritt** — pro Schleifendurchlauf mind. ein Token konsumieren (sonst
   Endlosschleife, sobald nur Missing-Token synthetisiert werden). Defensiv: prüfen, ob `_pos` sich bewegt hat.
2. **FOLLOW gewinnt** — bei `}`/`task` Schleife abbrechen statt es als Müll zu fressen.

**Gestaffelte Sync-Sets:** Jede Recovery skippt nur bis zum nächsten lokalen **oder übergeordneten**
Anchor (`;` lokal, aber `}`/`task` der äußeren Regel überlassen) — sonst reißt lokale Recovery die
äußeren Strukturen mit. Technik wie Roslyns C#-Parser (Anchor-Set-Stack).

Heutige `NotifyErrorListeners`-Fälle, die nachgebildet werden müssen (`NavGrammar.g4`,
`transitionDefinition`): **„missing edge"** und **„missing target node"**.

---

## Präprozessor-Direktiven

**Ist-Zustand:** Nav lext `#` über `HashToken` → `PreprocessorMode` → Keyword + Text bis EOL,
klassifiziert das, und meldet es als **nicht unterstützt** (`Nav3000InvalidPreprocessorDirective`,
`Nav3001…MustAppearOnFirstNonWhitespacePosition`). Es gibt **keine** echte Direktiven-Semantik.
Includes laufen bewusst auf **Grammatik**-Ebene (`taskref "…"`), nicht über den Präprozessor.

**Wie Roslyn es macht:** Direktiven sind **Trivia** — der Hauptparser sieht sie nie. Aber
**strukturierte** Trivia: `#if DEBUG` ist ein kleiner Syntaxteilbaum (`DirectiveTriviaSyntax`:
`#`, `if`, Expression, `EndOfDirectiveToken`), erreichbar via `trivia.HasStructure`/`GetStructure()`.
Konditionale Direktiven (`#if/#elif/#else/#endif`) tragen **Zustand über die Datei** (immutable
`DirectiveStack` im Lexer; toter Code wird als `DisabledTextTrivia` geschluckt). Diese Verzahnung von
Lexen und Direktiven-Auswertung ist die eigentliche Komplexität.

**Für Nav (vereinfacht):** Kein konditionaler Stack, kein Disabled-Text, keine Symbol-Auswertung nötig
— nur der zustandslose Teil. Beim `#` einen **Mini-Sub-Parser** aufrufen, der `#` + Keyword +
Text-bis-EOL zu **einer** `DirectiveTriviaSyntax` zusammenfasst. Der Hauptparser sieht nur Trivia.
„Unbekannte Direktive" wird damit eine **semantische** Frage (Diagnose am Trivia-Knoten), kein
Parserfehler mehr.

---

## Strukturierte Trivia & der architektonische Gabelpunkt

„Trivia" heißt nicht „flacher Span". Strukturierenswert: Direktiven, Region-Marker, Doc-Kommentare.
Whitespace/NewLine/normale Kommentare bleiben flach.

Hier muss eine bewusste Entscheidung fallen, weil Nav **nicht** Roslyns Trivia-Modell hat:

- **Weg A — flache Liste behalten (kleiner Eingriff):** Direktiven-Token bleiben im flachen Strom;
  zusätzlich optional ein `DirectiveTriviaSyntax`, das diese Token referenziert und in eine
  `Directives`-Collection am Root (`CodeGenerationUnitSyntax`) gehängt wird. Minimal-invasiv,
  passt zur jetzigen `SyntaxTokenList`.
- **Weg B — Roslyn-Stil Leading/Trailing-Trivia (großer Eingriff):** `SyntaxToken` bekommt
  `LeadingTrivia`/`TrailingTrivia`-Listen, strukturierte Trivia als Spezialfall (`GetStructure()`).
  Das ist der ursprünglich gewünschte „echte" Trivia-Wunsch — aber ein **Bruch** am Token-Modell;
  alles, was `SyntaxTokenList` per Position liest (Classification, GoTo, QuickInfo, FindReferences …),
  muss mitziehen.

**Empfohlene Reihenfolge (Risiken entkoppeln):**

1. Zuerst handgeschriebener Parser, der das **flache Token-Modell beibehält** — Output 1:1 wie ANTLR
   (Token-Strom gegen `cts.AllTokens` diffbar als Sicherheitsnetz). Direktiven als flache Token wie heute.
2. **Danach** separat das Trivia-Modell auf angehängt + strukturiert heben — ohne ANTLR im Weg,
   mit dem eigenen Parser, der die Trivia ohnehin in einem Durchlauf sieht. Direktiven werden der
   erste „Kunde" der strukturierten Trivia.

---

## Was der Tausch gewinnt

- ANTLR-Dependency + Grammatik-Codegen (`NavGrammarBaseVisitor` etc.) entfallen.
- Two-Phase-Visitor + `PostprocessTokens`-Reconciliation entfallen.
- Trivia natürlich in einem Durchlauf; Basis für strukturierte Trivia (Direktiven) und späteres
  inkrementelles Reparsen.
- Volle Kontrolle über Error-Recovery und Fehlermeldungen.

---

## Risiken / offene Punkte

- **Diagnostics-Parität:** exakte IDs/Locations/Messages treffen (`Nav0000`, `Nav3000/3001`, die
  `NotifyErrorListeners`-Fälle). Größtes Abweichungsrisiko.
- **Classification-Parität:** kontextabhängige `TextClassification` je Token exakt wie im Visitor.
- **Snapshot-Oberfläche:** Token-Strom treibt Classification/QuickInfo/etc. in **allen** Hosts +
  `.expected.cs`-Regression. → Deshalb erst der Test-Plan, dann der Umbau.
- **Inkrementelles Reparsen** ist eine **separate, spätere** Optimierung — nicht Teil des ersten Umbaus.

---

## Empfohlene Schritte (nach dem Test-Plan)

1. **Lexer** schreiben; gegen ANTLR-`cts.AllTokens` auf identische Token-Sequenz verifizieren. — **erledigt**
   (`Nav.Language\Syntax\NavLexer.cs`, Gate `NavLexerDifferentialTests`).
2. **Recursive-Descent-Parser**, der direkt die immutable Nodes baut (Visitor-Logik 1:1), Trivia
   gleich mit anhängen; flaches Token-Modell beibehalten. — **erledigt (Schritt A, Happy Path)**
   (`Nav.Language\Syntax\NavParser.cs`, Gate `NavParserDifferentialTests`).
3. Error-Recovery + Diagnostics-Parität; Golden-/Snapshot-Tests als Gate. — **erledigt (Schritt B)**:
   B1 (lexikalische Diagnostics `Nav0000`/`Nav3000`/`Nav3001`) **erledigt** (Commit `e0d35c99`);
   B2 (Golden-Verifikationsstrang für den Handparser, `*.hand.*`) **erledigt**; B3 (Error-Recovery:
   Missing-/Skipped-Token, gestaffelte Sync-Sets, missing edge/target) inkl. Tipp-Präfix-Robustheit
   **erledigt**.
4. *(Später, separat)* Trivia-Modell auf angehängt + strukturiert; Direktiven als strukturierte Trivia.
5. **Cutover (Schritt C):** `SyntaxTree.ParseText` auf den Handparser umstellen, ANTLR ausbauen.
   - **Step 1+2 (ParseText-Umstellung + Golden-Cutover) erledigt** — siehe Abschnitt
     „Stand nach Cutover Step 1+2" unten.
   - **Verbleibend: Rename `Syntax.Generated.cs` + ANTLR-Ausbau** — ebenda.

---

## Stand nach Cutover Step 1+2 (erledigt, uncommitted — Einstieg für die nächste Session)

> **Whole-File-Parsing läuft jetzt produktiv auf dem Handparser. Beide TFMs grün**
> (net10 1099 pass / 0 fail, net472 1099 pass / 0 fail, Regression/`.expected.cs` inklusive).
> ANTLR ist noch da, wird aber nur noch von den per-Regel-Test-Einstiegen und den Differential-Gates
> benutzt. Quelle der Wahrheit bleibt der Code.

### Was in dieser Session geändert wurde (alles uncommitted)

- **`Nav.Language\Syntax\SyntaxTree.cs`:** Der öffentliche `ParseText(text, filePath, ct)` ruft jetzt
  `NavParser.Parse`. Die alte ANTLR-Whole-File-Logik (interne `treeCreator`-Überladung) bleibt erhalten,
  erreichbar über den **neuen internen** `ParseTextAntlr(text, filePath, ct)` — der existiert **nur** für
  die Differential-Gates und fällt mit dem ANTLR-Ausbau weg.
- **`Nav.Language\Syntax\Generated\Syntax.Generated.cs`:** `ParseCodeGenerationUnit` (der einzige
  Produktions-Einstieg der Klasse; genutzt von `SyntaxProvider`/`OverlaySyntaxProvider`/`ParserService`)
  ruft jetzt den öffentlichen `SyntaxTree.ParseText` (= hand). Die **~50 per-Regel-`Syntax.ParseXxx`
  bleiben unverändert auf ANTLR** (sie sind test-only) — **bewusst nicht verschoben** (Nutzerentscheid:
  bleiben in `Nav.Language`, „schadet nicht"; werden beim ANTLR-Ausbau an Ort und Stelle umgebogen).
- **T4 stillgelegt:** `Syntax.Generated.tt` **gelöscht**; das Generator-Wiring (`<None Update>` +
  `<Compile Update>`-Block) für diese Datei aus `Nav.Language.csproj` entfernt. `Syntax.Generated.cs`
  wird ab jetzt **von Hand** gepflegt (Header entsprechend angepasst). Die anderen `.tt`
  (`SyntaxNodeVisitor`/`SyntaxNodeWalker`) sind unberührt und laufen weiter.
- **Differential-Gates umgebogen:** `NavParserDifferentialTests` und `NavLexerDifferentialTests` nutzen
  als ANTLR-Referenz jetzt `SyntaxTree.ParseTextAntlr` (sonst verglichen sie hand-gegen-hand).
- **Golden-Cutover:** `Syntax\Tests\*.nav.{tokens,tree,diag}` der 8 Recovery-Dateien (`BrokenGenerics`,
  `MissingCloseBrace`, `MissingEdge`, `MissingSemicolon`, `MissingTargetNode`, `PrematureEof`,
  `StringsAndGenerics`, `UnexpectedCharacter`) über `SyntaxGoldenTests.UpdateGolden` neu erzeugt —
  **byte-identisch zu den reviewten `.hand.*`** (per `diff` verifiziert). Alle wohlgeformten
  Korpus-Dateien unverändert.
- **`Nav.Language.Tests\Diagnostics\Tests\SyntaxErrorTokenRecognitionError.nav`:** ANTLRs spuriöses
  `Nav0002 token recognition error at '字'` aus den erwarteten Markern entfernt — der Handparser
  behandelt `字` als Preprocessor-Text und meldet sauber nur `Nav3000`.

### Verbleibend in Schritt C (Arbeit für die nächste Session)

1. **Rename `Syntax.Generated.cs` — erledigt.** Per `git mv` nach `Nav.Language\Syntax\Syntax.cs`
   verschoben (raus aus dem `Generated\`-Ordner, da dort nur noch echte Generate liegen). Namespace
   `Pharmatechnik.Nav.Language` unverändert; der SDK-Glob zieht die Datei automatisch (keine
   csproj-Compile-Einträge nötig — es gab auch keine). Header-Kommentar angepasst. Beide TFMs grün
   (net10 1099/0, net472 1099/0).
2. **ANTLR-Ausbau (der Rest):**
   - **Per-Regel-Einstiege auf den Handparser heben — erledigt.** `NavParser` hat jetzt einen
     uniformen per-Regel-Einstieg `ParseRule(text, Rule, filePath, ct)` (Cursor aufsetzen → die zur
     `Rule` gehörende private `Parse*` rufen → Rest als nicht-signifikant an die so entstandene Wurzel
     → `SyntaxTree` bauen); dispatcht über die nested `enum NavParser.Rule` (hält alle `Parse*` privat).
     Die 44 `Syntax.ParseXxx` rufen das (statt der ANTLR-Regel-Delegaten); `ParseArrayType` → `ParseCodeType`
     (deckt die Array-Regel mit ab). `ParseCodeGenerationUnit` bleibt Whole-File. Die ANTLR-`treeCreator`-
     Überladung wird damit nur noch von `ParseTextAntlr` (Differential-Gate) benutzt. Beide TFMs grün
     (net10 1099/0, net472 1099/0; generierte `SyntaxTests`/`ParseEmptyStringTests`, `SyntaxNodeTriviaTests`,
     `SyntaxTokenTests` inklusive).
   - **`SyntaxTokenType.cs` entkoppeln — erledigt.** Enumwerte `= NavTokens.X` auf feste Integer
     eingefroren (heutige ANTLR-Nummern 1:1, `using ...Generated` entfernt). Die Werte stimmen weiter mit
     ANTLRs `NavTokens` überein — wichtig, solange die Differential-Gates via `SyntaxTokenFactory` aus
     `IToken.Type` (ANTLR-Int) in den Enum casten.
   - **`SyntaxFacts.cs` entkoppeln — erledigt.** Keyword-/Punctuation-Literale fest hinterlegt (1:1 wie
     vormals über `NavGrammar.DefaultVocabulary`); `GetLiteralName`/`GetLiteralNameAsChar` und das
     `using ...Generated` entfernt. Beide TFMs grün (net10 1099/0, net472 1099/0).
   - **ANTLR-Plumbing löschen** (`Nav.Language\Internal\`): `NavGrammarVisitor`, `NavCommonTokenStream`,
     `NavLexerErrorListener`, `NavParserErrorListener`, `DiagnosticFactory` (`IToken`-basiert),
     `SyntaxBuildingExtension`; `Nav.Language\Syntax\SourceTextCharStream.cs`; die `IToken`/
     `ITerminalNode`-Overloads in `SyntaxTokenFactory.cs`/`TextExtentFactory.cs`. Plus die
     ANTLR-Hälfte von `SyntaxTree` (interne `treeCreator`-Überladung, `ParseTextAntlr`,
     `PostprocessTokens`, `SplitSingleLineCommenTokens`).
   - **Build:** in `Nav.Language.csproj` die zwei PackageReferences `Antlr4` + `Antlr4.Runtime` und die
     `<Antlr4 Update="Grammar\*.g4">`-Items entfernen; `Grammar\*.g4` aus dem Build nehmen.
     **`StringTemplate4` MUSS bleiben** (liefert `Antlr4.StringTemplate` für die Codegenerierung — nicht
     der Parser). Das ist die häufigste „grün, aber kaputt"-Falle.
   - **Test-Cleanup:** `NavParserDifferentialTests` + `NavLexerDifferentialTests` entfernen (brauchen
     ANTLR/`ParseTextAntlr`). Die `.hand.{tokens,tree,diag}` sind jetzt redundant (== `.nav.*`) →
     löschen, samt `NavParserGoldenTests`; `SyntaxGoldenTests` bleibt (pinnt jetzt den Handparser-Output
     als kanonisches Golden).
3. **Verifikation:** beide TFMs (net10 + net472) + Regression grün halten — nach jedem Teilschritt.

### Risiko-Hotspots (unverändert)

(a) `SyntaxTokenType`/`SyntaxFacts`-Entkopplung (stille Wertverschiebung), (b) die per-Regel-Einstiege,
(c) dass `StringTemplate4` nicht versehentlich mit ausgebaut wird.

---

## Stand & Handoff (Schritt A + B erledigt → Cutover (Schritt C) als Nächstes)

> **Schritt B vollständig erledigt.** Der Handparser ist jetzt fehlertolerant: `Eat` synthetisiert
> bei fehlendem Pflicht-Token ein nullbreites Missing-Token + Diagnose (Insertion, kein Vorrücken);
> der Panic-Mode `Recover` überspringt unerwartete Token bis zu einem Wiedereinstiegs- oder äußeren
> Anker-Token (`BreaksBody`: `}`/`task`/`taskref`/EOF) mit garantiertem Fortschritt (Deletion);
> übersprungene signifikante Token hängt `AttachNonSignificantTokens` — wie die Trivia — als
> `Skiped`-Token an die Wurzel (Round-Trip bleibt lückenlos). „missing edge"/„missing target node"
> sind in `ParseTransitionDefinition`/`ParseExitTransitionDefinition` nachgebildet. Abgesichert über
> den **Golden-Strang `NavParserGoldenTests`** (`*.hand.tokens/.hand.tree/.hand.diag`, [Explicit]
> `UpdateGolden`) plus **Tipp-Präfix-Robustheit** (jedes Präfix parst ohne Exception und round-trippt).
> Der Handparser liefert dabei bewusst **knappere** Diagnosen als ANTLR (eine treffende Meldung statt
> Kaskaden) und bei `StringsAndGenerics` sogar den **besseren Baum** (Body + zweiter Task erkannt,
> nur der ungültige Kopf-`[params …]` als Skiped). `SyntaxTree.ParseText` läuft weiterhin auf ANTLR —
> Umstellung erst beim Cutover (Schritt C). Beide TFMs grün.
>
> **Offener Review-Punkt:** Bei `PrematureEof` (`I1 -->`⟂EOF) meldet der Handparser drei kaskadierende
> „missing" (target node / `;` / `}`) an derselben EOF-Position — korrekt, aber etwas geschwätzig
> (ANTLR coalesct auf zwei). Bewusste, reviewbare Entscheidung; bei Bedarf leicht zu entschärfen.

---

## Schritt C — Cutover (Plan für die nächste Session)

**Ziel:** `SyntaxTree.ParseText` von ANTLR auf den Handparser umstellen und ANTLR **als Parser/Lexer**
vollständig ausbauen. Danach gibt es nur noch eine Syntax-Implementierung. Hartes Cutover ohne
Runtime-Schalter (so entschieden); die Golden-Diffs sind das Review-Artefakt.

### Befund-Lage (in dieser Session verifiziert — Einstieg, Quelle der Wahrheit bleibt der Code)

- **Produktion parst nur ganze Dateien.** Alle drei produktiven Aufrufer nutzen ausschließlich
  `Syntax.ParseCodeGenerationUnit` (Whole-File): `Nav.Language\Provider\SyntaxProvider.cs`,
  `Nav.Language\Workspace\OverlaySyntaxProvider.cs`, `Nav.Language.ExtensionShared\ParserService\ParserService.cs`
  (Default `GetParseMethod`). Das deckt `NavParser.Parse` bereits 1:1 ab.
- **Die per-Regel-Einstiege `Syntax.ParseXxx` sind test-only.** `Nav.Language\Syntax\Generated\Syntax.Generated.cs`
  (aus `.tt`) erzeugt je Grammatikregel ein `Syntax.ParseXxx`, das
  `SyntaxTree.ParseText(text, parser => parser.<rule>(), …)` mit **ANTLR-Regel-Delegat** aufruft.
  Konsumenten sind die generierten Tests (`Generated Tests\SyntaxTest.cs` = `SampleSyntax` je Regel,
  `ParseEmptyStringTests.cs`) und diverse Feature-Test-Helfer, die Snippets parsen. **Kein**
  Produktionscode. → Das ist die **eine echte Designfrage** des Cutovers (siehe unten).
- **`SyntaxTokenType` ist an ANTLR gekoppelt:** `Nav.Language\Syntax\SyntaxTokenType.cs` definiert jeden
  Enumwert als `= NavTokens.X` (ANTLR-generierte Konstante; nur `EndOfFile = 255` ist schon hart). Beim
  ANTLR-Ausbau müssen diese auf **feste Integer** eingefroren werden (die Zahlen lecken nirgendwo in
  Golden — `DumpTokens` schreibt den Namen —, aber sicherheitshalber die heutigen Werte 1:1 übernehmen).
- **`SyntaxFacts` ist an ANTLR gekoppelt:** `GetLiteralName(int)` nutzt `NavGrammar.DefaultVocabulary`,
  und viele `public static readonly string XxxKeyword = GetLiteralName(NavGrammar.Xxx)`. Beim Ausbau →
  hartkodierte Literal-Map (das Muster gibt es schon: `ModalEdgeKeyword = "o->"`, `ModalEdgeKeywordAlt = "*->"`).
- **StringTemplate bleibt!** `Nav.Language\CodeGen\CodeGenerator.cs` nutzt `using Antlr4.StringTemplate;` —
  das kommt aus dem **separaten** NuGet `StringTemplate4` (csproj-Zeile 46), **nicht** aus dem Parser-ANTLR.
  Nur `Antlr4` + `Antlr4.Runtime` (csproj-Zeilen 41–45) sind Parser/Lexer und fliegen raus; `StringTemplate4`
  **muss bleiben**, sonst bricht die Codegenerierung.
- **ANTLR-Plumbing zum Löschen/Entkoppeln** (`Nav.Language\Internal\`): `NavGrammarVisitor.cs`,
  `NavCommonTokenStream.cs`, `NavLexerErrorListener.cs`, `NavParserErrorListener.cs`, `DiagnosticFactory.cs`
  (`IToken`-basiert), `SyntaxBuildingExtension.cs`; `Nav.Language\Syntax\SourceTextCharStream.cs`;
  die `IToken`/`ITerminalNode`-Overloads in `SyntaxTokenFactory.cs`/`TextExtentFactory.cs`. Plus die
  ANTLR-Pfad-Hälfte von `SyntaxTree.ParseText` und `PostprocessTokens`/`SplitSingleLineCommenTokens`.
- **Build:** `Nav.Language\Nav.Language.csproj` Zeilen 53–60 (`<Antlr4 Update="Grammar\NavGrammar.g4"/>`
  + `NavTokens.g4`) und die zwei PackageReferences entfernen; `Grammar\*.g4` aus dem Build nehmen.
  Der `_build\CodeGen\GenerateCodeGenFacts.cs`-Generator (StringTemplate-Facts) hat **nichts** mit der
  Grammatik zu tun — bleibt.

### Die eine Designfrage: per-Regel-Einstiege (`Syntax.ParseXxx`) ohne ANTLR

Empfohlen: **`NavParser` per-Regel-Einstiege geben** und `Syntax.Generated.tt` so umschreiben, dass jede
`Syntax.ParseXxx` statt `parser => parser.<rule>()` einen Handparser-Einstieg ruft. Mechanisch: ein
uniformer Wrapper „Cursor aufsetzen → `ParseXxx()` → Rest als nicht-signifikant an die Wurzel →
`SyntaxTree` bauen", parametrisiert über die schon vorhandene private `Parse*`-Methode je Regel. Die
~25 Methoden existieren bereits — sie müssen nur als Einstieg aufrufbar werden (intern sichtbar reicht;
Tests haben `InternalsVisibleTo`). Damit bleiben `SyntaxTest`/`ParseEmptyStringTests` unverändert nutzbar.
Alternative (mehr Test-Churn): die per-Regel-Tests auf Whole-File umstellen — nicht empfohlen.

### Verifikation des Cutovers (Reihenfolge)

1. `SyntaxTree.ParseText(text, filePath, ct)` (Whole-File) intern auf `NavParser.Parse` umlegen, ANTLR-Pfad
   daneben **vorerst stehen lassen**, Solution baut (`dotnet build` für den .NET-Teil; volle Solution via
   `n build` / MSBuild.exe wegen der VSIX).
2. **Golden-Cutover:** Die bestehenden ANTLR-Golden `Syntax\Tests\*.nav.{tokens,tree,diag}` werden jetzt
   vom Handparser erzeugt. Praktisch: `SyntaxGoldenTests.UpdateGolden` neu laufen lassen und die Diffs
   gegen die schon reviewten `*.hand.*` prüfen (müssen identisch sein → die `.hand.*` werden redundant und
   können samt `NavParserGoldenTests`/`NavParserDifferentialTests` entfernt werden, sobald ANTLR weg ist).
3. `.expected.cs`-Regression (`RegressionTests`) und die volle Suite auf **net10 + net472** grün halten —
   das ist das End-to-End-Netz über die Codegenerierung (fängt subtile Baum-/Token-Abweichungen).
4. Erst **dann** ANTLR ausbauen (Pakete, `.g4`, Plumbing, `SyntaxTokenType`/`SyntaxFacts` entkoppeln) und
   erneut beide TFMs + Regression grün ziehen.

> **Risiko-Hotspots:** (a) `SyntaxTokenType`/`SyntaxFacts`-Entkopplung (stille Wertverschiebung), (b) die
> per-Regel-Einstiege, (c) dass `StringTemplate4` nicht versehentlich mit ausgebaut wird. (a)/(c) sind die
> häufigsten „grün, aber kaputt"-Fallen.

---

### Was steht (Schritt A)

- **`Nav.Language\Syntax\NavParser.cs`** — handgeschriebener Recursive-Descent-Parser über den
  `RawToken`-Strom des `NavLexer`. Eine `Parse*`-Methode je Grammatikregel (1:1 zu `NavGrammarVisitor`),
  baut die immutablen Nodes direkt, vergibt die kontextabhängige `TextClassification` je signifikantem
  Token und hängt es an seinen Knoten. Einstieg: `NavParser.Parse(text, filePath, ct) : SyntaxTree`.
- **`Nav.Language.Tests\Syntax\NavParserDifferentialTests.cs`** — Gate: difft Baum (`DumpTree`),
  Token-Strom (`DumpTokens`) und Round-Trip von `NavParser.Parse` gegen `SyntaxTree.ParseText` (ANTLR)
  über den `Syntax\Tests`-Korpus. Nicht-wohlgeformte Dateien (ANTLR meldet Diagnostics) werden via
  `Assert.Ignore` zurückgestellt.
- **`SyntaxTree.ParseText` ist unverändert auf ANTLR** — der neue Parser läuft nur hinterm Gate
  (wie beim Lexer). Umschalten erst in Schritt C.
- Verifiziert (beide TFMs grün): die 6 wohlgeformten Korpus-Dateien; ad-hoc zusätzlich `AllRules.nav`
  (nutzt jede Regel), die Regression-`.nav` und `LargeNav.nav` (2869 Zeilen) — Baum/Token/Round-Trip
  jeweils byte-identisch zu ANTLR.

### Kalibrierte Invarianten (an den Golden festgenagelt — beim Recovery-Umbau nicht brechen)

- **Trivia/Unknown/Präprozessor/EOF hängen ausnahmslos an der Wurzel** (`AttachNonSignificantTokens`),
  nicht am umschließenden Knoten — exakt wie das heutige `PostprocessTokens`. Der Parser selbst sieht
  nur signifikante Token (Cursor überspringt die in `IsHidden` gelisteten Typen).
- **EOF** ist ein nullbreites Token am Textende, Parent = Wurzel, Klassifikation `Whitespace`.
- **Wurzel-Extent** = `[Start des ersten signifikanten Tokens … EOF-Position]`; ohne signifikante
  Token (leere Datei, nur Whitespace/Kommentar) `[eof, eof]`.
- **Leere Knoten-/Transitionsblöcke** ⇒ Extent `TextExtent.Missing` (Knoten existiert trotzdem).
- **`TransitionDefinitionBlock` gruppiert im Baum erst alle `TransitionDefinition`, dann alle
  `ExitTransitionDefinition`** — *nicht* in Quelltext-Reihenfolge (die `(... | ...)*` mischen darf).
- **`init`-Disambiguierung:** `init` gefolgt von einer Kante ⇒ Transition (`initSourceNode`), sonst
  Knoten-Deklaration (`StartsNodeDeclaration`).
- **Knoten-Extent** = Min-Start/Max-End über konsumierte signifikante Token + Kindknoten
  (`ExtentBuilder`); entspricht ANTLRs `context.Start/Stop`.

### Schritt B — Aufgaben (Error-Recovery + Diagnostics-Parität)

1. **Lexikalische Diagnostics** (heute in `SyntaxTree.cs` `PostprocessTokens` erzeugt) — **erledigt (B1)**:
   - `Nav0000UnexpectedCharacter` je `Unknown`-Token.
   - `Nav3000InvalidPreprocessorDirective` je `HashToken` **und** je `PreprocessorKeyword`.
   - `Nav3001…MustAppearOnFirstNonWhitespacePosition` für `HashToken`, wenn vor dem `#` in der Zeile
     nicht nur Whitespace steht (`SourceText.SliceFromLineStartToPosition(...).IsWhiteSpace()`).
   Umgesetzt in `NavParser.ReportLexicalDiagnostics` (aufgerufen aus `AttachNonSignificantTokens`).
   Die Location wird über `NavParser.LexicalLocation` gebaut — Start-/End-Zeilenposition **identisch**
   an der Token-Startposition (im Test-Formatter nullbreit), exakt wie ANTLRs `IToken.GetLocation`
   (nicht der Zeilen*bereich* aus `SourceText.GetLocation`). Reihenfolge je `#`: erst `Nav3001`,
   dann `Nav3000`. Das Gate (`NavParserDifferentialTests`) vergleicht jetzt zusätzlich die Diagnostics
   und stellt nur noch Dateien mit ANTLR-**Parser**-Recovery (`Nav0002`) zurück — die beiden reinen
   Präprozessor-Dateien (`PreprocessorDirective.nav`, `PreprocessorNotAtLineStart.nav`) laufen voll mit.
2. **`Eat` umgebaut — erledigt (B3):** bei Mismatch meldet `Eat` jetzt `missing '<token>'` (Insertion)
   und rückt **nicht** vor; die `null`-Rückgabe trägt nichts zum Extent bei (kein Token im Strom). Plus
   Panic-Mode `Recover(Func<bool> recovered)` (Deletion): überspringt unerwartete Token bis zum
   Wiedereinstiegs-/Anker-Token, meldet `unexpected input '<token>'` einmal je Lauf, garantiert
   Fortschritt. Übersprungene signifikante Token werden nicht beim Skippen, sondern abschließend in
   `AttachNonSignificantTokens` als `Skiped`-Token an die Wurzel gehängt (Reconciliation über die
   Start-Position — nichts geht verloren, Round-Trip lückenlos).
3. **Gestaffelte Sync-/Anchor-Sets — erledigt (B3):** `BreaksBody()` (= `}`/`task`/`taskref`/EOF) ist
   der äußere Anker jeder Body-Schleife (Connection-Points, Transitionen); lokale Recovery überlässt
   diese Token der äußeren Regel. Die Listen-Schleifen (`memberDeclaration*`, Connection-Points,
   Transitionen) prüfen je Durchlauf erst Start, dann Anker, sonst `Recover` — Fortschritt ist über
   die `Recover`-Garantie sichergestellt. Vor dem Body-`{` überspringt ein gezielter `Recover` bis zum
   `{`/Body-Start/Anker (so wird z.B. ein ungültiges `[params …]` im Task-Kopf geskippt, der echte
   Body aber geparst).
4. **Die zwei `NotifyErrorListeners`-Fälle — erledigt (B3):** `missing edge` (Source ohne Kante) und
   `missing target node` (Kante ohne Ziel) in `ParseTransitionDefinition` **und** — symmetrisch —
   `ParseExitTransitionDefinition`.
5. **Verifikationsstrategie für die Recovery — erledigt (B2):** zweiter Golden-Satz `*.hand.tokens/`
   `.hand.tree/.hand.diag` über `NavParserGoldenTests` (Klon von `SyntaxGoldenTests`, gespeist von
   `NavParser.Parse`, [Explicit] `UpdateGolden`). `NavParserDifferentialTests` bleibt das Gate für den
   Bereich `hand == ANTLR` (wohlgeformt + lexikalisch; `Nav0002`-Dateien via `Assert.Ignore`); die
   `.hand.*` pinnen den divergenten Recovery-Output. Die `.hand.*` werden von den bestehenden
   csproj-Content-Globs (`**\*.tokens` usw.) und der `.gitattributes` (`-text`) automatisch miterfasst.
6. **Tipp-Präfix-Test — erledigt (B3b):** `NavParserGoldenTests.ParsesAndRoundTripsAllTypingPrefixes` —
   jedes Präfix jeder Korpus-Datei parst ohne Exception und round-trippt lückenlos (kein
   ANTLR-Vergleich, da Recovery divergiert). Dazu der korpusweite `TokenStreamRoundTrips`.

> **Erwartung:** An den `.diag`-Golden (`Syntax\Tests\*.nav.diag`) wird es **bewusste, reviewbare
> Diffs** geben — der Handparser darf bessere Meldungen liefern als ANTLR. Diffs gemeinsam prüfen,
> nicht blind übernehmen. ANTLR liefert z.B. `Nav0002 no viable alternative` (so in `StringsAndGenerics.nav`
> bei `taskref … [params …]` und massenhaft in `LargeNav.nav`) — diese ANTLR-spezifischen Meldungen sind
> *nicht* 1:1-Ziel.

### Korpus-Baseline (in Session zu B1 gemessen — Beleg, dass der Ansatz trägt)

Ad-hoc-Lauf über **alle 1912 `.nav` unter `D:\tfs\Main`** (temporärer `[Explicit]`-Harness im
Testprojekt, danach wieder entfernt — `NavParser.Parse` vs. `SyntaxTree.ParseText`):

- **0 Abstürze** im Handparser über den gesamten Korpus.
- **0 unerwartete Abweichungen**: Für **alle** Dateien ohne ANTLR-`Nav0002` (1908 Stück) sind Baum,
  Token-Strom, Diagnostics **und** Round-Trip byte-identisch zu ANTLR. Nur die 4 `Nav0002`-Dateien
  weichen ab (= die noch ausstehende Parser-Recovery).
- **Performance ~2x**: net10.0 Release ANTLR 251 ms vs. Handparser 122 ms je Volllauf (Debug
  611 ms vs. 304 ms) — der Gewinn kommt aus dem Wegfall von Visitor + `PostprocessTokens`-Reconciliation.

So lässt sich der Lauf reproduzieren: `[Explicit]`-NUnit-Test im Testprojekt, der den Korpus
rekursiv einliest (Endung exakt `.nav`), je Datei beide Parser aufruft, Ausnahmen sammelt, die
Dump-Helfer aus `NavParserDifferentialTests` zum Vergleich nutzt und mit `Stopwatch` (Warmup + 3
Iterationen) misst.

### Verifikation (beide TFMs Pflicht)

- net10.0: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`
- net472: Testprojekt mit `dotnet build …\Nav.Language.Tests.csproj -f net472` bauen, dann
  `_build\nunit.consolerunner\3.8.0\tools\nunit3-console.exe Nav.Language.Tests\bin\Debug\Nav.Language.Tests.dll`.
- **Neue `.cs` als UTF-8 mit BOM anlegen** (der Write-Standard erzeugt *ohne* BOM — danach umkodieren).
