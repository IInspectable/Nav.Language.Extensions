# Nav.Language/Feature-Ordner — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-15).** Abschließende Welle der XML-Doku-Kampagne über die
> Engine `Nav.Language`. Alle 7 Batches (B1–B7) fertig; die offenen Ordner sind doku-warnungsfrei
> (**0× CS1591**, **0× CS1570–84** unter allen offenen Ordnern), doku-only-Diff über alle 34
> geänderten Dateien mechanisch verifiziert (G1), BOM/CRLF sauber (G3), normaler Build 0
> Warnungen/0 Fehler. Ziel war: **alle noch nicht dokumentierten Ordner** durchgängig mit akkurater
> C#-XML-Dokumentation versehen — **ohne jede Code-Änderung**. Vorbild und Methodik:
> `doc/nav-codegen-xmldoc-status.md` und `doc/nav-syntax-xmldoc-status.md`.
>
> Damit ist die engine-weite XML-Doku-Kampagne über `Nav.Language` **vollständig** (alle Ordner
> außer generiertem `obj/`-Code und `Properties/AssemblyInfo.cs`).
>
> **Bereits abgeschlossen** (frühere Wellen, hier nicht mehr Gegenstand): `Syntax/`,
> `SemanticModel/`, `SemanticAnalyzer/`, `CodeFixes/`, `Text/`, `Diagnostic/`, `CodeGen/`,
> `Common/`, `FindReferences/`, `Provider/`.

## 1. Ziel & Ausgangslage (Audit vom 2026-07-15)

- **Scope: voller Ordner** (wie `Syntax/`/`CodeGen/`) — nicht nur die CS1591-messbare
  `public`-Surface. Viele Feature-Kerne haben eine schmale `public`-Fassade (Service-Klasse) über
  einem `internal`-Maschinenraum (Finder, Resolver, Builder, Regel-Objekte); genau dort sitzt die
  zu erklärende Logik, die CS1591 nicht misst.
- **Verbleibende Ordner/Dateien (Audit):**
  - `Generator/` (6), `Workspace/` (8), `Dependencies/` (4), `Formatting/` (16),
    `Completion/` (3), `QuickInfo/` (2), `GoTo/` (2), `CodeActions/` (2), `References/` (3),
    `Rename/` (1), `Symbols/` (1), `CallHierarchy/` (1), `Internal/` (4) sowie die Root-Datei
    `NavLanguageFeature.cs`.
  - **Nicht Gegenstand:** `Properties/AssemblyInfo.cs` (reine Assembly-Attribute, keine Doku-Fläche),
    `obj/` (generierter Code: `*.g.cs` aus Quellgeneratoren/T4 — kein Handdoku-Ziel),
    `NavLanguageVersion.cs` (bereits in der `Common/`-Welle dokumentiert — nur Lücken-Audit).
- **Messbare Ziellinie (Gate G2, Baseline verifiziert am 2026-07-15):**
  - **CS1591** (fehlende Doku an `public` Membern) unter den offenen Ordnern: **83**
    → Ziel **0**. Verteilung: `Generator/` 26, `Dependencies/` 20, `Workspace/` 19,
    `Completion/` 9, `CodeActions/` 4, `CallHierarchy/` 4, `QuickInfo/` 1. Die übrigen offenen
    Ordner (`Formatting/`, `References/`, `GoTo/`, `Rename/`, `Symbols/`, `Internal/`, Root) sind
    CS1591-frei — ihre `public`-Fassade ist schon dokumentiert, ihr `internal`-Kern jedoch nicht.
  - **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter den
    offenen Ordnern ist **5** (Altlasten in bereits teil-dokumentierten Dateien) → Ziel **0**.
    Diese sind vom jeweiligen Batch **mit zu reparieren**:
    - `CallHierarchy/NavCallHierarchyService.cs` (146/148/149): CS1573 — `GetExitUsagesAsync` hat
      keine `<param>`-Tags für `task`, `solution`, `cancellationToken`.
    - `Completion/NavCompletionContext.cs` (581): CS1574 — `cref="ChildTokens"` unauflösbar.
    - `Formatting/GapLayout.cs` (64): CS1573 — `NewLineAlignedColumn`-ctor ohne `<param>` für
      `BlankLinesBefore`.
- **CS1591 unterschätzt die reale Surface** (der `internal`-Kern zählt nicht). Ziellinie ist
  zweiteilig: **(a)** CS1591 unter den offenen Ordnern = 0 **und** **(b)** der `internal`-Kern
  durchgängig sinnvoll dokumentiert — geprüft per Stichproben-Review je Batch.
- **Stil-Referenz:** `Nav.Language\CodeGen\Shared\CodeBuilder.cs` und die frisch fertige
  `CodeGen/`-Doku (dichte, korrekte Bestands-Doku: deutsch mit echten Umlauten, `<see cref="…"/>`
  statt Klartext-Typnamen, Roslyn-Analogien wo tragfähig, `<param>`/`<returns>` an Methoden, knappe
  Ein-Zeilen-Summaries an trivialen Properties). Für `Formatting/` zusätzlich `doc/Formatter-DeepDive.md`
  als fachliche Referenz (nicht zitieren — die Doku beschreibt den Code, nicht das Dokument).

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder
   korrigiert). Mechanisch verifiziert durch Gate G1 (Abschnitt 4) — der Diff ohne
   `///`-Zeilen muss byte-identisch zu HEAD sein, inkl. Einrückung, Zeilenenden, BOM.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen (Host-Aufruf
   in LSP/MCP/Extension, Tests) oder dem Semantikmodell ableitbar sein. Bei Unsicherheit: Member
   **unkommentiert lassen** und im Batch-Report als „offen" melden — eine Lücke ist besser als
   falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `internal`/`protected`/`private` Member werden hier **breit mitdokumentiert** — der
   Feature-Kern ist bewusst `internal`. Triviale Durchreicher-Properties und offensichtliche Felder
   brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF.** Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku beschreibt den
   Code, nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, pro Batch, nach Review.
7. **Win-1252-Falle beachten** (CLAUDE.md): vor dem Bearbeiten einer Umlaut-Datei die Kodierung
   prüfen; bei Win-1252/`U+FFFD` erst `git checkout` + `nav fixenc`, dann Edits.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan und den Status; die eigentliche Doku-Arbeit läuft
**pro Batch in einem Subagenten** mit eigenem, frischem Kontext (Vorlage in Abschnitt 6). Die
Batches bearbeiten **disjunkte Ordner** und laufen daher gefahrlos parallel. Nach dem parallelen
Schreiben verifiziert der Orchestrator zentral (G1–G4 einmal über alle Änderungen) und liefert pro
Batch eine Commit-Message; **committen tut der Nutzer**.

## 4. Verifikations-Gates (alle Pflicht)

**G1 — Doku-only-Diff** (Git Bash): Der Diff darf ausschließlich aus `///`-Zeilen bestehen.
Byte-exakter Vergleich — schlägt das Gate flächig an, ist meist ein Zeilenenden-/BOM-Schaden die
Ursache; dann den Schaden beheben, **nie** das Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/**/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; **`--no-incremental`** ist
Pflicht, sonst verschluckt der inkrementelle Build die Warnungen):

```powershell
dotnet build Nav.Language\Nav.Language.csproj -c Debug `
  -p:GenerateDocumentationFile=true --no-incremental
```

Auswertung gegen die **Baseline vom 2026-07-15** (nur offene Ordner zählen):

- **CS1570–CS1584:** Baseline **5** (Abschnitt 1) → am Kampagnen-Ende **0**.
- **CS1591:** Baseline **83** → monoton sinkend, am Ende **0**.

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`, CRLF intakt.

```bash
for f in $(git diff --name-only -- 'Nav.Language/**/*.cs'); do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
```

**G4 — Build grün** (im Orchestrator): der G2-Aufruf genügt; am Kampagnen-Ende zusätzlich einmal
`nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

Reihenfolge/Schnitt ist ordner-diszipliniert (disjunkte Dateimengen → parallelisierbar).

| Batch | Ordner (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **B1 — Generator** (6) | `Generator/FileSpec`, `ILogger`, `NavCodeGeneratorPipeline`, `.LoggerAdapter`, `.RunResult`, `.Statistic` | 26 | **fertig** (2026-07-15) |
| **B2 — Workspace** (8) | `Workspace/NavSolution`, `NavWorkspaceCore`, `OverlaySyntaxProvider`, `IncludeDependencyGraph`, `DiagnosticsComputer`, `NavIgnore`, `NavIgnoreFile`, `NavIgnorePattern` | 19 | **fertig** (2026-07-15) |
| **B3 — Dependencies** (4) | `Dependencies/Dependency`, `DependencyItem`, `DependencyAnalyzer`, `DependencyExtensions` | 20 | **fertig** (2026-07-15) |
| **B4 — Formatting** (16) | `Formatting/` (Service, Optionen, Alignment-Map, Gap-Regeln/-Renderer, Enums, Facts) | 0 | **fertig** (2026-07-15) |
| **B5 — IntelliSense/Navigation** (9) | `Completion/` (3), `QuickInfo/` (2), `GoTo/` (2), `CodeActions/` (2) | 14 | **fertig** (2026-07-15) |
| **B6 — Referenzen/Symbole** (6) | `References/` (3), `Symbols/` (1), `CallHierarchy/` (1), `Rename/` (1) | 4 | **fertig** (2026-07-15) |
| **B7 — Internal + Root** (5) | `Internal/ExtentExtensions`, `NullableAttributes`, `SuppressCodeSanityCheckAttribute`, `SyntaxTokenFactory`, Root `NavLanguageFeature` (+ Lücken-Audit `NavLanguageVersion`) | 0 | **fertig** (2026-07-15) |

Die CS1591-Spalte summiert auf 83 (Baseline). Batches mit niedriger CS1591-Zahl (B4, B6, B7) sind
trotzdem inhaltlich relevant — es ist der undokumentierte `internal`-Kern, den CS1591 nicht misst.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst die Dateien eines Ordner-Batches unter `Nav.Language\` mit C#-XML-Doku.
> **Dateien dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine Umformatierung,
>   keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen.
> - Lies zuerst `doc/nav-features-xmldoc-status.md`, Abschnitte 1, 2 und 4 (Ziel + Regeln + Gates),
>   und als Stil-Referenz eine bereits fertige Datei, z.B. `Nav.Language\CodeGen\Shared\CodeBuilder.cs`.
> - Vor der Formulierung je Typ die **Verwendung** ansehen: Wer ruft die Service-/Kernklasse (LSP-/
>   MCP-/Extension-Host, andere Engine-Ordner), welche Tests fixieren das Verhalten, welches
>   Semantikmodell/welche Syntax fließt ein. Dokumentiere, welche Rolle der Typ im Feature spielt.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `internal`/`protected`/`private` hier **breit
>   mitdokumentieren** — nur triviale Durchreicher/Felder auslassen.
> - **Bestehende CS1570–84 in deinen Dateien mit reparieren** (siehe Abschnitt 1, falls dein Batch
>   betroffen ist): fehlende `<param>`-Tags ergänzen, unauflösbare `cref` korrigieren.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Vor dem Bearbeiten je Datei die Kodierung prüfen (Win-1252-Falle, CLAUDE.md); nach den Edits
>   BOM/CRLF wiederherstellen, falls das Tooling LF hinterlassen hat.
> - Danach Gates G1–G3 aus dem Status-Dokument **nur für deine Batch-Dateien** ausführen und die
>   Ausgabe in den Report aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der „offen"
> gelassenen Member mit Grund, Gate-Ergebnisse G1–G3.

## 7. Commit-Konvention

Pro Batch ein Commit, Muster:

```
Nav-Engine: XML-Doku für <Ordner> — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-15 | — | Plan erstellt, Audit durchgeführt. Baseline (Gate G2) kalibriert: 83× CS1591 + 5× CS1573/74 (Altlasten) unter den offenen Ordnern; `obj/` und `Properties/AssemblyInfo.cs` ausgeklammert. Scope: voller Ordner inkl. `internal`-Kern. 7 Batches (B1–B7) parallel an Subagenten vergeben. |
| 2026-07-15 | B1–B7 | Alle 7 Batches parallel per Subagent abgearbeitet. Neu dokumentiert v.a. der `internal`-Kern: B1 Generator (48 Member: Pipeline/LoggerAdapter/Statistic/FileSpec/ILogger), B2 Workspace (29: NavSolution/Overlay/NavIgnore*/Diagnostics), B3 Dependencies (24: Analyzer/Item/Extensions), B4 Formatting (14: AlignmentMapBuilder/FormatterSuppression/GapRenderer/-Trivia + CS1573-Fix `GapLayout`), B5 IntelliSense/Navigation (14: CompletionItem/HoverInfo/CodeAction + CS1574-Fix `NavCompletionContext`), B6 References/Symbols (25: HighlightSymbolFinder/ReferenceRootFinder + CS1573-Fix `NavCallHierarchyService`), B7 Internal/Root (9: SuppressCodeSanityCheck-Attribut + Lücken). |
| 2026-07-15 | Ende | Zentrale Verifikation: **G1 doku-only** über alle 34 Dateien byte-exakt OK (inkl. Root `NavLanguageVersion.cs`); **G2** `--no-incremental`-Doku-Build → **0× CS1591** in allen offenen Ordnern (Baseline 83→0), **0× CS1570–84** (die 5 Altlasten mitrepariert); **G3** BOM/CRLF sauber, kein `U+FFFD`; **G4** normaler Build 0 Warnungen/0 Fehler. **Kampagne abgeschlossen.** Nebenbefund (nicht Teil dieser Kampagne): `GoTo/`-Alt-Doku enthält vorbestehende ASCII-Ersatzschreibweisen (`aussen`/`gleichermassen`) — Kandidat für einen separaten Umlaut-Sweep. |
