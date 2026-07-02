# Skiped-Token als strukturierte Trivia (Plan & Handoff)

> Selbsttragender Einstieg für eine **frische Session ohne Gesprächskontext.** Ergänzt
> `doc/nav-kolibri.md` (Parser „Kolibri", Trivia-Modell) und `doc/nav-weg-b-structured-trivia.md` +
> `doc/nav-directive-subparser.md` (das Direktiv-Modell, das hier als Blaupause dient). Quelle der
> Wahrheit bleibt der Code; Pfade/Zeilen sind Einstieg (zum Erhebungszeitpunkt verifiziert,
> Stand 2026-07-02).
>
> **Status: geplant, noch nicht umgesetzt.** Der folgende Plan ist genehmigungs- bzw. startbereit.
>
> **Scope-Entscheid:** Der Umbau erfasst **beide** Skiped-Quellen — vom Panik-Modus übersprungene
> signifikante Token **und** lexikalisch unbekannte Zeichen (`Unknown`).

## Warum

Heute hängen vom Parser **übersprungene** Token (Panik-Modus-Recovery, z.B. das `[]` in `init [];`)
sowie lexikalisch **unbekannte** Zeichen als vollwertige `SyntaxToken` mit
`TextClassification.Skiped` **direkt an der Wurzel** (`CodeGenerationUnitSyntax`) im flachen
`SyntaxTree.Tokens`-Strom — erzeugt in `NavParser.AttachNonSignificantTokens`
(`Nav.Language\Syntax\NavParser.cs:2078`). Das sichert die lückenlose Round-Trip-Abdeckung des
Quelltexts, ist aber **nicht** das Roslyn-Modell: dort sind übersprungene Token *Trivia*
(`SkippedTokensTrivia`), die an einem Nachbar-Token hängen und ihre Token nur lokal führen.

Die rote Darstellung im Editor (z.B. die Klammern in `init [];`) entsteht aus genau dieser
`Skiped`-Klassifikation (der VS-Classifier bildet sie auf „Syntax Error" ab) **plus** dem separaten
Diagnose-Squiggle. Beides bleibt nach dem Umbau unverändert.

Ziel: Skiped-Token aus dem flachen Strom herausnehmen und als **strukturierte Trivia** führen —
neuer `SyntaxTokenType.SkippedTokensTrivia` mit einem `SkippedTokensTriviaSyntax`-Knoten, der die
Skip-Token **lokal** hält. `SyntaxTree.Tokens` enthält danach nur noch echte, geparste Token;
Fehler-/Skip-Text lebt in strukturierter Trivia und bleibt über `GetStructure()` erreichbar.

## Die Blaupause existiert bereits (Direktiven)

Die komplette „strukturierte Trivia"-Maschinerie ist für Präprozessor-Direktiven (`#version`) schon
gebaut und dient 1:1 als Vorlage:

- `SyntaxTrivia` trägt optional einen `_structure`-Knoten (`GetStructure()`/`HasStructure`) —
  `Nav.Language\Syntax\SyntaxTrivia.cs`.
- `DirectiveTriviaSyntax` hält seine Token **lokal** via `SetLocalTokens()` und liefert sie über
  `ChildTokens()` — `Nav.Language\Syntax\DirectiveTriviaSyntax.cs`.
- `BuildTrivia()` faltet jeden Direktiv-Lauf zu **einem** `DirectiveTrivia`-Stück (Extent deckt den
  Text, innere Roh-Token werden übersprungen: `index = directive.RawEnd - 1`) —
  `Nav.Language\Syntax\NavParser.cs:2212–2297` (Direktiv-Zweig ab `:2243`).
- Konsumenten lesen strukturierte Trivia bereits über `syntaxTree.Directives()` /
  `syntaxTree.Comments()` (`SyntaxTree.cs:64–80`), z.B. der VS-Classifier
  (`SyntacticClassificationTagger.cs:76–93`).
- Der Round-Trip rekonstruiert Trivia **über die Extent** (`trivia.ToString(source)`), nicht über
  lokale Token — `SyntaxGoldenTests.RoundTrip` (`Nav.Language.Tests\Syntax\SyntaxGoldenTests.cs:377`).
  Eine Skip-Trivia mit korrekter Extent round-trippt daher genauso sauber wie eine DirectiveTrivia.
  **Der Round-Trip bricht nicht.**

## Die eine echte Hürde (Architektur-Entscheidung)

**Direktiven sind vor dem Parsen bekannt, Skips erst danach.** `BuildTrivia()` läuft heute im
Konstruktor **vor** dem Parse-Lauf (`NavParser.cs:95`), weil Token inline während des Parsens mit
ihrer Trivia gebaut werden. Welche signifikanten Token *übersprungen* werden, steht aber erst
**nach** dem Parsen fest (Differenz aus `_raw` und den konsumierten `_tokens` — heute in
`AttachNonSignificantTokens`).

**Empfohlener Ansatz — Trivia-Finalisierung nach dem Parsen (direktiv-symmetrisch):**

1. Während des Parsens die **konsumierten** signifikanten Token wie bisher in `_tokens` sammeln
   (Token dürfen zunächst mit vorläufiger Trivia gebaut werden — die endgültige Trivia wird am Ende
   neu gesetzt).
2. Nach dem Parsen die **Skip-Läufe** bestimmen: maximale Läufe benachbarter Roh-Token, die entweder
   (a) unkonsumierte signifikante Token oder (b) `Unknown` sind. Reine Trivia zwischen ihnen bricht
   den Lauf nicht (sie fällt in dessen Extent).
3. `BuildTrivia()` **skip-bewusst** machen: pro Skip-Lauf — analog zum Direktiv-Zweig — genau ein
   `SkippedTokensTrivia`-Stück in `_allTrivia` einsetzen (Extent = `[erstesSkipStart, letztesSkipEnd)`),
   die inneren Roh-Token überspringen und einen `SkippedTokensTriviaSyntax`-Knoten mit **lokalen**
   Token (Klassifikation `Skiped`) bauen.
4. Eine **Finalisierungs-Pass** über `_tokens`: jedes (Struct-)Token mit frisch nachgeschlagener
   `LookupTrivia(token.Start)` neu setzen. Baumstruktur/Token-Identitäten ändern sich nicht — nur die
   Trivia-Slices. Skip-Token landen **nicht** mehr in `_tokens`.
5. `ReportLexicalDiagnostics` (Nav0000 für `Unknown`) bleibt erhalten, nur an die neue Stelle
   verschoben.

Das ist gegenüber heute eine Umstellung der **Reihenfolge** (Trivia final nach dem Parsen bauen) plus
ein zum Direktiv-Zweig symmetrischer Falt-Zweig — kein neues Konzept.

## Umzusetzende Änderungen

### Neue Bausteine (klein, nach Direktiv-Vorlage)
- **`SyntaxTokenType.SkippedTokensTrivia`** — neuer Enum-Wert (`Nav.Language\Syntax\SyntaxTokenType.cs`).
- **`SkippedTokensTriviaSyntax : SyntaxNode`** — neuer Knoten mit `SetLocalTokens`/`ChildTokens()`,
  gebaut wie `DirectiveTriviaSyntax`/`VersionDirectiveSyntax`. Muss **nicht** von
  `DirectiveTriviaSyntax` erben (Direktive und Skip sind fachlich verschieden); gemeinsame Basis nur,
  falls sich das Lokale-Token-Muster sauber teilen lässt.
- **`SyntaxFacts.IsTrivia(SyntaxTokenType)`** um `SkippedTokensTrivia` erweitern (`SyntaxFacts.cs:213`).
  `IsTrivia(TextClassification)` (`:204`) bleibt unverändert — `Skiped` darf **kein**
  Trivia-Classification sein, sonst färbt der Classifier die lokalen Skip-Token nicht mehr rot ein.

### Erzeuger (Kern der Arbeit)
- **`NavParser`** (`NavParser.cs`): Konstruktor-Reihenfolge, `BuildTrivia` (Skip-Falt-Zweig +
  Skip-Lauf-Erkennung), `AttachNonSignificantTokens` (hängt Skip-Token nicht mehr an die Wurzel),
  neue Trivia-Finalisierungs-Pass. `IsPreprocessorToken`/`IsHidden` ggf. anpassen.
- Der **direktiv-interne** Skiped-Überschuss (`NavDirectiveParser.cs:290`) bleibt wie er ist — er
  steckt bereits in lokalen Direktiv-Token.

### Konsumenten
- **`SyntaxTree`** (`SyntaxTree.cs`): Helfer `SkippedTokens()` analog zu `Directives()` (über
  `DescendantTrivia().Where(HasStructure)` … `OfType<SkippedTokensTriviaSyntax>()`).
- **`SyntacticClassificationTagger`** (VS, `.ExtensionShared`): zusätzliche Schleife über
  `syntaxTree.SkippedTokens()` → deren `ChildTokens()` mit `Skiped`-Classification einfärben — exakt
  wie der bestehende `Directives()`-Block (`:76–93`). **Rot bleibt rot.**
- **`SemanticTokensBuilder`** (LSP): mappt `Skiped` ohnehin auf `None`; da Skip-Token den flachen
  Strom verlassen, entfällt der Zweig faktisch. Nur toten Pfad/Kommentar bereinigen
  (`SemanticTokensBuilder.cs:57`). VS-Code-Verhalten (nur Squiggle, keine Semantic-Token-Farbe für die
  Klammern) bleibt gleich.
- **`NavCompletionContext`** (`Completion\`, Binärsuche über `tree.Tokens`) — **Haupt-Risiko**: die
  Kontext-Token-Suche sieht Skip-Token nicht mehr. An Fehlerstellen kann sich das Verhalten ändern.
  Über die Completion-Tests absichern; ggf. an der relevanten Stelle zusätzlich die angrenzende
  Skip-Trivia berücksichtigen.
- **`BraceMatchingTagger`** / **`DebugQuickInfoSource`** (`.ExtensionShared`): Fehler-Klammern per
  `FindAtPosition` nicht mehr im Strom — für Error-Klammern akzeptabel; nur prüfen, kein Fix nötig.

### Tests / Golden-Snapshots
- **`SyntaxGoldenTests`** (`Nav.Language.Tests\Syntax\`): `DumpTokens` verliert die Skiped-Zeilen
  (10 `.tokens`-Dateien, ~40 Zeilen), `DumpTrivia` gewinnt die neuen `SkippedTokensTrivia`-Einträge.
  Regeneration über den vorhandenen `[Test, Explicit] UpdateGolden`-Lauf (`:208`). Die
  Round-Trip-Tests (`TokenStreamRoundTrips`, `ParsesAndRoundTripsAllTypingPrefixes`, `:66/:79`) müssen
  **ohne** Anpassung grün bleiben — sie sind die zentrale Korrektheits-Absicherung.
- Gezielte Erwartungs-Updates: `SyntaxNodeTriviaTests.cs:244` (BOM/`Unknown`),
  `LanguageVersionTests.cs:80/98` (Direktiv-Überschuss bleibt lokal — vermutlich unverändert).
- **Neuer Test** (Muster: `TokenTriviaTests.PreprocessorDirectiveIsStructuredTriviaNotInTokenStream`,
  `:113`): für `init [];` — kein `OpenBracket`/`CloseBracket` in `tree.Tokens`; das Folge-Token trägt
  ein `SkippedTokensTrivia` mit `HasStructure == true` und `GetStructure()` vom Typ
  `SkippedTokensTriviaSyntax`, dessen `ChildTokens()` die beiden Klammern (Classification `Skiped`)
  liefert. Auf **net472 und net10** grün.

## Verifikation

1. `nav build` (Solution baut nur mit MSBuild.exe — `nav test` baut die Engine **nicht** vor).
2. `nav test` (net472) und `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` —
   beide TFMs grün. Besonders auf die Round-Trip- und Golden-Tests achten.
3. `UpdateGolden` explizit laufen lassen, Diffs der `.tokens`/`.trivia` sichten (Skip-Zeilen wandern
   von `.tokens` nach `.trivia`), dann Tests erneut grün.
4. VS-Extension: `init [];` öffnen — Klammern weiterhin **rot**, Tooltip
   „expected 'params' or 'abstractmethod'" unverändert.
5. LSP-Smoke gegen `nav.lsp`: Diagnose (Squiggle) für `init [];` unverändert; keine Semantic-Token-
   Farbe für die Klammern (wie bisher).
6. Completion an/neben Fehlerstellen stichprobenartig prüfen (Haupt-Risiko).

## Aufwand

Mittlerer Umbau (~1–1,5 Tage). Der Löwenanteil steckt in `NavParser` (Reihenfolge + skip-bewusstes
`BuildTrivia` + Finalisierungs-Pass) und in der Completion-Absicherung; alles andere folgt dem
etablierten Direktiv-Muster.
