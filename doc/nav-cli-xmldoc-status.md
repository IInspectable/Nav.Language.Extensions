# Nav.Cli — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-15, uncommittet).** Alle 9 Handdoku-Dateien dokumentiert;
> **30× CS1591 → 0**, **0× CS1570–84** (eingeschleppte 13× CS1574 + 1× CS1587 mitrepariert — s.u.).
> Gates G1 (doku-only, alle 9 byte-exakt) / G2 (`--no-incremental`-Doku-Build, 0 eigene CS15xx) / G3
> (BOM/CRLF/kein `U+FFFD`) grün. Ausgeklammert sind die compile-eingebundenen Fremd-/Build-Dateien
> (`..\Shared\Options.cs`, `..\Build\GobalAssemblyInfo.cs`). Das **Glossar** (`doc/Glossar.md`) wurde um
> **§9 CLI / Host `nav.exe`** ergänzt (11 Einträge + A–Z-Index). Commit macht der Nutzer (pro Batch +
> Glossar).
>
> **Ablauf-Hinweis:** Die 3 Batch-Subagenten wurden vom Session-Limit vorzeitig abgebrochen (B1 hatte
> `CommandLine.cs` fertig, B2 Typ+`Run` von `NavCodeGenerator.cs`, B3 nichts geschrieben). Der Rest wurde
> vom Orchestrator direkt fertiggestellt. Zwei Fallen dabei: **(a)** die Gate-Glob `Nav.Cli/**/*.cs`
> erwischt **keine Root-Dateien** (`Program.cs`/`CommandLine.cs`/`GrammarCommand.cs`) — stattdessen
> `-- Nav.Cli` (ganzes Verzeichnis) nutzen. **(b)** `CommandLine.cs` importiert `.Generator`/`.Analyzer`/
> `.CodeGen` **nicht** → crefs dorthin müssen **relativ zum Sub-Namespace** qualifiziert werden
> (`Generator.NavCodeGenerator`, `Analyzer.SyntaxAnalyzerProgram`, `CodeGen.GenerationOptions`), sonst
> CS1574 (using hinzufügen wäre eine verbotene Code-Änderung). **(c)** `///` auf einer **lokalen
> Funktion** (`ValidateOptions`) wirft CS1587 → dort keine XML-Doku (ein `//`-Kommentar wäre eine
> Code-Änderung), Doku sitzt an der umschließenden `CreatePipeline`.
>
> **Kontext & Zielbild.** Nach den abgeschlossenen XML-Doku-Kampagnen über `Nav.Language`
> (`doc/nav-features-xmldoc-status.md`), `Nav.Language.CodeAnalysis`
> (`doc/nav-codeanalysis-xmldoc-status.md`) und `Nav.Language.ExtensionShared`
> (`doc/nav-extension-xmldoc-status.md`) ist **`Nav.Cli`** (Assembly **`nav.exe`**) das nächste Ziel:
> der schlanke Kommandozeilen-Host, der die Engine zum Codegenerator/Analyzer macht. Ziel: **alle
> handgeschriebenen Dateien** durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne jede
> Code-Änderung** — und dabei auf das **Glossar** (`doc/Glossar.md`) stützen sowie es um die neuen
> CLI-/Host-Begriffe **ergänzen**. Vorbild und Methodik: `doc/nav-codeanalysis-xmldoc-status.md`
> (Orchestrator + Subagent je Batch, Gates G1–G4).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-15)

`Nav.Cli` (net10.0, Assembly **`nav`** → self-contained Single-File-Publish `nav.exe`) ist der
Kommandozeilen-Host der Nav-Engine: er löst `.nav`-Eingaben ein und **generiert C#-Code** (Standardpfad)
bzw. **analysiert**/**fixt** sie (Nebenpfade). Er referenziert `Nav.Language` und `Nav.Utilities`; die
eigentliche Sprachlogik liegt vollständig in der Engine — der CLI-Host fügt nur
Kommandozeilen-Parsing, Datei-Discovery, Konsolen-Logging und die Manifest-Ausgabe für inkrementelle
MSBuild-Builds hinzu.

Vier Konzern-Bereiche:

- **CLI-Rahmen (Root)** — `Program.Main` als Einstieg: Response-File-Expansion (`@datei`),
  `grammar`-Subcommand-Dispatch, dann die Weiche Analyze vs. Generate. `CommandLine` ist das per
  **NDesk.Options** geparste Options-Modell (`-d`/`-s`/`-g`/`-m`/`-dm`/… → `CommandLine`-Properties);
  `CodeGenerationOptions` ist das `[Flags]`-Enum der erzeugbaren Artefakt-Klassen. `GrammarCommand`
  bedient `nav grammar` (EBNF-Ausgabe, ganz oder pro Regel).
- **Generator** — `NavCodeGenerator` fährt den Standardpfad: Dateien einsammeln (`CollectFiles`:
  `/d`-Verzeichnis- vs. `/s`-Einzeldatei-Modus, `.navignore`-Auswertung), `NavCodeGeneratorPipeline`
  konfigurieren (`CreatePipeline`: `CodeGenerationOptions` → `GenerationOptions`, Wurzelverzeichnis-
  Validierung), laufen lassen, und bei Erfolg die **Outputs-/Abhängigkeits-Manifeste** schreiben
  (`WriteManifest`) — die Grundlage der inkrementellen Builds.
- **Logging** — `ConsoleLogger : ILogger` ist die Konsolen-Implementierung der Engine-`ILogger`-
  Abstraktion: farbige `stdout`-Ausgabe je Schweregrad, Diagnostics über `DiagnosticFormatter`
  formatiert, `NoWarnings`/`Verbose`/`FullPaths` steuern Umfang und Pfaddarstellung. Die
  `WriteVerbose`/`WriteInfo`/`WriteError`/`WriteWarning`-Hooks sind `protected virtual`
  (Erweiterungspunkt).
- **Analyzer** — die Nebenpfade hinter `--analyze`: `SyntaxAnalyzerProgram` fährt den
  `SyntaxAnalyzerPipeline` mit einem `SyntaxNodeAnalyzer` (Referenzanalyzer:
  `CodeNotImplementedAnalyzer`, zählt Knoten per Regex-Muster über den `SyntaxNodeWalker`);
  `CodeFixProgram` fährt den `CodeFixPipeline`, der Style-CodeFixes vorschlägt, die Datei per TFS-
  `checkout` freigibt und die Textänderungen zurückschreibt.

- **Scope: voller Ordner** (wie in den Vorkampagnen) — nicht nur die CS1591-messbare `public`-Surface.
  Die meisten CLI-Typen sind `internal`/`static`/`sealed` (Entry-Point-Maschinerie); CS1591 misst davon
  fast nichts. Genau dieser `internal`-Kern (Einstiegsablauf, Datei-Discovery, Pipeline-Verdrahtung,
  Manifest-Schreiben, Checkout) ist die zu erklärende Host-Logik.

- **Handdoku-Dateien (9):**
  - **Root (3):** `Program.cs`, `CommandLine.cs`, `GrammarCommand.cs`.
  - **`Generator/` (1):** `NavCodeGenerator.cs`.
  - **`Logging/` (1):** `ConsoleLogger.cs`.
  - **`Analyzer/` (4):** `SyntaxAnalyzerPipeline.cs`, `CodeFixPipeline.cs`, `SyntaxAnalyzerProgram.cs`,
    `CodeFixProgram.cs`.
- **Nicht Gegenstand (compile-eingebunden aus anderen Verzeichnissen):**
  - `..\Shared\Options.cs` (vendored **NDesk.Options** — Fremdcode; 66 eigene CS1591, außer Scope).
  - `..\Build\GobalAssemblyInfo.cs` (geteilte Assembly-Attribute/`required`-Polyfills — kein
    Doku-Ziel).
  - `obj\**` (generierte Assembly-Attribut-Stubs).

- **Messbare Ziellinie (Gate G2, Baseline verifiziert am 2026-07-15):**
  - **CS1591** (fehlende Doku an `public` Membern) unter den 9 Handdoku-Dateien: **30** → Ziel **0**.
    Verteilung: `Logging/ConsoleLogger.cs` **16** (die `public`-Klasse `ConsoleLogger` + Ctor +
    4 Properties + 5 `Log*`-Methoden + 4 `protected virtual Write*` + `FormatDiagnostic`),
    `Analyzer/SyntaxAnalyzerPipeline.cs` **8** (die beiden `public`-Klassen `SyntaxNodeAnalyzer` &
    `CodeNotImplementedAnalyzer` samt Membern), `CommandLine.cs` **6** (das `public`-`[Flags]`-Enum
    `CodeGenerationOptions` samt Werten). Die übrigen 6 Dateien (`Program`, `GrammarCommand`,
    `NavCodeGenerator`, `CodeFixPipeline`, `SyntaxAnalyzerProgram`, `CodeFixProgram`) sind
    `internal`/`static`/`sealed` → **0 CS1591**, obwohl inhaltlich zu dokumentieren.
  - **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter den
    Handdoku-Dateien **0** — nichts mitzureparieren.
- **CS1591 unterschätzt die reale Surface massiv** — der `internal`-Kern (5 der 9 Dateien, der gesamte
  Einstiegs-/Generator-/Analyzer-Ablauf) zählt nicht. Ziellinie ist zweiteilig: **(a)** CS1591 unter
  den Handdoku-Dateien = 0 **und** **(b)** der `internal`-Kern durchgängig sinnvoll dokumentiert —
  geprüft per Stichproben-Review je Batch.

- **Stil-Referenz:** die frisch fertige Engine-/CodeAnalysis-Doku (dichte, korrekte Doku: deutsch mit
  echten Umlauten, `<see cref="…"/>` statt Klartext-Typnamen, `<param>`/`<returns>` an Methoden, knappe
  Ein-Zeilen-Summaries an trivialen Properties). Konkret gut als Muster:
  `Nav.Language\CodeGen\Shared\CodeBuilder.cs`.
- **Bestands-Doku beachten:** `GrammarCommand` trägt bereits eine Typ-`<summary>` (13 `///`) — Lücken
  füllen (`Run`, `ShowHelp`), Bestehendes nur bei sachlichem Fehler anfassen. Die übrigen Dateien haben
  reichlich `//`-Erklärkommentare (Response-File, `.navignore`, Manifeste, `.nav`-Endungsfalle) —
  diese bleiben **unberührt**; die `///`-Doku destilliert sie in strukturierte XML-Doku.

## 2. Glossar-Anschluss (stützen **und** ergänzen)

Die Doku benutzt konsequent die **Kanon-Begriffe** aus `doc/Glossar.md` (Host, Engine, Codegenerator,
`.nav`-Datei, Task, taskref, Diagnostic, Symbol, WFL/IWFL, ConnectionPoint, Deklaration vs. Definition,
CodeFix/CodeAction). `Nav.Cli` bringt **neue Fachbegriffe** mit, die das Glossar noch nicht führt —
diese sind die „ggf. ergänzen"-Fläche (voraussichtlich als **neuer §9 CLI / Host `nav.exe`**):

- **CLI-Host / `nav.exe`** — der Kommandozeilen-Host der Engine (Codegenerator + Analyzer).
- **Response-File** (`@datei`) — Kommandozeilenargumente aus einer Datei (`Program.LoadArgs`).
- **Subcommand** (`nav grammar`) — Unterbefehl vor den Optionen.
- **Options-Modell / `OptionSet`** — das per NDesk.Options geparste `CommandLine`.
- **Manifest / Abhängigkeits-Manifest** — die Outputs-/`taskref`-Dateilisten für inkrementelle Builds.
- **`.navignore`** — Ausschluss-Mechanismus der Datei-Discovery.
- **`CodeGenerationOptions`** (WFL/IWFL/TO-Klassen) — die erzeugbaren Artefakt-Klassen.
- **Pipeline** (`NavCodeGeneratorPipeline`, `SyntaxAnalyzerPipeline`, `CodeFixPipeline`) —
  Host-seitige Verdrahtung der Engine-Kerne.

**Arbeitsmodus:** Jeder Batch-Subagent (a) verwendet die vorhandenen Glossar-Begriffe und (b) meldet im
Report **Kandidaten für neue Glossar-Einträge** (Begriff, Kurzdefinition, Vorschlag Kanon-Schreibweise
de/en). Der Orchestrator synthetisiert daraus **nach** den Batches eine Glossar-Ergänzung (neuer
Abschnitt §9 bzw. Einträge in §1/§4) und liefert dafür eine eigene Commit-Message.

## 3. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder korrigiert).
   Mechanisch verifiziert durch Gate G1 — der Diff ohne `///`-Zeilen muss byte-identisch zu HEAD sein,
   inkl. Einrückung, Zeilenenden, BOM. `//`-Kommentare, `#region`, `using`, Attribute bleiben unberührt.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen (Aufrufer,
   `CustomBuild.targets`, `Pharmatechnik.Nav.Language.targets`, Tests) oder dem Semantikmodell ableitbar
   sein. Bei Unsicherheit: Member **unkommentiert lassen** und im Batch-Report als „offen" melden — eine
   Lücke ist besser als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591 messbar).
   `internal`/`protected`/`private` Member werden hier **breit mitdokumentiert** — der Host-Kern ist
   bewusst nicht `public`. Triviale Durchreicher-Properties und offensichtliche Felder brauchen keine
   Doku. **Achtung `cref` in andere Assemblies:** Typen aus `Nav.Language`/`Nav.Utilities`
   (z.B. `NavCodeGeneratorPipeline`, `GenerationOptions`, `Diagnostic`, `DiagnosticFormatter`,
   `SyntaxNodeWalker`, `FileSpec`, `NavIgnore`, `NavSolution`, `NavGrammar`, `ILogger`,
   `ISyntaxProviderFactory`) sind referenzierbar und **sollen** als `cref` gesetzt werden — sie sind
   über die ProjectReferences sichtbar.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF.** Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** — die Doku beschreibt den Code, nicht den
   Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, pro Batch, nach Review.
7. **Win-1252-Falle beachten** (CLAUDE.md): vor dem Bearbeiten einer Umlaut-Datei die Kodierung prüfen.
   (Audit 2026-07-15: alle 9 Handdoku-Dateien sind sauber UTF-8 mit BOM, keine rohen Win-1252-Bytes.)

## 4. Verifikations-Gates (alle Pflicht)

**G1 — Doku-only-Diff** (Git Bash): Der Diff darf ausschließlich aus `///`-Zeilen bestehen. Byte-exakter
Vergleich — schlägt das Gate flächig an, ist meist ein Zeilenenden-/BOM-Schaden die Ursache; dann den
Schaden beheben, **nie** das Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Cli/**/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; **`--no-incremental`** ist Pflicht,
sonst verschluckt der inkrementelle Build die Warnungen). **`Nav.Cli` hat `RuntimeIdentifiers=win-x64`
→ die Warnungen erscheinen doppelt** (AnyCPU- + win-x64-Durchlauf); `sort -u` dedupliziert. Der
Doku-Build zieht referenzierte Projekte hoch — deren Warnungen sind **nicht** unser Scope. Nur die
eigenen Handdoku-Dateien zählen: Pfad enthält `\Nav.Cli\`, **ohne** `\obj\` und **ohne** `\Shared\`
(vendored NDesk.Options):

```bash
dotnet build Nav.Cli/Nav.Cli.csproj -c Debug \
  -p:GenerateDocumentationFile=true --no-incremental 2>&1 > /tmp/clidoc.txt
grep -F ': warning CS15' /tmp/clidoc.txt \
  | grep -F '\Nav.Cli\' | grep -Fv '\obj\' | grep -Fv '\Shared\' | sort -u \
  | grep -oE 'CS15[0-9][0-9]' | sort | uniq -c
```

Auswertung gegen die **Baseline vom 2026-07-15**:

- **CS1591:** Baseline **30** → monoton sinkend, am Ende **0**.
- **CS1570–CS1584:** Baseline **0** → bleibt **0**.

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`, CRLF intakt.

```bash
for f in $(git diff --name-only -- 'Nav.Cli/**/*.cs'); do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
```

**G4 — Build grün** (im Orchestrator): der G2-Aufruf genügt; am Kampagnen-Ende zusätzlich einmal
`nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

Ordner-/Konzern-diszipliniert (disjunkte Dateimengen → parallelisierbar), nach Aufwand balanciert.

| Batch | Dateien | CS1591 | Status |
|---|---|---:|---|
| **B1 — CLI-Rahmen (Root)** (3) | `Program.cs` (92 LOC; `Main`, Response-File `LoadArgs`, Grammar-Dispatch, Analyze/Generate-Weiche), `CommandLine.cs` (113 LOC; `CodeGenerationOptions`-Flags-Enum, `CommandLine`-Record, `Parse`/`ShowHelp` über NDesk `OptionSet`), `GrammarCommand.cs` (76 LOC; `Run`/`ShowHelp`, Typ-Doku vorhanden) | 6 | **vergeben** |
| **B2 — Generator + Logging** (2) | `Generator/NavCodeGenerator.cs` (172 LOC; `Run`, `CollectFiles` /d + /s + `.navignore`, `CreatePipeline` + `ValidateOptions`, `WriteManifest`), `Logging/ConsoleLogger.cs` (83 LOC; `ILogger`-Konsolen-Impl, farbige Ausgabe, `protected virtual`-Hooks, `FormatDiagnostic`) | 16 | **vergeben** |
| **B3 — Analyzer** (4) | `Analyzer/SyntaxAnalyzerPipeline.cs` (68 LOC; `SyntaxAnalyzerPipeline`, `SyntaxNodeAnalyzer`, `CodeNotImplementedAnalyzer`), `Analyzer/CodeFixPipeline.cs` (70 LOC; Suggest→Checkout→Apply→Write), `Analyzer/SyntaxAnalyzerProgram.cs` (35 LOC), `Analyzer/CodeFixProgram.cs` (44 LOC; TFS-`Checkout`) | 8 | **vergeben** |

Die CS1591-Spalte summiert auf 30 (Baseline). B2/B3 sind trotz überschaubarer CS1591-Zahl inhaltlich
groß — der `internal`-Generator-/Analyzer-Ablauf, den CS1591 nicht misst.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst die Dateien eines Batches unter `Nav.Cli\` mit C#-XML-Doku.
> **Dateien dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine Umformatierung,
>   keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare/`#region`/Attribute anfassen.
> - Lies zuerst `doc/nav-cli-xmldoc-status.md`, Abschnitte 1–4 (Ziel, Glossar-Anschluss, Regeln,
>   Gates), und als Stil-Referenz `Nav.Language\CodeGen\Shared\CodeBuilder.cs`.
> - **Glossar `doc/Glossar.md` konsultieren** und dessen Kanon-Begriffe verwenden (Host, Engine,
>   Codegenerator, `.nav`-Datei, Task, taskref, Diagnostic, WFL/IWFL, CodeFix …).
> - Vor der Formulierung je Typ die **Verwendung** ansehen: Wer ruft die Klasse (`Program`,
>   `CustomBuild.targets`/`Pharmatechnik.Nav.Language.targets`, MSBuild-Build), welche Engine-Typen
>   fließen ein (`NavCodeGeneratorPipeline`, `GenerationOptions`, `Diagnostic`, `SyntaxNodeWalker`,
>   `FileSpec`, `NavIgnore`, `NavSolution`, `NavGrammar`, `ILogger` …). Dokumentiere die Rolle des Typs
>   **im CLI-Host** (nicht die Engine-Interna nachbeten).
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise — auch für Engine-/
>   Utilities-Typen (über ProjectReferences auflösbar).
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `internal`/`protected`/`private` hier **breit
>   mitdokumentieren** — nur triviale Durchreicher/Felder auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Nach den Edits BOM/CRLF wiederherstellen, falls das Tooling LF hinterlassen hat.
> - Danach Gates G1–G3 aus dem Status-Dokument **nur für deine Batch-Dateien** ausführen und die
>   Ausgabe in den Report aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der „offen"
> gelassenen Member mit Grund, **Glossar-Kandidaten** (neue Begriffe mit Kurzdefinition + Vorschlag
> Kanon-Schreibweise), Gate-Ergebnisse G1–G3.

## 7. Commit-Konvention

Pro Batch ein Commit, Muster:

```
Nav-Cli: XML-Doku für <Bereich> — nur ///-Zeilen, doku-only-Diff verifiziert
```

Die Glossar-Ergänzung ist ein eigener Commit:

```
Nav-Engine: Glossar um CLI-/Host-Begriffe (nav.exe) ergänzt
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-15 | — | Projektwahl `Nav.Cli` (Nutzer-Entscheid). Plan + Audit. Baseline (Gate G2) kalibriert: **30× CS1591** unter den 9 Handdoku-Dateien (ConsoleLogger 16, SyntaxAnalyzerPipeline 8, CommandLine 6; die übrigen 6 Dateien `internal`/`static` → 0), **0× CS1570–84**. Ausgeklammert: `..\Shared\Options.cs` (vendored NDesk.Options, 66 eigene CS1591), `..\Build\GobalAssemblyInfo.cs`, `obj\`. Scope: voller Ordner inkl. `internal`-Kern. 3 Batches (B1–B3) an Subagenten vergeben. |
| 2026-07-15 | B1–B3 | Subagenten vom Session-Limit vorzeitig abgebrochen (nur `CommandLine.cs` + Teil `NavCodeGenerator.cs` geschrieben). Orchestrator vervollständigte alle 9 Dateien direkt: dokumentiert v.a. der `internal`-Kern (Programm-Einstieg + Response-File, Generier-Pfad inkl. Datei-Discovery/Manifeste, `ConsoleLogger`-`public`-Fläche, beide Analyzer-Nebenpfade). Eigenverschuldete Trailing-WS-Reste (`CommandLine.cs`/`ConsoleLogger.cs`) byte-exakt zurückgesetzt; eingeschleppte 13× CS1574 (Sub-Namespace-crefs in `CommandLine.cs`) + 1× CS1587 (`///` auf lokaler Funktion) mitrepariert. |
| 2026-07-15 | Ende | Zentrale Verifikation: **G1 doku-only** über alle 9 Dateien byte-exakt OK; **G2** `--no-incremental`-Doku-Build → **0× CS1591** (Baseline 30→0), **0× CS1570–84**; **G3** BOM/CRLF sauber, kein `U+FFFD`. **Glossar** um **§9 CLI / Host `nav.exe`** ergänzt (11 Einträge + A–Z-Index). **Kampagne abgeschlossen** — Commit durch Nutzer (pro Batch + Glossar). |
