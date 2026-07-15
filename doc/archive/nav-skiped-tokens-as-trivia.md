# Skiped-Token als strukturierte Trivia (Handoff)

> Selbsttragender Einstieg für eine **frische Session ohne Gesprächskontext.** Ergänzt
> `doc/nav-kolibri.md` (Parser „Kolibri", Trivia-Modell) und `doc/nav-weg-b-structured-trivia.md` +
> `doc/nav-directive-subparser.md` (das Direktiv-Modell, das als Blaupause diente). Quelle der
> Wahrheit bleibt der Code; Pfade sind Einstieg (Stand 2026-07-02).
>
> **Status: UMGESETZT.** Beide TFMs grün (net10.0 1236/0, net472 1244/0), Golden-Snapshots
> regeneriert. Der ursprüngliche Plan steht unten fort — wo die Umsetzung abweicht, ist es
> vermerkt.
>
> **Scope-Entscheid:** Der Umbau erfasst **beide** Skiped-Quellen — vom Panik-Modus übersprungene
> signifikante Token **und** lexikalisch unbekannte Zeichen (`Unknown`).

## Warum

Bis zu diesem Umbau hingen vom Parser **übersprungene** Token (Panik-Modus-Recovery, z.B. das `[]`
in `init [];`) sowie lexikalisch **unbekannte** Zeichen als vollwertige `SyntaxToken` mit
`TextClassification.Skiped` **direkt an der Wurzel** (`CodeGenerationUnitSyntax`) im flachen
`SyntaxTree.Tokens`-Strom — erzeugt im damaligen `NavParser.AttachNonSignificantTokens`. Das
sicherte die lückenlose Round-Trip-Abdeckung des Quelltexts, war aber **nicht** das Roslyn-Modell:
dort sind übersprungene Token *Trivia* (`SkippedTokensTrivia`), die an einem Nachbar-Token hängen
und ihre Token nur lokal führen.

Die rote Darstellung im Editor (z.B. die Klammern in `init [];`) entsteht aus genau dieser
`Skiped`-Klassifikation (der VS-Classifier bildet sie auf „Syntax Error" ab) **plus** dem separaten
Diagnose-Squiggle. Beides bleibt nach dem Umbau unverändert.

Ziel (erreicht): Skiped-Token aus dem flachen Strom herausnehmen und als **strukturierte Trivia**
führen — neuer `SyntaxTokenType.SkippedTokensTrivia` mit einem `SkippedTokensTriviaSyntax`-Knoten,
der die Skip-Token **lokal** hält. `SyntaxTree.Tokens` enthält jetzt nur noch echte, geparste Token
(plus das `EndOfFile`); Fehler-/Skip-Text lebt in strukturierter Trivia und bleibt über
`GetStructure()` erreichbar (`SyntaxTree.SkippedTokens()`).

## Die Blaupause existiert bereits (Direktiven)

Die komplette „strukturierte Trivia"-Maschinerie war für Präprozessor-Direktiven (`#version`) schon
gebaut und diente 1:1 als Vorlage:

- `SyntaxTrivia` trägt optional einen `_structure`-Knoten (`GetStructure()`/`HasStructure`) —
  `Nav.Language\Syntax\SyntaxTrivia.cs`.
- Das Lokale-Token-Muster (`SetLocalTokens()`/`ChildTokens()`) lag in `DirectiveTriviaSyntax` und
  ist im Zuge des Umbaus in die gemeinsame Basis `StructuredTriviaSyntax` gewandert —
  `Nav.Language\Syntax\StructuredTriviaSyntax.cs`.
- `BuildTrivia()` faltet jeden Direktiv-Lauf zu **einem** `DirectiveTrivia`-Stück (Extent deckt den
  Text, innere Roh-Token werden übersprungen: `index = directive.RawEnd - 1`) — der neue
  Skip-Zweig (`FoldSkippedRun`) arbeitet symmetrisch dazu (`Nav.Language\Syntax\NavParser.cs`).
- Konsumenten lesen strukturierte Trivia über `syntaxTree.Directives()` /
  `syntaxTree.SkippedTokens()` / `syntaxTree.Comments()` (`SyntaxTree.cs`), z.B. der VS-Classifier
  (`SyntacticClassificationTagger.cs`).
- Der Round-Trip rekonstruiert Trivia **über die Extent** (`trivia.ToString(source)`), nicht über
  lokale Token — `SyntaxGoldenTests.RoundTrip`. Eine Skip-Trivia mit korrekter Extent round-trippt
  daher genauso sauber wie eine DirectiveTrivia. **Der Round-Trip bricht nicht** (verifiziert).

## Die eine echte Hürde (Architektur-Entscheidung) — so umgesetzt

**Direktiven sind vor dem Parsen bekannt, Skips erst danach.** `BuildTrivia()` lief früher im
Konstruktor **vor** dem Parse-Lauf, weil Token inline während des Parsens mit ihrer Trivia gebaut
wurden. Welche signifikanten Token *übersprungen* werden, steht aber erst **nach** dem Parsen fest
(Differenz aus `_raw` und den konsumierten `_tokens`).

**Umgesetzter Ansatz — Trivia-Finalisierung nach dem Parsen (direktiv-symmetrisch):**

1. Während des Parsens sammelt `Tok()` die **konsumierten** signifikanten Token in `_tokens` —
   zunächst **ohne** Trivia (statt mit vorläufiger; die Trivia wird nur einmal gebaut).
2. Nach dem Parsen bestimmt `FinalizeTrivia()` die konsumierten Start-Positionen und baut die
   Trivia in einem Lauf: `BuildTrivia()` (jetzt Instanz-Methode) erkennt **Skip-Läufe** — maximale
   Läufe benachbarter Roh-Token, die unkonsumierte signifikante Token oder `Unknown` sind. Reine
   Trivia zwischen ihnen bricht den Lauf nicht (sie fällt in dessen Extent); ein Direktiv-Lauf, ein
   konsumiertes Token oder das Dateiende beendet ihn.
3. `FoldSkippedRun()` setzt — analog zum Direktiv-Zweig — pro Skip-Lauf genau ein
   `SkippedTokensTrivia`-Stück in `_allTrivia` ein (Extent = `[erstesSkipStart, letztesSkipEnd)`)
   und baut den `SkippedTokensTriviaSyntax`-Knoten mit **lokalen** Token (Klassifikation `Skiped`).
   Ein Skip-Lauf ist dabei — wie ein Direktiv-Lauf — **kein Trenner**: das Stück fließt in den
   umgebenden Trivia-Lauf und hängt nach der Roslyn-Regel am Nachbar-Token (gleiche Zeile ⇒
   Trailing des Vorgängers, z.B. `init` bei `init [];`).
4. Der **Finalisierungs-Pass** in `FinalizeTrivia()` setzt jedes (Struct-)Token in `_tokens` mit
   frisch nachgeschlagener `LookupTrivia(token.Start)` neu. Baumstruktur/Token-Identitäten ändern
   sich nicht (die Token-Gleichheit klammert Trivia ohnehin aus) — nur die Trivia-Slices.
   Skip-Token landen **nicht** mehr in `_tokens`.
5. `ReportLexicalDiagnostics` (Nav0000 für `Unknown`) läuft jetzt in `FoldSkippedRun()`.
6. `AttachNonSignificantTokens` ist auf `AttachEndOfFile` zusammengeschrumpft (nur noch das
   abschließende `EndOfFile` mit der finalen Datei-Trivia als Leading); `FinalizeDirectives` heißt
   jetzt `FinalizeStructuredTrivia` und schließt auch die Skip-Knoten an den Baum an
   (`FinalConstruct`, Parent = Wurzel).

Das war gegenüber vorher eine Umstellung der **Reihenfolge** (Trivia final nach dem Parsen bauen)
plus ein zum Direktiv-Zweig symmetrischer Falt-Zweig — kein neues Konzept.

## Umgesetzte Änderungen

### Neue Bausteine (klein, nach Direktiv-Vorlage)
- **`SyntaxTokenType.SkippedTokensTrivia`** — neuer Enum-Wert (`= 55`,
  `Nav.Language\Syntax\SyntaxTokenType.cs`).
- **`StructuredTriviaSyntax`** — neue gemeinsame abstrakte Basis (das Lokale-Token-Muster
  `SetLocalTokens`/`ChildTokens()` ließ sich sauber teilen): `DirectiveTriviaSyntax` und der neue
  Knoten erben beide davon (`Nav.Language\Syntax\StructuredTriviaSyntax.cs`).
- **`SkippedTokensTriviaSyntax : StructuredTriviaSyntax`** — `sealed partial` (Visitor-
  Quellgenerator!), ohne `[SampleSyntax]` (kein Grammatikregel-Knoten).
- **`SyntaxFacts.IsTrivia(SyntaxTokenType)`** um `SkippedTokensTrivia` erweitert.
  `IsTrivia(TextClassification)` blieb unverändert — `Skiped` darf **keine**
  Trivia-Classification sein, sonst färbt der Classifier die lokalen Skip-Token nicht mehr rot ein.

### Erzeuger (Kern der Arbeit)
- **`NavParser`** (`NavParser.cs`): siehe oben — `FinalizeTrivia` (nach dem Parsen),
  skip-bewusstes `BuildTrivia` + `IsSkippedToken`/`FoldSkippedRun`, `AttachEndOfFile`,
  `FinalizeStructuredTrivia`. `IsPreprocessorToken`/`IsHidden` blieben unverändert (der
  IsHidden-Trenner-Zweig deckt nur noch den theoretischen Fall eines Präprozessor-Tokens außerhalb
  eines Direktiv-Laufs).
- Der **direktiv-interne** Skiped-Überschuss (`NavDirectiveParser`) blieb wie er war — er
  steckt bereits in lokalen Direktiv-Token.

### Konsumenten
- **`SyntaxTree`**: Helfer `SkippedTokens()` analog zu `Directives()` (über
  `DescendantTrivia().Where(HasStructure)` … `OfType<SkippedTokensTriviaSyntax>()`).
- **`SyntacticClassificationTagger`** (VS, `.ExtensionShared`): zusätzliche Schleife über
  `syntaxTree.SkippedTokens()` → deren `ChildTokens()` mit `Skiped`-Classification — exakt wie der
  bestehende `Directives()`-Block. **Rot bleibt rot.**
- **`SemanticTokensBuilder`** (LSP): unverändert gelassen — die `Skiped → None`-Zeile ist **kein**
  toter Pfad, sie greift weiterhin für den direktiv-lokalen Skiped-Überschuss. VS-Code-Verhalten
  (nur Squiggle, keine Semantic-Token-Farbe für die Klammern) bleibt gleich.
- **`NavCompletionContext`** (Haupt-Risiko, kompensiert): Die Kontext-Token-Suche läuft jetzt über
  eine **gemergte Sicht** — `TokenLeftOf()` wählt zwischen dem letzten konsumierten Token (Binärsuche
  über `tree.Tokens`) und dem letzten übersprungenen Token aus der Skip-Trivia
  (`LastSkippedTokenStartingBefore`) den näher an der Position beginnenden Kandidaten. Das
  reproduziert den alten (vollen) Strom exakt; insbesondere bleibt der `CodeBlock`-Kontext am
  übersprungenen `[` eines leeren `[]` und der Partial-Edge-Rücklauf (`-`, `--`, `==` …) erhalten.
- **`ClassifiedTextExtensions.GetClassifiedText`**: `SkippedTokensTrivia` wird auf
  `TextClassification.Skiped` abgebildet (vorher trugen die Skip-Token ihre Klassifikation selbst).
- **`BraceMatchingTagger`** / **`DebugQuickInfoSource`** (`.ExtensionShared`): Fehler-Klammern per
  `FindAtPosition` nicht mehr im Strom — für Error-Klammern akzeptabel; beide behandeln `Missing`
  bereits (geprüft, kein Fix nötig).

### Tests / Golden-Snapshots
- **`SyntaxGoldenTests`**: `DumpTokens` verlor die Skiped-Zeilen, `DumpTrivia` gewann die
  `SkippedTokensTrivia`-Einträge, `.tree` verlor die Skip-Token in der Wurzel-Token-Liste
  (Regeneration via `[Explicit] UpdateGolden`). Die Round-Trip-Tests (`TokenStreamRoundTrips`,
  `ParsesAndRoundTripsAllTypingPrefixes`) blieben **ohne** Anpassung grün — die zentrale
  Korrektheits-Absicherung. Innere Trivia eines Skip-Laufs (Whitespace/Zeilenenden zwischen den
  Skip-Token) fällt in dessen Extent und erscheint nicht mehr als eigenes Trivia-Stück.
- Erwartungs-Updates: `SyntaxNodeTriviaTests` (BOM liegt jetzt als Skip-Trivia am ersten Token),
  `SyntaxTreeAllRulesTests.TestAllSyntaxesPresent` (49 Knotentypen, Skip-Schnipsel),
  `SyntaxWalkerTests` (Skip-Schnipsel einspeisen). `LanguageVersionTests` blieb unverändert
  (Direktiv-Überschuss ist lokal).
- **Neuer Test** `TokenTriviaTests.SkippedTokensAreStructuredTriviaNotInTokenStream`: für
  `init [];` — kein `OpenBracket`/`CloseBracket` in `tree.Tokens`; die Skip-Trivia hängt (Roslyn-
  Regel, gleiche Zeile) als **Trailing** von `init`, `GetStructure()` ist der
  `SkippedTokensTriviaSyntax` mit den beiden Klammern (Classification `Skiped`) als lokalen Token.
  Auf **net472 und net10** grün.

## Verifikation

1. `nav build` — Solution grün (0 Warnungen/0 Fehler). ✔
2. `nav test` (net472: 1244/0) und `dotnet test … -f net10.0` (1236/0) — beide TFMs grün, inkl.
   Round-Trip- und Golden-Tests. ✔
3. `UpdateGolden` gelaufen, Diffs gesichtet: Skip-Zeilen wandern von `.tokens` nach `.trivia`
   (je Lauf ein Stück), Wurzel-Token-Listen in `.tree` schrumpfen. ✔
4. VS-Extension (manuell, offen): `init [];` öffnen — Klammern weiterhin **rot**, Tooltip
   „expected 'params' or 'abstractmethod'" unverändert.
5. LSP-Smoke (manuell, offen): Diagnose (Squiggle) für `init [];` unverändert; keine
   Semantic-Token-Farbe für die Klammern (wie bisher).
6. Completion an Fehlerstellen: über die Completion-Tests abgedeckt (u.a. leeres `[]` je Wirt,
   Partial-Edge); zusätzlich die gemergte Sicht in `NavCompletionContext` (siehe oben).
