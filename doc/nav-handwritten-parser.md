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
3. Error-Recovery + Diagnostics-Parität; Golden-/Snapshot-Tests als Gate. — **als Nächstes (Schritt B)**.
4. *(Später, separat)* Trivia-Modell auf angehängt + strukturiert; Direktiven als strukturierte Trivia.

---

## Stand & Handoff (Schritt A erledigt → Schritt B als Nächstes)

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

1. **Diagnostics, die Schritt A bewusst noch NICHT emittiert** (heute in `SyntaxTree.cs`
   `PostprocessTokens` erzeugt — dort als Vorlage nachlesen, solange der ANTLR-Pfad noch lebt):
   - `Nav0000UnexpectedCharacter` je `Unknown`-Token.
   - `Nav3000InvalidPreprocessorDirective` je `HashToken` **und** je `PreprocessorKeyword`.
   - `Nav3001…MustAppearOnFirstNonWhitespacePosition` für `HashToken`, wenn vor dem `#` in der Zeile
     nicht nur Whitespace steht (`SourceText.SliceFromLineStartToPosition(...).IsWhiteSpace()`).
   Diese gehören in/neben `AttachNonSignificantTokens` (dort werden genau diese Token-Typen behandelt).
2. **`Eat` umbauen:** bei Mismatch heute `null` ohne Vorrücken → künftig zero-width
   `SyntaxToken.Missing` + Diagnose, **kein** Vorrücken (Insertion). Plus `SkipTo`/Panic-Mode, der
   überzählige Token als `Skiped`-Trivia konsumiert (Deletion) + Diagnose, mit garantiertem Fortschritt.
3. **Gestaffelte Sync-/Anchor-Sets:** `;` lokal, `}`/`task`/`taskref` der äußeren Regel überlassen
   (Anchor-Stack wie Roslyns C#-Parser). Listen-Schleifen brauchen die Fortschritts-Garantie
   (mind. ein Token pro Durchlauf) — heute implizit erfüllt, nach dem Umbau explizit absichern.
4. **Die zwei `NotifyErrorListeners`-Fälle** aus `NavGrammar.g4` `transitionDefinition` nachbilden:
   „missing edge" (nur `sourceNode`) und „missing target node" (`sourceNode edge` ohne Ziel).
5. **Gate erweitern** (`NavParserDifferentialTests`): die heute via `Assert.Ignore` übersprungenen
   kaputten Korpus-Dateien einbeziehen; zusätzlich einen **Tipp-Präfix-Test** (jedes Präfix jeder
   Datei muss matchen) wie in `NavLexerDifferentialTests`.

> **Erwartung:** An den `.diag`-Golden (`Syntax\Tests\*.nav.diag`) wird es **bewusste, reviewbare
> Diffs** geben — der Handparser darf bessere Meldungen liefern als ANTLR. Diffs gemeinsam prüfen,
> nicht blind übernehmen. ANTLR liefert z.B. `Nav0002 no viable alternative` (so in `StringsAndGenerics.nav`
> bei `taskref … [params …]` und massenhaft in `LargeNav.nav`) — diese ANTLR-spezifischen Meldungen sind
> *nicht* 1:1-Ziel.

### Verifikation (beide TFMs Pflicht)

- net10.0: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`
- net472: Testprojekt mit `dotnet build …\Nav.Language.Tests.csproj -f net472` bauen, dann
  `_build\nunit.consolerunner\3.8.0\tools\nunit3-console.exe Nav.Language.Tests\bin\Debug\Nav.Language.Tests.dll`.
- **Neue `.cs` als UTF-8 mit BOM anlegen** (der Write-Standard erzeugt *ohne* BOM — danach umkodieren).
