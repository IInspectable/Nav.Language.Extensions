# Inside Code Formatting: Technical Deep Dive

Dieses Dokument erklärt den **Nav-Formatter** von oben nach unten. Als **Leitfaden** dient die
Ausrichtung (Pfeile, Continuations, Trigger, Bedingungen, Task-Köpfe, Node-Raster, Trailing-Kommentare) —
sie ist die eine Zutat, die die sonst rein *lokale* Formatierung durchbricht und damit die
Vorpass-Architektur (vorab rechnen, dann pur entscheiden) überhaupt erzwingt. An ihr entlang bauen wir das Bild auf:
vom Endergebnis (der `AlignmentMap`) über den Vorpass, der sie füllt, hinunter zu den elementaren,
formatierungs-invarianten Rohdaten und zur Konsumseite, die die Map wieder ausliest — und weiter zu den
Ebenen, die *alle* Lücken tragen: das Regel-System ([§5](#5-das-regel-system-gaprules-und-rulepriority)),
der Fehler-Toleranz-Vorpass ([§8](#8-der-fehler-toleranz-vorpass-formattersuppression)) und der
Treiber-Loop, der alles orchestriert ([§9](#9-der-treiber-loop-navformattingservice)).

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
Vor-Breite` (das Padding, immer ≥ 1); für die Umbruch-Varianten des Task-Kopfs (`NewLineAlignedColumn`,
[§6](#6-die-konsumseite-der-gaprenderer)) ist der Wert die absolute Spalte nach dem Umbruch — numerisch
wieder genau die Space-Zahl, die nach dem Umbruch auszustoßen ist. Der Renderer soll gar nicht mehr
rechnen müssen — er soll nur noch Leerzeichen ausstoßen. Ausrichtungs-Padding ist immer Leerzeichen,
nie Tabs.

**Warum zwei Tabellen?** Fast alle Ausrichtungen (Pfeile, Continuations, Node-Raster, Trigger,
Bedingungen, Task-Köpfe) landen in `_spacesByGapStart` und werden über das reguläre `GapLayout.AlignedColumn`-
Nachschlagen abgeholt. Die **Trailing-`//`-Kommentar-Spalte** ist der Sonderfall: Sie wird nicht über
die normale Gap-Layout-Maschinerie aufgelöst, sondern der `GapRenderer` greift beim Setzen des
Kommentars *direkt* auf `_trailingCommentSpacesByGapStart` zu (siehe [§6](#6-die-konsumseite-der-gaprenderer)
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
var trivia = GapTrivia.Create(prev, next, syntaxTree.SourceText);
if (trivia.HasComment || trivia.HasDirective || trivia.HasSkippedTokens) {
    return -1;
}

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

Der vorgeschaltete Trivia-Guard ist der eine Fall, in dem gar nicht erst gefragt wird: Trägt die Lücke
einen Kommentar, eine Direktive oder Skipped-Trivia, ist sie nicht einzeilig-kanonisch vermessbar — ein
einzeiliger `/* … */` etwa würde mit seiner Textlänge in die Spaltenbreite eingehen. Solche Lücken
liefern `−1`, und der Kandidat fällt aus der Spalte (er wird zum Mitläufer,
[§3.5](#35-gruppenbildung-welche-zeilen-teilen-eine-spalte-groupcandidates)).

Dass das nicht in eine Endlosschleife läuft, hat zwei Entkopplungen plus eine Topologie:

**(a) Das Rule-Verdikt hängt nicht von der Map ab, nur die Breite eines `AlignedColumn`.**
`GapRules.Select` entscheidet nur, *welches* Layout eine Lücke bekommt (`Nothing`, `SingleSpace`,
`AlignedColumn(Arrow)`, … — das geschlossene Vokabular erklärt
[§5.1](#51-das-layout-vokabular-gaplayout)). Diese Entscheidung fällt allein aus dem Token-Paar und den
Eltern-Knoten — mit einer Ausnahme: die `TaskHeadLayoutRule` fragt für das mehrzeilige `[params]` die
Map selbst ab; dieser Lückentyp liegt aber nie auf einem Messpfad des Builders, die Entkopplung trägt
also überall dort, wo gemessen wird. Nur die *gerenderte Breite* eines `AlignedColumn` steht in der Map. Deshalb darf der Builder beim
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
Variablen, nie über `spaces`. Für `task Login [code MyCode] [result LoginResult r]` heißt das
`headColumn = 4 + 1 + 5 + 1 = 11`: Block 1 bleibt inline (genau ein Space hinter `Login`), jeder
Folgeblock landet nach dem Umbruch linksbündig unter dem `[` des ersten — der Map-Wert `11` ist hier
die absolute Spalte nach dem Umbruch ([§2](#2-die-alignmentmap--das-ergebnis-des-vorpasses)):

```
task Login [code MyCode]
           [result LoginResult r]
```

Das Node-Raster leitet Spalte 3 aus `nodeCol + node.Length` ab, wieder
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

Nicht-Teilnahme hat zwei Ursachen: Die Klausel fehlt (wie bei `B`) — oder eine Mess-Lücke trägt einen
Inline-Block-Kommentar (`A /* x */ --> …`) und die Anweisung wird nur aus der Spalte genommen, weil die
Spaltenbreite sonst an der Kommentar-Textlänge hinge (der Trivia-Guard aus
[§3.2](#32-die-auflösung-rules-mitlaufen-lassen--topologie)). Beides zerreißt nicht.

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
Leerzeilen zwischen den gruppierten Anweisungen werden nie unter den Deckel `MaxBlankLines` (≥ 2 oder
aus) kollabiert, und beide Schwellen (1, 2) liegen auf oder unter diesem Boden. Ein Gap mit ≥ 2 Interrupt-Zeilen bleibt ≥ 2, eines mit
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
  schreibt (mitten in `RenderVertical`, siehe [§6](#6-die-konsumseite-der-gaprenderer)).

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

Der Inhalt — die Token-Liste plus drei primitive Fakten und ein abgeleiteter:

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

## 5. Das Regel-System: GapRules und RulePriority

[§4.3](#43-gapcontext) endete mit der Zusicherung, dass eine `IGapRule.Apply` nichts außer einem
`GapContext` sieht — und darum *beweisbar pur* ist. Dieses Kapitel zeigt die Regeln selbst: die mittlere
Pipeline-Stufe `GapRules.Select(ctx) → GapLayout`, die aus dem Kontext genau eine Layout-Entscheidung
wählt, bevor der Renderer ([§6](#6-die-konsumseite-der-gaprenderer)) sie zu Text macht.

### 5.1 Das Layout-Vokabular (GapLayout)

Eine Regel wählt nicht frei, sondern aus einem **geschlossenen Vokabular** — dem `abstract record
GapLayout` (`GapLayout.cs`). Es gibt keinen Solver und keine Fernwirkung: eine Regel liefert direkt eine
dieser sechs Entscheidungen.

| Layout | Bedeutung |
|---|---|
| `Nothing` | kein Whitespace, Token kleben (`Node:Port`) |
| `SingleSpace` | genau ein Space; Variante `PullUp` zieht bloße authored Newlines hoch (Kopf-Kanonisierung) |
| `AlignedColumn(ColumnId)` | Spaces bis zur vorberechneten Gruppenspalte (die Zahl liest der Renderer aus der [`AlignmentMap`](#2-die-alignmentmap--das-ergebnis-des-vorpasses)) |
| `NewLine(BlankLinesBefore, IndentDepth)` | Umbruch auf Einzugstiefe; `BlankLinesBefore` ist ein Minimum, nie ein Kollaps |
| `NewLineAlignedColumn(BlankLinesBefore, ColumnId)` | Umbruch, dann Spalten-Einzug statt Tiefe (Kopf-Block-Stapel, mehrzeiliges `[params]`); als kanonisch erzwungenes Konstrukt kollabiert der Stapel authored Leerzeilen — die eine Ausnahme vom Nie-Kollabieren (Kommentar-/Direktivzeilen bleiben) |
| `Verbatim` | Lücke unverändert — es wird kein Change emittiert |

Der springende Punkt: **eine Regel → genau ein vollständiges Layout**, nie eine Kombination. Das ist die
[Ein-Change-pro-Lücke-Invariante](#43-gapcontext) auf Entscheidungsebene — dieselbe, die geometrisch im
disjunkten `Extent` wohnt. Die `ColumnId` in den beiden Aligned-Varianten ist reine Selbstdokumentation
im Regel-Code; der Renderer wertet sie nicht aus, sondern schlägt die Space-Zahl allein über
`Extent.Start` nach (der Vorpass hat die spaltenspezifische Logik längst erledigt).

### 5.2 Die Regel als reine Mini-Funktion (IGapRule)

Eine `IGapRule` (`IGapRule.cs`) ist eine isoliert testbare Mini-Funktion `GapContext → GapLayout?`:

```csharp
GapLayout? Apply(in GapContext ctx);   // null = „nicht zuständig", sonst genau ein Layout
RulePriority Tier { get; }             // der Prioritäts-Tier (§5.4)
```

`null` heißt „nicht zuständig — nächste Regel fragen". Weil eine Regel **ausschließlich
formatierungs-invariante Fakten** aus dem Kontext liest (Token-Typen, Baumstruktur, Newline-Anzahl — nie
das Ist-Whitespace), ist Idempotenz laut Klassen-Kommentar „eine *lokale* Eigenschaft jeder Regel statt
einer emergenten des Gesamtsystems". Das ist die Regel-Ebene des Leitmotivs: fünfzehn lokal-pure
Entscheidungen, keine davon kann vom Whitespace abhängen.

### 5.3 Die geordnete Liste IST die Spezifikation

Der Dispatcher `GapRules` (`GapRules.cs:25`) ist kein Regel-Solver, sondern eine **feste, von Hand nach
Tier geordnete Liste**, die top-down gelesen die ganze Spezifikation ist:

```csharp
static readonly IGapRule[] Rules = {
    // Safety
    new VerbatimWhenSuppressedRule(),      // unterdrückte Region -> Verbatim
    // Structure
    new BraceOnOwnLineRule(),              // vor '{'/'}' und nach '{' -> eigene Zeile (Allman)
    new BlankLineAroundBlockMembersRule(), // um task/taskref-Block-Member + nach [namespaceprefix] -> mindestens eine Leerzeile
    new MemberBreakRule(),                 // nach '}' und nach Top-Level-']' -> neuer Member auf Tiefe 0
    new BlankLineBeforeTransitionsRule(),  // Blockgrenze Deklarationen -> Transitionen: mindestens eine Leerzeile
    new StatementBreakRule(),              // nach ';' -> nächste Anweisung auf eigener Zeile
    // TokenPair
    new TightColonRule(),                  // Node ':' Port -> tight
    new PunctuationRule(),                 // tight vor ','/';', [-Innenränder, Typ-Interna
    new TaskHeadLayoutRule(),              // Task-/taskref-Kopf: Block 1 inline (Pull-up), Folgeblöcke gestapelt, mehrzeiliges [params]
    // Alignment
    new ArrowAlignmentRule(),              // Quell-Teil -> Edge-Keyword in Gruppe -> Pfeil-Spalte
    new ContinuationAlignmentRule(),       // Ziel-Teil -> --^/o-^ in Gruppe -> Continuation-Spalte
    new TriggerAlignmentRule(),            // Ziel-Teil -> on/spontaneous in Gruppe -> Trigger-Spalte
    new ConditionAlignmentRule(),          // Ziel-Teil -> if/else if/else in Gruppe -> Condition-Spalte
    new NodeGridAlignmentRule(),           // keyword -> node bzw. node -> rest -> 3-Spalten-Raster
    // Default
    new DefaultSingleSpaceRule(),          // Catch-all -> genau ein Space
};
```

Fünfzehn Regeln in fünf Tiers. Der Tier — `RulePriority` (`RulePriority.cs`) — ist eine **semantische**
Einordnung, kein Listenindex-Raten, und folgt dem Prinzip **„spezifisch schlägt generisch"**:

| Tier | Regeln | typische Ausgabe | Rolle |
|---|---|---|---|
| **Safety** | `VerbatimWhenSuppressedRule` | `Verbatim` | unterdrückte Region unangetastet — preemptiert alles |
| **Structure** | `BraceOnOwnLineRule`, `MemberBreakRule`, `StatementBreakRule`, `BlankLineBeforeTransitionsRule`, `BlankLineAroundBlockMembersRule` | `NewLine` | das vertikale Gerüst (Umbrüche, Leerzeilen-Minima) |
| **TokenPair** | `TightColonRule`, `PunctuationRule`, `TaskHeadLayoutRule` | `Nothing` / `SingleSpace.PullUp` / `NewLineAlignedColumn` | spezifische Token-Nachbarschaften + Kopf-Kanonisierung |
| **Alignment** | `ArrowAlignmentRule`, `ContinuationAlignmentRule`, `TriggerAlignmentRule`, `ConditionAlignmentRule`, `NodeGridAlignmentRule` | `AlignedColumn` | die Spalten-Familien aus §2/§3 |
| **Default** | `DefaultSingleSpaceRule` | `SingleSpace` | Catch-all — garantiert Totalität |

- **Safety (1).** `VerbatimWhenSuppressedRule` liest `ctx.IsSuppressed` — das Verdikt des
  Fehler-Toleranz-Vorpasses ([§8](#8-der-fehler-toleranz-vorpass-formattersuppression)) — und liefert
  `Verbatim`, sobald die Lücke in einer unterdrückten Region liegt. Als höchster Tier preemptiert sie
  *jede* Layout-Regel: in einem kaputten Task-Body ist belanglos, dass ein Pfeil-Gap „eigentlich"
  ausgerichtet würde.
- **Structure (5).** Das vertikale Gerüst — wo ein Umbruch beginnt und wie viele Leerzeilen mindestens
  stehen. Die Umbruch-Setzer (`BraceOnOwnLineRule` Allman, `MemberBreakRule` nach `}`/Top-Level-`]`,
  `StatementBreakRule` nach `;`) und die zwei Leerzeilen-Minimum-Heber (`BlankLineBeforeTransitionsRule`
  Deklarationen → Transitionen, `BlankLineAroundBlockMembersRule` um Body-Block-Member). Alle liefern
  `NewLine`; keine kappt je Autoren-Leerzeilen — `BlankLinesBefore` ist nur ein Boden.
- **TokenPair (3).** Spezifische Token-Nachbarschaften, die sonst der Catch-all mit falschen Spaces
  flutete: `TightColonRule` (`Node:Port` tight), `PunctuationRule` (tight vor `,`/`;`, `[…]`-Innenränder,
  Typ-Interna) und `TaskHeadLayoutRule` (Kopf-Kanonisierung: Block 1 inline per Pull-up, Folgeblöcke
  gestapelt, mehrzeiliges `[params]`).
- **Alignment (5).** Die Spalten-Familien aus [§2](#2-die-alignmentmap--das-ergebnis-des-vorpasses)/[§3](#3-der-ausrichtungs-vorpass-alignmentmapbuilder).
  Jede liefert *bedingungslos* `AlignedColumn(…)`, sobald die Lücke *strukturell* eine Ausrichtungs-Lücke
  ist — ob sie **wirklich** teilnimmt, entscheidet allein die Anwesenheit eines Map-Eintrags (sonst
  Single-Space-Fallback, [§6](#6-die-konsumseite-der-gaprenderer)). Die Regel stellt die Frage, Map +
  Fallback geben die Antwort; genau deshalb dürfen diese Regeln „dumm" sein.
- **Default (1).** `DefaultSingleSpaceRule` liefert bedingungslos `SingleSpace` und garantiert
  **Totalität**: jede Lücke bekommt eine Entscheidung. Der `return GapLayout.Verbatim.Instance` am Ende
  von `Select` ist damit unerreichbar, solange der Catch-all in der Liste steht — er ist nur die sichere
  Antwort für den unmöglichen Fall.

`Select` selbst ist dann nur noch der Short-Circuit „erste passende Regel gewinnt":

```csharp
public static GapLayout Select(in GapContext ctx) {
    AssertIntraTierDisjoint(ctx);
    foreach (var rule in Rules) {
        var layout = rule.Apply(in ctx);
        if (layout != null) {
            return layout;
        }
    }
    return GapLayout.Verbatim.Instance;   // unerreichbar (Catch-all)
}
```

### 5.4 Die zwei Ordnungs-Invarianten

Die Reihenfolge ist von Hand gelegt und wird **nie zur Laufzeit sortiert** — die Deklarationsreihenfolge
*ist* die Cross-Tier-Präzedenz.

**`RulePriority` steuert die Auswahl nicht — es prüft sie.** `Select` liest `.Tier` nie; die Präzedenz
lebt allein in der Array-Reihenfolge, die erste passende Regel gewinnt. Der Tier ist ausschließlich der
Prüfstein der beiden folgenden Wächter (und einer Test-Assertion) — ohne ihn liefe der Dispatch
byte-für-byte gleich, nur ungeschützt gegen eine falsch einsortierte Regel. Zwei Wächter halten die
händische Ordnung ehrlich:

**(a) Cross-Tier: monoton nach Tier — harter Wurf.** `EnsureRulesOrderedByTier` (`GapRules.cs:106`) läuft
im statischen Konstruktor beim Laden des Typs und wirft eine `InvalidOperationException`, falls eine
spätere Regel einen niedrigeren Tier trägt als eine frühere. Weil der Dispatcher die Liste unverändert
iteriert, bräche eine falsch einsortierte neue Regel die Präzedenz sonst *still*. Die Prüfung ist
lücken-unabhängig — sie sieht nur die statische Liste, nicht einen konkreten Kontext.

**(b) Intra-Tier: höchstens eine Regel pro Tier zuständig — Debug-Assert.** `AssertIntraTierDisjoint`
(`:79`, `[Conditional("DEBUG")]`) wertet für *jede* Lücke *alle* Prädikate aus und schlägt fehl
(`Debug.Fail`), wenn zwei Regeln desselben Tiers dieselbe Lücke matchen. Cross-Tier-Overlaps sind
**gewollt** (der höhere Tier preemptiert); ein Intra-Tier-Overlap dagegen heißt: die Prädikate sind nicht
scharf genug.

Die Designregel dahinter — **„Prädikat verschärfen, nicht die Reihenfolge zurechtschieben"** — ist im
Structure-Tier direkt sichtbar. Nach einem `;` an der Grenze Deklarationen → Transitionen könnten
`StatementBreakRule` *und* `BlankLineBeforeTransitionsRule` (beide Structure) feuern. Statt sich auf die
Listenposition zu verlassen, zieht sich `StatementBreakRule` per Prädikat zurück:

```csharp
if (BlankLineBeforeTransitionsRule.IsDeclarationToTransitionBoundary(in ctx)) {
    return null;   // die Leerzeile setzt die andere Regel — Disjunktheit per Prädikat, nicht per Reihenfolge
}
```

So besitzt genau eine Regel die Lücke, und die Disjunktheit ist eine *geprüfte* Eigenschaft, keine stille
Ordnungs-Abhängigkeit. Mehrere Structure-Regeln tragen darum solche wechselseitigen Guards
(`RaisesTopLevelBlankLine`, `IsDeclarationToTransitionBoundary`).

### 5.5 Durchgerechnet: ein Dispatch und seine Preemption

Nehmen wir die Pfeil-Lücke `(A, -->)` aus [§3.3](#33-durchgerechnet-pfeil--trigger--condition). `Select`
läuft die Liste ab:

```
VerbatimWhenSuppressedRule    IsSuppressed? nein               -> null
BraceOnOwnLineRule …          Prev/Next kein {/}/; …           -> null   (5x Structure)
TightColonRule, Punctuation   kein ':'/',' …                   -> null
TaskHeadLayoutRule            kein Kopf-Block                  -> null
ArrowAlignmentRule            NextParent = EdgeSyntax unter     -> AlignedColumn(Arrow)   ✋ return
                              TransitionDefinitionSyntax
```

Neun Regeln sagen „nicht zuständig", die zehnte trifft. Der Renderer liest dann `spaces[A.End] = 4`
aus der Map und macht daraus vier Leerzeichen ([§6](#6-die-konsumseite-der-gaprenderer)).

**Cross-Tier-Preemption.** Dieselbe Lücke, aber in einem defekten (klammer-losen) Task-Body: Jetzt greift
schon die **erste** Regel — `VerbatimWhenSuppressedRule` (Safety) sieht `ctx.IsSuppressed == true` und
liefert `Verbatim`. `Select` kehrt sofort zurück, `ArrowAlignmentRule` wird nie gefragt. Der höhere Tier
short-circuitet vor dem niedrigeren — und der Treiber lässt für ein `Verbatim`-Layout den Change ganz weg
([§8](#8-der-fehler-toleranz-vorpass-formattersuppression)). Genau so setzt „spezifisch schlägt generisch"
die Rangfolge **Sicherheit vor Ausrichtung** durch, ohne dass die Ausrichtungs-Regel etwas davon wissen
muss.

Der Bogen zum Leitmotiv: Der Dispatcher komponiert fünfzehn lokal-pure, whitespace-blinde Regeln zu
*einer* totalen Funktion `GapContext → GapLayout` mit *einer* geprüften Präzedenz. Kein Solver, keine
Fernwirkung, kein Ist-Whitespace — die Idempotenz jeder einzelnen Regel trägt sich per Konstruktion bis
ins Gesamtergebnis.

---

## 6. Die Konsumseite: der GapRenderer

Der `GapRenderer` schließt den Kreis: alles, was der Builder mit `spaces[gapStart] = …` gefüllt hat,
wird hier über *eine* Methode wieder ausgelesen:

```csharp
string AlignmentSpaces(in GapContext ctx, string fallback) {
    // Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs — unabhängig vom Einzugsstil.
    return ctx.Alignment.TryGetSpaces(ctx.Extent.Start, out var spaces) ? new string(' ', spaces) : fallback;
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
degradiert ein Same-Line-Layout zum Umbruch, wenn die Lücke einen `//`-Kommentar, einen mehrzeiligen
Block-Kommentar, eine Direktive oder einen Umbruch trägt — sonst würde ein Token hinter einem `//`
verschluckt. Für Ausrichtungs-Gaps ist das
Belt-and-Suspenders: der Builder hat solche Gaps via `CanonicalGapWidth → −1` ohnehin ausgeschlossen.

---

## 7. Der Bogen der Ausrichtung

Die Ausrichtungs-Maschinerie top-down zusammengefasst:

- **`AlignmentMap`** — das Ergebnis: `Lücke → Space-Zahl`, die eine nicht-lokale Zutat vorberechnet.
- **`AlignmentMapBuilder`** — der Vorpass: Spalten-Familien in *topologischer* Reihenfolge (Pfeil →
  tight-Klauseln), Zyklus gebrochen durch `spaces`-Memoisierung in `WidthUpToColumn`, `GroupCandidates`
  mit dem Drei-Zustands-Modell, Policy-Schicht auf dem breiten Gerüst.
- **`StatementFacts` / `GapTrivia`** — die invarianten Rohdaten, aus denen der Builder misst.
- **`GapContext`** — die Reinheits-Hülle, die alles bündelt.
- **`GapRules` → `GapRenderer`** — die pure Entscheidung und ihre einzige Materialisierung, die die Map
  über `Extent.Start` wieder ausliest und zum Whitespace macht.

Der rote Faden bis hierher ist **Idempotenz durch Invarianz**: jede Ebene liest nur kanonische Fakten,
nie den Ist-Zustand — von `GapTrivia`s verschlucktem Whitespace über die threshold-stabile
Gruppenbildung bis zu `PreserveDominant`s Fixpunkt. Deshalb ist ein zweiter Formatierlauf immer ein
No-Op.

Zwei Ebenen stehen noch aus: der Vorpass, der entscheidet, welche Lücken überhaupt angefasst werden
dürfen ([§8](#8-der-fehler-toleranz-vorpass-formattersuppression)), und die imperative Schale, die
alles zu *einem* Durchlauf sequenziert ([§9](#9-der-treiber-loop-navformattingservice)).

---

## 8. Der Fehler-Toleranz-Vorpass (FormatterSuppression)

Die bisherige Maschinerie lief unter einer stillen Doppel-Annahme: der Syntaxbaum ist intakt, und jede
Anweisung steht auf einer Zeile. `FormatterSuppression` ist der Vorpass, der genau diese Annahme
absichert. Er ist der **Zwilling des `AlignmentMapBuilder`**: beide laufen einmal *vor* dem Token-Loop,
beide lesen dieselben geteilten `StatementFacts` ([§4.1](#41-statementfacts)) — sie fragen nur
Verschiedenes. Der Builder fragt „welche Lücke bekommt wie viel Padding?"; die Suppression fragt „welche
Lücke darf ich überhaupt anfassen?".

Sein Verdikt je Anweisung ist eines von dreien:

| Klasse | Auslöser | Behandlung |
|---|---|---|
| **Normal** | einzeilig, strukturell gültig | voll formatiert — der Regelfall, kein Eintrag nötig |
| **Hand-gelegt** | mehrzeilig, aber gültig (endet mit `;`, keine Skipped-Trivia/Direktive) | Inneres **byte-genau**, nur der äußere Einzug per **Delta-Shift** re-gesetzt |
| **Verbatim** | Strukturbruch: fehlendes Token, Skipped-Trivia/Direktive, Error-Diagnostik | komplett unangetastet — dem Baum wird nicht getraut |

Der Vorpass produziert daraus drei Ausgänge, die alle nur *invariante* Fakten (fehlende Token,
Trivia-Klasse, Diagnostics) lesen — die Klassifikation ist damit selbst formatier-invariant, ganz im
Sinne des Leitmotivs **Idempotenz durch Invarianz**:

```
_verbatimExtents          : Liste der byte-genau zu erhaltenden Regionen   → IsSuppressed(gap)
_handLaidShiftByGapStart  : Lücke → Einrück-Delta einer hand-gelegten Zeile → TryGetHandLaidShift(start)
HasUsableMembers          : trägt die Datei überhaupt Member?              → Global-Fallback im Treiber
```

### 8.1 Die drei Quellen der Unterdrückung

`Compute` (`FormatterSuppression.cs:71`) sammelt Verbatim-Regionen aus drei unabhängigen Quellen.

**(1) Kaputte Blöcke — fehlende Klammern eines `task`/`taskref`.** `AddBrokenBlock` (`:141`) macht die
Asymmetrie der beiden fehlenden Klammern explizit:

```csharp
if (openBrace.IsMissing) {
    verbatim.Add(blockExtent);                                    // fehlendes '{' -> ganzer Task
} else if (closeBrace.IsMissing) {
    verbatim.Add(TextExtent.FromBounds(openBrace.End, blockExtent.End));  // fehlendes '}' -> ab hinter '{'
}
```

Fehlt das `{`, ist der Body gar nicht mehr lokalisierbar (Containment unsicher) → der **ganze** Task
wird verbatim. Fehlt nur das `}`, bleibt der Kopf (`task Name`) formatierbar, verbatim ist erst der
Body ab hinter dem `{`. Alles außerhalb des defekten Blocks formatiert normal weiter.

**(2) Error-Severity-Diagnostik → kleinste umschließende Anweisung.** Jede Syntax-Diagnostik der
Severity `Error` zieht die sie umschließende Anweisung ins Verbatim (`:87-101`). Zwei Ausnahmen sitzen
bewusst hier:

- **BOM-`Nav0000` bei Offset 0** ist ausgenommen (`:93`) — ein führendes U+FEFF wird als `Unknown`
  gelext und meldet sich als Fehler, ist aber kein Strukturbruch. Das BOM selbst schützt der BOM-Guard
  des Treibers: trägt die Leading-Trivia Skipped-Läufe, bleibt der Datei-Anfang verbatim und es
  entsteht nie ein Change an Offset 0 ([§9.2](#92-formatdocument-vorpässe-und-drei-zonen)).
- **Fehler *im Inhalt eines Code-Blocks*** (`CodeSyntax`) unterdrücken nicht. `FindEnclosingStatement`
  (`:204`) läuft die Ahnenkette hoch und liefert `null`, sobald es auf einen `CodeSyntax` trifft: ein
  kaputtes eingebettetes C#-Fragment (`[code Foo]`, ein defekter `[params]`-Typ) lässt die *Nav*-Struktur
  unangetastet — der Formatter fasst Code-Block-Inneres nie an, und echte Strukturbrüche fangen ohnehin
  die Token-basierten Auslöser (1) und (3) ab.

**(3) Anweisungen aus den `StatementFacts` — hier fällt die Dreiteilung.** `Classify` (`:162`) ist der
Kern, und er ist der Ort, an dem sich der in [§4.1](#41-statementfacts) angekündigte Nutzen der
**getrennt gehaltenen** Trivia-Primitive einlöst:

```csharp
static StatementClass Classify(StatementFacts facts) {

    if (!facts.EndsWithSemicolon || facts.HasStructuralBreakTrivia) {
        return StatementClass.Suppressed;
    }

    return facts.SpansMultipleLines ? StatementClass.HandLaid : StatementClass.Normal;
}
```

`HasStructuralBreakTrivia` und `SpansMultipleLines` sind hier **nicht** austauschbar. Der Unterschied
ist die ganze Existenzberechtigung der beiden Primitive: Eine Direktive oder ein `SkippedTokensTrivia`
mitten in der Anweisung (`HasStructuralBreakTrivia`) ließe sich *nicht* per Delta-Shift sauber auf den
Block-Einzug schieben — sie muss verbatim bleiben. Ein bloßer Newline im Inneren (`SpansMultipleLines`,
aber kein Struktur-Trivia) markiert eine gültige, nur mehrzeilig gesetzte Anweisung — genau der
hand-gelegte Fall, bei dem sich der äußere Einzug re-setzen lässt, während das Innere byte-genau bleibt.
Der `AlignmentMapBuilder` *ver-ODER-t* dieselben zwei Primitive zu `BreaksSingleLineForm` (ihm ist die
Unterscheidung egal — jede nicht mehr einzeilige Anweisung fällt aus der Spalte); die Suppression
*unterscheidet* sie. Zwei Konsumenten, zwei Kombinationen, dieselben Rohdaten.

### 8.2 Zwei Phasen: warum die Deltas *nach* den Verbatim-Regionen kommen

`Compute` sammelt in *einem* Durchlauf die Verbatim-Regionen (aus allen drei Quellen) **und** die
hand-gelegten Kandidaten, konstruiert die `FormatterSuppression`, und rechnet die Deltas erst in einem
**zweiten** Durchlauf:

```csharp
var suppression = new FormatterSuppression(verbatim, handLaid, ComputeHasUsableMembers(syntaxTree));

foreach (var facts in handLaidCandidates) {
    if (suppression.IsSuppressed(facts.Statement.Extent)) {
        continue;                                    // schon verbatim -> kein (widersprüchlicher) Shift
    }
    var delta = HandLaidDelta(syntaxTree, options, facts.Tokens[0]);
    for (var i = 1; i < facts.Tokens.Count; i++) {
        handLaid[facts.Tokens[i - 1].End] = delta;   // Delta an jede innere Lücke der Anweisung
    }
}
```

Die Reihenfolge ist kein Zufall: Eine hand-gelegte Anweisung kann *innerhalb* eines defekten Task-Bodys
liegen. Dann ist sie über Quelle (1) bereits verbatim — und darf **keinen** Delta-Shift bekommen, der
ihr widerspräche (verbatim heißt byte-genau, Delta-Shift verschöbe Zeichen). Der `IsSuppressed`-Check zu
Beginn der zweiten Schleife setzt die Rangfolge **Verbatim vor Hand-gelegt** durch — und das geht nur,
wenn *alle* Verbatim-Regionen zu diesem Zeitpunkt schon feststehen. Deshalb die Zweiteilung.

Der Delta wird pro innerer Lücke der Anweisung unter deren `Prev.End` abgelegt — dieselbe
Schlüssel-Konvention wie in der `AlignmentMap` ([§2](#2-die-alignmentmap--das-ergebnis-des-vorpasses)),
sodass der Treiber später mit `extent.Start` genau diesen Eintrag trifft.

### 8.3 Durchgerechnet: der Delta-Shift

`HandLaidDelta` (`:176`) ist eine kleine, exakte Rechnung: **Ziel-Einzug des Blocks − authored
Einrückung des ersten Tokens**. Der authored-Teil zählt die Whitespace-Zeichen unmittelbar vor dem
ersten Token zurück bis zum Zeilenanfang; steht davor Nicht-Whitespace (das Token beginnt nicht am
Zeilenanfang), gibt es keinen sinnvollen äußeren Einzug → Delta 0.

Nehmen wir eine über zwei Zeilen gesetzte Transition in einem Task-Body (`IndentSize = 4`, Block-Tiefe
1 → Ziel-Einzug 4 Zeichen; · = Leerzeichen):

```
task T
{
········A -->
············B;
}
```

Die Anweisung `A --> B;` ist strukturell gültig (endet mit `;`, keine Skipped-Trivia/Direktive), trägt
aber einen Newline in der inneren Lücke zwischen `-->` und `B` → `Classify` = **HandLaid**. `A` ist mit
8 Leerzeichen eingerückt, `B` mit 12.

**Delta-Rechnung** für `firstToken = A`:

```
authored  = 8                    (8 Leerzeichen vor A, davor ein '\n' -> A am Zeilenanfang)
depth     = ComputeIndentDepth(A) = 1
target    = depth * IndentSize    = 1 * 4 = 4
delta     = target - authored     = 4 - 8 = -4
```

Der Delta (`−4`) wird an **alle drei** inneren Lücken der Anweisung geschrieben (Schlüssel `A.End`,
`(-->).End`, `B.End`) — so umgeht jede innere Lücke die Regel-Pipeline und bleibt byte-genau. Wirksam
wird er nur an der Newline-Lücke `(-->).End`; für die einzeiligen Lücken ist der Shift ein No-Op
(`ShiftInteriorLines` gibt Text ohne `\n` unverändert zurück).

**Beim Rendern** trifft der Treiber das Paar `(-->, B)`, findet über `TryGetHandLaidShift((-->).End)` den
Delta `−4` und ruft `RenderRawShifted(ctx, −4)` ([§6](#6-die-konsumseite-der-gaprenderer),
`GapRenderer.cs:346`). Der rendert `ctx.Extent` — den Text `"\n············"` (Newline + 12 Leerzeichen)
— verbatim, verschiebt aber das Innenzeilen-Präfix um `−4` → `"\n········"` (Newline + 8). `B` landet auf
Spalte 8.

Die Zeile von `A` selbst wird *nicht* hier re-eingerückt: die Lücke *vor* `A` (zwischen `{` und `A`)
gehört nicht zur Anweisung, sie ist eine **normale** Lücke und rendert über den regulären
`BraceOnOwnLineRule → NewLine`-Pfad (nach `{` beginnt der Body auf eigener Zeile) auf Tiefe 1 →
Spalte 4. Der Clou: dieser normale Pfad verschiebt `A`
um exakt `target − authored = −4` — **denselben** Delta. Weil beide Zeilen um denselben Betrag wandern,
bleibt ihre *relative* Struktur erhalten:

```
task T
{
····A -->
········B;
}
```

`A`→`B` war Offset `+4` (8→12), bleibt Offset `+4` (4→8). Die zweizeilige Anweisung ist als Ganzes um 4
nach links übersetzt, ihre innere Form byte-identisch.

**Idempotenz.** Im zweiten Lauf steht `A` bereits auf Spalte 4 → `authored = 4`, `delta = 4 − 4 = 0`.
`RenderRawShifted(ctx, 0)` verschiebt nichts, der Text ist identisch, kein `TextChange`. Der Delta-Shift
ist ein **Fixpunkt** — genau wie `PreserveDominant` in [§3.6](#36-die-policy-schicht-resolvetargetcolumn),
nur auf den Einzug statt die Spalte angewandt.

### 8.4 HasUsableMembers — der Global-Fallback

Der dritte Ausgang ist ein einzelnes Bool. `ComputeHasUsableMembers` (`:227`) prüft, ob die Datei
überhaupt Member, Usings oder einen Namespace trägt. Trägt sie nichts (reiner Müll, leere Datei), wäre
jede paarweise Lücken-Entscheidung Rätselraten auf einem bedeutungslosen Token-Strom. Der Treiber schaltet
dann in einen **Global-Fallback**: nur die zwei konservativen Rand-Lücken (Datei-Anfang und
Final-Newline/EOF-Trim) werden angefasst, die gesamte Paar-Schleife entfällt. So bleibt eine kaputte
Datei erhalten, statt von einem sinnlosen Formatierlauf zerpflügt zu werden.

### 8.5 Zwei Ausgänge, zwei Konsum-Pfade

`IsSuppressed` und `TryGetHandLaidShift` fließen **auf verschiedenen Wegen** in den Treiber-Loop
(`NavFormattingService.cs:88`) — das ist die strukturelle Pointe des Vorpasses:

- **Verbatim** geht durch die **Regel-Pipeline**. `CreateContext` speist `suppression.IsSuppressed(extent)`
  als `ctx.IsSuppressed` ein (`:311`); die allererste, höchstpriore Regel `VerbatimWhenSuppressedRule`
  (`GapRules.cs:31`, Safety-Tier) liefert dann bedingungslos `GapLayout.Verbatim`. Der Treiber sieht das
  Verbatim-Layout und **lässt den `TextChange` einfach weg** (`NavFormattingService.cs:106`) — keine
  Änderung heißt byte-genau erhalten. (Warum diese Regel im Safety-Tier ganz vorn steht, erklärt
  [§5](#5-das-regel-system-gaprules-und-rulepriority).)

- **Hand-gelegt** *umgeht* die Regel-Pipeline. Noch vor `GapRules.Select` fragt der Treiber
  `TryGetHandLaidShift(extent.Start)` (`:93`); trifft er einen Delta, rendert er sofort per
  `RenderRawShifted` und `continue`t. Die Regeln laufen für diese Lücke gar nicht erst an — sie hätten
  auch keine Handhabe, ein „Inneres verbatim, Rand geshiftet" auszudrücken.

So schließt sich der Kreis zum Leitmotiv: Der Fehler-Toleranz-Vorpass liest ausschließlich invariante
Fakten, klassifiziert einmal, und liefert dem lokalen, puren Formatter zwei Vorab-Antworten — „fass das
nicht an" und „schieb das nur, byte-genau". Beide sind idempotent: die verbatim gelassene Region ändert
sich per Definition nie, der Hand-gelegt-Delta ist im zweiten Lauf 0.

---

## 9. Der Treiber-Loop (NavFormattingService)

Die Kapitel §2–§8 zeigten pure Bausteine — die Map, die Regeln, den Renderer, die zwei Vorpässe. Dieses
letzte Kapitel zeigt die **imperative Schale**, die sie zu *einem* Durchlauf sequenziert.
`NavFormattingService` (`NavFormattingService.cs`, `public static class`) ist der einzige VS-freie
Einstieg: ein Host reicht einen `SyntaxTree` herein und bekommt eine Liste `TextChange`s zurück.

### 9.1 Das Gap-Rewriter-Modell

Der Service ist laut Klassen-Doku ein reiner **Gap-Rewriter**: er ändert nie den Text signifikanter
Token, sondern schreibt ausschließlich die Whitespace-Lücken *zwischen* aufeinanderfolgenden
signifikanten Token neu. Eingabe ist bewusst der `SyntaxTree` — ein rein syntaktisches Feature (Token,
Trivia, Syntax-Diagnostics hängen dort; kein Semantik-Build nötig). Dieses „fasse nie Token-Text an" ist
die fundamentale Invariante des ganzen Features (die spätere [§9.5](#95-der-wächter-achse-a-selbsttest)
nennt sie *Achse A*) — und der Grund, warum ein Selbsttest überhaupt möglich ist.

Die Geometrie dahinter ist die aus [§4.3](#43-gapcontext), jetzt konkret genutzt: die FullSpans der Token
kacheln den Text lückenlos und überlappungsfrei; für zwei aufeinanderfolgende Token A, B ist die Lücke
exakt `[A.End, B.Start)`. Pro Lücke entsteht in einem einzigen Durchlauf **höchstens ein** `TextChange` —
die Change-Extents sind damit **konstruktiv paarweise disjunkt und geordnet**, der `TextChangeWriter` sieht
nie eine Überlappung, und eine Lücke zu unterdrücken ist das bloße *Weglassen* ihres Changes.

Ein winziges `init --> A;` macht die Kachelung greifbar — fünf Lücken, drei Zonen (jede Lücke `[Vor.End,
Nach.Start)`):

| Lücke | Extent | Zone |
|---|---|---|
| vor `init` | `[0, init.Start)` | **Leading** (`RenderLeadingGap`) |
| `init` → `-->` | `[init.End, (-->).Start)` | Paar-Schleife |
| `-->` → `A` | `[(-->).End, A.Start)` | Paar-Schleife |
| `A` → `;` | `[A.End, (;).Start)` | Paar-Schleife |
| `;` → `‹EOF›` | `[(;).End, EOF.End)` | **Final** (`RenderFinalGap`) |

Das nullbreite `EndOfFile` trägt die komplette Datei-End-Trivia als *Leading*-Trivia; die Final-Lücke
`[letztes reales Token, EOF)` läuft darum bewusst **nicht** zusätzlich durch die Paar-Schleife — zwei
Changes für eine Lücke brächen die Invariante.

### 9.2 FormatDocument: Vorpässe und drei Zonen

`FormatDocument` (`:38`) ist das Herzstück. Nach den Null-Guards und dem Leer-Kurzschluss (kein Token →
keine Changes) baut es die Vorpässe auf und läuft die drei Zonen ab.

**Setup — die Vorpässe einmal.** `StatementFacts.Compute` erhebt Token-Liste und Trivia-Befund pro
Anweisung *einmal*; beide Vorpässe teilen sich diese Messung (früher las jeder sie selbst — pro
Transition bis zu fünfmal):

```csharp
var statementFacts = StatementFacts.Compute(syntaxTree);
var alignment      = AlignmentMapBuilder.Build(syntaxTree, options, statementFacts);   // §2/§3
var suppression    = FormatterSuppression.Compute(syntaxTree, options, statementFacts); // §8
```

**Zone 1 — der Leading-Gap.** Die Leading-Trivia des ersten realen Tokens liegt *vor* der ersten
Paar-Lücke und wird gesondert über `RenderLeadingGap` normalisiert. Der **BOM-Guard**: trägt diese Trivia
Skipped-Läufe — insbesondere ein führendes BOM, das als `Unknown` → `SkippedTokensTrivia` gelext wird —
bleibt sie verbatim, sodass nie ein Change an Offset 0 entsteht, der das BOM anfasste.

**Zone 2 — die Paar-Schleife** `for (i = 0; i < tokens.Count - 2; i++)` (`:88`), geschützt durch
`suppression.HasUsableMembers`. Pro Lücke zwei Zweige:

```csharp
if (suppression.TryGetHandLaidShift(extent.Start, out var delta)) {   // hand-gelegt: Rand-Shift, Inneres verbatim
    var canonical = renderer.RenderRawShifted(in ctx, delta);          // umgeht die Regeln (§8.5)
    …
    continue;
}

var layout = GapRules.Select(in context);                            // §5: erste passende Regel
if (layout is GapLayout.Verbatim) {                                  // unterdrückt = Change weglassen
    continue;
}
var canonicalGap = renderer.Render(in context, layout);             // §6: Layout -> Text
```

Ein Change wird nur emittiert, wenn die kanonische Form vom Ist-Text abweicht (minimale Changes). Ist
`HasUsableMembers` falsch (reiner Müll/leer), entfällt die ganze Schleife — nur die zwei konservativen
Rand-Lücken laufen (Global-Fallback, [§8](#8-der-fehler-toleranz-vorpass-formattersuppression)).

**Zone 3 — der Final-Gap.** `RenderFinalGap` (`:274`) behandelt die eine Lücke `[letztes reales Token,
EOF)` — der einzige Ort für Final-Newline und EOF-Trailing-Trim (Kommentar-/Direktivzeilen am Dateiende
bleiben; dahinter endet die Datei mit genau einer Newline — ob sie ergänzt wird, steuert allein
`InsertFinalNewline`, der EOF-Trim läuft immer). Skipped-Läufe bleiben auch hier verbatim.

Zum Schluss: `return options.VerifyResult ? Guard(…) : changes` — der optionale Selbsttest
([§9.5](#95-der-wächter-achse-a-selbsttest)). Die drei Zonen sind exakt die Kachelung aus §9.1: Leading,
die Paare, Final — jede Lücke genau einmal behandelt.

### 9.3 CreateContext und ComputeIndentDepth

`CreateContext` (`:303`) ist die Stelle, an der der Treiber die pure Entscheidungsschicht *füttert*: es
assembliert den `GapContext` ([§4.3](#43-gapcontext)) eines Paares — `ComputeIndentDepth(next)`,
`GapTrivia.Create`, `IsSuppressed(extent)`, die Alignment-Map, die Options. Die Tiefe ist die von `next`,
nicht `prev` (ein Umbruch eröffnet die neue Zeile *vor* `next`).

`ComputeIndentDepth` (`:449`) verdient einen Blick, weil es eine Nav-spezifische Entscheidung verkörpert.
Nav ist **flach** — genau ein Block-Typ (`task`/`taskref`), keine Verschachtelung. Deshalb wird die Tiefe
**nicht** über Klammern gezählt (das bräche bei unbalancierten Eingaben), sondern über Ahnenkette +
Extent-Containment: das Token liegt genau dann im Body eines Blocks, wenn es hinter dessen *realer*
öffnender Klammer beginnt und vor der schließenden:

```csharp
if (token.Start >= openBrace.End && (closeBrace.IsMissing || token.Start < closeBrace.Start)) {
    depth++;
}
```

Allman: öffnende und schließende Klammer liegen an der Grenze und haben selbst Tiefe 0. Fehlt die
öffnende, zählt der Block nicht; fehlt die schließende, gilt der Rest als Body — und solche Bodies nimmt
die Fehler-Unterdrückung ohnehin von der Formatierung aus ([§8](#8-der-fehler-toleranz-vorpass-formattersuppression)).
Weil die Tiefe aus *Struktur* kommt (Containment), nicht aus Nachbar-Arithmetik, ist der Einzug robust
gegen einen defekten Nachbarn — dieselbe Robustheit, die [§4.3](#43-gapcontext) für `IndentDepth` notierte.

### 9.4 FormatRange: ein gefiltertes FormatDocument

Der zweite Einstieg, `FormatRange` (`:148`), hätte ein range-beschränkter Sonderpfad sein können — und
ist es bewusst nicht. Sein Modell: **immer das ganze Dokument formatieren, dann auf den erweiterten Range
filtern.**

```
FormatRange(x, r)  ≡  { c ∈ FormatDocument(x) : c.Extent ⊆ ExpandRange(r) }
```

Jeder nicht-lokale Pass (Suppression, Ausrichtung/`targetCol`, Einzug) läuft dabei über die **volle**
Datei, nie range-beschränkt — die `targetCol` ist darum identisch zum Voll-Modus. Daraus folgen gratis
die **Subset-/Monotonie-Garantien**: `FormatRange(x, ganzeDatei) == FormatDocument(x)`, und ein späterer
Voll-Format verschiebt nie, was ein Range-Format schon platziert hat (Range-Format ist eine Teilanwendung
desselben Ergebnisses). Der Final-Gap unterliegt demselben `⊆`-Filter (eine Auswahl ohne das Dateiende
fügt dort keine Newline ein).

Eine Kehrseite bleibt: Zerschneidet die Auswahl eine Ausrichtungsgruppe, bleiben Out-of-Range-Nachbarn
gegebenenfalls unausgerichtet stehen (erwartete Editor-Konvention „nur die Auswahl anfassen"). Die
Spalte selbst ist dank kanonischer Breitenmessung trotzdem identisch zum Voll-Modus — der nächste
Voll-Format zieht die Nachbarn nach, ohne das schon Platzierte zu verschieben.

`ExpandRange` (`:190`) erweitert die rohe Auswahl: (1) auf ganze Zeilen einrasten, (2) auf ganze
Anweisungs-/Member-Knoten ausweiten, die der Range *echt* schneidet — bis zum Knoten-Ende und nach vorn
bis zum Ende des vorangehenden signifikanten Tokens. Diese eine vordere Lücke `[prev.End, first.Start]`
ist der einzige Change, der den Einzug des Knotens setzt (ein Change pro Lücke); ohne sie bliebe der
Einzug der selektierten Anweisung unkorrigiert und eine mehrzeilige Anweisung (hand-gelegt, mehrzeiliges
`[params]`) würde nur halb formatiert.

### 9.5 Der Wächter (Achse-A-Selbsttest)

Das Gap-Rewriter-Modell hat eine *prüfbare* Sicherheitseigenschaft: Weil der Formatter nie signifikanten
Token-Text anfasst, muss `format(x)` zum **identischen signifikanten Token-Strom** zurück-parsen — gleiche
Token (Typ + Text), gleiche Direktiv-Sequenz, keine neuen Error-Diagnostics. `Guard` (`:326`) prüft genau
das, aber nur unter `options.VerifyResult`.

`MeaningPreserved` (`:357`) ist der eigentliche Vergleich — drei Klauseln:

- **(a) signifikante Token** (Typ + Text) via `SequenceEqual`.
- **(b) Direktiv-Sequenz**, verglichen als `(Text, AtLineStart)` statt bloßem Text — Direktiven leben in
  Trivia; ein reiner Token-Strom-Vergleich sähe ihre Zerstörung nicht. `AtLineStart` ist die
  *Lexer*-Definition der Direktiv-Fähigkeit (vor dem `#` steht auf seiner Zeile nur Whitespace), **nicht**
  „Spalte 0": der Formatter setzt eine eingerückte Direktive legitim auf Spalte 0 zurück — die absolute
  Position verschiebt sich, die Zeilenanfangs-*Eigenschaft* bleibt invariant.
- **(c) keine neue Error-Diagnostik** — die Multimenge der Error-Descriptor-Ids *nachher* ist eine
  **Teil-Multimenge** von *vorher* (jede Id höchstens so oft wie zuvor). Das verallgemeinert die reine
  Anzahl-Schranke und fängt zusätzlich einen Fehler-*Tausch* bei gleicher Anzahl. Einen Fehler zu
  *entfernen* ist erlaubt; erfinden kann der Formatter keinen (er fasst nie Token-Text an) — die Schranke
  ist das Sicherheitsnetz gegen genau das.

Weicht etwas ab, ist das **immer ein Bug** (kein legitimer Laufzustand) → `Debug.Fail`, die Changes werden
verworfen (die Eingabe bleibt unverändert) und einmalig auf `stderr` geloggt (konform zur Stdio-Log-Regel).
Nur die Tests setzen `VerifyResult` — der Re-Parse verdoppelt die Kosten grob, und die ausgelieferten
Hosts sollen das nicht tragen. So schließt der Wächter den Kreis der Gap-Rewriter-Zusage: Der Treiber
*tut* nicht nur die pure, lokale Umschreibung — unter Test *beweist* er, dass sie die Bedeutung erhielt.

### 9.6 Die imperative Schale um den puren Kern

Von außen nach innen gelesen ist der Treiber die Naht, an der alles zusammenkommt: Er baut zwei Vorpässe
über *einmal* erhobene invariante Fakten, läuft die Token-Kachelung *einmal* ab, füttert jede Lücke mit
einem puren `GapContext`, lässt die Regelschicht *ein* Layout wählen und den Renderer es materialisieren,
und fügt die entstehenden Changes zu einer paarweise disjunkten, geordneten Folge zusammen. Jede Ebene las
nur kanonische Fakten — von `GapTrivia`s verschlucktem Whitespace über die threshold-stabile
Gruppenbildung bis zum Fixpunkt-Delta-Shift. Die Idempotenz, lokal auf jeder Ebene etabliert, trägt global,
weil die Schale selbst keine einzige whitespace-abhängige Entscheidung hinzufügt. Ein zweiter
Formatierlauf ist ein No-Op — per Konstruktion.
