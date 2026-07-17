# Nav.Language/SemanticModel — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-14).** Alle 6 Batches fertig, `SemanticModel\` ist
> doku-warnungsfrei (0× CS1591, 0× CS1570–CS1584); doku-only-Diff über 48 geänderte von 50
> Dateien mechanisch verifiziert (1490 Insertions, 10 Deletions — ausschließlich `///`-Zeilen),
> `nav build` + `nav test` grün. Ziel war: alle Dateien unter `Nav.Language\SemanticModel\`
> durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne jede Code-Änderung**.
> Vorgehen war die Blaupause der Syntax-Kampagne (`doc/archive/nav-syntax-xmldoc-status.md`).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-14)

- 50 Dateien, ~3.260 Zeilen unter `Nav.Language\SemanticModel\`.
- **34 Dateien haben 0 Doku-Zeilen** — darunter die komplette Symbol-Basis (`Symbol`,
  `NodeSymbol`, `SymbolCollection`) und der `CodeGenerationUnitBuilder`. Die restlichen 16
  tragen nur punktuelle Doku (Spitzenreiter `EdgeExtensions` mit 21 `///`-Zeilen).
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-14):** **197 CS1591**-Warnungen
  (eindeutige Treffer; fehlende XML-Doku an öffentlichen Membern) unter `SemanticModel\` →
  Ziel ist **0**. Keine CS157x-Vorbelastung (0 Treffer) — anders als seinerzeit in `Syntax\`.
- Kodierungs-Lage: alle 50 Dateien UTF-8 **mit** BOM, kein `U+FFFD`, keine Win-1252-Altlast.
  Zeilenenden: Working Tree CRLF (git-Attribut `text=auto eol=crlf`, Index normalisiert auf LF);
  einzige Ausnahme ist `ContinuationTransition.cs` (w/lf — bekannte EOL-Altlast, **nicht**
  „reparieren", git normalisiert ohnehin).
- **Stil-Referenz bleibt `Nav.Language\Syntax\SyntaxTrivia.cs`** sowie die fertig dokumentierten
  `Syntax\`-Dateien: deutsche Doku mit echten Umlauten, `<see cref="…"/>` statt
  Klartext-Typnamen, Roslyn-Analogien wo tragfähig, `<param>`/`<returns>` an Methoden, knappe
  Ein-Zeilen-Summaries an trivialen Properties.
- **Belege für Semantik-Aussagen** liegen hier vor allem in den **Buildern**
  (`TaskDefinitionSymbolBuilder`, `TaskDeclarationSymbolBuilder`, `CodeGenerationUnitBuilder`
  — dort entstehen die Symbole und werden verdrahtet), in den semantischen Analyzern
  (`Nav.Language\Diagnostics\`, Nav-Fehlercodes), im Codegen (`CodeGen\` — was ein Symbol im
  erzeugten C# bedeutet) und in den Tests.

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder
   korrigiert). Mechanisch verifiziert durch Gate G1 (Abschnitt 4) — der Diff ohne
   `///`-Zeilen muss identisch zu HEAD sein.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen
   (Builder-Konstruktionsstelle, Analyzer, Codegen, Tests) ableitbar sein. Bei Unsicherheit:
   Member **unkommentiert lassen** und im Batch-Report als „offen" melden — eine Lücke ist
   besser als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `protected`/`internal`/`private` Member werden **mitdokumentiert, wo es Sinn
   ergibt** — d.h. überall dort, wo der Member eine Invariante, eine Entwurfsentscheidung oder
   nicht offensichtliches Verhalten trägt (Builder-Verdrahtung, Auflösungs-Logik, Caches);
   triviale private Felder/Durchreicher brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM.** Zeilenenden der Datei **unverändert
   belassen** (CRLF-Bestand bleibt CRLF; die LF-Altlast `ContinuationTransition.cs` bleibt LF).
   Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku
   beschreibt den Code, nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, nach Review.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan und den Status; die eigentliche Doku-Arbeit
läuft **pro Batch in einem Subagenten** mit eigenem, frischem Kontext (Vorlage in Abschnitt 6).

Ablauf je Batch:

1. Orchestrator startet den Subagenten mit der Auftrags-Vorlage (Abschnitt 6) + Dateiliste.
2. Subagent liest Dateien **und ihre Verwendungen**, schreibt die Doku, führt G1 + G3 selbst aus.
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
for f in $(git diff --name-only -- 'Nav.Language/SemanticModel/*.cs'); do
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

Auswertung gegen die **Baseline vom 2026-07-14** (nur `SemanticModel\`-Treffer zählen,
eindeutig gemacht):

- **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter
  `SemanticModel\` ist **0**. Jeder Treffer ist ein Fehler des laufenden Batches → sofort beheben.
- **CS1591** (fehlende Doku): Baseline unter `SemanticModel\` ist **197** (unique); die Zahl
  muss mit jedem Batch monoton sinken und am Kampagnen-Ende **0** sein.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -E "[\\\\/]SemanticModel[\\\\/]" | sort -u
grep -E "CS1591" build.log | grep -E "[\\\\/]SemanticModel[\\\\/]" | sed 's/^[[:space:]]*//' | sort -u | wc -l
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`; EOL-Bild laut
`git ls-files --eol` unverändert (49× `w/crlf`, `ContinuationTransition.cs` `w/lf`).
Edit/Write kann LF hinterlassen → dann CRLF (+BOM) wiederherstellen, **außer** bei der
LF-Altlast.

```bash
for f in Nav.Language/SemanticModel/*.cs; do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
git ls-files --eol Nav.Language/SemanticModel/ | grep -v 'w/crlf'   # erwartet: nur ContinuationTransition.cs
```

**G4 — Build grün** (im Orchestrator, einmal pro Batch): der G2-Aufruf genügt; am
Kampagnen-Ende zusätzlich einmal `nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

| Batch | Inhalt (Dateien) | Status |
|---|---|---|
| **B1 — Symbol-Basis & Code-Parameter** (7) | ISymbol, Symbol, SymbolPosition, SymbolCollection, SymbolList, ICodeParameter, CodeParameter | **fertig** (2026-07-14) |
| **B2 — Knoten-Symbole** (10) | INodeSymbol, NodeSymbol, INodeReferenceSymbol, INodeReferenceSymbolOfT, NodeReferenceSymbol, NodeReferenceSymbolOfT, IInitNodeAliasSymbol, InitNodeAliasSymbol, ITaskNodeAlias, TaskNodeAliasSymbol | **fertig** (2026-07-14) |
| **B3 — Connection Points & Trigger** (7) | IConnectionPointSymbol, ConnectionPointSymbol, IExitConnectionPointReferenceSymbol, ExitConnectionPointReferenceSymbol, ITriggerSymbol, TriggerSymbol, TriggerSymbolBuilder | **fertig** (2026-07-14) |
| **B4 — Kanten, Transitionen & Call** (12) | IEdge, IEdgeModeSymbol, EdgeModeSymbol, EdgeExtensions, ITransition, Transition, InitTransition, ChoiceTransition, TriggerTransition, ExitTransition, ContinuationTransition, Call | **fertig** (2026-07-14) |
| **B5 — Task-Deklaration/-Definition & Builder** (6) | ITaskDeclarationSymbol, TaskDeclarationSymbol, TaskDeclarationSymbolBuilder, ITaskDefinitionSymbol, TaskDefinitionSymbol, TaskDefinitionSymbolBuilder | **fertig** (2026-07-14) |
| **B6 — CodeGenerationUnit, Include & Extensions** (8) | IIncludeSymbol, IncludeSymbol, CodeGenerationUnit, CodeGenerationUnitBuilder, CodeGenerationUnitExtensions, ChoiceNodeSymbolExtensions, TaskNodeSymbolExtensions, TaskDefinitionSymbolExtensions | **fertig** (2026-07-14) |

Reihenfolge B1→B6 ist bewusst: erst die Basis (Symbol/Collections), dann Knoten und Kanten,
zuletzt die Aggregate (Task-Definition, CodeGenerationUnit) — so existieren die `cref`-Ziele
samt Doku, wenn übergeordnete Klassen auf sie verweisen. Interface + Implementierung liegen
bewusst im **selben** Batch (die Doku-Autorität sitzt am Interface; die Implementierung
verweist per `<inheritdoc/>` nur, wo C# das trägt — an `override`/Interface-Implementierungen).

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\SemanticModel\` mit C#-XML-Doku. **Dateien
> dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine
>   Umformatierung, keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen.
> - Lies zuerst `doc/archive/nav-semanticmodel-xmldoc-status.md`, Abschnitte 2 und 4 (Regeln + Gates),
>   und `Nav.Language\Syntax\SyntaxTrivia.cs` als Stil-Referenz.
> - Vor der Formulierung je Typ: die **Konstruktionsstelle im Builder**
>   (`TaskDefinitionSymbolBuilder.cs`, `TaskDeclarationSymbolBuilder.cs`,
>   `CodeGenerationUnitBuilder.cs`), die zugrunde liegende Syntax (`Nav.Language\Syntax\` —
>   dort ist alles dokumentiert) und die Verwendung in Analyzern/Codegen ansehen.
>   Dokumentiere das Nav-Sprachkonzept, das das Symbol repräsentiert (mit kurzem
>   Nav-Quelltext-Beispiel im `<summary>`, wo hilfreich).
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - Interface ist die Doku-Autorität; an Implementierungs-Membern `<inheritdoc/>` nutzen, wo
>   die Aussage identisch ist — eigenen Text nur, wo die Implementierung Zusätzliches trägt.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `protected`/`internal`/`private` überall
>   dort mitdokumentieren, wo der Member eine Invariante, Entwurfsentscheidung oder nicht
>   offensichtliches Verhalten trägt — triviale Felder/Durchreicher auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Zeilenenden der Datei unverändert belassen (CRLF-Bestand; `ContinuationTransition.cs`
>   ist LF-Altlast und bleibt LF); nach den Edits BOM prüfen und Gates G1 + G3 aus dem
>   Status-Dokument ausführen, Ausgabe in den Report aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der
> „offen" gelassenen Member mit Grund, Gate-Ergebnisse G1 + G3.

## 7. Commit-Konvention

Pro Batch ein Commit (oder ein Sammel-Commit am Ende — Entscheidung des Nutzers), Muster:

```
Nav-Engine: XML-Doku für SemanticModel/<Bereich> (Batch <n>/6) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-14 | — | Plan erstellt, Audit durchgeführt (34 von 50 Dateien ohne Doku); Gate G2 verifiziert (Baseline: 197× CS1591 unique, 0× CS157x unter `SemanticModel\`); Kodierung geprüft (überall BOM, keine Win-1252-Altlast, `ContinuationTransition.cs` = LF-Altlast) |
| 2026-07-14 | B1 | 7 Dateien, 170 `///`-Zeilen (36 Member, 1 Bestands-Präzisierung an `ISymbol.SyntaxTree`); G1–G4 grün, CS1591 197→172; Stichprobe verifiziert: `SyntaxTree` ist bei `taskref`-Includes `null` (TaskDeclarationSymbolBuilder nullt `Syntax` bei `_processAsIncludedFile`) |
| 2026-07-14 | B2 | 10 Dateien, ~304 `///`-Zeilen (107 Einheiten inkl. der ganzen Knoten-Klassenhierarchie); G1–G4 grün, CS1591 172→119; Erreichbarkeits- und Alias-Semantik an EdgeExtensions/TaskDefinitionSymbolBuilder belegt; Stichprobe verifiziert: Init-Symbol heißt `Init`, Bezeichner ist Alias (`Name => Alias?.Name ?? base.Name`) |
| 2026-07-14 | B3 | 7 Dateien, ~183 `///`-Zeilen (71 Einheiten); G1–G4 grün, CS1591 119→94; Codegen-Bezug belegt (`<Signal>Logic` via SignalTriggerCodeInfo); Stichproben verifiziert: `spont` heißt als Trigger stets `spontaneous` (TriggerSyntax.Keyword), Nav0012 bei `Declaration: null`; notiert: `// TODO wo ist der Alias?` an IInitConnectionPointSymbol bleibt unangetastet |
| 2026-07-14 | B4 | 12 Dateien (11 bearbeitet, ContinuationTransition.cs war vollständig und blieb als LF-Altlast unangetastet), ~61 Einheiten; G1–G4 grün, CS1591 94→54; Erreichbarkeits-/Choice-Auflösungs-Semantik an EdgeExtensions verifiziert (Choice erscheint nie selbst als Call; nur Kanten mit EdgeMode ergeben Calls); sauber differenziert: ExitConnectionPointReference null vs. Declaration null (Nav0012) |
| 2026-07-14 | B5 | 6 Dateien, ~341 `///`-Zeilen (84 Einheiten); G1–G4 grün, CS1591 54→32; **5 Bestands-Aussagen korrigiert** (u.a. `CodeGenerationUnit`-Doku betraf falsches Artefakt; `CreateNodeReference` erzeugt auch Quellreferenzen — an Continuation-Verdrahtung Zeile 531 verifiziert); Stichprobe verifiziert: `AsTaskDeclaration` nur bei Location-Identität (Builder Zeile 50) |
| 2026-07-14 | B6 | 8 Dateien, 256 `///`-Zeilen (47 Einheiten); G1–G4 grün, **CS1591 unter `SemanticModel\` = 0**; Stichprobe verifiziert: `CodeGenerationUnit.CodeNamespace` liefert die komplette `[namespaceprefix …]`-Deklaration samt Klammern (Kontrast `TaskDefinitionSymbol.CodeNamespace` mit `.Namespace?.ToString()`); Includes: `Symbols` filtert `IsIncluded`, inkludierte Deklarationen behalten `CodeGenerationUnit == null` |
| 2026-07-14 | Ende | Schlussabsicherung: `nav build` grün (0 Warnungen/0 Fehler), `nav test` 1842/1845 bestanden (3 explizit übersprungen) + 115/115 MCP-Tests; net10 `dotnet test` 1782/1782 grün (erster Lauf hatte 1 flaky Fehlschlag in `NavWorkspaceCoreSemanticCacheTests.SecondScan_ReturnsSameUnitInstances` — isoliert und im Wiederholungslauf grün, doku-only-Diff kann Verhalten nicht ändern). **Kampagne abgeschlossen.** |
