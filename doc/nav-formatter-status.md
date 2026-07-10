# Nav Code-Formatter — Design & Status

> **Lebendes Design-/Tracking-Dokument.** Es wird über **mehrere Runden** fortgeschrieben und
> zunehmend verfeinert; frühere Runden bleiben als Entscheidungsspur erhalten (Abschnitt
> „Entscheidungs-Log"). Stand: **Design-Phase** — es existiert noch **kein** Formatter-Code
> (verifiziert: keine `Format`/`PrettyPrint`/`Indent`-Treffer im `Syntax/`-Bereich außerhalb von
> Diagnostik-Text und C#-Codegen). Umsetzung ist in commit-große Steps zerlegt (Abschnitt „Step-Plan").

## Motivation

Die Nav-Sprache hat keinen Formatter. Gewünscht ist einer, der

- **wahlweise die ganze Datei oder nur die Selektion** normalisiert,
- sich bei **Syntaxfehlern / Unknown / Skiped-Token** robust verhält (nichts kaputt machen),
- **Kommentare** sinnvoll behandelt,
- und ein **empirisch belegtes** Zielformat erzeugt.

Der Formatter wird ein weiterer VS-freier **Feature-Kern in `Nav.Language`** nach dem etablierten
Muster (statischer `Nav<Feature>Service`, Eingabe `CodeGenerationUnit` + Settings, Ausgabe
`IReadOnlyList<TextChange>`, kanonische Defaults als „single authority" im Kern — vgl.
`Nav.Language/Completion/NavCompletionService.cs`). Alle Hosts (LSP/MCP/VS/CLI) könnten ihn später
anbinden.

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
- **`Node:Port`-Doppelpunkt tight** (~99%); Listen: Komma + Space.
- **Mehrzeilige `[params …]`** richten Fortsetzungszeilen unter dem ersten Parameter aus (v1 zurückgestellt).
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

**Load-bearing-Invariante: genau ein `TextChange` pro Lücke, ein einziger Durchlauf.** Alle realen
Nicht-EOF-Token haben Länge ≥ 1, nur EOF ist nullbreit und steht am Ende → die Change-Extents sind
paarweise disjunkt und geordnet → `TextChangeWriter.CheckForOverlappingChanges` kann **nie** werfen.
Dieselbe Invariante macht (a) die Fehler-Unterdrückung sicher (Unterdrücken = *Weglassen* eines Changes
über disjunkten Lücken → nie Overlap) und (b) „kämpfende" Ausrichtungsspalten unmöglich (jede Spalte ist
eine andere Lücke).

**Engine-Skelett:**

```
FormatDocument(unit, settings, options):
    tree       = unit.Syntax
    suppressed = ComputeSuppressedExtents(tree, options)   // Fehler-Regionen + BOM-Guard
    alignment  = BuildAlignmentMap(tree, suppressed)       // Lücke -> Zielspalte (Vorpass)
    changes = []
    toks = tree.Tokens                                     // inkl. terminalem EOF
    for i in 0 .. toks.Count-2:
        gap = Gap(toks[i], toks[i+1])                      // Extent = FromBounds(A.End, B.Start)
        if IntersectsSuppressed(gap, suppressed): continue // verbatim
        layout    = FirstMatchingRule(ctx(gap, ...))       // -> GapLayout (Regelsatz, s.u.)
        canonical = Render(layout, gap-Kommentare, settings)
        if canonical != tree.SourceText.Substring(gap.Extent):
            changes.Add(TextChange.NewReplace(gap.Extent, canonical))   // ≤1 pro Lücke
    changes.Add(RenderFinalGap(lastRealToken, EOF, settings, options))  // Final-Newline / EOF-Trim
    return changes
```

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
    sealed record Verbatim                       : GapLayout;  // unterdrückt / Kommentar-Inneres
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
    public GapTrivia    Trivia;        // hasComment / hasSkipped / newLineCount
    public bool         IsSuppressed;  // aus ComputeSuppressedExtents
    public AlignmentMap Alignment;     // vorberechnet: Lücke -> Zielspalte
}
```

Die **Regelliste ist die Spezifikation** — top-down lesbar, jede Zeile ein Satz:

```csharp
static readonly IReadOnlyList<IGapRule> Rules = [
    new VerbatimWhenSuppressedRule(),    // 1. unterdrückte Region / Kommentar-Inneres -> Verbatim
    new BraceOnOwnLineRule(),            // 2. vor '{' und '}' -> NewLine(0, depth)  (Allman)
    new StatementBreakRule(),            // 3. nach ';' -> NewLine(blank=Autorenzahl, depth)  (kein Kollaps)
    new BlankLineBeforeTransitionsRule(),// 4. letzte Deklaration -> erste Transition: NewLine(max(blank,1), depth)
    new TightColonRule(),                // 5. Node ':' Port -> Nothing
    new ArrowAlignmentRule(),            // 6. SourceNode -> Edge in Gruppe -> AlignedColumn(Arrow)
    new NodeGridAlignmentRule(),         // 7. keyword->node -> AlignedColumn(Node);
                                         //    node->rest -> AlignedColumn(DeclRest)  (3-Spalten-Raster)
    new DefaultSingleSpaceRule(),        // 8. Catch-all -> SingleSpace
];
```

Typische Regelgröße (komplett):

```csharp
sealed class TightColonRule : IGapRule {
    public GapLayout? Apply(in GapContext ctx) =>
        ctx.Next.Type == SyntaxTokenType.Colon || ctx.Prev.Type == SyntaxTokenType.Colon
            ? new GapLayout.Nothing()
            : null;
}
```

Der **Renderer** ist die **einzige** Stelle, die `GapLayout` + erhaltene Kommentare zu einem String macht
(Kommentare werden hier wieder eingefädelt, damit die Regeln simpel bleiben und nur das *Skelett*
bestimmen).

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
Verbatim/Suppression bewusst jede Layout-Regel). Läuft über Goldens **und** den Korpus → aus der impliziten
Ordnungs-Abhängigkeit wird eine **geprüfte** Eigenschaft. Ungewollter Intra-Tier-Overlap ⇒ **Prädikat
verschärfen** (oder Präzedenz explizit dokumentieren), nicht die Reihenfolge „zurechtschieben".

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

- **Pfeil-Spalte** = Lücke zwischen `TransitionDefinitionSyntax.SourceNode` (letztes Token) und
  `Edge.Keyword` (`-->`/`o->`/`==>`, Fortsetzung `--^`/`o-^` — alle 3 Zeichen).
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
    Art: `task` → `IdentifierAlias` **oder** `[donotinject]`/`[abstractmethod]`; `init` → `[params]`/
    `[abstractmethod]`/`do …`; `choice` → `[params]`. **`view`/`dialog`/`exit` haben nie eine Spalte 3**
    (Form `keyword Identifier;`), `end` ebenso wenig. **Nur `task`** kann ein zweites Identifier (Alias)
    tragen. Ausgerichtet über die Lücke `node → rest`; **nur der Start**, nie der Inhalt. Fehlt Spalte 3
    (z.B. `choice Decide;`, `view V;`), gibt es **kein Phantom-Padding**.

  Beide Identifier-Lücken (`keyword → node`, `node → rest`) sind je ein eigener Token-Paar-Lückentyp →
  je ein `AlignedColumn`-Layout, passt bruchlos in „ein Change pro Lücke". Spaltenwerte je Gruppe über
  `AlignmentColumnPolicy` (Default `NextTabStop`). Das ist ein **fester** 3-Spalten-Raster (tractable,
  idempotent) — **nicht** die zurückgestellte Mehrspalten-Ausrichtung, die die *variabel vielen*
  Trailing-Klauseln (`on`/`if`/`do`) an **Transitionen** meint.

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
Ist-Text (s. Fallstrick unten). (3) Zielspalte über die konfigurierte **`AlignmentColumnPolicy`**
bestimmen (Default `NextTabStop`, s.u.); `pad = targetCol − Breite + 1` (≥1 Space), Lücke durch genau
`pad` **Spaces** ersetzen. **Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs** — in Stein gemeißelt,
unabhängig vom `IndentStyle`.

**Fallstrick — Breite kanonisch messen, NIE aus `node.ToString()`:** die Vor-Spalten-Breite muss aus dem
**kanonisch-normalisierten Token-Rendering** des Knotens kommen (Summe der signifikanten Token-Textlängen
+ die inneren Gaps *so, wie der Formatter sie schreiben wird* — für `Node:Port` = 0, für Listen
Komma+Space usw.), **nicht** aus `SourceNode.ToString().Length`. Grund: `ToString()` eines Mehr-Token-
Knotens enthält dessen *Ist*-Whitespace, den der Formatter aber selbst normalisiert (z.B.
`Dialog : Ok` → `Dialog:Ok` via `TightColonRule`). Misst man `ToString()`, wandert `targetCol` zwischen
erstem und zweitem Lauf (11 → 9) → das Gruppen-`max` liefert unterschiedlich viele Padding-Spaces auf den
*anderen* Zeilen → **nicht idempotent** (und schon Lauf 1 richtet falsch aus). Die kanonische Breite
hängt dagegen nur an Token-Text + Regelentscheidung (beide formatierungs-invariant).

**`AlignmentColumnPolicy` (wie `targetCol` aus den kanonischen Breiten folgt):**

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

**Meta-Entscheidung ist Achse-B (Stil, keine Grundwahrheit) → per Korpus zu kalibrieren:** vor dem
endgültigen Festzurren pro Ausrichtungsgruppe im Korpus (`d:\tfs\main`, ~1900 `.nav`) `extra =
autorSpalte − tightMin` (Tabs bei `IndentSize` aufgelöst) histogrammieren. Cluster bei 0 → `Tight`
genügt; Cluster auf Tab-Stopp-Vielfachen → `NextTabStop` (die Arbeitshypothese); hochvariabel/bimodal →
keine bewahrbare Absicht, `Tight` ist der ehrliche Kanon.

**Idempotenz-Beweis:** `targetCol` (in `NextTabStop`/`Tight`) ist eine reine Funktion aus *kanonischen
Token-Breiten* + `IndentSize` — invariant unter Formatierung (Token-Text ändert sich nie; Einzug wird
separat davor gesetzt und **nicht** in die Breite eingerechnet → **tab-breiten-unabhängig**, „Tabs für
Einzug, Spaces für Ausrichtung"). Zweiter Lauf rechnet identisches `targetCol` und `pad` → identische
Ausgabe. Ausrichtungs-Spaces stehen stets **vor einem Token derselben Zeile**, nie vor einem Newline →
kollidieren nie mit dem Trailing-Whitespace-Trim. (`PreserveDominant` bleibt idempotent, weil nach Lauf 1
die Mehrheit exakt auf `targetCol` sitzt → derselbe dominante Wert; es liest aber als einzige Policy den
Ist-Whitespace und ist daher nicht-Default.)

> **Zurückgestellt:** Mehrspalten-Ausrichtung (`on`/`if`/`do`) und `[params]`-Spaltenausrichtung —
> empirisch stark nur die Pfeil-Spalte (~79%) und die Instanznamen-Spalte; die Kaskade bringt wenig
> Nutzen bei viel Komplexität.

## Verhalten bei Fehlern / Unknown / Skiped-Token

Zwei **getrennte** Belange (wichtige Korrektur gegenüber dem ersten Entwurf: **Kommentare sind KEIN
Unterdrückungs-Auslöser** — sonst würde der Formatter bei ~95% der Dateien nichts tun):

**(a) Kommentare** werden **innerhalb** der Lücken-Normalisierung behandelt, nie unterdrückt (s.
„Kommentare").

**(b) Strukturbruch → kleinste umschließende Anweisung unterdrücken (verbatim).** Auslöser:

- Struktur-Token fehlt: `transition.Semicolon.IsMissing`, `task.CloseBrace.IsMissing`.
- Lücke schneidet eine `SkippedTokensTrivia` (gefaltete Skiped-/Unknown-Läufe, `SyntaxTree.SkippedTokens()`).
- Eine **Error-Severity-Syntax-Diagnostik** überlappt die Anweisung — **BOM-`Nav0000` bei Offset 0
  ausgenommen**.

„Unterdrücken" = **für Lücken mit Extent ⊆ `FullExtent` der Anweisung keinen Change erzeugen** (verbatim)
und die Anweisung **aus Ausrichtungsgruppen ausschließen**. Da nur *weggelassen* wird und Lücken disjunkt
sind, kann Unterdrückung **nie** einen Overlap erzeugen. Fehlende `}` ⇒ **gesamten Task-Body verbatim**
(Containment unsicher), alles außerhalb wird weiter formatiert. **Global-Fallback:** keine brauchbaren
Member ⇒ nur die zwei konservativen Changes (Final-Newline, EOF-Trailing-Trim).

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

Es gibt **keinen** Fall, in dem Verbatim-Durchreichen überlappende Edits erzeugt — Durchreichen ist die
*Abwesenheit* eines Edits über disjunkten Lücken.

## Kommentare — Regeln

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
  *innerhalb* einer Trivia angefasst wird). Umsetzung: Teil der Kommentar-Normalisierung (Schritt S2);
  wegen der Seltenheit mehrzeiliger `/* */` kein v1-Blocker.
- Leerzeilen werden **nicht kollabiert** — die vom Autor gesetzte vertikale Trennung bleibt erhalten
  (nur Trailing-Whitespace auf Leerzeilen wird gestrippt, Einzug neu gesetzt). Grund: Leerzeilen tragen
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
Renderer emittiert nie ein Same-Line-Layout, wenn die vorangehende Trivia ein `//`-Kommentar ist.)

**Kein Teil-Reflow.** Man *könnte* nur dort umbrechen, wo Kommentare es erzwingen, und den Rest
zusammenziehen — aber (a) für Fortsetzungszeilen einer Anweisung existiert **kein** kanonischer Einzug
(Modell ist statement-granular, Tiefe 0/1), (b) die Pfeil-Spaltenausrichtung setzt eine *einzeilige*
`Source <Pfeil> Target`-Form voraus und ergibt für eine mehrzeilige Transition keinen Sinn, (c)
partieller Reflow macht die Idempotenz fragil. Deshalb: **kein Teil-Reflow.**

**Erkennung (strukturell, idempotent):** eine Anweisung gilt als **hand-gelegt**, sobald eine *innere*
Lücke (echt zwischen erstem Token und terminierendem `;`) einen **Newline**, einen **`//`-Kommentar** oder
einen **mehrzeiligen** Block-Kommentar enthält — also alles, was einen Zeilenumbruch erzwingt oder enthält.
Ein **einzeiliger** Block-Kommentar zählt **nicht** (er bleibt auf der Zeile, s. „Kommentare").
Kanonisch-einzeilige Anweisungen haben nie solche inneren Umbruch-Trivia, hand-gelegte behalten sie →
gleicher Befund bei jedem Lauf. Es ist **dasselbe Primitiv** wie die Fehler-Unterdrückung
(`ComputeSuppressedExtents` bekommt nur eine zweite Quelle).

**Verhalten:**

- **Inneres verbatim:** Intra-Zeilen-Spacing und Kommentar-Text unangetastet.
- **Äußere Kante normalisiert:** die Anweisung wird als Ganzes auf den Block-Einzug **re-gesetzt** — jede
  Zeile um dasselbe Einrück-**Delta** verschoben (relative Form erhalten, exakt der R4-Mechanismus: Clamp
  bei Spalte 0, Roh-Whitespace-Präfixe). Leerzeilen davor/danach nach Policy.
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
- `AlignmentColumnPolicy` = `NextTabStop` (Default) | `Tight` | `PreserveDominant` — wie die Zielspalte
  aus den Zeilenbreiten folgt (s. „Spaltenausrichtung"). **Ausrichtungs-Padding ist immer Leerzeichen**
  (nie Tabs), unabhängig vom `IndentStyle` des Einzugs — in Stein gemeißelt.
- `InsertFinalNewline = true`, `TrimTrailingWhitespace = true`. **Kein Leerzeilen-Kollaps** — die
  Anzahl aufeinanderfolgender Leerzeilen wird nie reduziert (`GapLayout.NewLine.BlankLinesBefore` gibt
  die vom Autor gesetzte Zahl unverändert weiter). Einzige strukturelle Ausnahme: die
  `BlankLineBeforeTransitionsRule` **stellt** zwischen Node-Deklarationen und Transitionen **mindestens
  eine** Leerzeile sicher (fügt bei 0 eine ein), kappt aber nach oben nichts.

`TextEditorSettings` (heute `{ TabSize, NewLine }`, geteilt/immutabel) wird **nicht** erweitert —
`IndentStyle` lebt in `NavFormattingOptions`. Newline für emittierte Umbrüche = `settings.NewLine`.

## Kern-API & betroffene Dateien

Neuer Ordner `Nav.Language/Formatting/`:

- `NavFormattingService.cs` — `static IReadOnlyList<TextChange> FormatDocument(CodeGenerationUnit unit,
  TextEditorSettings settings, NavFormattingOptions options)` (v1) und
  `FormatRange(…, TextExtent range, …)` (Stufe 2). Intern: Gap-Walk, Regelliste, Renderer,
  `ComputeSuppressedExtents`, `BuildAlignmentMap`.
- `NavFormattingOptions.cs` — Options-Record + `Default`.
- `GapLayout.cs`, `IGapRule.cs`, `GapContext.cs` + Regel-Klassen (klein, je eine Datei oder gebündelt).

**Wiederverwenden (nicht neu bauen):** `SyntaxTree.Tokens` + Trivia-API (`SyntaxToken.LeadingTrivia`/
`TrailingTrivia`/`Extent`/`Parent`), `SourceText.Substring`, `TextChange.NewReplace`,
`TextExtent.FromBounds`, `TextChangeWriter.ApplyTextChanges` (Tests/CLI). Layout-/Ausrichtungsregeln
stützen sich auf `TransitionDefinitionSyntax`, `EdgeSyntax`, `TaskDefinitionSyntax`,
`NodeDeclarationBlockSyntax`, `TaskNodeDeclarationSyntax`. Muster-Referenz für Service-Form + „single
authority": `Nav.Language/Completion/NavCompletionService.cs`.

## Vorher/Nachher-Beispiele

Einzug = Tab (hier visuell als Spaces dargestellt); Ausrichtungs-Padding = echte Spaces.

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
    init      --> Choice;
    Choice    o-> Dialog;
    Dialog:Ok --> Exit;
}
```

`Node:Port` bleibt tight; die längste Quelle (`Dialog:Ok`) definiert die Spalte.

**Node-Deklarationen: 3-Spalten-Raster `keyword | node | rest`** (Spalten auf `NextTabStop`, `IndentSize` 4)

```
// vorher
task Foo Alias1;
init Start [params int x];
choice Decide;
task LongerTypeName Alias2;
// nachher
task    Foo             Alias1;
init    Start           [params int x];
choice  Decide;
task    LongerTypeName  Alias2;
```

Spalte `node` (Spalte 2) auf dem nächsten Tab-Stopp hinter dem längsten Keyword; Spalte `rest`
(Spalte 3) hinter dem längsten `node`. `init`s `[params …]` und `task`s Alias teilen sich Spalte 3
(Variante 2, korpus-treu — ausgerichtet wird der *Start*, nicht der Inhalt); `choice Decide;` hat keine
Spalte 3 und bekommt kein Phantom-Padding.

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

## Entscheidungs-Log (Runden)

- **R1 — Architektur:** Gap-Rewriter statt Voll-Reprint gewählt; Kern verändert nur Trivia-Lücken. Ein
  Change pro Lücke als tragende Invariante (Overlap-Sicherheit gratis).
- **R1 — Zielformat:** empirisch aus einer großen realen `.nav`-Codebasis statt geraten.
- **R2 — Nutzerentscheide:** Pfeil-/Instanznamen-Ausrichtung **aktiviert**; Einzugsstil aus dem
  **bestehenden** Konfig-Kanal (nicht neuer Knopf); v1-Scope = **Kern + Tests** (Hosts zurückgestellt).
- **R2 — Fehler-Modell korrigiert:** Kommentare sind **kein** Unterdrückungs-Auslöser (sonst tut der
  Formatter fast nie etwas); Unterdrückung nur bei echtem Strukturbruch. Einzug ist **flach (0/1)** via
  Ahnenkette statt Klammern zählen. BOM-Guard ergänzt.
- **R3 — Regelsatz-Modell:** bewusste Abgrenzung von Roslyns Operation-/Solver-Modell. Gewählt: winzige
  feste Engine + **flache, geordnete Regelliste** (first-match-wins), Regeln als reine
  `ctx -> GapLayout?`-Funktionen über geschlossenem 5-Werte-Vokabular; einzige nicht-lokale Zutat
  (Ausrichtung) als `AlignmentMap`-Vorpass isoliert.
- **R4 — Mehrzeilige Kommentare:** drei Operationen unterschieden — (1) Inhalt-Reflow **nein**, (2)
  Innenzeilen **um das Einrück-Delta mitschieben** (relative Form erhalten) **ja**, (3) `*`-Präfix-
  Ausrichtung **nein** (keine Nav-Konvention). Frühere Pauschale „Inneres nie verändern" dadurch
  präzisiert. Shift ist idempotent (`Delta = 0` nach erstem Lauf), clampt bei Spalte 0, arbeitet auf
  Roh-Whitespace-Präfixen (Tabs), und faltet sich in das eine Lücken-Replacement ein (Invariante bleibt).
  Häufige `//`-Banner sind ohnehin schon abgedeckt; `/* */` mehrzeilig ist im Korpus selten → kein
  v1-Blocker.
- **R5 — Hand-gelegte Anweisungen:** Grenzfall „jedes Token eigene Zeile + Kommentare" geklärt. Harte
  Schranke: `//` verbietet Zusammenziehen. Kein Teil-Reflow (kein Fortsetzungs-Einzug-Modell, Ausrichtung
  kollabiert, Idempotenz fragil). Trigger **hand-gelegt** = innere Lücke mit Newline/Kommentar
  (strukturell, idempotent, dasselbe Primitiv wie Fehler-Unterdrückung + R4). Inneres verbatim, äußere
  Kante via Delta-Shift re-gesetzt, aus Ausrichtung ausgeschlossen. Bewusste Demut: mehrzeilige Anweisungen
  werden nie auf eine Zeile gezwungen (Dial: alternativ komplett verbatim).
- **R6 — Block-Kommentare präziser:** R5-Trigger verfeinert — der eigentliche Grund für „Finger weg" ist
  ein **Zeilenumbruch**, nicht „irgendein Kommentar". Ein **einzeiliger** `/* */`-Block-Kommentar erzwingt
  keinen Umbruch → löst „hand-gelegt" **nicht** aus, sondern wird wie ein Inline-Token behandelt
  (Umgebungs-Whitespace → Single-Space, Inhalt verbatim). `//` und **mehrzeilige** Block-Kommentare bleiben
  Umbruch-Auslöser. Bewusste Vereinfachung: eine Transition mit Inline-Kommentar im **Vor-Pfeil-Bereich**
  wird normalisiert, aber aus der Pfeil-Spalte ausgeschlossen (sonst hinge die Spaltenbreite am
  Kommentartext).
- **R7 — Korrektheits-Modell:** zwei Achsen getrennt. **Achse A** (Bedeutungserhalt = identischer
  signifikanter Token-Strom + keine neuen Diagnostics, Idempotenz, Totalität/Nicht-Überlappung) ist
  objektiv falsch-definierbar und wird per **fail-safe Laufzeit-Wächter** (Ergebnis re-lexen, bei
  Abweichung Changes verwerfen) **pro Aufruf** abgesichert → Achse-A-Falsch konstruktiv unmöglich. **Achse
  B** (Stil) hat kein objektives Falsch, nur Konformität zu Optionen + Goldens — nur *hier* greift das
  „opt-in". „Vollständig" = jede aus Grammatik/Korpus erreichbare Gap-Kontext-Nachbarschaft hat eine
  getestete Entscheidung (relativ, asymptotisch via Korpus-Diff, nicht geschlossen). Neuer Abschnitt
  „Korrektheits-Modell" + Verifikations-Bullets (Bedeutungserhalt, Abdeckung, Fuzz).
- **R8 — Dispatch & Priorität:** „genau eine Regel pro Lücke" = First-Match-Short-Circuit (Output-Overlap
  unmöglich = Umsetzung der Ein-Change-pro-Lücke-Invariante). Gegen *stillen* Applicability-Overlap:
  Prioritäts-Tiers (`Safety > Structure > TokenPair > Alignment > Default`; „spezifisch schlägt generisch")
  + Test, der pro Lücke alle Prädikate auswertet und **Intra-Tier-Disjunktheit** asertiert (Cross-Tier-
  Präzedenz gewollt/dokumentiert). Reihenfolge wird geprüfte statt implizite Spezifikation. `IGapRule` um
  `Tier` erweitert.
- **R9 — Ausrichtung, Wächter, Reihenfolge, Einzug-Tiefe (Grill-Runde):** fünf Punkte geschärft.
  (1) **Kein Reordering** — der Gap-Rewriter bewegt keine Token; Abschnitts-/`taskref`/`task`-Reihenfolge
  ist deskriptiv, nicht erzwungen (Reordering wäre ein separates Organize-Feature). (2) **`GapContext.
  IndentDepth := IndentDepth(Next)`** — der Einzug richtet sich nach der die neue Zeile eröffnenden
  Token-Tiefe (vorher mehrdeutig). (3) **Ausrichtungsbreite kanonisch messen, nie `node.ToString()`** —
  `ToString()` liest Ist-Whitespace, den der Formatter selbst normalisiert (`Dialog : Ok`→`Dialog:Ok`) →
  Gruppen-`max` wackelt zwischen Läufen → nicht idempotent; der Idempotenz-Beweis trägt nur mit
  kanonischer Breite. (4) **Spaltenpolicy** — Ausrichtungs-Padding **immer Leerzeichen, nie Tabs** (in
  Stein gemeißelt; der Korpus richtet mit Tabs aus, wird auf Spaces normalisiert). Default **`NextTabStop`**
  (nächster Tab-Stopp ab `tightMin`), weil die „weiter als nötig" ausgerichteten Korpus-Spalten
  Tab-Stopp-Artefakte sind, keine präzisen Breiten; Alternativen `Tight`/`PreserveDominant` als Strategie,
  Perzentil-Heuristik verworfen. Finaler Wert per **Korpus-Messung** (`d:\tfs\main`, ~1900 `.nav`) über
  die `extra = autorSpalte − tightMin`-Verteilung. (5) **Laufzeit-Wächter** re-lext **statement-/member-
  granular** (nicht datei-global) und ist **`Debug.Assert` in Debug + stderr-Log in Release** (Treffer =
  immer Bug, nie still verschlucken).
- **R10 — Node-Deklarations-Ausrichtung als 3-Spalten-Raster:** das frühere „Instanznamen-Spalte =
  Lücke Typ→Alias" war unterspezifiziert (nur `task`/`view`/`dialog`-mit-Alias, ignorierte die
  block-weit ausgerichtete Spalte hinter dem Keyword). Korrigiert zum korpus-realen **festen Raster
  `keyword | node | rest`**: `node` = erstes Identifier nach dem Keyword (Typ *oder* Name, über alle
  Node-Arten einheitlich), `rest` = erstes Token nach `node` (**Variante 2, korpus-treu**: Alias *oder*
  `[`-Block *oder* `do` — nur der Start ausgerichtet). Beide Identifier-Lücken je ein `AlignedColumn`,
  Spaltenwerte via `NextTabStop`; fehlende Spalte 3 ohne Phantom-Padding. Ausdrücklich **nicht** die
  zurückgestellte Mehrspalten-Ausrichtung (die betrifft die variabel vielen `on`/`if`/`do`-Klauseln an
  Transitionen). Regel `InstanceNameAlignmentRule` → `NodeGridAlignmentRule`; Option `AlignInstanceNames`
  → `AlignNodeGrid`. (Zwischenzeitlich erwogener, wieder verworfener Vorschlag: Deklarations-Ausrichtung
  ganz aus v1 herausschneiden — hinfällig, da das feste Raster tractable und idempotent ist.)
- **R11 — Gruppierung via `interruptLines`, kein Leerzeilen-Kollaps:** das Ausrichtungs-Gruppen-
  Trennkriterium ist nicht mehr „Leerzeile / eigene Kommentarzeile", sondern die **Zeilenanzahl im
  Leading Trivia** (`interruptLines` = leere + Kommentarzeilen zwischen zwei signifikanten Token). **Neue
  Gruppe ⟺ `interruptLines ≥ 2`** → eine einzelne Leerzeile *oder* eine einzelne Kommentarzeile bricht
  **nicht**; erst zwei (2 Leerzeilen bzw. Leerzeile+Kommentar) tun es (das Abschnitts-Header-Idiom: die
  Leerzeile *vor* dem Kommentar trennt, nicht der Kommentar). Damit einher geht: **Leerzeilen werden gar
  nicht mehr kollabiert** (vorher „max 1"). Grund: Leerzeilen tragen jetzt Gruppierungs-Bedeutung — ein
  Kollaps auf 1 würde ein `interruptLines=2`-Signal auf 1 senken → Gruppen verschmelzen im zweiten Lauf →
  **nicht idempotent** (der Kollaps zerstört genau das Trenn-Signal). Ohne Kollaps ist `interruptLines`
  formatierungs-invariant → Gruppierung trivial idempotent. Einzige verbliebene Blank-Normalisierung:
  `BlankLineBeforeTransitionsRule` sichert **≥1** Leerzeile zwischen Deklarationen und Transitionen (fügt
  bei 0 ein), kappt aber nie. (Verworfene Alternative: Kollaps-Cap 2 statt gar kein Kollaps — wäre auch
  idempotent gewesen, aber der Nutzer bevorzugt volle Erhaltung der vertikalen Trennung.)
- **R12 — Selektion = gefiltertes Voll-Format:** `FormatRange(x, r) ≡ { c ∈ FormatDocument(x) : c.Extent
  ⊆ ExpandTo(r) }` — intern immer das ganze Dokument formatieren, nur die In-Range-Changes anwenden.
  Garantiert `FormatRange(x, ganzeDatei) == FormatDocument(x)` + Monotonie (Voll-Format verschiebt nie,
  was Range-Format platziert hat). Tragende Bedingung: **alle nicht-lokalen Pässe voll-scope** —
  Suppression datei-weit, Gruppierung/`targetCol` block-weit (sonst schmalere Spalte → nicht monoton),
  `IndentDepth` aus Ahnenkette. Der **Final-Gap läuft durch denselben `⊆`-Filter** (nie als Extra-Schritt
  angehängt — sonst Newline-Insert außerhalb der Auswahl). Gruppen-zerschneidende Selektion lässt
  Out-of-Range-Nachbarn bewusst ragged (Editor-Konvention); Alternative „Emittieren auf ganze Gruppe
  ausweiten" verworfen (editiert außerhalb der Auswahl).
- **R13 — Node-Deklarations-Token-Strukturen verifiziert (Korrektur zu R10):** am Syntaxmodell
  gegengeprüft. **Nur `task`** trägt Typ + optional Alias (`Identifier` + `IdentifierAlias`); `init`/
  `choice`/`exit`/`view`/`dialog` sind Keyword + **ein** Identifier (der *Name*, kein Typ), **ohne**
  Alias; `end;` ist **nur Keyword** (kein Identifier → keine node-Spalte). Die R10-Formulierung „bei
  `task`/`view`/`dialog` der Typ" war falsch und ist in der Definition korrigiert. Das 3-Spalten-Raster
  selbst bleibt gültig: `node` = erstes Identifier (Typ nur bei `task`, sonst Name), `rest` = erstes
  Token danach (bei `view`/`dialog`/`exit`/`end` nie vorhanden → kein Phantom-Padding).

## Step-Plan

Jeder Step für sich baubar/testbar; nach jedem Step Code-Review + `nav test` (net472) **und**
`dotnet test … -f net10.0` (beide TFMs grün) + gelieferte Commit-Message (Commit macht der Nutzer).
**Fallstrick:** `nav test` baut nicht selbst → vor net472-Tests einmal `nav build`.

| # | Inhalt | Fertig, wenn | Status |
|---|---|---|---|
| **S0** | Dieses Doc + in `.slnx` eingehängt | Doc liegt unter `doc/`, in Solution sichtbar | **erledigt** |
| **S1** | `NavFormattingOptions` + Gap-Infrastruktur (Gap-Enumeration über `Tokens`, `GapContext`, `GapLayout`, Renderer-Gerüst, Ein-Change-pro-Lücke-Invariante) | Leere/triviale Datei = 0 Changes; Round-Trip idempotent | offen |
| **S2** | Layout-Regeln (fehlerfrei): Allman, Tiefe-0/1-Einzug via Ahnenkette, Space um Pfeile, tight `Colon`, Komma+Space, Final-Newline, Trailing-Trim, **kein** Leerzeilen-Kollaps (Autorenzahl erhalten, nur `BlankLineBeforeTransitionsRule` als Minimum-1); Kommentar-Normalisierung | Golden für saubere Dateien + Idempotenz grün | offen |
| **S3** | Ausrichtung: Pfeil-Spalte + Instanznamen-Spalte inkl. Gruppenbildung + `AlignmentMap`-Vorpass; **kanonische** Breitenmessung (nie `ToString()`), `AlignmentColumnPolicy` (Default `NextTabStop`, Padding immer Spaces) | Golden mit Spalten + Idempotenz grün | offen |
| **S4** | Fehler-Toleranz: `ComputeSuppressedExtents` (fehlende Struktur-Token, `SkippedTokensTrivia`, Error-Syntax-Diagnostik), BOM-Guard, Global-Fallback | Edge-Case-Fixtures grün, keine Overlap-Exception | offen |
| **S5** | Selektion: `FormatRange` (Zeilen-Einrasten → Anweisungs-Ausweitung → Block-weite Ausrichtung, Changes nur im Range) | Selektions-Fixtures grün | offen |

**Zurückgestellt (nicht v1):** Host-Anbindung (LSP `textDocument/formatting`+`rangeFormatting`, MCP
`nav_format` read-only, VS Format-Document/Selection-Command, CLI `format`-Verb mit `--check`/`--write`);
Mehrspalten-Ausrichtung (`on`/`if`/`do`); `[params]`-Spaltenausrichtung; Format-on-Type/-Paste;
Reformatierung von Direktiven (`#pragma`/`#version`); EOL-Normalisierung im Inneren mehrzeiliger Kommentare.

**Bewusst ausgeschlossen (nicht nur zurückgestellt):** Inhaltliches Umbrechen/Neu-Formatieren von
Kommentar-Text (Reflow); Ausrichten eines `*`-Präfixes in Block-Kommentaren (keine Nav-Konvention). Das
bloße **Mitschieben** der Kommentar-Innenzeilen um das Einrück-Delta ist hingegen **enthalten** (s.
„Kommentare").

## Korrektheits-Modell (woran „richtig"/„vollständig" hängt)

„Kann es per Definition kein Falsch geben, weil alles opt-in ist?" — nur zur Hälfte. Es gibt **zwei
Achsen** von „korrekt" mit völlig verschiedener Beweisbarkeit.

### Achse A — harte Korrektheit (objektiv, definierbar falsch)

Unabhängig von Geschmack; hier *gibt* es Falsch, und hier liegen die Bugs:

1. **Bedeutungserhalt (Kardinalregel):** `format(x)` muss zum **identischen signifikanten Token-Strom**
   (Typ + Text) zurück-parsen wie `x`, ohne **neue Diagnostics**. Wer die Tokenisierung ändert
   (`on Trigger` → `onTrigger`; ein Token hinter `//` verschluckt), ist *falsch* — egal wessen Geschmack.
2. **Idempotenz:** `format(format(x)) == format(x)`.
3. **Totalität & Nicht-Überlappung:** jede Lücke genau eine Entscheidung (Catch-all-Regel), nie ein Crash,
   nie überlappende Edits (Ein-Change-pro-Lücke).

**Architektur-Hebel:** weil wir **nie signifikanten Token-Text anfassen**, schrumpft die
korrektheitskritische Fläche auf eine **kleine, aufzählbare** Menge von „das darf Whitespace nie tun" (zwei
Token verschmelzen, `//` verschluckt ein Token, ein Pflicht-Trenner verschwindet). Das erlaubt einen
**Laufzeit-Wächter (fail-safe):** nach dem Berechnen der Changes das Ergebnis **re-lexen** und den
Token-Strom vergleichen; weicht er ab (oder gibt es neue Diagnostics), werden die betroffenen Changes
**verworfen** — die Datei bleibt dort unverändert. Damit wird Achse-A-„falsch" **konstruktiv unmöglich**;
der Preis ist, im Zweifel *nichts* zu tun statt etwas Falsches. Achse A ist also nicht nur testbar,
sondern **pro Aufruf verifizierbar** (Kosten: ein zusätzlicher Lex-Durchlauf — für einen Formatter
vernachlässigbar).

Zwei Präzisierungen, damit der Wächter nicht mehr schadet als er nützt:

- **Granularität: statement-/member-weise, nicht datei-global.** Ein einzelner fehlerhafter Change darf
  nicht das Formatierungsergebnis der ganzen Datei verwerfen (sonst tut der Formatter bei einer 2000-
  Zeilen-Datei wegen *einer* kaputten Lücke gar nichts). Da Changes per Konstruktion disjunkt pro Lücke
  sind und die Suppression ohnehin statement-/member-granular arbeitet (dieselbe Extent-Einheit wie
  `ComputeSuppressedExtents`), re-lext der Wächter **pro Anweisung/Member**: nur die Changes der Einheit,
  deren Re-Lex abweicht, werden verworfen — der Rest der Datei wird formatiert.
- **Sichtbarkeit: ein Wächter-Treffer ist IMMER ein Bug**, kein legitimer Laufzustand (der Formatter
  fasst nie signifikanten Token-Text an). Deshalb in **Debug/Test hart `Debug.Assert`/Fail** — der
  Wächter darf im Testlauf nie feuern; feuert er, ist ein Golden-/Fuzz-Fall reproduziert. In **Release**
  verwerfen + **einmalig auf `stderr`** loggen (host-neutral, konform zur Stdio-Log-Regel). So bleibt
  Achse-A-Sicherheit erhalten, aber der Bug wird laut statt still verschluckt.

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

**Fazit:** „kein Falsch, weil opt-in" gilt nur für den Stil. Es gibt ein **scharfes Falsch** (Achse A), das
wir sogar *pro Aufruf* absichern, und ein **konventionelles Falsch** (Achse B), das wir spezifizieren und
gegen den Korpus zur Deckung bringen.

## Verifikation (end-to-end)

- **Golden-Snapshot-Tests** in `Nav.Language.Tests` (Ordner `Formatting/`): Eingabe-`.nav` → erwartete
  Ausgabe, angewandt via `TextChangeWriter.ApplyTextChanges`. Fixtures als Raw-String-Literale
  (`"""…"""`), UTF-8 **mit BOM**, echte Umlaute. **Beide TFMs** grün.
- **Idempotenz-Test:** `format(format(x)) == format(x)` über alle Fixtures.
- **Bedeutungserhalt (Achse A):** `Tokens(format(x)) == Tokens(x)` (signifikante Token, Typ+Text) und
  **keine neuen Diagnostics** — als Property-Test über Fixtures **und** den großen Korpus; zusätzlich als
  **Laufzeit-Wächter** im Service (bei Abweichung: Changes verwerfen, Eingabe zurückgeben).
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
