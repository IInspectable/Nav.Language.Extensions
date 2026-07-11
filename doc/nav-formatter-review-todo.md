# Nav Code-Formatter — Review-TODO (Abarbeitungsliste)

> **Lebendes Arbeitsdokument.** Ergebnis des Formatter-Reviews (Spec `doc/nav-formatter-status.md` +
> alle 15 Dateien in `Nav.Language/Formatting/`, Stand Juli 2026). Das Review fand **keine
> Korrektheitsbugs** — die Befunde sind Options-Hygiene, Spec↔Code-Divergenzen im (nie feuernden)
> Achse-A-Wächter, tote Deklarationen, eine bewusst fehlende, aber sicher nachrüstbare
> Leerzeilen-Deckel-Option und Redundanz im `AlignmentMapBuilder`. Alle Punkte sind als sinnvoll
> bestätigt und werden hier in **commit-großen Steps** abgearbeitet — pro Session ein Step.
>
> **Kein Befund (bewusst so, nicht anfassen):** die Gruppenbildungs-Schwellen sind Absicht und
> konsistent umgesetzt — Pfeil-/Node-Grid-Spalten trennen erst bei **2** Trivia-Zeilen
> (`interruptLines ≥ 2`), die nachgestellten Klauseln (Trigger/Condition/Trailing-Kommentar) schon
> bei **1**. Ebenfalls geprüft und in Ordnung: qualifizierte Namen (`X.Y.Z`) sind ein Identifier-Token,
> `(`/`)` konsumiert der Parser nicht (nur Skiped → Suppression), Fortsetzungskanten `--^`/`o-^`
> sind kein `EdgeSyntax`, fallen auf Single-Space und brechen keine Pfeilgruppe.

## Arbeitsweise (gilt für jeden Step)

- Pro Step: umsetzen → Code-Review → `nav build` + `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (beide TFMs grün) →
  fertige **Commit-Message liefern** (Commit macht ausschließlich der Nutzer).
- Formatter-Tests laufen mit `VerifyResult = true` (Achse-A-Wächter an) — das bleibt so.
- Nach jedem Step: Status-Spalte hier fortschreiben und ggf. `doc/nav-formatter-status.md` angleichen.
- Reine Refactoring-Steps (S5, S6) müssen **byte-identische** Formatter-Ausgabe liefern — Goldens
  unverändert grün; im Zweifel Korpus-Smoke (`d:\tfs\main`, 1913 `.nav` × 2 Einzugsstile) gegenprüfen.

## Übersicht

| # | Step | Größe | Status |
|---|---|---|---|
| S1 | Options-Hygiene: `TrimTrailingWhitespace` + `InsertFinalNewline` entkoppeln | klein | **umgesetzt** (committed 89154ba2) |
| S2 | Achse-A-Wächter an die Spec angleichen (Direktiv-Position, Diagnostics-Vergleich, Granularität) | klein | **umgesetzt** (committed 4a8a2a35) |
| S3 | Dispatcher-Tier-Prüfung + tote Deklarationen (`ColumnId.TrailingComment`, `AlignedColumn.Column`) | klein | **umgesetzt** (Commit ausstehend) |
| S4 | Leerzeilen-Deckel: Option `MaxBlankLines` (gruppensemantik-erhaltend, ≥ 2) + Dateianfang/-ende | mittel | **umgesetzt** (Commit ausstehend) |
| S5 | `AlignmentMapBuilder` entdoppeln (ein Candidate-Typ + generischer Tight-Spalten-Baustein) | mittel | offen |
| S6 | Statement-Messung einmalig + geteiltes Hand-gelegt-Primitiv + Kleinkram (Perf) | mittel | offen |

Reihenfolge: S1–S3 sind unabhängige Quick-Wins, S4 ist das einzige neue Verhalten, S5/S6 sind
Refactorings (S6 baut sinnvollerweise auf S5 auf, weil beide den `AlignmentMapBuilder` anfassen).

---

## S1 — Options-Hygiene: `TrimTrailingWhitespace` + `InsertFinalNewline`

**Befund:**

1. `NavFormattingOptions.TrimTrailingWhitespace` (Default `true`) wird **nirgends gelesen** — das
   Trimmen passiert implizit und bedingungslos, weil der Renderer Lücken kanonisch neu schreibt.
   `false` hat keinerlei Wirkung: ein toter Schalter.
2. `InsertFinalNewline = false` schaltet in `NavFormattingService.RenderFinalGap` den **kompletten**
   Final-Gap ab — damit entfallen auch EOF-Leerzeilen-Trim und die Normalisierung von Kommentar-/
   Direktivzeilen am Dateiende (Options-Konflation).

**Umsetzung (Empfehlung):**

- `TrimTrailingWhitespace` **entfernen** statt verdrahten: ein ehrliches `false` müsste den
  Ist-Whitespace der Lücken bewahren und widerspräche dem kanonischen Gap-Rewriter-Modell
  fundamental (jede angefasste Lücke wird ohnehin komplett neu geschrieben). Das Trimmen ist ein
  Nebenprodukt des Modells, keine schaltbare Regel — genau das in `nav-formatter-status.md`
  („Optionen") festhalten.
- `InsertFinalNewline` **entkoppeln**: der Final-Gap wird immer gerendert (EOF-Trim +
  Kommentar-/Direktivzeilen-Normalisierung); die Option steuert nur noch, ob hinter dem letzten
  Inhalt die abschließende Newline ergänzt wird. Skiped-Guard (BOM etc.) bleibt unverändert davor.

**Betroffen:** `NavFormattingOptions.cs`, `NavFormattingService.RenderFinalGap`,
`GapRenderer.RenderFinalGap`, `doc/nav-formatter-status.md` (Abschnitt „Optionen & Konfiguration").

**Fertig, wenn:** Option weg bzw. entkoppelt, neue Testfälle für `InsertFinalNewline = false`
(EOF-Trim greift trotzdem; keine Newline am Ende), Spec angeglichen, beide TFMs grün.

---

## S2 — Achse-A-Wächter an die Spec angleichen

**Befund (drei Divergenzen zwischen Spec „Korrektheits-Modell" und `NavFormattingService.Guard`):**

1. **Direktiv-Vergleich zu schwach:** Spec verlangt Typ + Text + **Zeilenanfangs-Position** (eine
   eingerückte Direktive wird trotzdem als `DirectiveTrivia` gelext und bliebe unentdeckt);
   `Directives()` vergleicht nur `ToString()`.
2. **Diagnostics-Vergleich zu schwach:** `ErrorCount(after) <= ErrorCount(before)` — ein
   Fehler-*Tausch* (einer verschwindet, ein anderer entsteht) bliebe unentdeckt. Spec: „keine
   neuen Diagnostics".
3. **Granularität:** Spec verspricht statement-/member-weises Verwerfen (nur die Changes der
   abweichenden Einheit), `Guard` ist alles-oder-nichts.

**Umsetzung (Empfehlung):**

- (1) Direktiven als `(Text, stehtAmZeilenanfang)` vergleichen — die Position selbst verschiebt
  sich durch Formatierung, das Zeilenanfangs-**Prädikat** ist formatierungs-vergleichbar.
- (2) Statt Zählung die **Multimenge der `Descriptor.Id`s** der Error-Diagnostics vergleichen
  (Positionen verschieben sich; die Id-Multimenge fängt den Tausch-Fall ab).
- (3) Granularität: **Spec an den Code angleichen** — das statement-weise Verwerfen ist für einen
  opt-in Entwicklungs-Selbsttest, der nie feuern darf (ein Treffer ist immer ein Bug),
  Überengineering; „datei-global verwerfen" als bewusste Vereinfachung dokumentieren. (Alternative,
  falls doch gewünscht: pro `FormatterSuppression`-Einheit re-lexen — dann aber eigener Step.)

**Betroffen:** `NavFormattingService.cs` (`Guard`/`MeaningPreserved`/`Directives`/`ErrorCount`),
`doc/nav-formatter-status.md` (Abschnitt „Korrektheits-Modell", Präzisierung „Granularität").

**Fertig, wenn:** stärkere Vergleiche implementiert (Wächter-Tests: eingerückte Direktive und
Fehler-Tausch werden erkannt — z.B. über ein künstlich verfälschtes Change-Set), Spec-Absatz zur
Granularität umformuliert, beide TFMs grün (Wächter feuert im Testlauf weiterhin nie regulär).

**Umgesetzt** (in `NavFormattingService.MeaningPreserved`, `internal` für Tests):

- **(1) Direktiv-Vergleich — Befund korrigiert.** Statt „Text" jetzt `(Text, steht am Zeilenanfang)`.
  Wichtig: das Prädikat ist die **Lexer-Definition** (nur Whitespace vor dem `#` auf seiner Zeile),
  **nicht „Spalte 0"** — ein erster Versuch mit Spalte-0 ließ prompt den Golden
  `DirectiveIsResetToColumnZero` scheitern: der Formatter normalisiert eine eingerückte Direktive
  bewusst auf Spalte 0, Einrücken ist also **bedeutungserhaltend** und darf nicht als Bruch gewertet
  werden. Das Prädikat ist damit vor allem dokumentierende Absicherung der Invariante (für echte
  Direktiven immer wahr; ein echter Bruch schlägt ohnehin schon im Token-Strom durch). Neuer Test
  `GuardTreatsDirectiveIndentationAsInsignificant` pinnt genau das (MeaningPreserved == true) und wirkt
  als Regressionssicherung gegen ein erneutes „Spalte-0"-Verschärfen.
- **(2) Diagnostics — Teil-Multimenge statt Zählung.** `NoNewErrorDiagnostics` prüft, ob die Error-Id-
  Multimenge *nach* der Formatierung in der *davor* enthalten ist (`IsSubMultiset`, `internal`). Das
  verallgemeinert das frühere `nachher ≤ vorher` (Wegfall bleibt erlaubt) und fängt zusätzlich den
  **Fehler-Tausch** bei gleicher Anzahl. Ein natürlicher Tausch bei identischem Token-Strom lässt sich
  nicht parsen, deshalb prüfen `GuardDetectsErrorSwap`/`GuardAllowsErrorRemoval` die Multimengen-Semantik
  direkt.
- **(3) Granularität — Spec an Code angeglichen.** „datei-global verwerfen" als bewusste Vereinfachung
  dokumentiert (opt-in Entwicklungs-Selbsttest, der nie feuern darf; statement-weises Verwerfen wäre
  Überengineering und verschleierte einen echten Bug).

Beide TFMs grün (net472 1672 passed/0 failed/3 skipped, net10 171/171 Formatting). Spec
`doc/nav-formatter-status.md` (Korrektheits-Modell, Punkt 1 + Granularität) angeglichen.

---

## S3 — Dispatcher-Tier-Assert + tote Deklarationen

**Befund:**

1. Spec und XML-Doku von `GapRules` sagen „der Dispatcher **sortiert** nach Tier, dann
   Deklarationsreihenfolge" — der Code iteriert die manuell geordnete Liste. Eine falsch
   einsortierte neue Regel bräche Cross-Tier-Präzedenz still; der Intra-Tier-Check fängt genau
   das nicht.
2. `ColumnId.TrailingComment` wird nirgends referenziert (die Trailing-Kommentar-Tabelle läuft
   bewusst am `GapLayout` vorbei — dann braucht es den Enum-Wert nicht).
3. Der `Column`-Parameter von `GapLayout.AlignedColumn`/`NewLineAlignedColumn` wird nie
   ausgewertet (der Renderer schlägt nur `Extent.Start` in der `AlignmentMap` nach) — reine
   Selbstdokumentation, aber nirgends als solche ausgewiesen.

**Umsetzung:**

- (1) Einmaliges Debug-Assert (statischer Konstruktor von `GapRules`): die `Tier`s der
  Regel-Liste sind **monoton aufsteigend**. Doku-Formulierung „sortiert" in Code + Spec auf
  „geprüft geordnet" korrigieren.
- (2) `ColumnId.TrailingComment` entfernen; seine XML-Doku (der wertvolle Teil) wandert an
  `AlignmentMap.TryGetTrailingCommentSpaces`.
- (3) `Column` als bewusste Selbstdokumentation kommentieren (am Record) — er benennt im
  Regel-Code, *welche* Spalte gemeint ist, auch wenn der Renderer nur die Position nachschlägt.

**Betroffen:** `GapRules.cs`, `ColumnId.cs`, `AlignmentMap.cs`, `GapLayout.cs`,
`doc/nav-formatter-status.md` (Abschnitt „Dispatch & Priorität").

**Fertig, wenn:** Assert vorhanden (und bei absichtlich vertauschter Liste im Debug-Test feuernd),
Enum-Wert weg, Kommentare gesetzt, beide TFMs grün.

**Umgesetzt:**

- **(1) Tier-Ordnung — statt Debug-Assert ein harter Wurf.** `GapRules` bekommt einen statischen
  Konstruktor, der `EnsureRulesOrderedByTier` aufruft: ist die von Hand gelegte `Rules`-Liste nicht
  monoton aufsteigend nach `RulePriority`, wirft er beim Laden des Typs `InvalidOperationException`
  (Meldung listet die volle Regel→Tier-Folge). **Bewusst kein `Debug.Assert`/`[Conditional("DEBUG")]`** —
  ausgeliefert wird ohnehin nur Debug, ein Assert-Dialog ist kein sauberer Fehler; „entweder ein Fehler
  oder keiner". Die Prüfung läuft nur einmal beim Typ-Laden, der Wurf ist also gratis. Reine, testbare
  Trennung: `internal static bool IsMonotonicByTier(IReadOnlyList<RulePriority>)` + `internal RuleTiers`
  (echte Reihenfolge). Zwei Tests (`NavFormattingServiceTests`): `RuleListIsOrderedByTier` pinnt die echte
  Liste, `MonotonicByTierRejectsSwappedList` belegt, dass ein Tier-Rückschritt erkannt wird. Doku „sortiert"
  → „von Hand geordnet, geprüft" in `GapRules`, `RulePriority` und Spec.
- **(2) `ColumnId.TrailingComment` entfernt.** Der Enum-Wert war nur an seiner eigenen Deklaration
  referenziert (die Trailing-Kommentar-Tabelle läuft am `GapLayout` vorbei). Der wertvolle Doku-Teil
  (tight unter längstem Zeileninhalt; einzige Spalte, die nicht über ein `GapLayout`/`ColumnId`
  nachgeschlagen wird, sondern direkt vom Renderer) ist als `<remarks>` an
  `AlignmentMap.TryGetTrailingCommentSpaces` gewandert; eine kurze Anmerkung in `ColumnId` erklärt die
  bewusste Abwesenheit. Spec-Absatz angepasst (kein `ColumnId.TrailingComment` mehr).
- **(3) `Column` als Selbstdokumentation ausgewiesen.** `<param name="Column">` an `GapLayout.AlignedColumn`
  und `NewLineAlignedColumn`: benennt im Regel-Code, *welche* Spalte gemeint ist — der `GapRenderer`
  wertet den Wert nicht aus, sondern schlägt die aufgelöste Space-Zahl allein über `Extent.Start` in der
  `AlignmentMap` nach.

Beide TFMs grün (net472 1674 passed/0 failed/3 explicit skipped, net10 173/173 Formatting).

---

## S4 — Leerzeilen-Deckel: Option `MaxBlankLines`

**Befund:** Der einzige Punkt, in dem der Formatter hinter gängigen Formatern zurückbleibt: es gibt
keinen Deckel für aufeinanderfolgende Leerzeilen — fünf Leerzeilen mitten im Task bleiben für immer
stehen; ebenso führende Leerzeilen am Dateianfang. Der komplette Kollaps-Verzicht ist mit der
`interruptLines`-Gruppensemantik begründet — das Argument trägt aber nur gegen Kollaps **auf 1**:

> Ein Deckel bei **2** ist gruppensemantik-erhaltend: `interruptLines ≥ 2` bleibt `≥ 2`
> (Gruppenbruch erhalten), `1` bleibt `1` (die Schwelle-1-Spalten unberührt) — und idempotent.

**Umsetzung:**

- Neue Option `MaxBlankLines` (`int?`, `null` = kein Deckel). **Zulässig ist nur ≥ 2** — Werte
  darunter zerstören die Gruppensemantik und werden auf 2 geklemmt (oder per Argument-Validierung
  abgelehnt; im Step entscheiden und dokumentieren).
- **Default-Entscheidung im Step treffen:** `2` (Verhalten gängiger Formatter, aber größerer
  Einmal-Diff über den Korpus) vs. `null` (opt-in, kein Diff). Vor der Entscheidung den
  Einmal-Diff per Korpus-Smoke beziffern.
- Wirkort ist der **Renderer** (`GapRenderer.RenderVertical`/`RenderLeadingGap`): Läufe von
  Leerzeilen beim Emittieren auf den Deckel kappen — Kommentar-/Direktivzeilen zählen nicht als
  Leerzeilen und setzen den Lauf zurück. `BlankLinesBefore` bleibt Minimum-Semantik (unverändert).
- **Dateianfang:** führende Leerzeilen vor dem ersten Inhalt kappen (üblich wäre 0; mindestens
  demselben Deckel unterwerfen — im Step entscheiden).
- **Idempotenz-Argument in die Spec:** nach Lauf 1 ist jeder Lauf ≤ Deckel → Lauf 2 ändert nichts;
  Gruppierung kippt nicht, weil die Schwellen-Klassifikation (`≥ 2` / `≥ 1`) unter dem Deckel
  invariant ist.

**Betroffen:** `NavFormattingOptions.cs`, `GapRenderer.cs`, `doc/nav-formatter-status.md`
(Abschnitte „Kommentare & Direktiven" — „Leerzeilen werden nicht kollabiert" relativieren — und
„Optionen & Konfiguration").

**Fertig, wenn:** Goldens für Deckel an/aus (inkl. Grenzfall „Leerzeile + Kommentarzeile +
Leerzeilen"), Idempotenz- und Gruppierungs-Property-Tests grün, Korpus-Smoke ohne
Idempotenz-/Token-/Wächter-Brüche, Default entschieden + dokumentiert, beide TFMs grün.

**Umgesetzt:**

- **Option `MaxBlankLines` (`int?`, Default `null`).** `null` = kein Deckel — die dokumentierte „kein
  Kollaps"-Grundhaltung bleibt der **Default**; der Deckel ist **opt-in**, damit keine bestehende Datei
  ungefragt umbricht (Korpus-Einmal-Diff bei Default = 0). **Nur ≥ 2 zulässig**, Werte darunter werden im
  `init`-Accessor **still auf 2 geklemmt** (ein Formatter wirft nicht an seiner Konfig; die Property
  spiegelt immer den wirksamen Wert).
- **Warum ≥ 2 (nicht bloß „gegen Kollaps auf 1"):** der Boden 2 **ist** die Gruppenbruch-Schwelle
  (`interruptLines ≥ 2`). Ein Deckel ≥ 2 verschiebt **keinen** Lauf über die Schwelle (jeder ≥ 2-Lauf
  bleibt ≥ 2, jeder 1-Lauf unberührt) → Gruppierung invariant, idempotent. Ein Deckel bei 1 zöge einen
  bewussten 2-Leerzeilen-Gruppenbruch auf 1 (kein Bruch) → vorher getrennte Transitionen verschmölzen,
  und zwar erst im **zweiten** Lauf (Gruppen aus dem geparsten Baum, vor dem Kappen) → nicht idempotent.
  `2` vs. `3` ist reine Geschmackssache (beide sicher); Korpus-Einmal-Diff ggü. kein-Deckel: **382** (Deckel
  2) vs. **86** (Deckel 3) vs. **8** (Deckel 5) der 1913 Dateien.
- **Ein Mechanismus, drei Abnehmer:** `GapRenderer.CapBlankRuns(List<string>)` kappt Leerzeilen-Läufe
  (Kommentar-/Direktivzeile setzt den Lauf zurück); genutzt von `RenderVertical` (mitten im Code),
  `RenderLeadingGap` (Dateianfang) und `RenderFinalGap` (Dateiende). `BlankLinesBefore` bleibt
  Minimum-Semantik (das Minimum ≤ 1 liegt stets unter dem Deckel ≥ 2, bleibt also erreichbar). **Dateianfang
  entschieden:** demselben Deckel unterworfen (nicht auf 0 gezwungen).
- **Tests:** sechs Goldens in `NavFormattingGoldenTests` (Klemm-/Default-Unit-Test, no-cap-Erhalt,
  Kappen im Body, Kommentarzeile-als-Reset-Grenzfall, Dateiende zwischen Kommentaren, Dateianfang) — alle
  mit `VerifyResult = true` (Achse-A-Wächter feuert nie). **Korpus-Smoke mit Deckel = 2** über 1913 `.nav`
  × 2 Einzugsstile (3826 Läufe): **0 Crashes, 0 nicht-idempotent, 0 Token-Brüche, 0 neue Fehler**.
- Beide TFMs grün (net472 1680 passed/0 failed/3 explicit skipped, net10 1620/0; Formatting 179/179).
  Spec `doc/nav-formatter-status.md` (Optionen, „Kommentare & Direktiven", Spaltenausrichtungs-Absatz)
  angeglichen.

---

## S5 — `AlignmentMapBuilder` entdoppeln

**Befund:** Der eine Ort mit echter Redundanz (~950 Zeilen): `TriggerCandidate`,
`ConditionCandidate` und `TrailingCommentCandidate` sind feldidentisch (`Statement`, `BreaksGroup`,
`IsAligned`, `GapStart`, `Width`); `AddTriggerColumns`/`AddConditionColumns` sind bis auf den
Klausel-Selektor Copy-Paste; der Block „Gruppe → Teilnehmer ≥ 2 → `max(Width) + 1` → Spaces
schreiben" steht dreimal wörtlich da.

**Umsetzung:**

- Ein gemeinsamer Candidate-Typ (z.B. `ClauseCandidate`) für Trigger/Condition/Trailing-Kommentar.
- Ein generischer Tight-Spalten-Baustein: Kandidaten erzeugen (per Klausel-Selektor
  `Func<SyntaxNode, …>` + Vermessung via `WidthUpToColumn`), `GroupCandidates(…,
  interruptThreshold: 1)`, tight auflösen, Spaces schreiben — von drei Aufrufern parametriert.
- **Arrow und NodeGrid bleiben separat** (Policy-gesteuert via `ResolveTargetColumn` +
  `AuthoredColumn`, NodeGrid mit drei Teilspalten) — nicht in die Abstraktion zwängen.
- Erwartete Ersparnis ~150–200 Zeilen ohne Verlust der spaltenweisen Lesbarkeit; die
  Vorpass-Reihenfolge Pfeil → Trigger → Condition → Trailing-Kommentar bleibt exakt erhalten.

**Betroffen:** nur `AlignmentMapBuilder.cs` (+ ggf. Spec-Satz zur Struktur).

**Fertig, wenn:** reines Refactoring — **byte-identische** Ausgabe (alle Goldens unverändert grün,
Korpus-Smoke diff-frei gegen vorher), beide TFMs grün.

---

## S6 — Statement-Messung einmalig + geteiltes Hand-gelegt-Primitiv + Kleinkram

**Befund (Perf + Duplikate):**

1. `syntaxTree.Tokens[statement.Extent].ToList()` + `IsHandLaid`-Scan laufen pro Transition bis zu
   **fünfmal** (Arrow, Trigger, Condition, Trailing-Kommentar im Builder + strukturgleich
   `FormatterSuppression.Classify`).
2. Zwei fast-gleiche Hand-gelegt-Detektoren (`FormatterSuppression.Classify` vs.
   `AlignmentMapBuilder.IsHandLaid`) — die Spec nennt es „dasselbe Primitiv", der Code hat zwei.
3. Die Statement-Aufzählung (Transition + Exit-Transition + NodeDeclaration) existiert dreifach
   (`NavFormattingService.FormattableNodes`, `FormatterSuppression.Compute`,
   `AlignmentMapBuilder`).
4. Klein: `NavFormattingService.HasSkippedTokens` dupliziert, was `GapTrivia` weiß;
   `FormatterSuppression.IsSuppressed` ist eine Linearsuche pro Lücke (O(n·m) bei
   diagnostikreichen Dateien).

**Umsetzung:**

- Eine pro Statement **einmal** berechnete Messung (Token-Liste + Gap-Trivia-Fakten +
  Hand-gelegt-/Defekt-Befund), die Suppression **und** alle Spalten-Vorpässe konsumieren — damit
  fällt (1)+(2) zusammen. Natürlicher Ort: ein kleiner interner Typ im Formatting-Ordner, den
  `FormatterSuppression.Compute` und `AlignmentMapBuilder.Build` teilen (Berechnung im Service,
  Durchreichung an beide).
- (3) Ein gemeinsamer Statement-Enumerations-Helper.
- (4) `HasSkippedTokens` über `GapTrivia` bzw. einen geteilten Helper; `IsSuppressed` nur bei
  Bedarf auf sortierte Extents + Binärsuche umstellen (erst messen — vermutlich irrelevant).
- **Vorher/Nachher messen** mit dem bestehenden Scratchpad-Harness (FormatPerf, Phasen-Split
  Zeit + Alloc über den Korpus) — die Ersparnis beziffern und hier eintragen.

**Betroffen:** `AlignmentMapBuilder.cs`, `FormatterSuppression.cs`, `NavFormattingService.cs`.

**Fertig, wenn:** reines Refactoring — byte-identische Ausgabe (Goldens + Korpus-Smoke diff-frei),
Perf-Zahlen erhoben, beide TFMs grün.

---

## Erledigt

*(Steps nach Abschluss hierher verschieben, mit Commit-Hash und Datum.)*
