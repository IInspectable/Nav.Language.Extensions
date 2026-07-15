# Nav.Language/CodeFixes — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-14).** Alle 4 Batches fertig, `CodeFixes\` ist
> doku-warnungsfrei (0× CS1591, 0× CS1570–CS1584); doku-only-Diff über 38 Dateien mechanisch
> verifiziert (830 Insertions, 1 Deletion — ausschließlich `///`-Zeilen), Build grün (0 Fehler).
> Ziel war: alle Dateien unter `Nav.Language\CodeFixes\` durchgängig mit akkurater C#-XML-Doku
> versehen — **ohne jede Code-Änderung**. Vorgehen war die Blaupause der vorangegangenen Kampagnen
> (`doc/nav-syntax-xmldoc-status.md`, `doc/nav-semanticmodel-xmldoc-status.md`,
> `doc/nav-text-xmldoc-status.md`).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-14)

- 38 Dateien unter `Nav.Language\CodeFixes\` (7 Wurzel, 9 `StyleFix\`, 9 `ErrorFix\`,
  13 `Refactoring\`). ~28 tragen Warnungen, der Rest ist bereits doku-warnungsfrei.
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-14):** **145 CS1591**-Warnungen
  (eindeutige Treffer; fehlende XML-Doku an öffentlichen Membern) unter `CodeFixes\` → Ziel ist
  **0**. **Keine CS1570–CS1584-Vorbelastung (0 Treffer).**
  - **Achtung Zähl-Falle:** MSBuild listet jede Warnung doppelt; erst `sort -u` liefert die echten
    145 (die ungefilterte Rohzählung suggeriert fälschlich ~290).
- Kodierungs-Lage: alle Dateien UTF-8 **mit** BOM, kein `U+FFFD`, keine Win-1252-Altlast.
  Zeilenenden: **alle `w/crlf`** (kein LF-Sonderfall).
- **Stil-Referenz bleibt `Nav.Language\Syntax\SyntaxTrivia.cs`** sowie die fertig dokumentierten
  `SemanticModel\`- und `Text\`-Dateien: deutsche Doku mit echten Umlauten, `<see cref="…"/>`
  statt Klartext-Typnamen, Roslyn-Analogien wo tragfähig (das Muster spiegelt Roslyns
  `CodeFixProvider`/`CodeAction`), `<param>`/`<returns>` an Methoden, knappe Ein-Zeilen-Summaries
  an trivialen Properties/Enum-Werten.

### Fachlicher Kontext (für die Doku-Belege)

- **`CodeFix`** (`CodeFixes\CodeFix.cs`) ist die abstrakte Basis aller Fixes: hält den
  `CodeFixContext`, deklariert abstrakt `Name`/`Impact`/`ApplicableTo`/`Prio`/`Category` und
  bietet `protected`-Helfer, die **`TextChange`-Sequenzen** erzeugen (`GetRemoveChanges`,
  `GetInsertChanges`, `GetRename*Changes`, `Compose*`) — d.h. die Fixes berechnen nur ein
  Edit-Set (`<see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>`), sie mutieren nichts
  selbst. Das ist die inhaltliche Brücke zur gerade dokumentierten `Text\`-Familie.
- Drei **Familien** mit je eigener Basisklasse, die von `CodeFix` erbt: `StyleFix\StyleCodeFix`
  (Stil/Aufräumen — ungenutzte Deklarationen/Knoten/Includes entfernen, Semikola ergänzen),
  `ErrorFix\ErrorCodeFix` (behebt Fehler-Diagnosen — fehlende Exit-Transition, Versions-Direktive),
  `Refactoring\RefactoringCodeFix` (Umgestaltung — Choice einführen, Umbenennen).
- Jeder Fix hat i.d.R. einen zugehörigen **`…CodeFixProvider`** (findet die anwendbaren Fixes zu
  einer Position/Diagnose — Roslyn-Analogon `CodeFixProvider`). **Provider und Fix gehören in
  denselben Batch.**
- **Belege** für Aussagen: die auslösenden Diagnosen (`Nav.Language\Diagnostic\`,
  `SemanticAnalyzer\`, Nav-Fehlercodes), der `CodeFixContext` (was ein Fix an Eingaben bekommt),
  die erzeugten `TextChange`s (`Text\`) und die Tests.

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder korrigiert).
   Mechanisch verifiziert durch Gate G1 — der Diff ohne `///`-Zeilen muss identisch zu HEAD sein.
   **Keine Leerzeilen zwischen Membern einfügen/verschieben** (häufiger Subagent-Fehler → G1-Bruch).
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen (Provider,
   auslösende Diagnose, erzeugte `TextChange`s) und den Tests ableitbar sein. Bei Unsicherheit:
   Member **unkommentiert lassen** und im Batch-Report als „offen" melden.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Ordner ≠ Namespace:** vor einem `cref` auf einen Nachbar-Typ den **tatsächlichen** Namespace
   prüfen (nicht vom Ordner ableiten — z.B. liegt `Location` unter `Common\`, aber im Namespace
   `Pharmatechnik.Nav.Language`). Sibling-Namespace-Verweise ggf. qualifizieren.
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `protected`/`internal`/`private` mitdokumentieren, wo der Member eine Invariante,
   Entwurfsentscheidung oder nicht offensichtliches Verhalten trägt (v.a. die `protected`-Helfer in
   `CodeFix`); triviale Felder/Durchreicher brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM.** Zeilenenden der Datei **unverändert**
   belassen (CRLF-Bestand bleibt CRLF). Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md).
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, nach Review.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan; die Doku-Arbeit läuft **pro Batch in einem
Subagenten** mit frischem Kontext (Vorlage in Abschnitt 6). Da die Batches **disjunkte
Unterordner** bearbeiten, laufen sie **parallel**; die Gates fährt der Orchestrator **einmal
zentral** über den ganzen Ordner (wie in der `Text\`-Kampagne bewährt).

## 4. Verifikations-Gates (zentral, alle Pflicht)

**G1 — Doku-only-Diff** (Git Bash): Der Diff darf ausschließlich aus `///`-Zeilen bestehen.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/CodeFixes/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; **`--no-incremental`** Pflicht;
Warnungen doppelt → immer `sort -u`):

```powershell
dotnet build Nav.Language\Nav.Language.csproj -c Debug `
  -p:GenerateDocumentationFile=true -p:WarningsAsErrors= --no-incremental 2>&1 > build.log
```

Auswertung gegen die **Baseline vom 2026-07-14** (nur `CodeFixes\`-Treffer):

- **CS1570–CS1584**: Baseline **0**. Jeder Treffer ist ein Fehler des laufenden Batches → beheben.
- **CS1591**: Baseline **145** (unique); muss monoton auf **0** sinken.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -iE "[\\/]CodeFixes[\\/]" | sort -u
grep -E "CS1591" build.log | grep -iE "[\\/]CodeFixes[\\/]" | sed 's/^[[:space:]]*//' | sort -u | wc -l
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`; EOL laut
`git ls-files --eol` unverändert (alle `w/crlf`).

```bash
find Nav.Language/CodeFixes -name '*.cs' | grep -v obj | while read -r f; do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD: $f"
done
git ls-files --eol 'Nav.Language/CodeFixes/*.cs' | grep -v 'w/crlf'   # erwartet: leer
```

**G4 — Build grün** (im Orchestrator, einmal): der G2-Aufruf genügt; am Kampagnen-Ende zusätzlich
`nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

| Batch | Inhalt | CS1591 | Status |
|---|---|---:|---|
| **B1 — Kern/Abstraktion** (7, Wurzel) | `CodeFix`, `CodeFixContext`, `CodeFixCategory`, `CodeFixImpact`, `CodeFixPrio`, `SyntaxTreeExtensions`, `TaskDefinitionSymbolExtensions` | 47 | **fertig** (2026-07-14) |
| **B2 — StyleFix** (9) | `StyleCodeFix` + `RemoveUnusedTaskDeclaration*`, `RemoveUnusedNodes*`, `RemoveUnusedIncludeDirective*`, `AddMissingSemicolonsOnIncludeDirectives*` (je Fix + Provider) | 38 | **fertig** (2026-07-14) |
| **B3 — ErrorFix** (9) | `ErrorCodeFix` + `AddMissingExitTransition*`, `MoveVersionDirectiveToTop*`, `SetValidLanguageVersion*`, `SetSupportedLanguageVersion*` (je Fix + Provider) | 38 | **fertig** (2026-07-14) |
| **B4 — Refactoring** (13) | `RefactoringCodeFix` + `IntroduceChoice*`, gesamte `Rename*`-Familie (Node-/Task-/Exit-/View-/Init-/Dialog-/Choice-/TaskDeclaration-Rename) + Provider | 22 | **fertig** (2026-07-14) |

Die abstrakte Basis `CodeFix` (B1) ist die Doku-Autorität für die geerbten/abstrakten Member; die
Familien-Basen (`StyleCodeFix`/`ErrorCodeFix`/`RefactoringCodeFix`) und die konkreten Fixes nutzen
`<inheritdoc/>` an den `override`-Membern, wo die Aussage identisch ist. Fix + zugehöriger Provider
liegen bewusst im **selben** Batch.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\CodeFixes\` (Repo: D:\git\Nav.Language.Extensions)
> mit C#-XML-Doku. **Dateien dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine
>   Umformatierung, keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen,
>   **keine Leerzeilen zwischen Membern einfügen/verschieben**.
> - Lies zuerst `doc/nav-codefixes-xmldoc-status.md`, Abschnitte 1 (fachlicher Kontext), 2 und 4,
>   und `Nav.Language\Syntax\SyntaxTrivia.cs` als Stil-Referenz.
> - Vor der Formulierung je Fix: die **auslösende Diagnose** (`Nav.Language\Diagnostic\`,
>   `SemanticAnalyzer\`), den zugehörigen **Provider** und die erzeugten **`TextChange`s** ansehen.
>   Dokumentiere, *welches Problem* der Fix behebt und *welche Edits* er erzeugt.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise. **Vor `cref` auf
>   Nachbar-Typen den echten Namespace prüfen** (Ordner ≠ Namespace).
> - Basisklasse/abstrakte Member sind die Doku-Autorität; an `override`-Membern `<inheritdoc/>`
>   nutzen, wo die Aussage identisch ist.
> - **Sichtbarkeits-Scope:** `public` Pflicht; `protected`/`internal`/`private` dort, wo der Member
>   Invariante/Entwurfsentscheidung/nicht-offensichtliches Verhalten trägt — triviale
>   Felder/Durchreicher auslassen. **Enum-Member einzeln** dokumentieren.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Zeilenenden unverändert (CRLF), UTF-8 mit BOM erhalten. Nach den Edits Gates G1 + G3 aus dem
>   Status-Dokument ausführen (Git Bash), Ausgabe in den Report. **Nicht selbst bauen** (G2/G4
>   macht der Orchestrator zentral).
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der „offen"
> gelassenen Member mit Grund, Gate-Ergebnisse G1 + G3.

## 7. Commit-Konvention

```
Nav-Engine: XML-Doku für CodeFixes/<Bereich> (Batch <n>/4) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-14 | — | Plan erstellt, Audit durchgeführt; Gate G2 verifiziert (Baseline: **145× CS1591 unique** — Roh-Doppelzählung ~290 verworfen; 0× CS157x unter `CodeFixes\`); Kodierung geprüft (überall BOM, kein `U+FFFD`, alle `w/crlf`) |
| 2026-07-14 | B1 | 7 Dateien, 59 Member; `CodeFix`-Basis inkl. der 12 `protected` Edit-Helfer (je erzeugtes `TextChange`-Edit-Set), `CodeFixContext`, alle 3 Enums Wert für Wert; **Beifund (Code-Smell, nicht behoben):** `CodeFixContext.FindSymbols(bool)`/`FindSymbols<T>(bool)` werten den Parameter `includeOverlapping` nicht aus → bewusst ohne `<param>` dokumentiert, Kandidat fürs Code-Review |
| 2026-07-14 | B2 | 9 Dateien (StyleFix), 45 Member; Fix+Provider-Paare, `<inheritdoc/>` an geerbten Membern; cref-Falle vermieden (`NodeSymbolExtension`, nicht `…Extensions`); ein versehentlicher Whitespace-Edit byte-genau restauriert |
| 2026-07-14 | B3 | 9 Dateien (ErrorFix), ~42 Member; Diagnose-Bezüge belegt (Nav0025 via `GetUnconnectedExits`; Nav3002/3003/3004/5001 für die `#version`-Fixes; Ziel `NavLanguageVersion.Latest`); Overrides via `<inheritdoc/>` |
| 2026-07-14 | B4 | 13 Dateien (Refactoring — vollständige Rename-Familie, größer als Baseline vermuten ließ), ~73 Member; Alignment/Leerzeilen-Fehler selbst erkannt und korrigiert |
| 2026-07-14 | Gates | Zentral über ganz `CodeFixes\`: G1 doku-only-Diff grün (830 Ins/1 Del, nur `///`); G3 BOM/EOL grün. **Erster G2-Build:** CS1591 145→1, **13× CS1574 durch B4** (`cref="INodeSymbol.Outgoings/Incomings"` — die Member sind auf `ISourceNodeSymbol`/`ITargetNodeSymbol` deklariert, nicht auf `INodeSymbol`) + 1 offener `ErrorCodeFix`-ctor. Korrigiert: 12 crefs uniform auf die deklarierenden Interfaces, ctor-Doku ergänzt. **Zweiter G2-Build: CS1591 = 0, CS157x = 0, 0 Fehler.** Stichprobe verifiziert: `AddMissingExitTransitionCodeFixProvider` iteriert `GetUnconnectedExits()`; `SetSupportedLanguageVersionCodeFix` erzeugt `TextChange.NewReplace(…, NavLanguageVersion.Latest)`. **Kampagne abgeschlossen** (Schluss-`nav build`/`nav test` steht dem Nutzer nach Review frei). |
