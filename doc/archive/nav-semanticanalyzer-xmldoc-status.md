# Nav.Language/SemanticAnalyzer — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-14).** Alle 5 Batches fertig, `SemanticAnalyzer\` ist
> doku-warnungsfrei (0× CS1591, 0× CS1570–CS1584); doku-only-Diff über alle 52 Dateien
> mechanisch verifiziert (722 Insertions, 5 Deletions — ausschließlich `///`-Zeilen),
> `nav build` + `nav test` grün. Ziel war: alle Dateien unter `Nav.Language\SemanticAnalyzer\`
> durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne jede Code-Änderung**.
> Vorgehen war die Blaupause der Syntax- und SemanticModel-Kampagnen
> (`doc/nav-syntax-xmldoc-status.md`, `doc/nav-semanticmodel-xmldoc-status.md`).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-14)

- 52 Dateien, ~1.730 Zeilen unter `Nav.Language\SemanticAnalyzer\`.
- **49 Dateien haben 0 Doku-Zeilen.** Teil-Doku tragen nur `Nav5000FeatureRequiresNavLanguageVersion.cs`
  (11), `Nav5001NavLanguageVersionNotSupported.cs` (7) und
  `Nav0124GeneratedMember0CollidesWithAnotherMember.cs` (22 `///`-Zeilen).
- Struktur des Ordners: `Analyzer.cs` (Basis `NavAnalyzer` + `INavAnalyzer` + statische
  Analyzer-Registry, vom `Nav.Analyzer.SourceGenerator` als `CreateAll()` gespeist) und
  `AnalyzerContext.cs` sind Infrastruktur; die übrigen 50 Dateien sind **je genau ein Analyzer
  für genau eine Diagnose** (`Nav####…`-Namensschema = `DiagnosticId` + Message-Kurzform).
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-14):** **157 CS1591**-Warnungen
  (eindeutige Treffer; fehlende XML-Doku an öffentlichen Membern) unter `SemanticAnalyzer\` →
  Ziel ist **0**. Keine CS157x-Vorbelastung (0 Treffer).
- Kodierungs-Lage: alle 52 Dateien valides UTF-8 **mit** BOM (streng per .NET-`UTF8Encoding`
  verifiziert — `iconv` existiert in der Git-Bash nicht, dessen „Fehlschläge" waren exit 127!),
  kein `U+FFFD`, keine Win-1252-Altlast. Zeilenenden durchgängig `w/crlf`, keine LF-Ausnahme.
- **Stil-Referenz bleibt `Nav.Language\Syntax\SyntaxTrivia.cs`** sowie die fertig dokumentierten
  `Syntax\`- und `SemanticModel\`-Dateien: deutsche Doku mit echten Umlauten, `<see cref="…"/>`
  statt Klartext-Typnamen, `<param>`/`<returns>` an Methoden, knappe Ein-Zeilen-Summaries an
  trivialen Properties.
- **Belege für Semantik-Aussagen** liegen für Analyzer besonders günstig:
  - **`Nav.Language\Diagnostic\DiagnosticDescriptors.Semantic.cs` / `.DeadCode.cs`** — die
    Descriptoren mit Message-Template, Kategorie und Severity (jeder Analyzer trägt seinen
    Descriptor als `Descriptor`-Property).
  - **`Nav.Language.Tests\Diagnostics\Tests\Nav####….nav`** — Golden-Fixtures je Diagnose;
    liefern belegte Nav-Quelltext-Beispiele, die die Diagnose auslösen (ideal für ein kurzes
    Beispiel im `<summary>`).
  - Das frisch dokumentierte **`SemanticModel\`** (Symbole/Kanten, `cref`-Ziele) und die
    Aufrufstelle `CodeGenerationUnitBuilder` (wann/wie Analyzer laufen).

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder
   korrigiert). Mechanisch verifiziert durch Gate G1 (Abschnitt 4) — der Diff ohne
   `///`-Zeilen muss identisch zu HEAD sein.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, dem Descriptor
   (Message-Template), den Golden-Fixtures oder den Verwendungen ableitbar sein. Bei
   Unsicherheit: Member **unkommentiert lassen** und im Batch-Report als „offen" melden —
   eine Lücke ist besser als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `protected`/`internal`/`private` Member werden **mitdokumentiert, wo es Sinn
   ergibt** — d.h. überall dort, wo der Member eine Invariante, eine Entwurfsentscheidung oder
   nicht offensichtliches Verhalten trägt (Erreichbarkeits-Prädikate, Hilfslogik); triviale
   private Felder/Durchreicher brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF** (kein LF-Sonderfall in diesem
   Ordner). Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku
   beschreibt den Code, nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, nach Review.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan und den Status; die eigentliche Doku-Arbeit
läuft **pro Batch in einem Subagenten** mit eigenem, frischem Kontext (Vorlage in Abschnitt 6).

Ablauf je Batch:

1. Orchestrator startet den Subagenten mit der Auftrags-Vorlage (Abschnitt 6) + Dateiliste.
2. Subagent liest Dateien **und ihre Belege** (Descriptor, Fixtures, Verwendungen), schreibt
   die Doku, führt G1 + G3 selbst aus.
3. Orchestrator verifiziert unabhängig: G1–G3 erneut ausführen, G4 (Build) einmal,
   dazu Stichproben-Review von 2–3 Dateien des Batches inkl. Nachschlagen der stärksten
   Behauptung im Code.
4. Status-Tabelle (Abschnitt 5) fortschreiben.

## 4. Verifikations-Gates (pro Batch, alle Pflicht)

**G1 — Doku-only-Diff** (Herzstück, Git Bash): Der Diff darf ausschließlich aus
`///`-Zeilen bestehen. Die Git-Bash-`grep` liest textmodus-bedingt CR-tolerant, daher ist der
Vergleich EOL-unempfindlich; schlägt das Gate an, ist es eine echte Code-Änderung → beheben,
**nie** das Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/SemanticAnalyzer/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; ändert keine Datei,
`-p:` wirkt nur auf den Aufruf; **`--no-incremental`** ist Pflicht, sonst verschluckt der
inkrementelle Build die Warnungen — und MSBuild listet Warnungen doppelt, daher immer
`sort -u` über die bereinigten Zeilen):

```powershell
dotnet build Nav.Language\Nav.Language.csproj -c Debug `
  -p:GenerateDocumentationFile=true --no-incremental
```

Auswertung gegen die **Baseline vom 2026-07-14** (nur `SemanticAnalyzer\`-Treffer zählen,
eindeutig gemacht):

- **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter
  `SemanticAnalyzer\` ist **0**. Jeder Treffer ist ein Fehler des laufenden Batches → sofort
  beheben.
- **CS1591** (fehlende Doku): Baseline unter `SemanticAnalyzer\` ist **157** (unique); die Zahl
  muss mit jedem Batch monoton sinken und am Kampagnen-Ende **0** sein.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -E "[\\\\/]SemanticAnalyzer[\\\\/]" | sort -u
grep -E "CS1591" build.log | grep -E "[\\\\/]SemanticAnalyzer[\\\\/]" | sed 's/^[[:space:]]*//' | sort -u | wc -l
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`; EOL-Bild laut
`git ls-files --eol` unverändert (52× `w/crlf`, keine Ausnahme). Edit/Write kann LF
hinterlassen → dann CRLF (+BOM) wiederherstellen. **Achtung:** `iconv` existiert in der
Git-Bash nicht — UTF-8-Validität bei Bedarf per PowerShell/.NET (`UTF8Encoding($false,$true)`)
prüfen, nicht per `iconv`.

```bash
for f in Nav.Language/SemanticAnalyzer/*.cs; do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
git ls-files --eol Nav.Language/SemanticAnalyzer/ | grep -v 'w/crlf'   # erwartet: leer
```

**G4 — Build grün** (im Orchestrator, einmal pro Batch): der G2-Aufruf genügt; am
Kampagnen-Ende zusätzlich einmal `nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

| Batch | Inhalt (Dateien) | Status |
|---|---|---|
| **B1 — Infrastruktur & Symbol-Auflösung** (8) | Analyzer, AnalyzerContext, Nav0010CannotResolveTask0, Nav0011CannotResolveNode0, Nav0012CannotResolveExit0, Nav0023AnOutgoingEdgeForTrigger0IsAlreadyDeclared, Nav0024OutgoingEdgeForExit0AlreadyDeclared, Nav0025NoOutgoingEdgeForExit0Declared | **fertig** (2026-07-14) |
| **B2 — Graph-Regeln I: Init/GoTo-Erreichbarkeit** (8) | Nav0103InitNodeMustNotContainIncomingEdges, Nav0104ChoiceNode0MustOnlyReachedByGoTo, Nav0105ExitNode0MustOnlyReachedByGoTo, Nav0106EndNode0MustOnlyReachedByGoTo, Nav0107ExitNode0HasNoIncomingEdges, Nav0108EndNodeHasNoIncomingEdges, Nav0109InitNode0HasNoOutgoingEdges, Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2 | **fertig** (2026-07-14) |
| **B3 — Graph-Regeln II: Kanten-Pflichten & Init-Signaturen** (9) | Nav0111ChoiceNode0HasNoIncomingEdges, Nav0112ChoiceNode0HasNoOutgoingEdges, Nav0113TaskNode0HasNoIncomingEdges, Nav0114DialogNode0HasNoIncomingEdges, Nav0115DialogNode0HasNoOutgoingEdges, Nav0116ViewNode0HasNoIncomingEdges, Nav0117ViewNode0HasNoOutgoingEdges, Nav0118EndNode0NotAllowedBecauseReachableFromInit1, Nav0119InitNode0HasSameSignatureAsInitNode1 | **fertig** (2026-07-14) |
| **B4 — Continuation, Trigger & Versions-Gates** (13) | Nav0120SourceNode0OfContinuationMustBeViewOrDialog, Nav0121TargetNode0OfContinuationMustBeTask, Nav0122DifferentViewsInContinuationNotSupported, Nav0124GeneratedMember0CollidesWithAnotherMember, Nav0200SignalTriggerNotAllowedAfterInit, Nav0201SpontaneousNotAllowedInSignalTrigger, Nav0203TriggerNotAllowedAfterChoice, Nav0220ConditionsAreNotAllowedInTriggerTransitions, Nav0221OnlyIfConditionsAllowedInExitTransitions, Nav0222Node0IsReachableByDifferentEdgeModes, Nav2000IdentifierExpected, Nav5000FeatureRequiresNavLanguageVersion, Nav5001NavLanguageVersionNotSupported | **fertig** (2026-07-14) |
| **B5 — Dead-Code-Hinweise (Nav1xxx)** (14) | Nav1002UsingDirective0AppearedPreviously, Nav1003IncludeNotRequired, Nav1005TaskDeclaration0NotRequired, Nav1007ChoiceNode0HasNoIncomingEdges, Nav1008ChoiceNode0HasNoOutgoingEdges, Nav1009ChoiceNode0NotRequired, Nav1010TaskNode0HasNoIncomingEdges, Nav1012TaskNode0NotRequired, Nav1014DialogNode0NotRequired, Nav1015DialogNode0HasNoIncomingEdges, Nav1016DialogNode0HasNoOutgoingEdges, Nav1017ViewNode0NotRequired, Nav1018ViewNode0HasNoIncomingEdges, Nav1019ViewNode0HasNoOutgoingEdges | **fertig** (2026-07-14) |

Reihenfolge B1→B5 ist bewusst: erst die Infrastruktur (`NavAnalyzer`/`AnalyzerContext` sind
`cref`-Ziele aller übrigen Dateien), dann die Fehler-Analyzer nach Themengruppen, zuletzt die
formelhaften Dead-Code-Hinweise (Nav1xxx, alle nach demselben Muster — profitieren maximal von
den vorher etablierten Formulierungen).

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\SemanticAnalyzer\` mit C#-XML-Doku. **Dateien
> dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine
>   Umformatierung, keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen.
> - Lies zuerst `doc/nav-semanticanalyzer-xmldoc-status.md`, Abschnitte 2 und 4 (Regeln +
>   Gates), und `Nav.Language\Syntax\SyntaxTrivia.cs` als Stil-Referenz.
> - Vor der Formulierung je Analyzer: den **Descriptor** in
>   `Nav.Language\Diagnostic\DiagnosticDescriptors.Semantic.cs` bzw. `.DeadCode.cs`
>   (Message-Template, Kategorie, Severity) und die **Golden-Fixtures**
>   `Nav.Language.Tests\Diagnostics\Tests\Nav####….nav` ansehen. Das `<summary>` des Analyzers
>   beschreibt die geprüfte Sprachregel (was ist verboten/erforderlich und **warum**, soweit
>   belegbar) — mit kurzem Nav-Quelltext-Beispiel aus den Fixtures, wo hilfreich. Die
>   Symbol-/Kanten-Begriffe sind in `SemanticModel\` fertig dokumentiert — `cref` darauf.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - `Descriptor`-Property und `Analyze`-Overrides: knapp halten; `<inheritdoc/>` an
>   Interface-/Basisklassen-Implementierungen nutzen, wo die Basisaussage trägt — eigener Text
>   nur, wo der konkrete Analyzer Zusätzliches tut.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `protected`/`internal`/`private` überall
>   dort mitdokumentieren, wo der Member eine Invariante, Entwurfsentscheidung oder nicht
>   offensichtliches Verhalten trägt — triviale Felder/Durchreicher auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Zeilenenden der Dateien unverändert belassen (durchgängig CRLF); nach den Edits BOM
>   prüfen und Gates G1 + G3 aus dem Status-Dokument ausführen, Ausgabe in den Report
>   aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der
> „offen" gelassenen Member mit Grund, Gate-Ergebnisse G1 + G3.

## 7. Commit-Konvention

Pro Batch ein Commit (oder ein Sammel-Commit am Ende — Entscheidung des Nutzers), Muster:

```
Nav-Engine: XML-Doku für SemanticAnalyzer/<Bereich> (Batch <n>/5) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-14 | — | Plan erstellt, Audit durchgeführt (49 von 52 Dateien ohne Doku); Gate G2 verifiziert (Baseline: 157× CS1591 unique, 0× CS157x unter `SemanticAnalyzer\`); Kodierung geprüft (überall BOM+CRLF, valides UTF-8, keine Win-1252-Altlast; Hinweis: `iconv` fehlt in der Git-Bash — Prüfung per .NET-Decoder) |
| 2026-07-14 | B1 | 8 Dateien, 156 `///`-Zeilen (29 Einheiten); G1–G4 grün, CS1591 157→130; Stichproben verifiziert: Nav0011 prüft nur die Zielseite — die Quellseite meldet `TaskDefinitionSymbolBuilder` (`case null` beim Binden der Transitionen); Nav0025 übergeht unreferenzierte Task-Knoten (Nav1012-Zuständigkeit, Code-Kommentar) und nutzt `GetUnconnectedExits`. Offen (bewusst): `Analyzer.AnalyzerList` (Registry-`//`-Kommentar trägt bereits), Nav0023-`triggerMap`-Quirk (nur letzter Trigger je Knoten verglichen — Semantik vs. Quirk unklar, nur Sprachregel dokumentiert) |
| 2026-07-14 | B2 | 8 Dateien, 24 Einheiten; G1–G4 grün, CS1591 130→106; Stichproben verifiziert: Nav0103-Begründung (Init ist bewusst kein `ITargetNodeSymbol`, dessen Doku nennt Nav0103) und Nav0110 (transitive Choice-Auflösung via `GetReachableCalls`, Diagnose am Kanten-Operator, Gegenstück Nav0118). Bewusst nicht behauptet: fachliches „Warum" des Goto-Zwangs bei Exit/End (nicht belegbar); Nav0109-Severity-TODO nicht in die Doku gehoben |
| 2026-07-14 | B3 | 9 Dateien, 27 Einheiten; G1–G4 grün, CS1591 106→79; je „HasNo…Edges"-Analyzer den Dead-Code-Zwilling (Nav1xxx) samt abweichendem Diagnose-Anker dokumentiert; Stichprobe verifiziert: Nav0119-Signatur = geordnete Parameter-**Typtexte** (whitespace-bereinigt, Namen unerheblich, parameterlos = leere Signatur), Meldung je Folge-Doppelgänger gegen den ersten Knoten. Report-Notiz: Nav0115 ohne Golden-Fixture, Nav0117 nur `_2Edges`; kein Analyzer dieses Batches nutzt `IsWarningDisabled` (grep-verifiziert: nur Nav0107/Nav1012) |
| 2026-07-14 | B4 | 13 Dateien, 38 Einheiten; G1–G4 grün, CS1591 79→42; **1 Bestands-Korrektur** (Nav0124-`MemberSurfaces`: „exakt die Auswahl von CodeModelBuilderV2" war zu stark — Abstrakt-Ausschluss sitzt im Context-Bau, Choice-Erreichbarkeit hier via `INodeSymbol.IsReachable`; am `TransitionCallContextCodeModel` nachverifiziert). Stichprobe verifiziert: Nav0122-Quellen-Pooling deckungsgleich mit den V2-Call-Contexten. Offen/notiert: Nav0201 vermutlich nicht auslösbar (`spontaneous`/`spont` sind unbedingte Lexer-Keywords, `ParseSignalTrigger` akzeptiert nur Identifier — toter Code oder Altlast-Absicherung, nur Sprachregel dokumentiert); Nav0120–0122 ohne Golden-Fixtures |
| 2026-07-14 | B5 | 14 Dateien, 42 Einheiten; G1–G4 grün, **CS1591 unter `SemanticAnalyzer\` = 0**; Dead-Code-Zwillinge zurückverlinkt (Anker je Datei am Code verifiziert: DeadCode an den Kanten, damit der Editor toten Code ausgraut); Stichproben verifiziert: Nav1002 überschreibt bewusst den Unit-Einstieg (Datei-Kopf statt Task-Auffächer, `<inheritdoc cref="INavAnalyzer.Analyze"/>` gegen irreführende Basisklassen-Remarks), Nav1012-Opt-out via `IsWarningDisabled`. Notiert: Nav1010 trägt redundantes inneres `Outgoings.Any()`; Nav1007/1008/1015/1018 ohne eigene Fixtures (über Zwillings-Fixtures belegt) |
| 2026-07-14 | Ende | Schlussabsicherung: `nav build` grün (0 Warnungen/0 Fehler), `nav test` 1907/1910 bestanden (3 explizit übersprungen) + 115/115 MCP-Tests; net10 `dotnet test` 1847/1847 grün. **Kampagne abgeschlossen.** |
