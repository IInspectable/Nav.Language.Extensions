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

Statistisch über einen großen realen `.nav`-Bestand ermittelt (Größenordnung mehrere tausend Dateien);
die folgenden Anteile sind die dominanten Konventionen:

- **Einzug:** Tab (Breite 4) dominiert (~66% der Dateien tab-lastig) vs. 4 Spaces (~33%); starke
  **Intra-Datei-Mischung** → der Formatter **normalisiert** auf den konfigurierten Stil.
- **Klammern:** Allman (öffnende `{` in eigener Zeile) ~98,5%; schließende `}` immer eigene Zeile.
- **Abschnitts-Reihenfolge:** `[namespaceprefix]`, dann `[using]`-Block, dann `taskref`, dann
  `task`-Block; im Task erst Node-Deklarationen, eine Leerzeile, dann die Transitionen.
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
    sealed record NewLine(int BlankLinesBefore, int IndentDepth) : GapLayout;
    sealed record Verbatim                       : GapLayout;  // unterdrückt / Kommentar-Inneres
}
```

Eine Regel ist eine **reine, isoliert testbare** Mini-Funktion über einen vorberechneten Kontext:

```csharp
interface IGapRule {
    GapLayout? Apply(in GapContext ctx);   // null = "nicht zuständig", nächste Regel fragen
}

readonly struct GapContext {
    // reine, formatierungs-invariante Fakten — NIE das aktuelle Whitespace (außer Newline-Anzahl)
    public SyntaxToken Prev, Next;
    public SyntaxNode? PrevParent, NextParent;
    public int          IndentDepth;   // direkt aus Ahnenkette, nicht aus Nachbar-Operationen
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
    new StatementBreakRule(),            // 3. nach ';' -> NewLine(blank?, depth)
    new BlankLineBeforeTransitionsRule(),// 4. letzte Deklaration -> erste Transition: NewLine(1, depth)
    new TightColonRule(),                // 5. Node ':' Port -> Nothing
    new ArrowAlignmentRule(),            // 6. SourceNode -> Edge in Gruppe -> AlignedColumn(Arrow)
    new InstanceNameAlignmentRule(),     // 7. Typ -> Alias in Gruppe -> AlignedColumn(Instance)
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
- **Instanznamen-Spalte** = Lücke zwischen Typ-Identifier und Alias-Identifier in Node-Deklarationen
  (z.B. `TaskNodeDeclarationSyntax.Identifier` → `IdentifierAlias`).

Algorithmus je Gruppe: (1) Block in **Gruppen** partitionieren, getrennt durch **Leerzeile**, **eigene
Kommentarzeile** oder **unterdrückte Anweisung**. (2) Pro Zeile die natürliche Vor-Spalten-Breite in
**Zeichen** messen (reine Token-Textbreite, z.B. `SourceNode.ToString().Length`). (3)
`targetCol = max(Breiten)`, `pad = targetCol − Breite + 1` (≥1 Space), Lücke durch genau `pad`
**Spaces** ersetzen.

**Idempotenz-Beweis:** `targetCol` ist ein `max` über *Token-Textbreiten* — invariant unter
Formatierung (Token-Text ändert sich nie; Einzug wird separat davor gesetzt und **nicht** in die Breite
eingerechnet → **tab-breiten-unabhängig**, „Tabs für Einzug, Spaces für Ausrichtung"). Zweiter Lauf
rechnet identisches `targetCol` und `pad` → identische Ausgabe. Ausrichtungs-Spaces stehen stets **vor
einem Token derselben Zeile**, nie vor einem Newline → kollidieren nie mit dem Trailing-Whitespace-Trim.

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

Es gibt **keinen** Fall, in dem Verbatim-Durchreichen überlappende Edits erzeugt — Durchreichen ist die
*Abwesenheit* eines Edits über disjunkten Lücken.

## Kommentare — Regeln

- **Trailing (gleiche Zeile):** `SingleLineComment` in `A.TrailingTrivia` vor dem ersten Newline → auf der
  Zeile belassen, **genau ein Space** davor.
- **Eigene Zeile (leading, inkl. Banner `// ----`):** auf eigener Zeile auf **aktuellem Block-Einzug**;
  Kommentar-**Text verbatim** (Banner-Innenleben nie anfassen).
- **`MultiLineComment`:** der **Inhalt wird nie umgebrochen/neu formatiert** (kein Reflow — ASCII-Art,
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
- Leerzeilen um Kommentare auf **max. 1** kollabieren.

## Ganze Datei vs. Selektion

- **Ganze Datei (`FormatDocument`):** alle Lücken + Final-Newline/EOF-Trim.
- **Selektion (`FormatRange`):** (1) Range **auf ganze Zeilen** einrasten, dann **auf ganze
  Anweisungsknoten** ausweiten, die er teilweise schneidet (via `FullExtent`) — sonst wird ein
  mehrzeiliges `[params]` oder eine umgebrochene Transition halb formatiert. (2) Ausrichtungsgruppen über
  den **ganzen umschließenden Block** rechnen, aber **nur Changes für Lücken mit Extent ⊆ erweitertem
  Range** emittieren → In-Range-Pfeile bleiben zu Out-of-Range-Nachbarn spaltenkonsistent. Selektion in
  Kommentar / über unterdrückte Region → sicher (verbatim).

## Optionen & Konfiguration

Neuer Kern-Typ `NavFormattingOptions` (kanonische `Default`-Instanz als „single authority" im Kern,
analog `NavCompletionService.TriggerCharacters`):

- `IndentStyle` (Tabs|Spaces) + `IndentSize` — **kommen aus dem bestehenden Editor-Konfig-Kanal**, nicht
  als neuer Formatter-Knopf: VS `textView.Options` (`ConvertTabsToSpaces`/`IndentSize`; vgl.
  `NavLanguagePreferences.InsertTabs = false` / `IndentSize = 4`, `TextViewExtensions.GetEditorSettings`),
  LSP `FormattingOptions.insertSpaces`/`tabSize`, CLI-Flag. Default bei Unbekannt: **Tabs**
  (Korpus-Mehrheit).
- `AlignArrows = true`, `AlignInstanceNames = true`.
- `InsertFinalNewline = true`, `TrimTrailingWhitespace = true`, Leerzeilen-Kollaps auf 1 (in v1
  hartkodiert).

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

**Node-Deklarationen: Instanznamen-Spalte**

```
// vorher                       // nachher
task Foo Alias1;                 task Foo            Alias1;
task LongerTypeName Alias2;      task LongerTypeName Alias2;
task Bar;                        task Bar;
```

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

## Step-Plan

Jeder Step für sich baubar/testbar; nach jedem Step Code-Review + `nav test` (net472) **und**
`dotnet test … -f net10.0` (beide TFMs grün) + gelieferte Commit-Message (Commit macht der Nutzer).
**Fallstrick:** `nav test` baut nicht selbst → vor net472-Tests einmal `nav build`.

| # | Inhalt | Fertig, wenn | Status |
|---|---|---|---|
| **S0** | Dieses Doc + in `.slnx` eingehängt | Doc liegt unter `doc/`, in Solution sichtbar | **erledigt** |
| **S1** | `NavFormattingOptions` + Gap-Infrastruktur (Gap-Enumeration über `Tokens`, `GapContext`, `GapLayout`, Renderer-Gerüst, Ein-Change-pro-Lücke-Invariante) | Leere/triviale Datei = 0 Changes; Round-Trip idempotent | offen |
| **S2** | Layout-Regeln (fehlerfrei): Allman, Tiefe-0/1-Einzug via Ahnenkette, Space um Pfeile, tight `Colon`, Komma+Space, Final-Newline, Trailing-Trim, Leerzeilen-Kollaps; Kommentar-Normalisierung | Golden für saubere Dateien + Idempotenz grün | offen |
| **S3** | Ausrichtung: Pfeil-Spalte + Instanznamen-Spalte inkl. Gruppenbildung + `AlignmentMap`-Vorpass | Golden mit Spalten + Idempotenz grün | offen |
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

## Verifikation (end-to-end)

- **Golden-Snapshot-Tests** in `Nav.Language.Tests` (Ordner `Formatting/`): Eingabe-`.nav` → erwartete
  Ausgabe, angewandt via `TextChangeWriter.ApplyTextChanges`. Fixtures als Raw-String-Literale
  (`"""…"""`), UTF-8 **mit BOM**, echte Umlaute. **Beide TFMs** grün.
- **Idempotenz-Test:** `format(format(x)) == format(x)` über alle Fixtures.
- **Fehler-Fixtures:** je Edge-Case-Zeile → Assert: unterdrückte Regionen byte-genau erhalten, keine
  Overlap-Exception.
- **Selektions-Tests:** Range mitten in Block / `[params]` / Kommentar / unterdrückter Region.
- **Korpus-Smoke (optional, lokal):** `FormatDocument` über eine kuratierte Teilmenge einer realen
  `.nav`-Codebasis + Idempotenz-Prüfung (kein Diff bei zweitem Lauf) — starker Konfidenz-Test, nicht
  eingecheckt.
