# Inside Code Formatting: Technical Deep Dive

Dieses Dokument erklärt die **Ausrichtungs-Maschinerie** des Nav-Formatters von oben nach unten —
vom Endergebnis (der `AlignmentMap`) über den Vorpass, der sie füllt, bis hinunter zu den
elementaren, formatierungs-invarianten Rohdaten und schließlich zur Konsumseite, die die Map wieder
ausliest. Der Fokus liegt auf dem Ausrichtungs-Teil (Pfeile, Trigger, Bedingungen, Task-Köpfe,
Node-Raster, Trailing-Kommentare); die übrigen Kapitel (Fehler-Toleranz-Vorpass, Treiber-Loop,
Regel-Tiers) reichen wir nach.

Alle Datei- und Zeilenangaben beziehen sich auf `Nav.Language/Formatting/`.

---

## 1. Überblick — eine Engine-Idee: Idempotenz durch Invarianz

Der Formatter läuft im Kern **lokal und pur**: Er geht den Token-Strom Paar für Paar durch und
entscheidet für jede *Lücke* zwischen zwei aufeinanderfolgenden signifikanten Token „was gehört hier
hin?" — nichts, ein Leerzeichen, ein Umbruch. Diese Entscheidung braucht nur die beiden Nachbar-Token
und deren Trivia. Das ist einfach und testbar.

**Ausrichtung durchbricht diese Lokalität.** Damit Pfeile mehrerer Transitionen in einer Spalte stehen

```
init          --> A;
verylongname  --> B;
```

muss die Lücke hinter `init` wissen, wie breit `verylongname` ist — also von *Nachbarzeilen* abhängen.
Das ist die eine Sache, die lokal nicht entscheidbar ist. Die Lösung: ein **Vorpass**
(`AlignmentMapBuilder`) rechnet all diese nicht-lokalen Spalten vorab aus und legt in der
`AlignmentMap` nur das Endergebnis pro Lücke ab. Danach bleibt der eigentliche Formatter wieder
komplett lokal und pur — er schlägt bei einer Lücke nur noch nach.

Die Pipeline hat drei Stufen, durch die ein einziger Wert (`GapContext`) fließt:

```
NavFormattingService   →  baut GapContext   (nur invariante Fakten)
        ↓
GapRules.Select(ctx)   →  wählt EIN GapLayout   (pur, whitespace-blind)
        ↓
GapRenderer.Render(ctx, layout)   →  String   (die einzige Stelle mit Trivia → Text)
```

Der rote Faden durch das gesamte Design ist **Idempotenz durch Invarianz**: Jede Ebene liest nur
*kanonische* Fakten, nie den Ist-Zustand des Whitespace. Deshalb ist ein zweiter Formatierlauf immer
ein No-Op. Dieses Prinzip taucht auf jeder Ebene wieder auf — merke es dir als Prüfstein.

---

## 2. Die AlignmentMap — das Ergebnis des Vorpasses

Die `AlignmentMap` ist eine **vorberechnete Nachschlage-Tabelle für horizontale Ausrichtung**. Genau
genommen sind es zwei `Dictionary<int, int>`:

```
_spacesByGapStart                : Lücke → aufgelöste Anzahl Leerzeichen
_trailingCommentSpacesByGapStart : Lücke vor einem //-Trailing-Kommentar → Anzahl Leerzeichen
```

Der Schlüssel ist jeweils eine **Lücke**, identifiziert über ihre **Startposition** — und das ist per
Konvention das **Ende des vorangehenden Tokens** (`token.End`, ein `int`-Offset in den Quelltext). Der
Wert ist die **fertig aufgelöste Anzahl Leerzeichen**, die an dieser Lücke gerendert werden sollen.

**Warum Space-Zahl und nicht Zielspalte?** Nur der Vorpass kennt die kanonischen Breiten des
Zeileninhalts vor der Lücke. Für eine ausgerichtete Spalte ist die Space-Zahl `Zielspalte − kanonische
Vor-Breite` (das Padding, immer ≥ 1). Der Renderer soll gar nicht mehr rechnen müssen — er soll nur
noch Leerzeichen ausstoßen. Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs.

**Warum zwei Tabellen?** Fast alle Ausrichtungen (Pfeile, Node-Raster, Trigger, Bedingungen,
Task-Köpfe) landen in `_spacesByGapStart` und werden über das reguläre `GapLayout.AlignedColumn`-
Nachschlagen abgeholt. Die **Trailing-`//`-Kommentar-Spalte** ist der Sonderfall: Sie wird nicht über
die normale Gap-Layout-Maschinerie aufgelöst, sondern der `GapRenderer` greift beim Setzen des
Kommentars *direkt* auf `_trailingCommentSpacesByGapStart` zu (siehe [§5](#5-die-konsumseite-der-gaprenderer)
und [§3.5](#warum-trailing-kommentare-anders-behandelt-werden)).

Auf dieser Ebene ist das Ganze also nur eine reine Lookup-Tabelle `Lücke → Leerzeichen`, die die eine
nicht-lokale Formatierungs-Zutat vorberechnet, damit der Rest des Formatters lokal und pur bleiben kann.

---

## 3. Der Ausrichtungs-Vorpass (AlignmentMapBuilder)

`AlignmentMapBuilder.Build` orchestriert den Vorpass: Es partitioniert die Anweisungen block-weit in
Gruppen, misst je Zeile die **kanonische** Vor-Spalten-Breite (nie den Ist-Text), löst die Zielspalte
über die konfigurierte Policy auf und legt in der `AlignmentMap` nur das Ergebnis ab. Bevor wir die
einzelnen Spalten-Familien ansehen, das zentrale konzeptionelle Problem.

### 3.1 Das Henne-Ei-Problem

Der Builder muss die Vor-Spalten-Breite messen, um das Padding zu berechnen. Diese Breite hängt aber
von den *inneren Lücken* ab — und was in eine innere Lücke gehört, entscheiden die `GapRules`. Muss der
Builder also die Regeln vorher laufen lassen? Eine Regel könnte ja theoretisch sagen „zwischen A und B
drei Leerzeichen", und ein späteres Token soll an Spalte X ausgerichtet werden.

Ja — das Problem ist real. Und der Builder löst es, indem er **die Regeln tatsächlich mitlaufen lässt**.

### 3.2 Die Auflösung: Rules mitlaufen lassen + Topologie

Der Kern steckt in `CanonicalGapWidth`:

```csharp
var ctx = new GapContext(prev, next, indentDepth: 0, trivia, isSuppressed: false, AlignmentMap.Empty, options);
return GapRules.Select(in ctx) switch {
    GapLayout.Nothing       => 0,
    GapLayout.SingleSpace   => 1,
    GapLayout.AlignedColumn => 1,
    _                       => -1,
};
```

Das ist wörtlich „lass die Regeln laufen und miss, was rauskommt". Der Builder rekonstruiert keine
parallele Breiten-Logik — er befragt *dieselbe* `GapRules.Select`, die später auch der Renderer
befragt. Damit können Messung und Rendering per Konstruktion nicht auseinanderlaufen.

Dass das nicht in eine Endlosschleife läuft, hat zwei Entkopplungen plus eine Topologie:

**(a) Das Rule-Verdikt hängt nicht von der Map ab, nur die Breite eines `AlignedColumn`.**
`GapRules.Select` entscheidet nur, *welches* Layout eine Lücke bekommt (`Nothing`, `SingleSpace`,
`AlignedColumn(Arrow)`, …). Diese Entscheidung fällt allein aus dem Token-Paar und den Eltern-Knoten.
Nur die *gerenderte Breite* eines `AlignedColumn` steht in der Map. Deshalb darf der Builder beim
Messen `AlignmentMap.Empty` reinreichen und `AlignedColumn` → `1` mappen (den Single-Space-Fallback,
den auch der Renderer nimmt, wenn kein Map-Eintrag existiert).

**(b) In dieser Grammatik ist jede innere Lücke binär: 0 oder 1.** Es gibt keine „feste 3 Spaces"-
Regel. Multi-Space entsteht *ausschließlich* aus Ausrichtung. Jedes andere Verdikt ist `Nothing` (0)
oder `SingleSpace` (1); alles darüber (`NewLine`, `Verbatim`) ist nicht einzeilig-kanonisch → `−1`, und
der Kandidat fällt aus der Spalte. Genau deshalb ist der `switch` sauber auf 0/1 reduzierbar.

**(c) Alignment-auf-Alignment wird links-nach-rechts aufgelöst und memoisiert.** Der eigentliche
Zyklus wäre: eine ausgerichtete Lücke, *links* von der schon eine andere ausgerichtete Lücke liegt.
Der wird durch die **Aufrufreihenfolge in `Build`** gebrochen — Pfeil → Continuation → Trigger →
Condition, strikt links-nach-rechts. Jede aufgelöste Spalte schreibt ihr Padding sofort in `spaces`.
Die Messung der *nachgelagerten* Spalten läuft über `WidthUpToColumn`, das pro innerer Lücke **zuerst
die Map liest**:

```csharp
var start = tokens[i - 1].End;
if (!spaces.TryGetValue(start, out var gapWidth)) {
    gapWidth = CanonicalGapWidth(syntaxTree, options, tokens[i - 1], tokens[i]);
}
```

Die schon aufgelöste Pfeil-Spalte wird als *konkrete Zahl* wiederverwendet, nicht als 1 neu geschätzt.
Keine Spalte misst je über eine Spalte, die noch nicht feststeht — der Abhängigkeitsgraph ist ein DAG,
und die Aufrufreihenfolge ist seine topologische Sortierung. Die vielen „zwingend nach
`AddArrowColumns`"-Kommentare in `Build` sind darum keine Kosmetik, sondern die Terminierungs-Garantie.

### 3.3 Durchgerechnet: Pfeil → Trigger → Condition

Zwei Transitionen in einer Gruppe, mit absichtlich unterschiedlich langen Quell- und Trigger-Namen
(die Eingabe ist bewusst „krumm" — der Builder misst kanonisch, nicht den Ist-Whitespace):

```
Zeile 1:  A     -->   X1      on   Ev1   if   C1   ;
Zeile 2:  Bcde  -->   Longer  on   X     if   C2   ;
```

Token-Längen: `A`=1, `Bcde`=4, `-->`=3, `X1`=2, `Longer`=6, `on`=2, `Ev1`=3, `X`=1, `if`=2.
Optionen: `AlignArrows`, `AlignTriggers`, `AlignConditions`, Policy `Tight`.

**Pass 1 — Pfeil (`AddArrowColumns`).** `CreateArrowCandidate` misst nur den Vor-Pfeil-Teil (hier je
ein Token):

```
Zeile 1:  Width = len("A")    = 1     GapStart = A.End
Zeile 2:  Width = len("Bcde") = 4     GapStart = Bcde.End
```

`tightMin = max(1,4) + 1 = 5`, Policy Tight → `targetCol = 5`:

```
spaces[A.End]    = 5 − 1 = 4
spaces[Bcde.End] = 5 − 4 = 1
```

`spaces` = `{ A.End: 4, Bcde.End: 1 }`. Beide Pfeile fluchten auf Spalte 5.

**Pass 2 — Trigger (`AddTightClauseColumns` mit `TriggerOf`).** `WidthUpToColumn` misst bis vor `on`.
Zeile 1, Tokens `A · --> · X1 · (Stopp bei on)`:

```
width = len("A") = 1                              gapStart = A.End
  Lücke A.End      → spaces → 4   (PFEIL)   width = 1 + 4 + 3  = 8    (+ -->)
  Lücke (-->).End  → canonical → 1          width = 8 + 1 + 2  = 11   (+ X1)   gapStart = X1.End
  Stopp                                     → width = 11, gapStart = X1.End
```

Zeile 2 analog → `width = 15` (`Bcde` und `Longer` sind länger), `gapStart = Longer.End`.
`tightMin = max(11,15) + 1 = 16`:

```
spaces[X1.End]     = 16 − 11 = 5
spaces[Longer.End] = 16 − 15 = 1
```

`on` fluchtet auf Spalte 16.

**Pass 3 — Condition (`AddTightClauseColumns` mit `ConditionOf`).** Jetzt greift *ein* Durchlauf von
`WidthUpToColumn` Ergebnisse aus **zwei** früheren Pässen auf. Zeile 1, bis vor `if`:

```
width = len("A") = 1                                        gapStart = A.End
  Lücke A.End       → spaces → 4     (PFEIL)      width = 1 + 4 + 3  = 8    (+ -->)
  Lücke (-->).End   → canonical → 1               width = 8 + 1 + 2  = 11   (+ X1)
  Lücke X1.End      → spaces → 5     (TRIGGER)    width = 11 + 5 + 2 = 18   (+ on)
  Lücke on.End      → canonical → 1               width = 18 + 1 + 3 = 22   (+ Ev1)  gapStart = Ev1.End
  Stopp                                           → width = 22, gapStart = Ev1.End
```

Zeile 2 analog → `width = 20`, `gapStart = X.End`. `tightMin = max(22,20) + 1 = 23`:

```
spaces[Ev1.End] = 23 − 22 = 1
spaces[X.End]   = 23 − 20 = 3
```

**Render-Ergebnis** (· = Leerzeichen):

```
Spalte:  0    5   9              16  19  23
Zeile 1: A····-->·X1·····on·Ev1·if·C1;
Zeile 2: Bcde·-->·Longer·on·X···if·C2;
```

Drei Spalten fluchten: `-->` auf 5, `on` auf 16, `if` auf 23. Der springende Punkt: **derselbe
`WidthUpToColumn`-Lauf liest bei `A.End`/`Bcde.End` das Pfeil-Padding und wenige Schritte später bei
`X1.End`/`Longer.End` das Trigger-Padding** — beide aus `spaces`, beide aus separaten Pässen. So bleibt
die Messung deckungsgleich mit dem, was Pfeil- und Trigger-Spalte tatsächlich gerendert haben.

**Gegenprobe.** Würde `WidthUpToColumn` die Pfeil-Lücke *nicht* aus `spaces` holen, sondern stur
canonical mit 1 messen, ergäbe sich für Zeile 1 eine Trigger-Breite von 8 statt 11 — und `on` landete
beim echten Rendern (Pfeil-Lücke ist ja 4) auf Spalte 19 statt 16. Die Spalte wäre kaputt. Der
`spaces.TryGetValue`-First ist exakt der Mechanismus, der Messung und Rendering deckungsgleich hält.

### 3.4 Wann WidthUpToColumn nötig ist — und wann nicht

`WidthUpToColumn` steht nur an zwei Quellstellen (`MeasureTransitionClause`, `MeasureTrailingComment`),
läuft aber effektiv einmal pro **rechts-gestapelter** Spalte. `MeasureTransitionClause` ist der
generische Rumpf, der über einen `clauseOf`-Selektor dreimal bestückt wird (Continuation, Trigger,
Condition); `MeasureTrailingComment` ist der vierte Fall.

Die übrigen Spalten (Pfeil, Node-Raster, Task-Kopf) rufen `WidthUpToColumn` *nicht* auf. Das genaue
Kriterium ist nicht „linkeste Spalte", sondern:

> `WidthUpToColumn` wird gebraucht, wenn die Breite von einem Padding abhängt, das ein **früherer,
> separater Pass** aufgelöst hat und das nur noch aus dem `spaces`-Dictionary rückholbar ist.

Eine Spalte umgeht es, wenn ihre Breite *innerhalb ihres eigenen Passes* berechenbar ist — aus zwei
Gründen:

| Spalte | Abhängigkeit | warum kein `WidthUpToColumn` |
|---|---|---|
| **Pfeil** | keine (linkeste) | nichts wiederzuverwenden; eigene Schleife über `CanonicalGapWidth` |
| **Task-Kopf**, **Node-Raster** | von einer früheren Spalte im *selben* Pass | Zwischenwert liegt als lokale `int` vor (`headColumn`/`nodeCol`) |
| **Trigger, Condition, Continuation, Trailing-Kommentar** | von Paddings aus *separaten* Pässen | nur über `spaces` rückholbar → **braucht** `WidthUpToColumn` |

Der Task-Kopf etwa rechnet `headColumn = keyword.Length + 1 + identifier.Length + 1` und
`paramsColumn = headColumn + "[params ".Length` — eine interne Abhängigkeit, aber über lokale
Variablen, nie über `spaces`. Das Node-Raster leitet Spalte 3 aus `nodeCol + node.Length` ab, wieder
lokal. Deshalb ist beides „selbst-tragend".

### 3.5 Gruppenbildung: welche Zeilen teilen eine Spalte? (GroupCandidates)

Ausrichtung ist ein **Gruppen-Phänomen**: erst ab zwei Teilnehmern wird gepaddet
(`participants.Count < 2 → continue`). `GroupCandidates` partitioniert die *nach Position geordnete*
Kandidatenliste in maximale Läufe, die eine Spalte teilen dürfen.

**Drei Zustände, nicht zwei.** Ein Kandidat ist in einem von drei Zuständen:

| Zustand | `BreaksGroup` | `IsAligned` | Wirkung |
|---|---|---|---|
| **Wall** | `true` | — | **kein** Mitglied; beendet die Gruppe und schließt sich selbst aus |
| **Mitläufer** | `false` | `false` | Mitglied, hält die Gruppe zusammen, wird aber nicht gemessen/gepaddet |
| **Teilnehmer** | `false` | `true` | Mitglied, gemessen *und* gepaddet |

Der Mitläufer ist das Subtile. Im Trigger-Pass:

```
A --> X1 on E1;      ← Teilnehmer (hat Trigger)
B --> X2;            ← Mitläufer  (kein Trigger → IsAligned=false, bricht aber nicht)
C --> X3 on E3;      ← Teilnehmer
```

Gruppe `[A, B, C]`, Teilnehmer `[A, C]` → `on` von A und C fluchten. `B` läuft in der Mitte mit, ohne
die Gruppe zu zerreißen und ohne selbst Padding zu bekommen. Würde `B` *brechen*, zerfiele die Gruppe
in `[A]` und `[C]` — keine Ausrichtung. Genau das ist der Unterschied: **Nicht-Teilnahme zerreißt
nicht, Defekt zerreißt.**

**Zwei Bruch-Achsen.**

1. **`breaksGroup` — die harte Wall.** Ein defekter (fehlendes `;`/fehlende Kante) oder hand-gelegter
   (mehrzeiliger) Kandidat ist toxisch: er darf weder teilnehmen noch darf die Spalte über ihn
   *hinwegspringen*. Er beendet die laufende Gruppe und wird per `continue` nie Mitglied.

2. **`InterruptLines >= interruptThreshold` — der weiche Bruch.** Zu viel vertikaler Abstand.
   `InterruptLines = NewLineCount − 1` (ein Newline ist die normale Zeilenendung, kein Interrupt):

   | dazwischen | InterruptLines |
   |---|---|
   | direkt untereinander | 0 |
   | eine Leerzeile | 1 |
   | eine eigene Kommentarzeile | 1 |
   | zwei Leerzeilen | 2 |

   Ein *Trailing*-Kommentar zählt nicht (kein zusätzlicher Newline). Anders als bei der Wall bleiben
   hier beide Seiten Mitglieder ihrer jeweiligen Gruppe.

**Warum die Schwelle mal 2, mal 1 ist.** Pfeil und Node-Raster nutzen `interruptThreshold: 2`, die
tight-Klauseln (Continuation/Trigger/Condition/Trailing-Kommentar) `1`. Dieselbe Situation, eine
Leerzeile dazwischen:

```
A --> X1 on E1;
B --> X2 on E2;
                     ← eine Leerzeile
C --> X3 on E3;
```

- **Pfeil-Pass** (Schwelle 2): `1 < 2` → eine Gruppe `[A,B,C]`, alle drei Pfeile fluchten über die
  Leerzeile hinweg.
- **Trigger-Pass** (Schwelle 1): `1 >= 1` → Bruch in `[A,B]` und `[C]`; C steht allein → keine
  Trigger-Ausrichtung.

Der Gedanke: Der Pfeil ist das dominante visuelle Gerüst eines Transitions-Blocks und soll einen Absatz
(Leerzeile/Kommentar) überspannen. Die tight-Klauseln sind enger geknüpft; eine Leerzeile markiert
einen neuen Absatz, und die Spalte fängt frisch an.

**Idempotenz.** `InterruptLines` liest den Ist-Whitespace, aber die *Entscheidung* ist invariant:
Leerzeilen werden nie unter den Deckel `MaxBlankLines` (≥ 2 oder aus) kollabiert, und beide Schwellen
(1, 2) liegen auf oder unter diesem Boden. Ein Gap mit ≥ 2 Interrupt-Zeilen bleibt ≥ 2, eines mit
genau 1 bleibt 1. Die Klassifikation ändert sich nie durch einen Formatierlauf.

#### Warum Trailing-Kommentare anders behandelt werden

Der Trailing-`//`-Kommentar ist die einzige Ausrichtung mit eigener Tabelle, ohne `ColumnId`, ohne
`GapLayout`. Der Grund ist strukturell: **er richtet kein Token auf einer horizontalen Lücke aus,
sondern Trivia im Trailing-Segment einer Lücke, die ohnehin schon ein Umbruch ist.**

Alle anderen Spalten richten die Lücke *vor einem signifikanten Token* aus (Edge-Keyword, `on`, `if`,
Node-Identifier) — genau dafür ist die `GapRules → GapLayout.AlignedColumn → GapRenderer`-Pipeline
gebaut. Ein Trailing-`//` sitzt aber in der `TrailingTrivia` des *letzten* Tokens; auf seiner Zeile
folgt kein weiteres Token. Die Lücke, die ihn enthält, ist `(letztes Token, erste Anweisung der
nächsten Zeile)` — und die rendert vertikal (`StatementBreakRule → NewLine`).

Daraus folgt jede Abweichung:

- **Kein `GapLayout`:** Die Lücke hat schon ihre eine Entscheidung (`NewLine`). Ein zusätzliches
  `AlignedColumn` wären zwei Entscheidungen für eine Lücke (Ein-Change-pro-Lücke-Invariante).
- **Kein `ColumnId`:** `ColumnId` benennt nur Spalten, die über `GapLayout.AlignedColumn` nachgeschlagen
  werden — der Trailing-Kommentar wird nie so nachgeschlagen.
- **Eigene Tabelle + direkter Read:** Der Renderer holt den Wert dort ab, wo er das Trailing-Segment
  schreibt (mitten in `RenderVertical`, siehe [§5](#5-die-konsumseite-der-gaprenderer)).

Geteilt bleibt trotzdem alles Wesentliche: dieselbe Schlüssel-Konvention (`Prev.End`), dieselbe
Vermessung (`WidthUpToColumn` mit `columnStart: int.MaxValue`), derselbe `AddTightClauseColumns`-
Baustein (`interruptThreshold: 1`). Nur das Ziel-Dictionary ist ein anderes.

*Namensnotiz:* Die Tabelle heißt `trailingCommentSpaces`, nicht `triviaSpaces` — obwohl das *Prinzip*
trivia-positioniert ist, hält sie *tatsächlich* nur Paddings für saubere Trailing-`//`-Kommentare
(Block-Kommentare, Direktiven, Whitespace nehmen nie teil, geprüft in `HasCleanTrailingLineComment` und
am Render-Gate). Der Name beschreibt den Inhalt, nicht die abstrakte Mechanik-Kategorie.

### 3.6 Die Policy-Schicht (ResolveTargetColumn)

Die Policy-Schicht ist der einzige Ort, an dem der Formatter *nicht* rein kanonisch rechnet: Sie
entscheidet, wo genau die Zielspalte einer Gruppe landet. Eingang ist `tightMin = max(kanonische
Breite) + 1` — der **Boden aller Policies** (weniger als ein Leerzeichen gibt es nie); die Policy darf
die Spalte nur nach rechts verschieben.

```csharp
switch (options.AlignmentColumnPolicy) {
    case AlignmentColumnPolicy.Tight:            return tightMin;
    case AlignmentColumnPolicy.PreserveDominant: return Math.Max(tightMin, DominantColumn(authoredColumns));
    default: /* NextTabStop */                   return (tightMin + indentSize - 1) / indentSize * indentSize;
}
```

**Geltungsbereich — nur das breite Gerüst.** `ResolveTargetColumn` wird nur an drei Stellen aufgerufen:
Pfeil-Spalte, Node-Spalte (col 2), Alias-Rest-Spalte (col 3). Alles andere ist **immer tight**:
Trigger, Condition, Continuation, Trailing-Kommentar, Node-`[params]` (`max(Breite) + 1`) sowie der
Task-Kopf (reine Token-Längen-Arithmetik). Dieselbe Asymmetrie wie bei der `interruptThreshold`: das
breite Gerüst ist die dominante Raster-Struktur, an der Autoren historisch Tab-Stopps setzten; die
nachgelagerten Klauseln sollen minimal hinter dem Inhalt kleben, damit ein schwergewichtiger Block
(langer Trigger, `[params]`) die Spalte nicht nach rechts zieht.

**Die drei Policies** am Pfeil-Beispiel (`A` Breite 1, `Bcde` Breite 4, `tightMin = 5`, `IndentSize = 4`):

- **`Tight`** → `targetCol = 5`. Reinste kanonische Form, breiteste Zeile genau ein Space.
- **`NextTabStop`** (Default) → nächstes Vielfaches von `IndentSize ≥ tightMin`: `(5+3)/4*4 = 8`.
  Pfeil auf Tab-Stopp 8. Per Korpus-Kalibrierung bestätigt (~91 % der uniform ausgerichteten
  Pfeil-Spalten lagen auf einem Tab-Vielfachen) — deshalb der deterministische, ausreißer-immune Default.
- **`PreserveDominant`** → `max(tightMin, dominante Ist-Spalte)`. Bewahrt eine breitere, konsistente
  Autorenspalte.

**Der Boden als Überlauf-Schutz.** `tightMin` ist ein Gruppen-Maximum: eine einzige sehr breite Zeile
hebt `tightMin` für alle an und schiebt die ganze Spalte nach rechts. So bekommt niemand je weniger als
ein Leerzeichen, egal welche Policy.

**Idempotenz.** `Tight` und `NextTabStop` sind reine Funktionen kanonischer Breiten → trivial
formatier-invariant. `PreserveDominant` ist die einzige Policy, die den Ist-Whitespace liest (über
`AuthoredColumn`), und deshalb nicht Default — sie ist aber ein *Fixpunkt*: Nach dem ersten Lauf sitzen
alle Teilnehmer exakt auf `targetCol`, `DominantColumn` liefert `targetCol`, und `max(tightMin,
targetCol) = targetCol`. Lauf 2 = Lauf 1.

**Statushinweis.** `PreserveDominant` ist derzeit **nirgends im Einsatz**: Default ist `NextTabStop`,
kein Host verdrahtet die Policy um, und kein Test setzt sie (nur `Tight` und der Default sind abgedeckt).
Der Zweig ist implementiert und korrekt, aber effektiv latent — der unterlegene Kandidat der einmaligen
Korpus-Kalibrierung, als Option erhalten. Nebeneffekt: `AuthoredColumn` wird eager pro Kandidat berechnet
und nie gelesen.

---

## 4. Die invarianten Rohdaten

Alle bisherigen Entscheidungen fußen auf drei elementaren, formatierungs-invarianten Bausteinen. Sie
sind die Wurzel der Idempotenz.

### 4.1 StatementFacts

Die pro Anweisung **einmal** erhobenen Fakten, die sich der Fehler-Toleranz-Vorpass
(`FormatterSuppression`) und alle Ausrichtungs-Vorpässe teilen. Früher las jeder Vorpass die
Token-Liste selbst — pro Transition bis zu fünfmal; `Compute` erhebt es einmal für den ganzen Baum.

Das flache Anweisungs-Set (`EnumerateStatements`) sind Transitionen, Exit-Transitionen und
Node-Deklarationen — Letztere schließen die `taskref`-Verbindungspunkte mit ein, weil
`ConnectionPointNodeSyntax` von `NodeDeclarationSyntax` erbt.

Die Fakten — drei Primitive + ein abgeleitetes:

- **`Tokens`** — die signifikanten Token der Anweisung.
- **`EndsWithSemicolon`** — letztes Token ist `;` (prüft das Node-Raster).
- **`HasStructuralBreakTrivia`** — eine innere Lücke trägt `SkippedTokensTrivia` oder eine Direktive.
- **`SpansMultipleLines`** — eine innere Lücke trägt einen Newline oder einen zeilen-erzwingenden Kommentar.
- **`BreaksSingleLineForm`** (abgeleitet) = `HasStructuralBreakTrivia || SpansMultipleLines` — genau die
  „Toxizität", die bei `GroupCandidates` als `BreaksGroup = true` erscheint.

**Das Herzstück: warum die zwei Trivia-Fakten getrennt bleiben.** Die zwei Konsumenten kombinieren die
Primitive verschieden:

- **`FormatterSuppression`** *unterscheidet* sie: `HasStructuralBreakTrivia` → verbatim (eine Direktive
  ließe sich nicht per Delta-Shift auf Spalte 0 halten); nur `SpansMultipleLines` → hand-gelegt, aber
  gültig → das Innere bleibt byte-genau, nur der äußere Einzug wird per Delta-Shift re-gesetzt.
- **`AlignmentMapBuilder`** ist die Unterscheidung egal — jede nicht mehr einzeilig-kanonische
  Anweisung fällt aus der Spalte. Er OR-t beide zu `BreaksSingleLineForm`.

Die Lektion: **speichere die orthogonalen Primitive, lass jeden Konsumenten sie selbst kombinieren.**

Die `Map` bietet zwei Sichten aus derselben Aufzählung: `All` (Liste, für die Suppression) und
`For(node)` über den Knoten (für den Ausrichtungs-Pass). Weil beide aus derselben Quelle stammen, ist
jeder adressierte Knoten garantiert enthalten — deshalb darf `For` den Indexer nehmen (harter Wurf bei
Miss), nicht `TryGetValue`.

### 4.2 GapTrivia

Die Grundwährung, die in jedem Kapitel auftauchte — der Mechanismus, der die Regeln **whitespace-blind**
hält. Der Klassen-Kommentar: „die einzige Sicht, die Regeln auf den Lückeninhalt bekommen (nie das
aktuelle Whitespace, außer der Newline-Anzahl)."

Der Lückeninhalt zwischen Token A und B ist exakt `A.TrailingTrivia ++ B.LeadingTrivia` (zusammenhängend,
disjunkt) — dieselbe Partition, die auch der Renderer benutzt. `Create` läuft ihn einmal durch und
akkumuliert fünf Fakten:

- **`HasComment`** — irgendein Kommentar (ein- oder mehrzeilig).
- **`HasLineBreakingComment`** — ein Kommentar, der einen Umbruch erzwingt/enthält: ein `//` oder ein
  *mehrzeiliger* Block-Kommentar. Ein einzeiliger `/* x */` zählt nicht (verhält sich wie ein Inline-Token).
- **`HasSkippedTokens`** — schneidet `SkippedTokensTrivia`.
- **`HasDirective`** — enthält eine Direktive.
- **`NewLineCount`** — Anzahl der `NewLine`-Trivia (Newlines *im Inneren* mehrzeiliger Kommentare zählen
  nicht — sie sind Teil des Kommentar-Texts).

Zwei Distinktionen tragen fast die gesamte Logik: `HasComment` vs. `HasLineBreakingComment` (horizontal
einbettbar vs. umbruch-erzwingend) und `NewLineCount` als *Zahl* statt Bool (für `InterruptLines` und
die Leerzeilen-Logik).

**Der Design-Clou: die Abwesenheit eines Whitespace-Falls.** Der `Accumulate`-`switch` hat keinen
`case SyntaxTokenType.Whitespace` — rohe Leerzeichen/Tabs fallen durch und setzen *nichts*. Das ist kein
Versehen, es *ist* der Mechanismus: Whitespace-Blindheit entsteht nicht durch Konvention, sondern
dadurch, dass es keinen Weg gibt, die Menge zu erfragen. Der `struct` ist definiert durch das, was er
bewusst *nicht* exponiert.

Formatier-Invarianz mit einer ehrlichen Nuance: die vier Booleans sind streng invariant (Anwesenheit
von Trivia-Klassen bzw. der eigene Kommentar-Text); `NewLineCount` ist layout-abgeleitet, aber
threshold-stabil (Konsumenten vergleichen nur gegen Schwellen unter dem Kapp-Boden); der einzige
`sourceText`-Blick (`IndexOf('\n')` zur Mehrzeiligkeits-Einstufung) liest die verbatim erhaltenen Bytes
des Kommentars selbst.

### 4.3 GapContext

Der Wert, der durch alle drei Pipeline-Stufen fließt — die formale **Reinheits-Hülle** einer
Lücken-Entscheidung. Eine `IGapRule.Apply` bekommt nichts außer einem `GapContext`; enthält dieser nur
invariante Fakten, sind die Regeln damit *beweisbar* pur.

| Gruppe | Felder | wofür |
|---|---|---|
| **Identität** | `Prev`, `Next` (+ `PrevParent`/`NextParent`) | Token und Baumknoten — Grundlage der Typ-/Parent-Prüfungen |
| **Struktur** | `IndentDepth` | Einzugstiefe für Umbruch-Layouts |
| **Inhalt** | `Trivia` (`GapTrivia`) | die fünf vorberechneten Trivia-Bits |
| **Vorpass-Ergebnisse** | `IsSuppressed`, `Alignment` | Suppression + Ausrichtungs-Tabelle |
| **Konfiguration** | `Options` | Feature-Schalter des Laufs |
| **Position** | `Extent` (abgeleitet) | `[Prev.End, Next.Start)` |

Zwei Felder lohnen den zweiten Blick:

**`IndentDepth` ist die Tiefe von `Next`, nicht von `Prev`.** Ein Umbruch eröffnet eine neue Zeile *vor*
`Next`, und die richtet sich nach dem Token, das sie beginnt. Die Tiefe kommt aus `Next`s Ahnenkette
(strukturell), nie aus Nachbar-Arithmetik — das macht den Einzug robust gegen defekte Nachbarn.

**`Extent` definiert die Schlüssel-Korrespondenz.** `Extent.Start == Prev.End` — und das ist der
Schlüssel, unter dem der Builder schreibt (`spaces[Prev.End]`) und der Renderer liest
(`TryGetSpaces(ctx.Extent.Start)`). Die FullSpans der Token kacheln den Text lückenlos und
überlappungsfrei; aufeinanderfolgende Lücken sind damit paarweise disjunkt und geordnet — die
**Ein-Change-pro-Lücke-Invariante** wohnt geometrisch in diesem `Extent`.

**Jedes Feld ist invariant** — auch die zwei „zustandshaft aussehenden": `Alignment` ist eine Funktion
kanonischer Breiten (invariant, lauf-konstant), `IsSuppressed` eine Funktion der `StatementFacts`
(invariant), `Options` lauf-konstant. Eine Regel, die nur einen `GapContext` sieht, kann per
Konstruktion nicht vom Ist-Whitespace abhängen. Dieselbe Durchsetzungs-Idee wie `GapTrivia`s fehlender
Whitespace-Fall, auf Kontext-Ebene gehoben.

---

## 5. Die Konsumseite: der GapRenderer

Der `GapRenderer` schließt den Kreis: alles, was der Builder mit `spaces[gapStart] = …` gefüllt hat,
wird hier über *eine* Methode wieder ausgelesen:

```csharp
string AlignmentSpaces(in GapContext ctx, string fallback) {
    return ctx.Alignment.TryGetSpaces(ctx.Extent.Start, out var spaces)
        ? new string(' ', spaces)
        : fallback;
}
```

Zwei Dinge sind entscheidend: der Schlüssel `ctx.Extent.Start` ist derselbe `gapStart`, den der Builder
geschrieben hat (kein Neu-Rechnen, reiner `int`-Match), und das Padding ist immer Leerzeichen, nie Tabs.

**Unser Pfeil-Gap end-to-end** (`spaces[A.End] = 4`): Der Treiber trifft das Paar `(A, -->)`,
`GapRules.Select` → `AlignedColumn(Arrow)`, `Render` → `RenderHorizontal(ctx, separator:
AlignmentSpaces(ctx, fallback: " "))`, `AlignmentSpaces` → `TryGetSpaces(A.End)` → 4 → `"    "`. Der
Treiber vergleicht mit dem Ist-Text und emittiert bei Abweichung einen `TextChange`.

**Der Single-Space-Fallback.** `fallback: " "` erklärt zwei Dinge, die wir vorher nur behauptet haben:

- **Warum die Regel „dumm" sein darf.** `ArrowAlignmentRule` liefert *bedingungslos* `AlignedColumn`,
  sobald es ein Vor-Pfeil-Gap ist — sie weiß nichts von Gruppen oder Teilnehmerzahl. Ob die Lücke
  *wirklich* teilnimmt, entscheidet die **Anwesenheit** eines Map-Eintrags. Kein Eintrag → Fallback
  `" "`. Die Regel stellt die Frage, Map + Fallback geben die Antwort.
- **Warum das mit der Messung konsistent ist.** `CanonicalGapWidth` mappte `AlignedColumn → 1`; der
  Renderer rendert ein unaufgelöstes Ausrichtungs-Gap als 1 Leerzeichen. Messung und Rendering sind per
  Konstruktion deckungsgleich — auch am Fallback-Rand.

**Die zweite Tabelle — direkt, ohne `GapLayout`.** Der Trailing-Kommentar wird mitten im
Vertikal-Rendering abgeholt:

```csharp
if (!trailingCommentAligned &&
    trivia.Type == SyntaxTokenType.SingleLineComment &&
    ctx.Alignment.TryGetTrailingCommentSpaces(ctx.Extent.Start, out var pad)) {
    sb.Append(' ', pad).Append(CommentText(trivia));   // Kommentar auf gemeinsame Block-Spalte
} else {
    sb.Append(' ').Append(CommentText(trivia));        // Standard: genau ein Space davor
}
```

Dieselbe Schlüssel-Konvention, aber eigene Tabelle und direkter Zugriff (vgl. [§3.5](#warum-trailing-kommentare-anders-behandelt-werden)).

**`NewLineAlignedColumn` — Spalte nach einem Umbruch.** Der Task-Kopf-Stapel nutzt `AlignmentSpaces`
als *Zeilen-Präfix* statt als Separator: Umbruch, dann so viele Leerzeichen, dass der Folgeblock unter
dem `[` des ersten sitzt. Hier ist die Map-Zahl die *absolute* Spalte nach dem Umbruch; der Fallback ist
folgerichtig der normale Tiefen-Einzug.

**Der Rest des Renderers** ist orthogonal zur Ausrichtung: das Vertikalmodell zerlegt eine Lücke entlang
ihrer Newline-Trivia in Trailing-Segment / Innenzeilen / Leading-Segment (`SplitLines`), erhält die
*authored* Zeilenstruktur, normalisiert pro Zeile nur den Whitespace, kappt Leerzeilen-Läufe
(`CapBlankRuns`) und ergänzt fehlende bis zum Minimum. Die **Renderer-Schranke** (`RequiresLineBreak`)
degradiert ein Same-Line-Layout zum Umbruch, wenn die Lücke einen `//`-Kommentar/eine Direktive/einen
Umbruch trägt — sonst würde ein Token hinter einem `//` verschluckt. Für Ausrichtungs-Gaps ist das
Belt-and-Suspenders: der Builder hat solche Gaps via `CanonicalGapWidth → −1` ohnehin ausgeschlossen.

---

## 6. Der Bogen

Die Ausrichtungs-Maschinerie top-down zusammengefasst:

- **`AlignmentMap`** — das Ergebnis: `Lücke → Space-Zahl`, die eine nicht-lokale Zutat vorberechnet.
- **`AlignmentMapBuilder`** — der Vorpass: Spalten-Familien in *topologischer* Reihenfolge (Pfeil →
  tight-Klauseln), Zyklus gebrochen durch `spaces`-Memoisierung in `WidthUpToColumn`, `GroupCandidates`
  mit dem Drei-Zustands-Modell, Policy-Schicht auf dem breiten Gerüst.
- **`StatementFacts` / `GapTrivia`** — die invarianten Rohdaten, aus denen der Builder misst.
- **`GapContext`** — die Reinheits-Hülle, die alles bündelt.
- **`GapRules` → `GapRenderer`** — die pure Entscheidung und ihre einzige Materialisierung, die die Map
  über `Extent.Start` wieder ausliest und zum Whitespace macht.

Der rote Faden durch alles ist **Idempotenz durch Invarianz**: jede Ebene liest nur kanonische Fakten,
nie den Ist-Zustand — von `GapTrivia`s verschlucktem Whitespace über die threshold-stabile
Gruppenbildung bis zu `PreserveDominant`s Fixpunkt. Deshalb ist ein zweiter Formatierlauf immer ein
No-Op.

---

## Noch offen (folgt)

Diese Kapitel sind in dieser Session nur am Rand gestreift worden und werden nachgereicht:

- **`FormatterSuppression`** — der zweite Vorpass (verbatim vs. hand-gelegt), der ebenfalls
  `StatementFacts` konsumiert und dessen `IsSuppressed`/Delta-Shift in denselben Renderer fließt.
- **Der Treiber-Loop (`NavFormattingService`)** — Leading-Gap, Paar-Schleife, Final-Gap; wer pro Lücke
  `GapRules.Select` + `renderer` aufruft und die `TextChange`s zusammensetzt.
- **Das Regel-System (`GapRules` / `RulePriority`)** — die geordnete Regelliste, die Tier-Präzedenz
  (Safety > Structure > TokenPair > Alignment > Default) und die Disjunktheits-Prüfungen.
