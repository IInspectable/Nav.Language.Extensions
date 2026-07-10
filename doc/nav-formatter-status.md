# Nav Code-Formatter — Spezifikation & Status

> **Spezifikations- und Status-Dokument.** Beschreibt den **Soll-Zustand** des Formatters; verworfene
> Alternativen sind dort vermerkt, wo sie zur jeweiligen Entscheidung gehören (nicht als Chronik).
> Stand: **S5 umgesetzt** — der Formatter ist damit als Engine-Kern (v1) vollständig. Neu die **Selektion**:
> `FormatRange` ist ein **gefiltertes** `FormatDocument`
> (`FormatRange(x, r) ≡ { c ∈ FormatDocument(x) : c.Extent ⊆ ExpandRange(r) }`) — der Range rastet auf ganze
> Zeilen ein und weitet sich auf die Anweisungs-/Member-Knoten aus, die er schneidet (inkl. der vorangehenden
> Lücke, die den Einzug des Knotens setzt); alle nicht-lokalen Pässe laufen über die volle Datei, der
> Final-Gap unterliegt demselben `⊆`-Filter. Subset-/Monotonie-Garantie und `FormatRange(x, ganzeDatei) ==
> FormatDocument(x)` folgen konstruktiv. Voriger Stand (S4, weiter gültig): `FormatterSuppression`-Vorpass
> (verbatim bei fehlendem `;`/`}`, Skiped/Direktive im Statement, Error-Diagnostik — Code-Block-Inhaltsfehler
> und BOM-`Nav0000`@0 ausgenommen; Global-Fallback), der **Hand-gelegt-Delta-Shift** (äußerer Einzug
> mehrzeiliger Anweisungen) und der gleichgebaute Delta-Shift der Innenzeilen mehrzeiliger
> `/* */`-Kommentare (ein Mechanismus `ShiftInteriorLines`, zwei Abnehmer) sowie der **Laufzeit-Wächter**
> (Achse A: Token-Strom + Direktiven + keine neuen Error-Diagnostics) — seit dem Perf-Pass **Opt-in**
> (`NavFormattingOptions.VerifyResult`, Default aus; nur die Tests schalten ihn ein, weil der Re-Parse Parse
> und Apply grob verdoppelt, s. „Korrektheits-Modell"). Korpus-Smoke über alle 1913 `.nav` × 2 Einzugsstile:
> 0 Idempotenz-/Token-/Direktiv-/Fehler-Brüche, 0 Crashes, Wächter feuert nie.
> **Host-Anbindung VS + VS Code erledigt** (VS Format-Document/Selection-Command + Symbolleisten-Schaltfläche;
> LSP `textDocument/formatting`+`rangeFormatting`, siehe „Zurückgestellt"); offen bleiben nur noch MCP
> `nav_format` und das CLI-`format`-Verb.

## Motivation

Die Nav-Sprache hat keinen Formatter. Gewünscht ist einer, der

- **wahlweise die ganze Datei oder nur die Selektion** normalisiert,
- sich bei **Syntaxfehlern / Unknown / Skiped-Token** robust verhält (nichts kaputt machen),
- **Kommentare** sinnvoll behandelt,
- und ein **empirisch belegtes** Zielformat erzeugt.

Der Formatter wird ein weiterer VS-freier **Feature-Kern in `Nav.Language`** nach dem etablierten
Muster (statischer `Nav<Feature>Service`, Ausgabe `IReadOnlyList<TextChange>`, kanonische Defaults als
„single authority" im Kern — vgl. `Nav.Language/Completion/NavCompletionService.cs`). Eingabe ist —
bewusste Abweichung vom Completion-Muster, das Semantik braucht — der **`SyntaxTree`**, nicht die
`CodeGenerationUnit`: der Formatter ist rein syntaktisch (Token, Trivia, Syntax-Diagnostics liegen alle
am `SyntaxTree`) und erzwingt so keinen Semantik-Build; Hosts mit `CodeGenerationUnit` reichen deren
Syntaxbaum durch. Alle Hosts (LSP/MCP/VS/CLI) können ihn später anbinden.

**Scope erste Ausbaustufe (v1): nur Engine-Kern + Tests.** Host-Anbindung ist bewusst zurückgestellt.

## Ermitteltes Zielformat (empirisch aus einer großen realen `.nav`-Codebasis)

Statistisch über einen großen realen `.nav`-Bestand ermittelt — **Quelle der Wahrheit: `d:\tfs\main`
(~1900 `.nav`-Dateien)**; die im Repo eingecheckte `Nav.Language.Tests\Resources\LargeNav.nav` ist ein
repräsentatives Einzelstück daraus. Die folgenden Anteile sind die dominanten Konventionen:

- **Einzug:** Tab (Breite 4) dominiert (~66% der Dateien tab-lastig) vs. 4 Spaces (~33%); starke
  **Intra-Datei-Mischung** → der Formatter **normalisiert** auf den konfigurierten Stil.
- **Klammern:** Allman (öffnende `{` in eigener Zeile) ~98,5%; schließende `}` immer eigene Zeile.
- **Abschnitts-Reihenfolge (deskriptiv — wird NICHT erzwungen):** typischerweise `[namespaceprefix]`,
  dann `[using]`-Block, dann `taskref`, dann `task`-Block. Der Formatter **ordnet nie um** — als reiner
  Gap-Rewriter bewegt er keine signifikanten Token. `taskref` (`TaskDeclarationSyntax`) und `task`
  (`TaskDefinitionSyntax`) liegen zudem gemischt in *einer* `Members`-Liste in Quellreihenfolge und sind
  grammatikalisch **frei anordbar** → ihre Reihenfolge bleibt unangetastet. Erzwungen wird nur der
  **Whitespace** zwischen den Abschnitten, nie ihre Reihenfolge. Im Task ist „erst Node-Deklarationen,
  dann Transitionen" bereits **grammatikalisch fix** (getrennte `NodeDeclarationBlock` /
  `TransitionDefinitionBlock`); Formatter-Sache ist dort einzig die **Leerzeile** dazwischen
  (`BlankLineBeforeTransitionsRule`). (Reihenfolge-Normalisierung wäre ein separates, opt-in
  Code-Action-/Organize-Feature — kein Whitespace-Formatter.)
- **Pfeile:** Space auf **beiden** Seiten (~98%); **Spaltenausrichtung ~79%** (bewusst aktiviert, s.u.).
  Pfeiltypen: `-->` ~81%, `o->` ~19%; alles andere <0,2% (Tippfehler).
- **`Node:Port`-Doppelpunkt tight** (~99%; Exit-Transition `Source:ExitPort`); Listen: Komma + Space.
  **Ausnahme Basistyp-Doppelpunkt** im `[base WfsType : Interface]`-Kopf-Block: davor tight, **danach
  ein Space** (`[base X:Y]` → `[base X: Y]`) — über den Eltern-Knoten `CodeBaseDeclarationSyntax` von
  der Node:Port-Nachbarschaft getrennt.
- **Task-Kopf-Code-Blöcke** (`[code]`/`[base]`/`[generateto]`/`[params]`/`[result]` zwischen `task <Id>` und
  `{`): der erste Block ein Space nach dem Identifier, weitere Blöcke je eigene Zeile linksbündig darunter
  (immer gestapelt). **Mehrzeilige `[params …]`** richten die Parameter unter dem ersten aus
  (Autor-Umbruch bewahrt); Einzeiler bleiben einzeilig. Details s. „Task-Kopf-Ausrichtung".
- **Kommentare:** `//` dominiert (~95% eigene Zeile / leading); Banner `// ----` als Idiom; `/* */`
  selten, **inline** (z.B. `[result bool /* SomeResultType */]`).
- **Hygiene:** CRLF 99,5%; **Final-Newline fehlt in 55%** → normalisieren auf *vorhanden*;
  Trailing-Whitespace häufig (~5% der Zeilen) → strippen.

## Architektur: Gap-Rewriter (single pass, ein Change pro Lücke)

Der Formatter **ändert niemals den Text signifikanter Token** — er schreibt ausschließlich die
Whitespace-**„Lücken" (gaps) zwischen** aufeinanderfolgenden signifikanten Token neu. Das ist die
tragende Idee.

**Belegte Grundlagen** (aus `Nav.Language/Syntax/`, echtes Roslyn-Modell):

- `SyntaxTree.Tokens` = flacher, positionssortierter Strom **nur signifikanter** Token; Trivia hängt als
  `LeadingTrivia`/`TrailingTrivia` (readonly structs) an den Token.
- Die FullSpans der Token **kacheln den Text lückenlos & überlappungsfrei** (`SyntaxTokenList`-Invariante,
  aufgebaut in `NavParser.BuildTrivia`). Für zwei aufeinanderfolgende Token A, B gilt exakt:
  `gapExtent = TextExtent.FromBounds(A.Extent.End, B.Extent.Start)`, Lückeninhalt =
  `A.TrailingTrivia ++ B.LeadingTrivia` (zusammenhängend, disjunkt).
- **Fehlende Token stehen NIE im Strom** — `SyntaxToken.Missing` wird nur bedarfsweise von
  Knoten-Properties (`task.CloseBrace`, `transition.Semicolon`) synthetisiert. Der Walk sieht also nur
  reale Token + den terminalen `EndOfFile`.
- **`EndOfFile`** ist das letzte Token und trägt die **komplette Datei-End-Trivia** als LeadingTrivia
  (`AttachEndOfFile`). Die letzte Lücke `[letztesReale.End, textLen]` ist der Ort für
  Final-Newline / EOF-Trim.
- **Direktiven (`#pragma …`) sind strukturierte Trivia**, keine Token — sie liegen *in* den Lücken und
  werden dort erhalten (s. „Kommentare & Direktiven").

**Load-bearing-Invariante: genau ein `TextChange` pro Lücke, ein einziger Durchlauf.** Alle realen
Nicht-EOF-Token haben Länge ≥ 1, nur EOF ist nullbreit und steht am Ende → die Change-Extents sind
paarweise disjunkt und geordnet → `TextChangeWriter.CheckForOverlappingChanges` kann **nie** werfen.
Dieselbe Invariante macht (a) die Fehler-Unterdrückung sicher (Unterdrücken = *Weglassen* eines Changes
über disjunkten Lücken → nie Overlap) und (b) „kämpfende" Ausrichtungsspalten unmöglich (jede Spalte ist
eine andere Lücke).

**Engine-Skelett:**

```
FormatDocument(tree, settings, options):
    suppressed = ComputeSuppressedExtents(tree, options)   // Fehler-Regionen + BOM-Guard
    alignment  = BuildAlignmentMap(tree, suppressed)       // Lücke -> aufgelöste Space-Zahl (Vorpass)
    changes = []
    toks = tree.Tokens                                     // inkl. terminalem EOF
    // Datei-Anfang: die Leading-Trivia des ERSTEN Tokens liegt vor der ersten Paar-Lücke ->
    // RenderLeadingGap (Kommentare auf Token-Tiefe, Direktiven Spalte 0, Fehl-Einzug entfernt);
    // Skiped-Läufe (insb. BOM -> SkippedTokensTrivia) bleiben verbatim = BOM-Guard am Offset 0
    changes.Add(RenderLeadingGap(toks[0], settings, options))
    for i in 0 .. toks.Count-3:                            // alle Paare REALER Token (EOF-Paar exklusive)
        gap = Gap(toks[i], toks[i+1])                      // Extent = FromBounds(A.End, B.Start)
        if IntersectsSuppressed(gap, suppressed): continue // verbatim
        layout    = FirstMatchingRule(ctx(gap, ...))       // -> GapLayout (Regelsatz, s.u.)
        canonical = Render(layout, gap-Trivia, settings)
        if canonical != tree.SourceText.Substring(gap.Extent):
            changes.Add(TextChange.NewReplace(gap.Extent, canonical))   // ≤1 pro Lücke
    // die EINE verbleibende Lücke (letztesReale, EOF): Final-Newline / EOF-Trim —
    // unterliegt derselben Suppression + BOM-Guard, ist NICHT zusätzlich zur Schleife
    changes.Add(RenderFinalGap(lastRealToken, EOF, settings, options, suppressed))
    return changes
```

(Die EOF-Lücke wird bewusst **nur** von `RenderFinalGap` behandelt — liefe sie zusätzlich durch die
Schleife, entstünden zwei Changes für eine Lücke und die Invariante bräche.)

### Einzug (flach: Tiefe 0/1)

Nav hat genau einen Block (`TaskDefinitionSyntax` / `TaskDeclarationSyntax` `{ … }`), **keine
Verschachtelung**. Deshalb **nicht Klammern zählen** (bricht bei unbalancierten Eingaben), sondern aus
Ahnenkette + Extent-Containment ableiten:

```
IndentDepth(tok): depth = 0
  for node in tok.Parent.AncestorsAndSelf():
    if node is TaskDefinition/TaskDeclaration mit Body und
       !OpenBrace.IsMissing && tok.Start >= OpenBrace.End &&
       (CloseBrace.IsMissing ? <suppress statt raten> : tok.Start < CloseBrace.Start):
      depth++
  return depth
```

Öffnende/schließende Klammer liegen an der Grenze → Tiefe 0 (Allman). Fehlende `}` ⇒ Body unterdrücken.
Einzugseinheit = `IndentDepth × oneIndent`, `oneIndent` = ein Tab **oder** `IndentSize` Spaces (je
`IndentStyle`).

## Regelsatz: eine kleine Engine + flache, geordnete Regelliste

**Bewusste Abgrenzung von Roslyn.** Roslyns (früher berüchtigt schwer verständlicher) Formatter definiert
Regeln als **Operationen**, die ein separater Solver global auflöst (`AbstractFormattingRule` mit
`GetAdjustSpacesOperation`/`GetAdjustNewLinesOperation`/`AddIndentBlockOperation`/
`AddAnchorIndentationOperation`/`AddSuppressOperation`, verkettet über `NextAction`). Der Einzug ergibt
sich dort aus dem **Zusammenspiel vieler Operationen über den ganzen Baum** — diese Fernwirkung +
Constraint-Solving ist der schwer durchschaubare Teil.

Nav ist winzig und flach → wir brauchen **keinen Solver**. Modell:

- **Nicht** „viele unabhängige Formatter" (jeder ein eigener Pass → Reihenfolge-/Overlap-Bugs) und
  **nicht** ein monolithischer `switch`.
- Sondern: die **feste, winzige Gap-Walk-Engine** fragt pro Lücke eine **geordnete Regelliste** —
  **erste passende Regel gewinnt** und liefert *eine* Layout-Entscheidung. Deckt sich 1:1 mit der
  „ein Change pro Lücke"-Invariante.

Regeln liefern **keine** Operationen, sondern einen Wert aus einem **winzigen, geschlossenen Vokabular**:

```csharp
// Was zwischen zwei Token stehen soll — kein Solver, keine Fernwirkung.
abstract record GapLayout {
    sealed record Nothing                        : GapLayout;  // tight, z.B. Node:Port
    sealed record SingleSpace                    : GapLayout;  // genau 1 Space
    sealed record AlignedColumn(ColumnId Column) : GapLayout;  // Spaces bis zur Gruppenspalte
    sealed record NewLine(int BlankLinesBefore, int IndentDepth) : GapLayout;  // BlankLinesBefore:
                                                                               // Autorenzahl (kein Kollaps)
    sealed record NewLineAlignedColumn(int BlankLinesBefore, ColumnId Column) : GapLayout;
                                                               // Umbruch, DANN Spaces bis zur Gruppen-
                                                               // spalte (statt Tiefen-Einzug) — für den
                                                               // Task-Kopf-Block-Stack + mehrzeiliges [params]
    sealed record Verbatim                       : GapLayout;  // unterdrückte Region
}
```

Eine Regel ist eine **reine, isoliert testbare** Mini-Funktion über einen vorberechneten Kontext:

```csharp
interface IGapRule {
    RulePriority Tier { get; }             // Safety > Structure > TokenPair > Alignment > Default
    GapLayout?   Apply(in GapContext ctx); // null = "nicht zuständig", nächste Regel fragen
}

readonly struct GapContext {
    // reine, formatierungs-invariante Fakten — NIE das aktuelle Whitespace (außer Newline-Anzahl)
    public SyntaxToken Prev, Next;
    public SyntaxNode? PrevParent, NextParent;
    public int          IndentDepth;   // := IndentDepth(Next): Tiefe des die neue Zeile eröffnenden
                                       // Tokens (aus Ahnenkette, nie aus Nachbar-Operationen). NewLine-
                                       // Layouts richten sich immer nach der beginnenden Zeile; Prev-
                                       // Tiefe wird für Einzug nie gebraucht (ggf. via ctx.Prev ableitbar).
    public GapTrivia    Trivia;        // hasComment / hasSkipped / hasDirective / newLineCount
    public bool         IsSuppressed;  // aus ComputeSuppressedExtents
    public AlignmentMap Alignment;     // vorberechnet: Lücke -> aufgelöste Space-Zahl
}
```

(Die `AlignmentMap` legt bewusst die bereits **aufgelöste Space-Zahl** ab statt der Zielspalte: nur der
Vorpass kennt die kanonischen Vor-Spalten-Breiten — für `AlignedColumn` ist der Wert das Padding
`targetCol − Breite`, für `NewLineAlignedColumn` die absolute Spalte nach dem Umbruch. Regeln und
Renderer schlagen nur nach und bleiben pur.)

Die **Regelliste ist die Spezifikation** — top-down lesbar, jede Zeile ein Satz:

```csharp
static readonly IReadOnlyList<IGapRule> Rules = [
    // Safety
    new VerbatimWhenSuppressedRule(),    //  1. unterdrückte Region -> Verbatim
    // Structure
    new BraceOnOwnLineRule(),            //  2. vor '{' und vor '}' -> NewLine(blank=Autorenzahl, depth)  (Allman;
                                         //     auch hier kein Leerzeilen-Kollaps)
    new MemberBreakRule(),               //  3. nach '}' und nach Top-Level-']' ([namespaceprefix]/[using])
                                         //     -> NewLine(blank=Autorenzahl, 0) — AUSSER Next == ';' ("};")
    new BlankLineBeforeTransitionsRule(),//  4. Blockgrenze letzte Deklaration -> erste Transition:
                                         //     NewLine(max(blank,1), depth)
    new StatementBreakRule(),            //  5. nach ';' -> NewLine(blank=Autorenzahl, depth) — exkludiert
                                         //     die Blockgrenze aus Regel 4 (Intra-Tier-Disjunktheit)
    // TokenPair
    new TightColonRule(),                //  6. Node ':' Port -> Nothing; Base-Doppelpunkt ([base X:Y])
                                         //     davor tight, danach Space (Gap fällt durch -> Catch-all)
    new PunctuationRule(),               //  7. tight vor ','/';' , [-Innenränder, Typ-Interna (s.u.)
    new TaskHeadLayoutRule(),            //  8. Task-Kopf: Id->Block1 = SingleSpace; Block->Block =
                                         //     NewLineAlignedColumn(TaskHeadBlock); mehrzeiliges [params]:
                                         //     ','->Param = NewLineAlignedColumn(ParamsList)  (s. „Task-Kopf-Ausrichtung")
    // Alignment
    new ArrowAlignmentRule(),            //  9. SourceNode -> Edge in Gruppe -> AlignedColumn(Arrow)
    new NodeGridAlignmentRule(),         // 10. keyword->node -> AlignedColumn(Node);
                                         //     node->[params] -> AlignedColumn(NodeParams) (tight, eigene Spalte);
                                         //     node->rest -> AlignedColumn(DeclRest)  (Node-Raster)
    // Default
    new DefaultSingleSpaceRule(),        // 11. Catch-all -> SingleSpace
];
```

**`PunctuationRule` im Detail** (die Interpunktions-Grundwahrheiten, die sonst der Catch-all mit
falschen Spaces fluten würde):

- tight **vor** `,` und **vor** `;` (überall — deckt auch `end;`, `choice Decide;`, `};` ab);
  *nach* `,` liefert der Catch-all das Komma+Space-Idiom.
- tight **nach öffnendem `[`** und **vor schließendem `]`** eines Code-Blocks (`[params`, `bool]`).
- **Typ-Interna** in `[params]`-Typen sind tight: Generik-Spitzklammern und Array-Klammern
  (`T2<T3, T4<T5>>`, `T6[][]`) kleben beidseitig; das `,` in Generik-Argumenten behält Komma+Space.
  Die Lücke **Typ-Ende → Parametername** ist dagegen `SingleSpace` — unterscheidbar an der
  Knotengrenze innerhalb `ParameterSyntax` (Typ-Teil vs. Name-Token), nicht am Token-Typ.
- Der Lückentyp `] → [` kommt **zweimal** vor: Array-Rank (`T6[][]`, tight, hier) vs. Task-Kopf-Blöcke
  (`NewLineAlignedColumn`, Regel 8) — via Eltern-Knoten disjunkt; genau so ein Fall, den der
  Intra-Tier-Disjunktheits-Check (s. „Dispatch") absichert.

Die vollständige Token-Paar-Tabelle wird nicht hier gepflegt, sondern fällt aus der
**Gap-Kontext-Abdeckungs-Prüfung** (s. „Verifikation"): jede aus der Grammatik erreichbare Nachbarschaft
bekommt in S2 eine explizite, getestete Entscheidung.

Typische Regelgröße (komplett):

```csharp
sealed class TightColonRule : IGapRule {
    public RulePriority Tier => RulePriority.TokenPair;

    public GapLayout? Apply(in GapContext ctx) =>
        ctx.Next.Type == SyntaxTokenType.Colon || ctx.Prev.Type == SyntaxTokenType.Colon
            ? new GapLayout.Nothing()
            : null;
}
```

Der **Renderer** ist die **einzige** Stelle, die `GapLayout` + erhaltene Trivia (Kommentare, Direktiven)
zu einem String macht — Kommentare/Direktiven werden hier wieder eingefädelt, damit die Regeln simpel
bleiben und nur das *Skelett* bestimmen.

### Renderer: Vertikalmodell kommentarreicher Lücken (festgezurrt, umgesetzt)

Wie der Renderer eine Lücke mit Kommentaren/Direktiven/Leerzeilen um das Layout-Skelett herum aufbaut
(`GapRenderer`, per Unit-Tests genagelt):

- Die Lücke wird entlang ihrer `NewLine`-Trivia in ihre **authored Zeilenstruktur** zerlegt:
  Trailing-Segment (auf der Zeile von `Prev`), null oder mehr **Innenzeilen**, Leading-Segment (auf der
  Zeile von `Next`). Newlines **im Inneren** eines mehrzeiligen Kommentars sind Teil des Kommentar-Texts
  und zerteilen nicht (ein `/* */` ist *eine* Trivia).
- Das Layout bestimmt **nur zwei Dinge**: ob `Next` auf derselben Zeile bleibt (horizontale Layouts)
  und seinen horizontalen Ziel-Ort (nichts/Space/Spalte bzw. Einzugstiefe/Spalte). Die **Innenstruktur**
  (Reihenfolge von Leer-, Kommentar- und Direktivzeilen) wird nie erfunden, entfernt oder umsortiert —
  normalisiert wird pro Zeile nur der Whitespace (Kommentarzeile → Zeilen-Präfix des Layouts, Direktive →
  Spalte 0, Leerzeile → leer; Whitespace um Inline-Kommentare → ein Space).
- **Leerzeilen-Minimum:** `BlankLinesBefore` wirkt als Minimum (Regeln reichen die Autorenzahl durch,
  `BlankLineBeforeTransitionsRule` hebt an) — fehlende Leerzeilen werden unmittelbar vor der Zeile von
  `Next` ergänzt, vorhandene nie gekappt.
- **Renderer-Schranke (Defense-in-Depth, im Renderer selbst):** verlangt ein Layout Same-Line, obwohl
  die Lücke zeilen-erzwingende Trivia enthält (Newline, `//`-Kommentar, mehrzeiliger Block-Kommentar,
  Direktive), degradiert es zum Umbruch auf `ctx.IndentDepth` mit erhaltener Innenstruktur. Enthält die
  Lücke eine `SkippedTokensTrivia`, rendert der Renderer unabhängig vom Layout verbatim. **Einzige
  Ausnahme (Pull-up, seit S3):** die Task-/`taskref`-Kopf-Kanonisierung liefert
  `SingleSpace.PullUp` — **bloße** authored Newlines werden dann hochgezogen (entfallen); die harte
  Schranke bleibt: über einen `//`-Kommentar, einen mehrzeiligen Block-Kommentar oder eine Direktive
  wird auch mit Pull-up nie zusammengezogen.
- **Lexer-Fallstricke (empirisch verifiziert):** ein `//`-Kommentar **verschluckt beim Lexen das `\r`**
  des Zeilenendes (die `NewLine`-Trivia trägt dann nur `\n`) — der Renderer schreibt Zeilenenden selbst
  (`settings.NewLine`) und kappt daher Zeilenend-Whitespace des `//`-Kommentar-Texts (`CommentText`),
  sonst entstünde `\r\r\n`. Eine `DirectiveTrivia` trägt dagegen **nur den Zeileninhalt** (`#pragma …`
  ohne Zeilenende); ihr terminierendes `NewLine` ist eine eigene Trivia — die Zeilen-Zerlegung trägt
  also ohne Sonderfall. Eine (unzulässig) **eingerückte** Direktive wird trotzdem als `DirectiveTrivia`
  gelext; der Renderer setzt sie auf Spalte 0 zurück.
- Der Delta-Shift der Innenzeilen mehrzeiliger `/* */`-Kommentare ist **seit S4 enthalten** (`GapRenderer.
  ShiftInteriorLines` / `OwnLineCommentText`): die erste Kommentarzeile wandert auf das Zeilen-Präfix, die
  Folgezeilen werden um dasselbe Zeichen-Delta mitgeschoben (relative Einrückung erhalten, kein Reflow) —
  derselbe Mechanismus wie der Hand-gelegt-Delta-Shift.

### Dispatch & Priorität — wie „genau eine Regel pro Lücke" garantiert wird

Zwei Bedeutungen von „mehrere Regeln greifen" auseinanderhalten:

- **Output-Overlap** (mehrere Entscheidungen für *eine* Lücke kombiniert): **konstruktiv unmöglich** —
  `FirstMatchingRule` bricht beim ersten Nicht-`null` ab (Short-Circuit). Es entsteht **genau ein**
  `GapLayout` pro Lücke; das *ist* die Umsetzung der „ein Change pro Lücke"-Invariante auf
  Entscheidungsebene. Deterministisch (feste Liste, reine Prädikate) → Voraussetzung für Idempotenz. Da
  jede Regel ein *vollständiges* Layout liefert, gibt es nichts zu kombinieren.
- **Applicability-Overlap** (mehrere Regeln *würden* matchen, die Reihenfolge entscheidet **still**): die
  eigentliche Gefahr — die Reihenfolge wird zur *impliziten Spezifikation*, ein falsch eingefügter/
  umsortierter Eintrag ändert Verhalten lautlos (die Roslyn-Falle). First-Match allein *maskiert* so einen
  Fehler.

**Wer bestimmt die Priorität:** die **geordnete Liste ist die Priorität** — aber die Reihenfolge ist
**nicht beliebig**, sondern folgt einem Prinzip: **Sicherheit/Verbatim zuerst, dann harte Struktur, dann
spezifische Token-Paar-Regeln, dann Ausrichtung, zuletzt der Catch-all** („spezifisch schlägt generisch",
wie Pattern-Match-Reihenfolge/CSS-Spezifität). Formalisiert als **Prioritäts-Tiers** (Enum `RulePriority`:
`Safety > Structure > TokenPair > Alignment > Default`): der Dispatcher sortiert nach Tier, dann nach
Deklarationsreihenfolge. Eine neue Regel einzufügen heißt damit, ihren **Tier zu wählen** (semantische
Entscheidung) — nicht einen Listenindex zu raten (fragil).

**Wie stiller Overlap verhindert wird:** ein Test-/Debug-Modus wertet für **jede** Lücke **alle** Prädikate
aus und prüft: **innerhalb eines Tiers matcht höchstens eine Regel** (Intra-Tier-Disjunktheit).
Cross-Tier-Overlaps sind **gewollt** und dokumentiert (der höhere Tier preemptiert — z.B. schlägt
Verbatim/Suppression bewusst jede Layout-Regel; die Struktur-Regel „vor `{`" schlägt das
Task-Kopf-Layout). Läuft über Goldens **und** den Korpus → aus der impliziten Ordnungs-Abhängigkeit wird
eine **geprüfte** Eigenschaft. Ungewollter Intra-Tier-Overlap ⇒ **Prädikat verschärfen** (oder Präzedenz
explizit dokumentieren), nicht die Reihenfolge „zurechtschieben". Zwei bereits bekannte, per Prädikat
disjunkt gehaltene Paare im selben Tier: `StatementBreakRule` exkludiert die Blockgrenze der
`BlankLineBeforeTransitionsRule` (beide Structure, beide matchen „nach `;`"); `PunctuationRule` und
`TaskHeadLayoutRule` teilen sich `] → [` nach Eltern-Knoten (beide TokenPair).

(Verworfene Alternative: global disjunkte Prädikate ganz ohne Priorität — am strengsten, aber jede neue
Regel müsste gegen *alle* anderen als disjunkt bewiesen werden; die Safety-Regeln überlappen ohnehin
absichtlich mit allem. Tiers + Intra-Tier-Check ist der wartbare Kompromiss.)

**Warum das nicht zu Roslyn-Verhältnissen führt:**

| Roslyn (schwer) | Nav-Ansatz (verständlich) |
|---|---|
| Regeln emittieren Operationen, Solver löst global | Regel liefert **direkt** die Entscheidung, kein Solver |
| Einzug = Zusammenspiel von Anchor/Indent/Suppress | Einzug = `IndentDepth` **direkt** aus Ahnenkette |
| Token-Paar + verketteter `NextAction` | geordnete Liste, **first-match-wins**, top-down lesbar |
| Interaktion schwer vorhersehbar | jede Regel **pur & isoliert testbar** (`ctx -> GapLayout`) |
| Idempotenz emergent | Idempotenz **lokal** (Regeln lesen nur invariante Fakten) |

Die **einzige** nicht-lokale Zutat ist die Ausrichtung (eine Spalte hängt von Nachbarzeilen ab). Sie wird
bewusst als **Vorberechnung** (`AlignmentMap`) an *einer* benannten, testbaren Stelle isoliert; die
`ArrowAlignmentRule` schlägt nur nach und bleibt selbst pur.

### Spaltenausrichtung (aktiviert)

Jede Spalte ist eine Entscheidung auf **einem bestimmten Lückentyp**:

- **Pfeil-Spalte** = Lücke zwischen dem letzten Token des Quell-Teils und `Edge.Keyword` — gilt für
  `TransitionDefinitionSyntax` (`SourceNode`) **und** `ExitTransitionDefinitionSyntax`
  (`Source:ExitPort --> Ziel;`, Quell-Teil inkl. tightem `:ExitPort`). Alle Edge-Keywords sind
  **3 Zeichen** breit (`-->`/`o->`/`==>`, Fortsetzungen `--^`/`o-^`) → die Spalte hinter dem Pfeil
  fluchtet automatisch mit.
- **Condition-Spalte** (`ColumnId.Condition`) = Lücke vor dem **führenden** `if`/`else`/`else if` einer
  `ConditionClauseSyntax` (dem Klausel-Start; beim `else if` also das `else`, nicht das innere `if`). Sie
  richtet aufeinanderfolgende Bedingungen unter dem längsten Ziel-Teil aus (im Korpus das häufige Muster
  „mehrere Kanten vom selben Choice, je mit einer Bedingung"). Die Breite wird **kanonisch ab
  Zeilenanfang** gemessen (Token-Texte + regel-entschiedene Lücken) und **baut auf die bereits aufgelöste
  Pfeil-Spalte auf** — die Condition-Lücke sitzt in derselben Zeile rechts vom Pfeil, deshalb übernimmt
  die Breitenmessung das Pfeil-Padding aus der `AlignmentMap` (der Vorpass fügt die Condition-Spalte
  **nach** der Pfeil-Spalte hinzu). Gruppenbildung und die „≥ 2 Teilnehmer"-Regel sind dieselben wie bei
  der Pfeil-Spalte; eine **bedingungslose** Transition ist kein Teilnehmer, **bricht die Gruppe aber
  nicht** (nur `on`/`do` bleiben zurückgestellt). Die Spalte ist — anders als Pfeil/Node-Grid — **immer
  tight** (`col = max(Breite) + 1`, **kein** Tab-Stopp, keine `AlignmentColumnPolicy`, wie die
  `[params]`-Spalte `ColumnId.NodeParams`): die nachgestellte Klausel soll minimal sitzen, nicht unnötig
  weit nach rechts; die breiteste Zeile bekommt genau einen Space. Über die Option `AlignConditions`
  schaltbar (Default `true`).
- **Node-Deklarations-Raster (drei virtuelle Spalten `keyword | node | rest`)** — die Node-Arten sind
  verschieden gebaut, aber positionell einheitlich ausrichtbar:
  - **Spalte 1 `keyword`** = `init`/`task`/`choice`/`view`/`dialog`/`exit`/`end` (steht am Zeilenanfang
    auf Block-Einzug, nicht ausgerichtet).
  - **Spalte 2 `node`** = das **erste `Identifier` nach dem Keyword**. Das ist **nur bei `task`** der
    referenzierte *Typ* (`TaskNodeDeclarationSyntax.Identifier`); bei `init`/`choice`/`exit`/`view`/
    `dialog` ist es der *Name* des Knotens selbst. **`end;` hat gar keinen Identifier** → keine
    node-Spalte (nur Keyword; nimmt an der Ausrichtung nicht teil). Ausgerichtet über die Lücke
    `keyword → node`.
  - **Spalte 3 `rest`** = das **erste Token *nach* dem node-Identifier**, sofern vorhanden. Inhalt je
    Art: `task` → `IdentifierAlias` **oder** `[donotinject]`/`[abstractmethod]`; `init` →
    `[abstractmethod]`/`do …`; **`view`/`dialog`/`exit` haben nie eine Spalte 3** (Form
    `keyword Identifier;`), `end` ebenso wenig. **Nur `task`** kann ein zweites Identifier (Alias)
    tragen. Ausgerichtet über die Lücke `node → rest`; **nur der Start**, nie der Inhalt. Fehlt Spalte 3
    (z.B. `choice Decide;`, `view V;`), gibt es **kein Phantom-Padding** (die Lücke `node → ;` ist
    tight via `PunctuationRule`, höherer Tier).
  - **Eigene Spalte `[params]`** (`ColumnId.NodeParams`) = ein `[params]`-Block direkt hinter dem
    node-Identifier (`init`/`choice`; an Body-`task`-Knoten gibt es noch kein `[params]`). Bewusst
    **getrennt** von Spalte 3 `rest`, damit ein langer Alias/Node den schwergewichtigen `[params]`-Block
    nicht in eine gemeinsame Spalte nach rechts zieht. **Ausrichtung nur bei ≥ 2 params-Teilnehmern je
    Gruppe** (ein einzelner `init [params …]` bekommt nur ein Leerzeichen — kein Wandern nach rechts);
    dann **immer tight** (`col = max(node-Ende der params-Teilnehmer) + 1`, **kein** Tab-Stopp, keine
    `AlignmentColumnPolicy`). Ausgerichtet über dieselbe Lücke `node → rest`, nur mit anderer Spalte —
    disjunkt zu Spalte 3, weil eine Zeile ihren `[params]`-Block **entweder** als `rest` **oder** gar
    nicht trägt.

  Alle Identifier-Lücken (`keyword → node`, `node → rest`) sind je ein eigener Token-Paar-Lückentyp →
  je ein `AlignedColumn`-Layout, passt bruchlos in „ein Change pro Lücke". Spaltenwerte je Gruppe über
  `AlignmentColumnPolicy` (Default `NextTabStop`) — außer der tighten `[params]`-Spalte. Das ist ein
  **fester** Raster (tractable, idempotent) — **nicht** die zurückgestellte Mehrspalten-Ausrichtung, die
  die *variabel vielen* Trailing-Klauseln (`on`/`if`/`do`) an **Transitionen** meint.

Algorithmus je Gruppe: (1) Block in **Gruppen** partitionieren. Trenn-Kriterium ist die **Zeilenanzahl im
Leading Trivia** des nächsten signifikanten Tokens, nicht „Leerzeile" als solche:

```
interruptLines = Zeilen im Gap-Trivia STRIKT zwischen den beiden signifikanten Token
                 (leere Zeilen + eigene Kommentarzeilen) = (Newlines im Trivia − 1)
```

**Neue Gruppe ⟺ `interruptLines ≥ 2`.** Damit brechen *eine* Leerzeile **oder** *eine* eigene
Kommentarzeile die Gruppe **nicht** (gleiche Gruppe); erst zwei Umbruch-Zeilen tun es — z.B. **zwei
Leerzeilen** oder **Leerzeile + Kommentarzeile**. Eleganter Nebeneffekt: nicht der Kommentar trennt,
sondern die **Leerzeile davor** — genau das Abschnitts-Header-Idiom. Zusätzlich brechen (wie gehabt) eine
**unterdrückte** oder **hand-gelegte** (mehrzeilige) Anweisung die Gruppe; ebenfalls aus der Spalte
ausgeschlossen: eine Transition mit **Inline-Block-Kommentar im Vor-Pfeil-Bereich** (s. „Kommentare").
Weil Leerzeilen **nicht kollabiert** werden (s. „Optionen"), ist `interruptLines` formatierungs-invariant
→ die Gruppierung ist ohne Sonderkniff idempotent.
(2) Pro Zeile die natürliche Vor-Spalten-Breite in **Zeichen** messen — **kanonisch**, nicht aus dem
Ist-Text (s. Fallstrick unten). Spalten zählen **ab Inhaltsbeginn der Zeile** (nach dem Einzug); der
Einzug geht nie in die Breite ein. (3) Zielspalte über die konfigurierte **`AlignmentColumnPolicy`**
bestimmen (Default `NextTabStop`, s.u.); `pad = targetCol − Breite` (durch `tightMin` immer ≥ 1), Lücke
durch genau `pad` **Spaces** ersetzen. **Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs** — in
Stein gemeißelt, unabhängig vom `IndentStyle`.

**Gruppen der Größe 1 werden nicht ausgerichtet** (Layout `SingleSpace`): Ausrichtung ist ein
Gruppen-Phänomen — eine einzeln stehende Transition `B --> Exit;` bekommt kein Tab-Stopp-Padding
verpasst. Idempotent, weil die Gruppengröße nur von formatierungs-invarianten Fakten abhängt
(`interruptLines`, Suppression, Hand-gelegt).

**Fallstrick — Breite kanonisch messen, NIE aus `node.ToString()`:** die Vor-Spalten-Breite muss aus dem
**kanonisch-normalisierten Token-Rendering** des Knotens kommen (Summe der signifikanten Token-Textlängen
+ die inneren Gaps *so, wie der Formatter sie schreiben wird* — für `Node:Port` = 0, für Listen
Komma+Space usw.), **nicht** aus `SourceNode.ToString().Length`. Grund: `ToString()` eines Mehr-Token-
Knotens enthält dessen *Ist*-Whitespace, den der Formatter aber selbst normalisiert (z.B.
`Dialog : Ok` → `Dialog:Ok` via `TightColonRule`). Misst man `ToString()`, wandert `targetCol` zwischen
erstem und zweitem Lauf → das Gruppen-`max` liefert unterschiedlich viele Padding-Spaces auf den
*anderen* Zeilen → **nicht idempotent** (und schon Lauf 1 richtet falsch aus). Die kanonische Breite
hängt dagegen nur an Token-Text + Regelentscheidung (beide formatierungs-invariant).

**`AlignmentColumnPolicy` (wie `targetCol` aus den kanonischen Breiten folgt):** `targetCol` ist die
(0-basierte) **Startspalte** der ausgerichteten Spalte, gemessen ab Inhaltsbeginn.

- `tightMin = max(kanonische Breite) + 1` — das **erzwungene Minimum** (weniger als 1 Space ist nie
  möglich); zugleich der Boden aller Policies. Ausreißer, deren natürliche Breite ≥ `targetCol` ist,
  bekommen 1 Space und überlaufen (automatisch, keine negative Padding-Wahl).
- **`NextTabStop` (Default):** `targetCol` = nächster Tab-Stopp ≥ `tightMin`, d.h. auf ein Vielfaches
  von `IndentSize` aufgerundet. Ehrt die reale Autoren-Absicht — die im Korpus beobachteten „weiter als
  nötig" ausgerichteten Spalten (Vielfache von 4 in `LargeNav`) sind **Tab-Stopp-Artefakte** vom
  Tab-Tippen, keine präzise gewählten Breiten. Deterministisch, idempotent (nur Funktion von Token-Breite
  + `IndentSize`), ausreißer-immun, in einem Satz erklärbar. In Spaces gerendert (Lock oben).
- **`Tight`:** `targetCol = tightMin`. Reinste kanonische Form; verkleinert jede über-gepaddete Spalte
  → großer Einmal-Diff. Fallback, falls die Korpus-Messung keine konsistente Absicht zeigt.
- **`PreserveDominant`:** `targetCol = max(tightMin, dominante Ist-Spalte)`, wobei die dominante Spalte
  aus dem Ist-Layout gelesen wird. Bewahrt bewusst breitere, konsistente Autorenspalten, liest aber
  Ist-Whitespace und braucht Tab-Auflösung → nicht-Default. (Verworfen als Default: die früher erwogene
  Perzentil-Heuristik „targetCol = wo >X% der Kanten fallen" — idempotent, aber magische Konstante,
  Ist-Whitespace-abhängig, bei raggedem Input rauschanfällig, schwer erklärbar; ein Per-Zeile-Padding-
  Deckel zerstört die Spalte und ist nur als Obergrenze `targetCol ≤ tightMin + X` verteidigbar.)

**Meta-Entscheidung ist Achse-B (Stil, keine Grundwahrheit) → per Korpus kalibriert (S3, erledigt):**
pro Ausrichtungsgruppe im Korpus (`d:\tfs\main`, 1913 `.nav`) wurde `extra = autorSpalte − tightMin`
(Tabs bei `IndentSize` 4 aufgelöst, Spalten ab Inhaltsbeginn) erhoben — nur über Gruppen (≥ 2
Teilnehmer), deren Autor-Spalte uniform ist. Ergebnis: **Pfeil-Spalte** (n=3299 uniforme Gruppen, dazu
1956 ragged): nur 8,0% sitzen exakt auf `tightMin`, aber **90,6% auf einem Tab-Vielfachen** (26,2%
exakt auf dem *nächsten* Tab-Stopp; der Überschuss dahinter fällt glatt ab — Tab-Tipp-Artefakte, keine
präzise gewählten Breiten). **Node-Grid** (n=1785): `NextTabStop` exakt 49,1% vs. `Tight` exakt 42,4%.
→ **`NextTabStop` ist der bestätigte Default** — die beste deterministische Policy auf beiden Spalten;
den variablen Überschuss könnte nur das Ist-Whitespace-lesende `PreserveDominant` bewahren (bewusst
nicht Default).

**Idempotenz-Beweis:** `targetCol` (in `NextTabStop`/`Tight`) ist eine reine Funktion aus *kanonischen
Token-Breiten* + `IndentSize` — invariant unter Formatierung (Token-Text ändert sich nie; Einzug wird
separat davor gesetzt und **nicht** in die Breite eingerechnet → **tab-breiten-unabhängig**, „Tabs für
Einzug, Spaces für Ausrichtung"). Zweiter Lauf rechnet identisches `targetCol` und `pad` → identische
Ausgabe. Ausrichtungs-Spaces stehen stets **vor einem Token derselben Zeile**, nie vor einem Newline →
kollidieren nie mit dem Trailing-Whitespace-Trim. (`PreserveDominant` bleibt idempotent, weil nach Lauf 1
die Mehrheit exakt auf `targetCol` sitzt → derselbe dominante Wert; es liest aber als einzige Policy den
Ist-Whitespace und ist daher nicht-Default.)

### Task-Kopf-Ausrichtung (Code-Blöcke + mehrzeiliges `[params]`)

Der `TaskDefinitionSyntax`-**Kopf** trägt zwischen `task <Id>` und `{` bis zu **fünf** Code-Blöcke in
fester Grammatik-Reihenfolge (`[code]`, `[base]`, `[generateto]`, `[params]`, `[result]` —
`CodeBlockFacts.DeclarationKeywords(TaskDefinition)`). Zwei getrennte Belange:

**(A) Äußeres Stapeln (bedingungslos).** Der **erste** vorhandene Block steht **immer genau ein Space
nach dem Identifier** — auch wenn der Autor ihn umbrochen hatte (die Lücke `Id → Block1.[` = `SingleSpace`).
**Jeder weitere** Block steht auf **eigener Zeile**, linksbündig unter dem `[` des ersten Blocks (die Lücke
`Block.] → nächster.[` = `NewLineAlignedColumn(TaskHeadBlock)`). Das ist eine bewusste **Kanonisierung** (im
Gegensatz zur sonstigen Umbruch-Erhaltung), weil mehrere schwere Blöcke auf einer Zeile schwer lesbar sind.
**Grenzfall Kommentar in der Lücke `Id → Block1.[`:** erzwingt dort ein Kommentar den Umbruch (`//` oder
mehrzeiliges `/* */`), kann Block 1 nicht hochgezogen werden (Renderer-Schranke, s. „Hand-gelegte
Anweisungen") — er fällt dann wie ein Folgeblock auf `NewLineAlignedColumn(TaskHeadBlock)`; das ist
konsistent, weil die Spalte kanonisch ist und nicht an der Ist-Position von Block 1 hängt.

Die **Kopf-Spalte ist kanonisch** und pro Task-Definition lokal: `col = "task ".Length + Id.Length + 1`
(depth 0 → reine **Space**-Ausrichtung ab Spalte 0, kein Tab). Reine Funktion des kanonischen
Identifier-Textes → idempotent. Blöcke ohne innere Liste (`[code]`/`[base]`/`[generateto]`/`[result]`) sind
für das Stapeln nur Ganzes — nur `[params]` hat eine Komma-Liste (Belang B).

**(B) Inneres `[params]` (Autor-Umbruch bewahrt).** Einzeilige `[params …]` bleiben **einzeilig** (nur
Spacing normalisiert: `params `+Space, Komma+Space). Hat der Autor die Liste **mehrzeilig** gelegt, wird
jeder Parameter unter den **ersten** ausgerichtet: `Param → ','` tight (via `PunctuationRule`),
`',' → nächster Param` = `NewLineAlignedColumn(ParamsList)`. Params-Spalte = `Block.[`-Spalte +
`"[params ".Length`. Das schließende `]` bleibt **tight** am letzten Parameter (Listen-Idiom, **nicht**
Allman — das gilt nur für den Task-Body).

**`[params]` ist vom Hand-gelegt-Freeze ausgenommen.** Anders als eine mehrzeilige Transition wird ein
mehrzeiliges `[params]` **nicht** verbatim eingefroren, sondern kanonisch ausgerichtet — **auch mit
Kommentaren** (Trailing-`//` je Zeile → Single-Space). Das ist sicher: die Params-Spalte hängt an der
**Parameter-Position, nicht am Kommentartext** (kein Arrow-Column-Problem), und weil jeder Parameter ohnehin
auf eigener Zeile bleibt, wird nur führender Whitespace angefasst — nie ein Token auf eine `//`-Zeile
gezogen (Achse-A-sicher, der Wächter feuert nie). Ein mehrzeiliger `/* */`-Block-Kommentar im `[params]`
wird nicht reflowt, sondern per Delta-Shift mitgeschoben (s. „Kommentare & Direktiven").

**Der `taskref`-Kopf stapelt symmetrisch (depth 0).** Der `TaskDeclarationSyntax`-Kopf trägt
`[namespaceprefix]`/`[notimplemented]`/`[result]` (nur Einzel-Fragmente, keine Listen;
`CodeBlockFacts.DeclarationKeywords(TaskRef)`). Er wird **identisch zum Task-Kopf** behandelt (Belang A):
Block 1 inline hinter dem Identifier (Pull-up), jeder Folgeblock gestapelt unter dem `[` des ersten
(`NewLineAlignedColumn(TaskHeadBlock)`, Kopf-Spalte = `"taskref ".Length + Id.Length + 1`). Ein `taskref`
hat kein `[params]`, daher entfällt Belang B. Das war früher eine bewusste Ausnahme (einzeilige
Normalisierung „die Blöcke sind leichtgewichtig") — zugunsten *eines* mentalen Modells und der
Respektierung vom Autor gelegter Struktur aufgegeben.

**Sonstige Code-Block-Wirte:**

- **Node**-Deklarationen im Body (`init`/`choice`/`task`-Knoten mit `[params]`/`[abstractmethod]`/
  `[donotinject]`) unterliegen dem **Node-Grid** und bleiben einzeilig. Legt ein Autor eine
  Node-Deklaration mehrzeilig, fällt sie — wie eine hand-gelegte Anweisung — über das bestehende
  Primitiv aus dem Grid (kein Sonderfall).

Gap-Layouts im Überblick (`Id`/`[`/`]`/`,` = signifikante Token des Kopfs):

| Lücke | Layout | Regel |
|---|---|---|
| `Id → Block1.[` | `SingleSpace` (Block 1 immer inline) | `TaskHeadLayoutRule` |
| `Block.] → nächster Block.[` | `NewLineAlignedColumn(TaskHeadBlock)` | `TaskHeadLayoutRule` |
| `[params …]` einzeilig, `',' → Param` | `SingleSpace` | Catch-all |
| `[params …]` mehrzeilig, `',' → Param` | `NewLineAlignedColumn(ParamsList)` | `TaskHeadLayoutRule` |
| `Param → ','` (immer) | `Nothing` (tight) | `PunctuationRule` |
| letzter Param `→ ]` | `Nothing` (tight) | `PunctuationRule` |
| leeres `[params]`: `params → ]` | `Nothing` (tight) | `PunctuationRule` (kein „erster Parameter" — im Korpus real, per Smoke gefunden) |
| letzter `Block.] → {` | `NewLine` (Allman) | `BraceOnOwnLineRule` (Structure, preemptiert) |

Beide Spalten (`TaskHeadBlock`, `ParamsList`) sind reine Funktionen kanonischer Token-Breiten → der
Idempotenz-Beweis der übrigen Ausrichtung trägt unverändert.

> **Zurückgestellt:** Mehrspalten-Ausrichtung (`on`/`if`/`do`) und die *Interna* mehrzeiliger
> `[params …]` **außerhalb** des Task-Kopfs (Node-Wirte `init`/`choice` bleiben einzeilig; ihr
> `[params]`-Block als Ganzes nimmt aber an der eigenen `NodeParams`-Spalte teil, s.o.) — empirisch
> stark nur die Pfeil-Spalte (~79%) und das Node-Grid; die Kaskade bringt wenig Nutzen bei viel
> Komplexität.

## Verhalten bei Fehlern / Unknown / Skiped-Token

Zwei **getrennte** Belange (wichtig: **Kommentare sind KEIN Unterdrückungs-Auslöser** — sonst würde der
Formatter bei ~95% der Dateien nichts tun):

**(a) Kommentare** werden **innerhalb** der Lücken-Normalisierung behandelt, nie unterdrückt (s.
„Kommentare & Direktiven").

**(b) Strukturbruch → unterdrücken (verbatim).** Auslöser:

- Struktur-Token fehlt: `transition.Semicolon.IsMissing`, `task.CloseBrace.IsMissing`.
- Lücke schneidet eine `SkippedTokensTrivia` (gefaltete Skiped-/Unknown-Läufe, `SyntaxTree.SkippedTokens()`).
- Eine **Error-Severity-Syntax-Diagnostik** (`SyntaxTree.Diagnostics`) überlappt die Anweisung —
  **BOM-`Nav0000` bei Offset 0 ausgenommen**.

**Unterdrückungs-Einheit:** liegt der Auslöser **innerhalb** einer Anweisung, wird die **kleinste
umschließende Anweisung** verbatim gelassen (für Lücken mit Extent ⊆ `FullExtent` der Anweisung keinen
Change erzeugen) und **aus Ausrichtungsgruppen ausgeschlossen**. Liegt eine `SkippedTokensTrivia`
dagegen in einer Lücke **zwischen** zwei Anweisungen (kein gemeinsamer Anweisungs-Elter), wird **nur
diese eine Lücke** verbatim gelassen — die Nachbarn werden normal formatiert. Da nur *weggelassen* wird
und Lücken disjunkt sind, kann Unterdrückung **nie** einen Overlap erzeugen. Fehlende `}` ⇒ **gesamten
Task-Body verbatim** (Containment unsicher), alles außerhalb wird weiter formatiert. **Global-Fallback:**
keine brauchbaren Member ⇒ nur die zwei konservativen Changes (Final-Newline, EOF-Trailing-Trim).

**BOM-Guard:** führendes U+FEFF wird als `Unknown` → `SkippedTokensTrivia` + `Nav0000` gelext; explizit
von Unterdrückung ausnehmen und **nie einen Change mit Extent-Start 0 emittieren, wenn `text[0]=='﻿'`**.

### Edge-Case-Tabelle

| Eingabe | Formatter emittiert |
|---|---|
| Fehlendes `;` an Transition | Interne Lücken der Anweisung verbatim; Rand-Lücke (Zeilenumbruch/Einzug) normal; aus Pfeil-Ausrichtung ausgeschlossen |
| Fehlendes Task-`}` | Ganzer Task-Body verbatim; Version/Namespace/Usings/andere Member weiter formatiert |
| Müll-Token mitten in Transition (`SkippedTokensTrivia`) | Umschließende Anweisung verbatim; Skiped-Text byte-genau erhalten |
| Streu-Token zwischen Deklarationen | Nur die Lücke mit der `SkippedTokensTrivia` verbatim; Nachbarn formatiert |
| Unterminierter String | `StringLiteral` reicht bis EOF als reales Token → Text nie geändert; sicher |
| Unterminierter `/* Kommentar` | Ein `MultiLineComment` bis EOF; Inneres unangetastet; nur führender Whitespace normalisiert |
| BOM bei Offset 0 | Erhalten; von Unterdrückung ausgenommen; kein Change bei Offset 0 |
| Nur Kommentare / leer / nur Whitespace | Kommentare erhalten + auf Tiefe 0 eingerückt; Trailing-Trim; eine Final-Newline |
| Transition mit jedem Token auf eigener Zeile + Kommentaren | Als **hand-gelegt** erkannt (innere Newline/`//`) → Inneres verbatim, nur Block-Einzug per Delta-Shift re-gesetzt, aus Pfeil-Ausrichtung ausgeschlossen; nie auf eine Zeile gezogen |
| Einzeiliger Inline-Block-Kommentar (`A /*x*/ --> B;`) | **Nicht** hand-gelegt → Umgebungs-Whitespace auf Single-Space normalisiert, Inhalt verbatim; bei Vor-Pfeil-Position aus der Pfeil-Spalte ausgeschlossen |
| `#pragma`-Direktive vor/zwischen Membern | Eigene Zeile ab **Spalte 0**, Text verbatim; wird nie eingerückt oder verschoben |

Es gibt **keinen** Fall, in dem Verbatim-Durchreichen überlappende Edits erzeugt — Durchreichen ist die
*Abwesenheit* eines Edits über disjunkten Lücken.

## Kommentare & Direktiven — Regeln

- **Trailing (gleiche Zeile):** `SingleLineComment` in `A.TrailingTrivia` vor dem ersten Newline → auf der
  Zeile belassen, **genau ein Space** davor.
- **Eigene Zeile (leading, inkl. Banner `// ----`):** auf eigener Zeile auf **aktuellem Block-Einzug**;
  Kommentar-**Text verbatim** (Banner-Innenleben nie anfassen).
- **Einzeiliger `/* */`-Block-Kommentar (inline, kein innerer Newline):** verhält sich wie ein
  Inline-Token — **erzwingt keinen Umbruch** und darf mit den Symbolen auf der Zeile bleiben. Der
  **Umgebungs-Whitespace wird auf ein Space normalisiert**, der Inhalt bleibt verbatim
  (`A/*x*/-->B` → `A /*x*/ --> B`). Löst **nicht** „hand-gelegt" aus. **Ausnahme Ausrichtung:** sitzt er im
  **Vor-Pfeil-Bereich** (zwischen `SourceNode` und `Edge`), wird die Transition zwar normalisiert, aber aus
  der **Pfeil-Spalte ausgeschlossen** (sonst hinge die Spaltenbreite an der Kommentar-Textlänge); ein
  Kommentar *nach* dem Pfeil ist unkritisch.
- **Mehrzeiliger `/* */`-Block-Kommentar:** der **Inhalt wird nie umgebrochen/neu formatiert** (kein Reflow — ASCII-Art,
  eingebettete Beispiele, bewusste Formatierung bleiben). **Aber** die Folgezeilen werden **um dasselbe
  Delta mitgeschoben**, um das die erste Kommentarzeile beim Neu-Einrücken wandert (relative Einrückung
  erhalten, wie Roslyn/ReSharper im konservativen Modus). Kein Ausrichten eines `*`-Präfixes (im Nav-Korpus
  keine Konvention). Fallstricke: **negatives Delta clampt bei Spalte 0** (nie Nicht-Whitespace anfassen);
  der Shift arbeitet auf den **Roh-Whitespace-Präfixen** der Zeilen (nicht auf Spaltenarithmetik), damit
  Tabs im Inneren nicht mehrdeutig werden. Idempotent (nach dem ersten Lauf ist `Delta = 0`). Deckt auch
  unterminierte `/* … EOF` ab. Der neu geschriebene Kommentar wird in das **eine** Replacement seiner Lücke
  eingefaltet → die „ein Change pro Lücke"-Invariante bleibt (einziger gesegneter Fall, in dem Text
  *innerhalb* einer Trivia angefasst wird). **Umgesetzt in S4** (`ShiftInteriorLines`), zusammen mit dem
  gleichgebauten Delta-Shift der hand-gelegten Anweisungen (ein Mechanismus, zwei Abnehmer).
- **Direktiven (`#pragma …`):** strukturierte Trivia, kein Token — der Renderer behandelt sie wie einen
  Eigene-Zeile-Kommentar, aber mit **erzwungenem Einzug 0 und Text verbatim**: eine Direktive bleibt
  immer auf eigener Zeile **ab Spalte 0**. Grund ist das Lexer-Gate (`#` mitten in der Zeile ⇒ `Nav0000`):
  jedes Einrücken oder Verschieben auf eine andere Zeile zerstörte die Direktive — ein Achse-A-Bruch, den
  auch der Laufzeit-Wächter erkennt (s. „Korrektheits-Modell"). Reformatierung des Direktiv-**Inneren**
  (Spacing zwischen `#pragma`, Keyword, Argument) ist zurückgestellt.
- Leerzeilen werden **nicht kollabiert** — die vom Autor gesetzte vertikale Trennung bleibt erhalten
  (nur Trailing-Whitespace auf Leerzeilen wird gestrippt). Grund: Leerzeilen tragen
  seit dem `interruptLines`-Gruppierungskriterium **Bedeutung** (≥2 = Gruppenbruch); ein Kollaps würde
  dieses Signal zerstören und die Gruppierung zwischen zwei Läufen kippen (nicht idempotent).

## Hand-gelegte Anweisungen (mehrzeilig / reich kommentiert)

**Grenzfall:** eine Transition, in der der Autor *jedes* Token auf eine eigene Zeile mit Trailing-/
Leading-Kommentar gelegt hat:

```
Source          // Quelle
    :Port       // Port
    -->         // Kante
    Target      // Ziel
    on Trigger  // Auslöser
    ;           // Ende
```

**Harte Korrektheits-Schranke:** ein `//`-Kommentar läuft bis Zeilenende — das folgende Token kann
**nie** auf dieselbe Zeile gezogen werden (sonst würde es Teil des Kommentars). „Auf eine Zeile
zusammenziehen" ist für eine solche Anweisung also gar nicht verfügbar. (Als Defense-in-Depth: der
**Renderer emittiert nie ein Same-Line-Layout, wenn die vorangehende Trivia ein `//`-Kommentar ist** —
diese „Renderer-Schranke" gilt überall, auch im Task-/`taskref`-Kopf.)

**Kein Teil-Reflow.** Man *könnte* nur dort umbrechen, wo Kommentare es erzwingen, und den Rest
zusammenziehen — aber (a) für Fortsetzungszeilen einer Anweisung existiert **kein** kanonischer Einzug
(Modell ist statement-granular, Tiefe 0/1), (b) die Pfeil-Spaltenausrichtung setzt eine *einzeilige*
`Source <Pfeil> Target`-Form voraus und ergibt für eine mehrzeilige Transition keinen Sinn, (c)
partieller Reflow macht die Idempotenz fragil. Deshalb: **kein Teil-Reflow.**

**Erkennung (strukturell, idempotent):** eine Anweisung gilt als **hand-gelegt**, sobald eine *innere*
Lücke (echt zwischen erstem Token und terminierendem `;`) einen **Newline**, einen **`//`-Kommentar** oder
einen **mehrzeiligen** Block-Kommentar enthält — also alles, was einen Zeilenumbruch erzwingt oder enthält.
Ein **einzeiliger** Block-Kommentar zählt **nicht** (er bleibt auf der Zeile, s. „Kommentare &
Direktiven"). Kanonisch-einzeilige Anweisungen haben nie solche inneren Umbruch-Trivia, hand-gelegte
behalten sie → gleicher Befund bei jedem Lauf. Es ist **dasselbe Primitiv** wie die Fehler-Unterdrückung
(`ComputeSuppressedExtents` bekommt nur eine zweite Quelle). **Ausnahmen von diesem Freeze:** das
mehrzeilige `[params]` im Task-Kopf (kanonisch ausgerichtet, s. „Task-Kopf-Ausrichtung") und die
Kopf-Lücken von `task`/`taskref` selbst (Kanonisierung).

**Verhalten:**

- **Inneres verbatim:** Intra-Zeilen-Spacing und Kommentar-Text unangetastet.
- **Äußere Kante normalisiert:** die Anweisung wird als Ganzes auf den Block-Einzug **re-gesetzt** — jede
  Zeile um dasselbe Einrück-**Delta** verschoben (relative Form erhalten, exakt der Delta-Shift-Mechanismus
  der mehrzeiligen Block-Kommentare: Clamp bei Spalte 0, Roh-Whitespace-Präfixe). Leerzeilen davor/danach
  nach Policy.
- **Aus Ausrichtungsgruppen ausgeschlossen** (keine Pfeil-Spalte für mehrzeilige Transitionen; trennt
  zugleich die Gruppe der einzeiligen Nachbarn).

Unterschied zur Fehler-Unterdrückung: dort ist alles verbatim (dem Baum wird nicht getraut); hier ist die
Struktur *gültig* (nur manuell gelegt) → der äußere Einzug wird per Delta-Shift re-gesetzt statt
eingefroren.

**Bewusste Demut (Trade-off):** der Formatter zieht eine deliberat mehrzeilige Anweisung **nie** auf eine
Zeile zurück und erfindet keinen Fortsetzungs-Einzug — die ehrliche Konsequenz eines token-basierten
Formatters ohne Breiten-/Reflow-Modell, deckungsgleich mit der Korpus-Konvention. **Dial:** noch
konservativer = Inneres *inklusive* erster Zeile komplett verbatim (kein Delta-Shift; korrigiert dann den
äußeren Einzug nicht).

## Ganze Datei vs. Selektion

**Tragendes Modell: `FormatRange` = gefiltertes `FormatDocument`.** Intern wird **immer das ganze
Dokument** formatiert; angewandt (emittiert) werden **nur die Changes, deren Extent im (erweiterten)
Range liegt**:

```
FormatRange(x, r) ≡ { c ∈ FormatDocument(x) : c.Extent ⊆ ExpandTo(r) }
```

Daraus folgt **gratis**: `FormatRange(x, ganzeDatei) == FormatDocument(x)` und **Monotonie** — ein
späterer Voll-Format verschiebt nie, was ein Range-Format schon platziert hat (Range-Format ist eine
*Teilanwendung* desselben Ergebnisses, nie ein Widerspruch dazu).

- **Ganze Datei (`FormatDocument`):** alle Lücken inkl. Final-Gap (Final-Newline/EOF-Trim).
- **Selektion (`FormatRange`):**
  1. **Range erweitern:** erst **auf ganze Zeilen** einrasten, dann **auf ganze Anweisungsknoten**
     ausweiten, die er teilweise schneidet (via `FullExtent`, inkl. Leading-Trivia — so wird auch der
     Einzug der selektierten Anweisung mitkorrigiert) — sonst wird ein mehrzeiliges `[params]` oder eine
     umgebrochene Transition halb formatiert.
  2. **Alle nicht-lokalen Pässe laufen über die volle Datei / den vollen Block, nie range-beschränkt** —
     nur so gilt die Subset-Garantie:
     - `ComputeSuppressedExtents` **datei-weit** (ein fehlendes `}` suppremiert seinen Body unabhängig
       vom Range → eine In-Range-Lücke wird identisch entschieden wie im Voll-Modus).
     - **Gruppierung + `targetCol` block-weit** (die *tragende* Invariante): würde `targetCol` nur über
       In-Range-Zeilen gerechnet, käme eine schmalere Spalte heraus → die In-Range-Zeilen würden auf
       eine Spalte gesetzt, die der Voll-Format wieder verschiebt → **nicht monoton**. Dank kanonischer
       Breite (s. „Spaltenausrichtung") ist `targetCol` ohnehin identisch, auch wenn eine breitere
       Out-of-Range-Zeile gerade falsch formatiert ist.
     - `IndentDepth` aus der Ahnenkette (ohnehin range-unabhängig).
  3. **Nur** Changes mit Extent **⊆ erweitertem Range** emittieren → In-Range-Pfeile bleiben zu
     Out-of-Range-Nachbarn spaltenkonsistent. **Der Final-Gap unterliegt demselben `⊆`-Filter** — er wird
     **nicht** als Extra-Schritt nach dem Filtern angehängt (sonst würde eine Selektion, die das
     Dateiende nicht enthält, dort trotzdem eine Newline einfügen → Edit außerhalb der Auswahl, kein
     Subset mehr).
- **Erwartete (kein Bug) Konsequenz:** zerschneidet die Selektion eine Ausrichtungsgruppe, werden nur die
  In-Range-Zeilen auf `targetCol` gesetzt; Out-of-Range-Nachbarn bleiben ggf. **ragged** — „nur die
  Auswahl anfassen" (Editor-Konvention), löst sich beim nächsten Voll-Format. (Verworfene Alternative:
  das Emittieren auf die ganze Gruppe ausweiten — editiert außerhalb der Selektion, bricht die
  Subset-Garantie.)
- Selektion in Kommentar / über unterdrückte Region → sicher (verbatim, via datei-weite Suppression).

## Optionen & Konfiguration

Neuer Kern-Typ `NavFormattingOptions` (kanonische `Default`-Instanz als „single authority" im Kern,
analog `NavCompletionService.TriggerCharacters`):

- `IndentStyle` (Tabs|Spaces) + `IndentSize` — **kommen aus dem bestehenden Editor-Konfig-Kanal**, nicht
  als neuer Formatter-Knopf: VS `textView.Options` (`ConvertTabsToSpaces`/`IndentSize`; vgl.
  `NavLanguagePreferences.InsertTabs = false` / `IndentSize = 4`, `TextViewExtensions.GetEditorSettings`),
  LSP `FormattingOptions.insertSpaces`/`tabSize`, CLI-Flag. Default bei Unbekannt: **Tabs**
  (Korpus-Mehrheit).
- `AlignArrows = true`, `AlignNodeGrid = true` (das 3-Spalten-Deklarations-Raster `keyword | node |
  rest`, s. „Spaltenausrichtung").
- `AlignTaskHeadBlocks = true` — Task-Kopf-Code-Blöcke stapeln (Block 1 inline, weitere je Zeile
  darunter) **und** mehrzeilige `[params]` unter dem ersten Parameter ausrichten (s. „Task-Kopf-
  Ausrichtung"). Padding immer Spaces.
- `AlignmentColumnPolicy` = `NextTabStop` (Default) | `Tight` | `PreserveDominant` — wie die Zielspalte
  aus den Zeilenbreiten folgt (s. „Spaltenausrichtung"). **Ausrichtungs-Padding ist immer Leerzeichen**
  (nie Tabs), unabhängig vom `IndentStyle` des Einzugs — in Stein gemeißelt.
- `InsertFinalNewline = true`, `TrimTrailingWhitespace = true`. **Kein Leerzeilen-Kollaps** — die
  Anzahl aufeinanderfolgender Leerzeilen wird nie reduziert; **jede** `NewLine`-Regel (auch vor `{`/`}`)
  reicht die Autorenzahl über `GapLayout.NewLine.BlankLinesBefore` unverändert weiter. Einzige
  strukturelle Ausnahme: die `BlankLineBeforeTransitionsRule` **stellt** zwischen Node-Deklarationen und
  Transitionen **mindestens eine** Leerzeile sicher (fügt bei 0 eine ein), kappt aber nach oben nichts.

`TextEditorSettings` (heute `{ TabSize, NewLine }`, geteilt/immutabel) wird **nicht** erweitert —
`IndentStyle` lebt in `NavFormattingOptions`. Newline für emittierte Umbrüche = `settings.NewLine`.

## Kern-API & betroffene Dateien

Neuer Ordner `Nav.Language/Formatting/`:

- `NavFormattingService.cs` — `static IReadOnlyList<TextChange> FormatDocument(SyntaxTree syntaxTree,
  TextEditorSettings settings, NavFormattingOptions options)` (S1–S4) und
  `FormatRange(…, TextExtent range, …)` (S5). Eingabe ist bewusst der `SyntaxTree` (rein syntaktisches
  Feature; Token, Trivia und Syntax-Diagnostics hängen dort — kein Semantik-Build nötig). Intern:
  Gap-Walk, Regelliste, Renderer, `ComputeSuppressedExtents`, `BuildAlignmentMap`.
- `NavFormattingOptions.cs` — Options-Record + `Default` (dazu `IndentStyle.cs`, `AlignmentColumnPolicy.cs`).
- `GapLayout.cs`, `IGapRule.cs`, `RulePriority.cs`, `GapContext.cs`, `GapTrivia.cs`, `ColumnId.cs`,
  `AlignmentMap.cs`, `AlignmentMapBuilder.cs`, `GapRenderer.cs`, `FormatterSuppression.cs` (S4-Fehler-Toleranz-
  Vorpass: Klassifikation je Anweisung/Member in verbatim/hand-gelegt, Hand-gelegt-Deltas, Global-Fallback);
  Dispatcher + Regel-Klassen gebündelt in `GapRules.cs`. Öffentlich sind nur Service + Options(+Enums); die
  Gap-Maschinerie bleibt `internal` (Tests via `InternalsVisibleTo`).

**Wiederverwenden (nicht neu bauen):** `SyntaxTree.Tokens`/`Diagnostics`/`SkippedTokens()` + Trivia-API
(`SyntaxToken.LeadingTrivia`/`TrailingTrivia`/`Extent`/`Parent`), `SourceText.Substring`,
`TextChange.NewReplace`, `TextExtent.FromBounds`, `TextChangeWriter.ApplyTextChanges` (Tests/CLI).
Layout-/Ausrichtungsregeln stützen sich auf `TransitionDefinitionSyntax`/`ExitTransitionDefinitionSyntax`,
`EdgeSyntax`, `TaskDefinitionSyntax` (Kopf-Blöcke + `Identifier`/`OpenBrace`),
`CodeParamsDeclarationSyntax`/`ParameterListSyntax`/`ParameterSyntax`, `NodeDeclarationBlockSyntax`,
`TaskNodeDeclarationSyntax`, `CodeBlockFacts`. Muster-Referenz für Service-Form + „single authority":
`Nav.Language/Completion/NavCompletionService.cs`.

## Vorher/Nachher-Beispiele

Einzug = Tab (hier visuell als Spaces dargestellt); Ausrichtungs-Padding = echte Spaces. Alle Beispiele
mit `AlignmentColumnPolicy = NextTabStop`, `IndentSize` 4.

**Transitionen: Pfeil-Spalten ausrichten + Spacing normalisieren**

```
// vorher
task Sample
{
    init          -->Choice;
    Choice     o-> Dialog;
    Dialog:Ok-->Exit;
}
// nachher
task Sample
{
    init        --> Choice;
    Choice      o-> Dialog;
    Dialog:Ok   --> Exit;
}
```

`Node:Port` bleibt tight; die längste Quelle (`Dialog:Ok`, kanonische Breite 9) ergibt `tightMin` 10,
`NextTabStop` hebt auf Spalte 12 (nächstes Vielfaches von 4).

**Node-Deklarationen: Raster `keyword | node | rest` + eigene `[params]`-Spalte**

```
// vorher
task Foo Alias1;
init Start [params int x];
choice Decide;
task LongerTypeName Alias2;
// nachher
task    Foo             Alias1;
init    Start [params int x];
choice  Decide;
task    LongerTypeName  Alias2;
```

Spalte `node` (Spalte 2) auf dem nächsten Tab-Stopp hinter dem längsten Keyword (`choice`, 6 → Spalte 8);
Spalte `rest` (Spalte 3) hinter dem längsten `node` **mit Alias** (`LongerTypeName`, endet Spalte 22 →
Spalte 24). Der `[params]`-Block von `init Start` nimmt **nicht** an Spalte 3 teil (eigene, tighte
`NodeParams`-Spalte), und weil er hier der einzige params-Teilnehmer ist, steht er mit nur einem
Leerzeichen — kein Wandern nach rechts. `choice Decide;` hat keinen Rest und bekommt kein Phantom-Padding.

Erst **mehrere** aufeinanderfolgende `[params]` richten sich untereinander aus (tight, ein Space hinter
dem längsten node der params-Gruppe):

```
// nachher
init    Kalkulation                 [params BORef a, bool b];
init    KalkulationFromArtikelsuche [params BORef a];
```

**Task-Kopf: Blöcke stapeln + mehrzeiliges `[params]` ausrichten**

```
// vorher
task Sample [code Foo] [params int x, string label] [result bool]
{ … }

// vorher (params vom Autor mehrzeilig gelegt)
task Other
  [params int x,
  string label]
{ … }

// nachher
task Sample [code Foo]
            [params int x, string label]
            [result bool]
{ … }

// nachher (Autor-Umbruch bewahrt -> Parameter unter dem ersten ausgerichtet)
task Other [params int x,
                   string label]
{ … }
```

Der erste Block sitzt ein Space nach dem Identifier; weitere Blöcke stapeln linksbündig darunter
(`TaskHeadBlock`-Spalte von `Sample`: 5+6+1 = 12). Das einzeilige `[params]` (Sample) bleibt einzeilig;
das mehrzeilige (Other) wird unter dem ersten Parameter ausgerichtet (`ParamsList`-Spalte: `[` auf 11 +
`"[params ".Length` 8 = 19), `]` tight. Bei `Other` wird zudem der vom Autor umbrochene erste Block auf
ein Space hinter den Identifier hochgezogen.

**Fehler/Skiped-Token: umschließende Anweisung bleibt verbatim**

```
// vorher                    // nachher
task Broken                  task Broken
{                            {
    init-->A                     init-->A          // fehlendes ';' -> verbatim
    A  @@@  --> B;                A  @@@  --> B;    // Skiped-Token -> verbatim
    B-->Exit;                    B --> Exit;        // intakt -> formatiert
}                            }
```

`B --> Exit;` ist nach Ausschluss der beiden defekten Nachbarn eine **Gruppe der Größe 1** → keine
Spalten-Ausrichtung, nur Single-Space-Normalisierung.

## Korrektheits-Modell (woran „richtig"/„vollständig" hängt)

„Kann es per Definition kein Falsch geben, weil alles opt-in ist?" — nur zur Hälfte. Es gibt **zwei
Achsen** von „korrekt" mit völlig verschiedener Beweisbarkeit.

### Achse A — harte Korrektheit (objektiv, definierbar falsch)

Unabhängig von Geschmack; hier *gibt* es Falsch, und hier liegen die Bugs:

1. **Bedeutungserhalt (Kardinalregel):** `format(x)` muss zum **identischen signifikanten Token-Strom**
   (Typ + Text) zurück-parsen wie `x`, mit **identischer Direktiv-Trivia-Sequenz** (Typ + Text +
   Zeilenanfangs-Position — Direktiven leben in Trivia, ein Token-Strom-Vergleich allein sähe ihre
   Zerstörung nicht) und ohne **neue Diagnostics**. Wer die Tokenisierung ändert (`on Trigger` →
   `onTrigger`; ein Token hinter `//` verschluckt; ein `#pragma` eingerückt), ist *falsch* — egal wessen
   Geschmack.
2. **Idempotenz:** `format(format(x)) == format(x)`.
3. **Totalität & Nicht-Überlappung:** jede Lücke genau eine Entscheidung (Catch-all-Regel), nie ein Crash,
   nie überlappende Edits (Ein-Change-pro-Lücke).

**Architektur-Hebel:** weil wir **nie signifikanten Token-Text anfassen**, schrumpft die
korrektheitskritische Fläche auf eine **kleine, aufzählbare** Menge von „das darf Whitespace nie tun" (zwei
Token verschmelzen, `//` verschluckt ein Token, ein Pflicht-Trenner verschwindet, eine Direktive verliert
ihren Zeilenanfang). Das erlaubt einen **Laufzeit-Wächter (fail-safe):** nach dem Berechnen der Changes das
Ergebnis **re-lexen** und Token-Strom + Direktiv-Trivia vergleichen; weicht etwas ab (oder gibt es neue
Diagnostics), werden die betroffenen Changes **verworfen** — die Datei bleibt dort unverändert. Damit wird
Achse-A-„falsch" **konstruktiv unmöglich**; der Preis ist, im Zweifel *nichts* zu tun statt etwas Falsches.
Achse A ist damit nicht nur testbar, sondern **verifizierbar** — allerdings zum Preis eines vollen
**Re-Parse plus zweitem Apply** je Aufruf (Messung: grobe Verdopplung von Parse + Apply, ~+770 ms über den
Korpus). Weil der Wächter ein reines Entwicklungs-Werkzeug ist (ein Treffer ist **immer** ein Formatter-Bug,
kein Laufzustand) und die Hosts einen Debug-Build ausliefern, läuft er **nicht** unbedingt mit, sondern nur
per **Opt-in** (`NavFormattingOptions.VerifyResult`, Default `false`): die Tests schalten ihn ein, die
ausgelieferten Hosts nicht.

Zwei Präzisierungen, damit der Wächter nicht mehr schadet als er nützt:

- **Granularität: statement-/member-weise, nicht datei-global.** Ein einzelner fehlerhafter Change darf
  nicht das Formatierungsergebnis der ganzen Datei verwerfen (sonst tut der Formatter bei einer 2000-
  Zeilen-Datei wegen *einer* kaputten Lücke gar nichts). Da Changes per Konstruktion disjunkt pro Lücke
  sind und die Suppression ohnehin statement-/member-granular arbeitet (dieselbe Extent-Einheit wie
  `ComputeSuppressedExtents`), re-lext der Wächter **pro Anweisung/Member**: nur die Changes der Einheit,
  deren Re-Lex abweicht, werden verworfen — der Rest der Datei wird formatiert.
- **Sichtbarkeit: ein Wächter-Treffer ist IMMER ein Bug**, kein legitimer Laufzustand (der Formatter
  fasst nie signifikanten Token-Text an). Wenn er läuft, ist er hart: `Debug.Fail` **plus** die betroffenen
  Changes verwerfen und **einmalig auf `stderr`** loggen (host-neutral, konform zur Stdio-Log-Regel) — der
  Bug wird laut statt still verschluckt, im Testlauf feuert er nie. Weil er per Default aus ist
  (`VerifyResult`, s.o.), tragen ihn nur die Tests: sie schalten ihn per Opt-in ein, sodass die
  Selbsttest-Abdeckung über Goldens **und** Korpus erhalten bleibt, während die ausgelieferten Hosts die
  Re-Parse-Kosten nicht zahlen.

### Achse B — Stil/Konvention (subjektiv, keine Grundwahrheit)

Allman vs. K&R, Tabs vs. Spaces, Ausrichten ja/nein, eine vs. zwei Leerzeilen. Hier gibt es **kein
objektives Falsch**, nur „entspricht der gewählten Spezifikation" oder nicht. „Korrekt" heißt: **konform zu
(Optionen + Golden-Beispielen)** — nichts wird bewiesen, es wird *spezifiziert und abgeglichen*. Das
„opt-in" betrifft **nur diese Achse**: es schützt die *Präferenz*, nicht die *Implementierung* — eine
eingeschaltete Regel kann trotzdem falsch sein (Achse A verletzen, Idempotenz brechen, vom eigenen Golden
abweichen).

### „Vollständig" — relativ, nicht geschlossen

Ein Regelwerk ist nie in geschlossener Form „vollständig". Erreichbar ist eine **Abdeckung**: der
Lückenkontext ist `(prevTokenType, nextTokenType, Eltern-Knoten, Trivia-Klasse)`; die real vorkommenden
**Token-Nachbarschaften sind endlich und aufzählbar** (aus Grammatik + Korpus). Vollständigkeit = **jede
erreichbare Nachbarschaft hat eine explizite, getestete Entscheidung**. Das ist das Nächstliegende an
„beweisbar vollständig" und ist *erreichbar* (Adjazenz-Menge generieren, gegen die Regelliste prüfen).
Die Feinheiten der `PunctuationRule` (Typ-Interna, Generik) werden genau über diese Prüfung festgezurrt.

**Fazit:** „kein Falsch, weil opt-in" gilt nur für den Stil. Es gibt ein **scharfes Falsch** (Achse A), das
wir sogar *pro Aufruf* absichern, und ein **konventionelles Falsch** (Achse B), das wir spezifizieren und
gegen den Korpus zur Deckung bringen.

## Verifikation (end-to-end)

- **Golden-Snapshot-Tests** in `Nav.Language.Tests` (Ordner `Formatting/`): Eingabe-`.nav` → erwartete
  Ausgabe, angewandt via `TextChangeWriter.ApplyTextChanges`. Fixtures als Raw-String-Literale
  (`"""…"""`), UTF-8 **mit BOM**, echte Umlaute. **Beide TFMs** grün.
- **Idempotenz-Test:** `format(format(x)) == format(x)` über alle Fixtures.
- **Bedeutungserhalt (Achse A):** `Tokens(format(x)) == Tokens(x)` (signifikante Token, Typ+Text) +
  identische Direktiv-Trivia-Sequenz und **keine neuen Diagnostics** — als Property-Test über Fixtures
  **und** den großen Korpus; zusätzlich als **Laufzeit-Wächter** im Service (bei Abweichung: Changes
  verwerfen, Eingabe zurückgeben) — **Opt-in** via `NavFormattingOptions.VerifyResult`, in den Tests an.
- **Gap-Kontext-Abdeckung:** die aus Grammatik/Korpus erreichbaren `(prevType, nextType, …)`-Nachbarschaften
  aufzählen und sicherstellen, dass jede eine explizite, getestete Layout-Entscheidung hat.
- **Fuzz/Differential:** zufällig gültige bzw. mutierte `.nav` erzeugen, die Achse-A-Invarianten
  (Token-Erhalt, Idempotenz, kein Crash, keine Overlap-Exception) asserten — ohne Output-Orakel.
- **Regel-Disjunktheit:** Test-Modus wertet pro Lücke **alle** Prädikate aus und asertiert
  **Intra-Tier ≤ 1 Match** (dokumentierte Cross-Tier-Präzedenz ausgenommen), über Goldens + Korpus — macht
  die Prioritäts-Reihenfolge zur geprüften statt impliziten Eigenschaft.
- **Fehler-Fixtures:** je Edge-Case-Zeile → Assert: unterdrückte Regionen byte-genau erhalten, keine
  Overlap-Exception.
- **Selektions-Tests:** Range mitten in Block / `[params]` / Kommentar / unterdrückter Region. Zusätzlich
  die **Subset-/Monotonie-Garantie** prüfen: `FormatRange(x, r) == filter(FormatDocument(x), r)` über
  Fixtures, inkl. `r = ganze Datei` ⇒ Gleichheit mit `FormatDocument`, und Range am Dateiende vs. nicht am
  Dateiende (Final-Gap-Filter).
- **Korpus-Smoke (optional, lokal):** `FormatDocument` über eine kuratierte Teilmenge des realen Korpus
  (`d:\tfs\main`, ~1900 `.nav`) + Idempotenz-Prüfung (kein Diff bei zweitem Lauf) — starker Konfidenz-
  Test, nicht eingecheckt.
- **Spalten-Kalibrierung (einmalig, vor dem Festzurren der `AlignmentColumnPolicy`):** über den Korpus
  pro Ausrichtungsgruppe `extra = autorSpalte − tightMin` histogrammieren (Tabs bei `IndentSize`
  aufgelöst) → entscheidet empirisch zwischen `Tight` / `NextTabStop` / `PreserveDominant`.

## Step-Plan

Jeder Step für sich baubar/testbar; nach jedem Step Code-Review + `nav test` (net472) **und**
`dotnet test … -f net10.0` (beide TFMs grün) + gelieferte Commit-Message (Commit macht der Nutzer).
**Fallstrick:** `nav test` baut nicht selbst → vor net472-Tests einmal `nav build`.

| # | Inhalt | Fertig, wenn | Status |
|---|---|---|---|
| **S0** | Dieses Doc + in `.slnx` eingehängt | Doc liegt unter `doc/`, in Solution sichtbar | **erledigt** |
| **S1** | `NavFormattingOptions` + Gap-Infrastruktur (Gap-Enumeration über `Tokens`, `GapContext`, `GapLayout`, Renderer-Gerüst, Ein-Change-pro-Lücke-Invariante inkl. Final-Gap-Sonderrolle) | Leere/triviale Datei = 0 Changes; Round-Trip idempotent | **erledigt** — Ordner `Nav.Language/Formatting/` komplett (Options+Enums, `GapLayout`/`GapContext`/`GapTrivia`/`AlignmentMap`, `GapRenderer` mit Vertikalmodell + Renderer-Schranke, `GapRules`-Dispatcher mit Intra-Tier-Debug-Check, `NavFormattingService`-Walk inkl. `IndentDepth` + Final-Gap-Hook). Regelsatz = Safety + Verbatim-Catch-all ⇒ Identität; Tests `Formatting/` (Service-Eigenschaften + Renderer-Goldens), beide TFMs grün |
| **S2** | Layout-Regeln (fehlerfrei): Allman, Tiefe-0/1-Einzug via Ahnenkette, Member-/Statement-Breaks, Space um Pfeile, tight `Colon`, `PunctuationRule` (Komma/Semikolon/`[`-Ränder/Typ-Interna via Abdeckungs-Prüfung), Final-Newline, Trailing-Trim, **kein** Leerzeilen-Kollaps (Autorenzahl erhalten, nur `BlankLineBeforeTransitionsRule` als Minimum-1); Kommentar-Normalisierung + Direktiven-Erhalt (Spalte 0, verbatim) | Golden für saubere Dateien + Idempotenz grün | **erledigt** — Regelsatz komplett (`BraceOnOwnLineRule` inkl. „nach `{`", `MemberBreakRule` mit Top-Level-`]`-Prüfung via `CodeGenerationUnitSyntax`-Elter, `BlankLineBeforeTransitionsRule`/`StatementBreakRule` per Prädikat disjunkt, `TightColonRule`, `PunctuationRule`, Single-Space-Catch-all); dazu **Datei-Anfang** (`RenderLeadingGap`: Kopf-Kommentare auf Tiefe 0, Direktiven auf Spalte 0, Fehl-Einzug entfernt; Skiped/BOM ⇒ verbatim) und **Final-Lücke** (`RenderFinalGap`: genau eine Final-Newline, EOF-Leerzeilen-Trim, Kommentar-/Direktivzeilen erhalten; leere/Whitespace-Dateien bleiben leer). Renderer-Fix: einzeilig authored Lücke mit Umbruch-Layout emittierte Inline-Kommentare doppelt (in S1 unerreichbar). Goldens `NavFormattingGoldenTests`, beide TFMs grün. Kommentar-Delta-Shift bewusst nach S4 verschoben (s. „Kommentare & Direktiven") |
| **S3** | Ausrichtung: Pfeil-Spalte + Node-Grid (`keyword\|node\|rest`) + **Task-Kopf** (Blöcke stapeln + mehrzeiliges `[params]` unter erstem Parameter, `NewLineAlignedColumn`) inkl. Gruppenbildung (`interruptLines`, Größe-1-Ausnahme) + `AlignmentMap`-Vorpass; **kanonische** Breitenmessung (nie `ToString()`), `AlignmentColumnPolicy` (Default `NextTabStop`, Padding immer Spaces) | Golden mit Spalten + Task-Kopf + Idempotenz grün | **erledigt** — `AlignmentMapBuilder` (Vorpass: Gruppen via `interruptLines ≥ 2`, Hand-gelegt/defekt bricht, Kommentar-Ausschluss je Spalte, kanonische Breite über die Regelentscheidung selbst, je Spalte ≥ 2 Teilnehmer), `ArrowAlignmentRule`/`NodeGridAlignmentRule` (Alignment-Tier, schlagen nur nach; ohne Eintrag Single-Space-Fallback), `TaskHeadLayoutRule` (TokenPair: Block 1 `SingleSpace.PullUp`, Stapel/`[params]` `NewLineAlignedColumn`, `taskref` einzeilig; leeres `[params]` bleibt der `PunctuationRule`), Renderer-Schranken-Ausnahme Pull-up, `GapTrivia.HasLineBreakingComment`, Options im `GapContext`. Policy per Korpus kalibriert (s. „Spaltenausrichtung"); Korpus-Smoke über alle 1913 `.nav`: 0 Crashes, idempotent, Token-Strom erhalten, Intra-Tier-Disjunktheit (Debug-Assert) über jede Lücke. Goldens `NavFormattingAlignmentGoldenTests`, beide TFMs grün |
| **S4** | Fehler-Toleranz: `ComputeSuppressedExtents` (fehlende Struktur-Token, `SkippedTokensTrivia`, Error-Syntax-Diagnostik), BOM-Guard, Global-Fallback; Hand-gelegt-Freeze + Delta-Shift (äußerer Einzug hand-gelegter Anweisungen **und** Innenzeilen mehrzeiliger `/* */`-Kommentare — ein Mechanismus); Laufzeit-Wächter (Achse A) | Edge-Case-Fixtures grün, keine Overlap-Exception, Wächter feuert im Testlauf nie | **erledigt** — `FormatterSuppression` (Klassifikation je Anweisung/Member: `Suppressed` bei fehlendem `;`/`}`, Skiped/Direktive im Statement, Error-Diagnostik über die kleinste umschließende Anweisung — **Code-Block-Inhaltsfehler** wie `[code Foo]`/`[params BADTYPE]` und **BOM-`Nav0000`@0** ausgenommen; `HandLaid` bei Newline/zeilen-erzwingendem Kommentar; `HasUsableMembers`-Global-Fallback), `GapRenderer.ShiftInteriorLines`/`RenderRawShifted`/`OwnLineCommentText` (ein Delta-Shift, zwei Abnehmer — Leerzeilen ohne neuen Trailing-Whitespace, letzte Lücken-Zeile = Einzug vor dem nächsten Token wird mitgeschoben), `NavFormattingService.Guard` (re-parst das Ergebnis, vergleicht Token-Strom + Direktiven + Error-Count; bei Abweichung `Debug.Fail` + verwerfen + `stderr` — **Opt-in** via `NavFormattingOptions.VerifyResult`, Default aus, nur Tests an; s. „Korrektheits-Modell"). Goldens `NavFormattingErrorGoldenTests`, Property-Fixtures + Direktiv-/Fehler-Erhaltung erweitert; Korpus-Smoke 1913×2: 0 Brüche, 0 Crashes, Wächter feuert nie. Beide TFMs grün |
| **S5** | Selektion: `FormatRange` (Zeilen-Einrasten → Anweisungs-Ausweitung → Block-weite Ausrichtung, Changes nur im Range) | Selektions-Fixtures grün | **erledigt** — `FormatRange` als gefiltertes `FormatDocument` (`{ c ∈ FormatDocument(x) : c.Extent ⊆ ExpandRange(r) }`); `ExpandRange` rastet auf ganze Zeilen ein und weitet auf die schneidenden Anweisungs-/Member-Knoten aus (`FormattableNodes` = Transition/Exit-Transition/Node-Deklaration + Task-Kopf-`[params]`), inkl. der vorangehenden Lücke `[prev.End, first.Start]` (`LeadingGapStart`), die als einziger Change den Einzug des Knotens setzt — Ende bleibt bei `node.End` (die Trailing-Lücke setzt den Einzug des *nächsten* Knotens und bleibt draußen). Alle nicht-lokalen Pässe laufen über die volle Datei (Suppression/`targetCol`/Einzug → Subset gilt konstruktiv); der Final-Gap unterliegt demselben `⊆`-Filter (keine Newline, wenn die Auswahl das Dateiende nicht enthält). Tests `NavFormattingRangeTests` (Ganze-Datei-Gleichheit, Subset-Garantie, Idempotenz, Final-Gap-Filter, Auswahl mitten im Block/`[params]`/Kommentar/unterdrückter Region), beide TFMs grün |

**Host-Anbindung (VS + VS Code erledigt):** Die **VS**-Standardbefehle *Format Document*
(`Edit.FormatDocument`) und *Format Selection* (`Edit.FormatSelection`) greifen für `.nav` über den
modernen Commanding-Weg (`FormatCommandHandler : ICommandHandler<FormatDocumentCommandArgs>`/
`<FormatSelectionCommandArgs>` in `Nav.Language.ExtensionShared/Commands/`); zusätzlich eine
**Format-Dokument-Schaltfläche** in der Nav-Editor-Symbolleiste (`NavMarginControl`). Beide teilen den
Helfer `NavFormatCommand` (SyntaxTree via `ParserService`, Optionen aus `ITextView.GetFormattingOptions()`,
undo-fähig über `ITextChangeService`). **VS Code** läuft über den **LSP-Server**:
`textDocument/formatting` + `textDocument/rangeFormatting` (`NavLanguageServer.Formatting`/`RangeFormatting`,
Capabilities `DocumentFormattingProvider`/`DocumentRangeFormattingProvider`); der VS-Code-Client braucht
keine Änderung (die Format-Befehle leuchten über die gemeldeten Capabilities automatisch auf), Format-on-Save
wird bewusst nicht erzwungen. `IndentStyle`/`IndentSize` kommen je Host aus dem Editor-Konfig-Kanal
(VS `textView.Options`, LSP `FormattingOptions.insertSpaces`/`tabSize`).

**Zurückgestellt (nicht v1):** restliche Host-Anbindung (MCP `nav_format` read-only, CLI `format`-Verb mit
`--check`/`--write`);
Mehrspalten-Ausrichtung (`on`/`if`/`do`); die Ausrichtung der **Interna** mehrzeiliger `[params]`
**außerhalb** des Task-Kopfs (Node-Wirte `init`/`choice` bleiben einzeilig — ihr `[params]`-Block als
Ganzes hat aber seit dem Node-Raster eine eigene, tighte `NodeParams`-Spalte; der Task-Kopf ist in S3
enthalten); Format-on-Type/-Paste;
Reformatierung des **Inneren** von Direktiven (`#pragma`-Spacing — ihr Erhalt auf eigener Zeile ab
Spalte 0 ist dagegen v1-Pflicht); EOL-Normalisierung im Inneren mehrzeiliger Kommentare.

**Bewusst ausgeschlossen (nicht nur zurückgestellt):** Inhaltliches Umbrechen/Neu-Formatieren von
Kommentar-Text (Reflow); Ausrichten eines `*`-Präfixes in Block-Kommentaren (keine Nav-Konvention). Das
bloße **Mitschieben** der Kommentar-Innenzeilen um das Einrück-Delta ist hingegen **enthalten** (s.
„Kommentare & Direktiven").
