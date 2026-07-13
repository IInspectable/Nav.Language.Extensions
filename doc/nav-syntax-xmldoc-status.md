# Nav.Language/Syntax — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Lebendes Status-Dokument.** Neue Session? Hier weiterlesen, Status-Tabelle prüfen, nächsten
> offenen Batch abarbeiten. Ziel: alle Dateien unter `Nav.Language\Syntax\` durchgängig mit
> akkurater C#-XML-Dokumentation versehen — **ohne jede Code-Änderung**.

## 1. Ziel & Ausgangslage (Audit vom 2026-07-13)

- 68 Dateien, ~7.400 Zeilen unter `Nav.Language\Syntax\`.
- **42 Dateien haben 0 Doku-Zeilen** — fast ausschließlich die Syntax-Knoten-Klassen
  (Deklarationen, Transitionen, Typen, Code-Blöcke).
- Die Infrastruktur ist bereits weitgehend dokumentiert (`SyntaxNode` 154 `///`-Zeilen,
  `NavParser` 805, `NavDirectiveParser` 123, `SyntaxTrivia` 60, `SyntaxFacts` 66, …) —
  dort sind nur Lücken zu schließen und der Bestand auf Korrektheit zu prüfen.
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-13):** 952 CS1591-Warnungen
  (fehlende XML-Doku an öffentlichen Membern) unter `Syntax\` → Ziel ist **0**.
  Einzige In-Scope-Vorbelastung: `NavDirectiveParser.cs:269` trägt ein nicht auflösbares
  `cref="SetLocalTokens"` (CS1574) — wird in Batch B7 mitkorrigiert.
- **Stil-Referenz ist `Nav.Language\Syntax\SyntaxTrivia.cs`**: deutsche Doku mit echten
  Umlauten, `<see cref="…"/>` statt Klartext-Typnamen, Roslyn-Analogien wo tragfähig,
  `<param>`/`<returns>` an Methoden, knappe Ein-Zeilen-Summaries an trivialen Properties.

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder
   korrigiert). Mechanisch verifiziert durch Gate G1 (Abschnitt 4) — der Diff ohne
   `///`-Zeilen muss byte-identisch zu HEAD sein, inkl. Einrückung, Zeilenenden, BOM.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen
   (Parser-Konstruktionsstelle, Semantikmodell, Tests) oder einem `[SampleSyntax]`-Attribut
   ableitbar sein. Bei Unsicherheit: Member **unkommentiert lassen** und im Batch-Report als
   „offen" melden — eine Lücke ist besser als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `protected`/`internal`/`private` Member werden **mitdokumentiert, wo es Sinn
   ergibt** — d.h. überall dort, wo der Member eine Invariante, eine Entwurfsentscheidung oder
   nicht offensichtliches Verhalten trägt (Parser-Hilfsmethoden, Recovery-Logik, Caches);
   triviale private Felder/Durchreicher brauchen keine Doku. Vorbild: `NavDirectiveParser.cs`
   dokumentiert auch interne Maschinerie.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF.** Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku
   beschreibt den Code, nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, pro Batch, nach Review.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan und den Status; die eigentliche Doku-Arbeit
läuft **pro Batch in einem Subagenten** mit eigenem, frischem Kontext (Vorlage in Abschnitt 6).
So liest die Haupt-Session nie die Dateiinhalte selbst und läuft nicht voll; realistisch passt
die gesamte Kampagne in 1–2 Sessions.

Ablauf je Batch:

1. Orchestrator startet den Subagenten mit der Auftrags-Vorlage (Abschnitt 6) + Dateiliste.
2. Subagent liest Dateien **und ihre Verwendungen**, schreibt die Doku, führt G1–G3 selbst aus.
3. Orchestrator verifiziert unabhängig: G1–G3 erneut ausführen, G4 (Build) einmal,
   dazu Stichproben-Review von 2–3 Dateien des Batches.
4. Orchestrator liefert Commit-Message-Vorschlag; **Nutzer committet**.
5. Status-Tabelle (Abschnitt 5) fortschreiben.

Nach einem Commit ist jeder Batch ein sauberer Wiederaufsetzpunkt — Session-Abbruch kostet
nie mehr als den laufenden Batch.

## 4. Verifikations-Gates (pro Batch, alle Pflicht)

**G1 — Doku-only-Diff** (Herzstück, Git Bash): Der Diff darf ausschließlich aus
`///`-Zeilen bestehen. Byte-exakter Vergleich — schlägt das Gate flächig an, ist meist
ein Zeilenenden-/BOM-Schaden die Ursache; dann den Schaden beheben, **nie** das Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/Syntax/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; ändert keine Datei,
`-p:` wirkt nur auf den Aufruf; **`--no-incremental`** ist Pflicht, sonst verschluckt der
inkrementelle Build die Warnungen):

```powershell
dotnet build Nav.Language\Nav.Language.csproj -c Debug `
  -p:GenerateDocumentationFile=true --no-incremental
```

Auswertung gegen die **Baseline vom 2026-07-13** (nur `Syntax\`-Treffer zählen):

- **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter
  `Syntax\` ist **genau 1 Treffer** (CS1574 in `NavDirectiveParser.cs:269`, wird in B7
  behoben). Jeder neue Treffer ist ein Fehler des laufenden Batches → sofort beheben.
- **CS1591** (fehlende Doku): Baseline unter `Syntax\` ist **952**; die Zahl muss mit jedem
  Batch monoton sinken und am Kampagnen-Ende **0** sein.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -E "[\\\\/]Syntax[\\\\/]" | sort -u
grep -E "CS1591" build.log | grep -cE "[\\\\/]Syntax[\\\\/]"
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`, CRLF intakt.
Vor dem Bearbeiten außerdem die Win-1252-Falle prüfen (`iconv -f UTF-8 -t UTF-8 <file>`,
siehe CLAUDE.md); Edit/Write kann LF hinterlassen → dann CRLF+BOM wiederherstellen.

```bash
for f in Nav.Language/Syntax/*.cs; do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
```

**G4 — Build grün** (im Orchestrator, einmal pro Batch): der G2-Aufruf genügt; am
Kampagnen-Ende zusätzlich einmal `nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

| Batch | Inhalt (Dateien) | Status |
|---|---|---|
| **B1 — Typen & Parameter** (7) | ArrayRankSpecifierSyntax, ArrayTypeSyntax, GenericTypeSyntax, SimpleTypeSyntax, CodeTypeSyntax, ParameterSyntax, ParameterListSyntax | offen |
| **B2 — Code-Deklarationen** (11) | CodeSyntax, CodeDeclarationSyntax, CodeAbstractMethodDeclarationSyntax, CodeBaseDeclarationSyntax, CodeDoNotInjectDeclarationSyntax, CodeGenerateToDeclarationSyntax, CodeNamespaceDeclarationSyntax, CodeNotImplementedDeclarationSyntax, CodeParamsDeclarationSyntax, CodeResultDeclarationSyntax, CodeUsingDeclarationSyntax | offen |
| **B3 — Knoten-Deklarationen** (10) | NodeDeclarationSyntax, NodeDeclarationBlockSyntax, ChoiceNodeDeclarationSyntax, DialogNodeDeclarationSyntax, EndNodeDeclarationSyntax, ExitNodeDeclarationSyntax, InitNodeDeclarationSyntax, TaskNodeDeclarationSyntax, ViewNodeDeclarationSyntax, ConnectionPointNodeSyntax | offen |
| **B4 — Transitionen & Kanten** (9) | EdgeSyntax, TransitionDefinitionSyntax, TransitionDefinitionBlockSyntax, ExitTransitionDefinitionSyntax, SourceNodeSyntax, TargetNodeSyntax, TriggerSyntax, ConditionClauseSyntax, DoClauseSyntax | offen |
| **B5 — Wurzel, Task & Helfer** (10) | Syntax.cs, TaskDefinitionSyntax, TaskDeclarationSyntax, MemberDeclarationSyntax, IdentifierOrStringSyntax, IncludeDirectiveSyntax, SampleSyntaxAttribute, SyntaxTokenType, SyntaxTokenComparer, SyntaxTokenExtensions | offen |
| **B6 — Lücken-Audit Bestand** (17) | BadDirectiveTriviaSyntax, CodeBlockFacts, CodeGenerationUnitSyntax, ContinuationEdgeSyntax, ContinuationTransitionSyntax, DirectiveRun, DirectiveTriviaSyntax, SkippedTokensTriviaSyntax, StructuredTriviaSyntax, SyntaxFacts, SyntaxNode, SyntaxToken, SyntaxTokenList, SyntaxTree, SyntaxTrivia, SyntaxTriviaList, VersionDirectiveSyntax — undokumentierte Member ergänzen, Bestands-Doku auf Korrektheit prüfen | offen |
| **B7 — Lexer & Parser** (4) | NavLexer, NavDirectiveParser, NavParser.Extents, NavParser (2.745 Zeilen, bereits stark dokumentiert — nur Lücken schließen; Klassen-Summary + öffentliche/zentrale Member zuerst) | offen |

Reihenfolge B1→B7 ist bewusst: erst die Blätter (Typen), dann die zusammengesetzten Knoten —
so existieren die `cref`-Ziele samt Doku, wenn übergeordnete Klassen auf sie verweisen.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\Syntax\` mit C#-XML-Doku. **Dateien dieses
> Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine
>   Umformatierung, keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen.
> - Lies zuerst `doc/nav-syntax-xmldoc-status.md`, Abschnitte 2 und 4 (Regeln + Gates), und
>   `Nav.Language\Syntax\SyntaxTrivia.cs` als Stil-Referenz.
> - Vor der Formulierung je Klasse: die **Konstruktionsstelle im Parser**
>   (`NavParser.cs`/`NavDirectiveParser.cs`), vorhandene `[SampleSyntax]`-Attribute und die
>   Verwendung im Semantikmodell ansehen. Dokumentiere das Nav-Sprachkonstrukt, das der
>   Knoten repräsentiert (mit kurzem Nav-Quelltext-Beispiel im `<summary>`, wo hilfreich).
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `protected`/`internal`/`private` überall
>   dort mitdokumentieren, wo der Member eine Invariante, Entwurfsentscheidung oder nicht
>   offensichtliches Verhalten trägt — triviale Felder/Durchreicher auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Vor dem Bearbeiten je Datei die Kodierung prüfen (Win-1252-Falle, CLAUDE.md); nach den
>   Edits BOM/CRLF wiederherstellen, falls das Tooling LF hinterlassen hat.
> - Danach Gates G1–G3 aus dem Status-Dokument ausführen und die Ausgabe in den Report
>   aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der
> „offen" gelassenen Member mit Grund, Gate-Ergebnisse G1–G3.

## 7. Commit-Konvention

Pro Batch ein Commit, Muster:

```
Nav-Engine: XML-Doku für Syntax/<Bereich> (Batch <n>/7) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-13 | — | Plan erstellt, Audit durchgeführt (42 Dateien ohne Doku); Gate G2 verifiziert und kalibriert (Baseline: 952× CS1591, 1× CS1574 unter `Syntax\`) |
