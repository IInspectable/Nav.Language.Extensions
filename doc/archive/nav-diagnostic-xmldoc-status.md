# Nav.Language/Diagnostic — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: FERTIG (2026-07-15).** Alle 3 Batches dokumentiert, Gates G1–G3 zentral grün
> (CS1591 unter `Diagnostic\` von 70 → **0**, CS157x **0**, Doku-only-Diff verifiziert). Noch
> uncommittet. Ziel: alle Dateien unter `Nav.Language\Diagnostic\`
> durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne jede Code-Änderung**.
> Vorgehen ist die Blaupause der vorangegangenen Kampagnen (`doc/archive/nav-text-xmldoc-status.md`,
> `doc/archive/nav-codefixes-xmldoc-status.md`).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-15)

- 13 Dateien unter `Nav.Language\Diagnostic\` (8 mit Warnungen, 5 bereits doku-warnungsfrei:
  `DiagnosticId.cs`, `DiagnosticDescriptors.{DeadCode,Semantic,Syntax}.cs` sowie in Teilen
  `DiagnosticDescriptors.cs`).
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-15):** **70 CS1591**-Warnungen (unique) →
  Ziel **0**. **Keine CS1570–CS1584-Vorbelastung (0 Treffer).**
  - Zähl-Falle: MSBuild listet jede Warnung doppelt → immer `sort -u` (die Rohzahl ~124 verwerfen).
- Kodierung: alle Dateien UTF-8 **mit** BOM, kein `U+FFFD`; Zeilenenden alle `w/crlf`.
- **Stil-Referenz:** `Nav.Language\Syntax\SyntaxTrivia.cs` + die fertigen `Text\`/`CodeFixes\`-Dateien.

### Fachlicher Kontext (für die Doku-Belege)

- **`Diagnostic`** (`Diagnostic.cs`) = eine einzelne, an einer `Location` verortete Diagnose-Instanz;
  hält den `DiagnosticDescriptor` und die Message-Argumente, aus denen die formatierte `Message`
  entsteht. Roslyn-Analogon `Microsoft.CodeAnalysis.Diagnostic`.
- **`DiagnosticDescriptor`** = die wiederverwendbare Beschreibung einer Diagnose-Art:
  `Id` (`NavXXXX`), `MessageFormat`, `Category`, `DefaultSeverity`. Roslyn-Analogon
  `Microsoft.CodeAnalysis.DiagnosticDescriptor`.
- **`DiagnosticDescriptors`** (+ partials `.Syntax`/`.Semantic`/`.DeadCode`) = der **Katalog** aller
  Nav-Diagnosen; **`DiagnosticId`** hält die `NavXXXX`-String-Konstanten. Die konkrete
  Bedeutung/der Text jedes Codes ist dort und in `doc/Errors.md` belegt.
- **`DiagnosticFormatter`** rendert eine `Diagnostic` zu Text (Dateipfad, Span, Kategorie+Code,
  Message); `UnitTestDiagnosticFormatter` ist die Test-Variante. Enums `DiagnosticCategory`,
  `DiagnosticSeverity` (+ `…Extension`/`DiagnosticExtensions`) klassifizieren/erweitern.
- **Belege:** die Erzeugungsstellen der Diagnosen (`SemanticAnalyzer\`, Parser/`Syntax\`), die
  Fehlercodes (`doc/Errors.md`), die Tests.

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert** — ausschließlich `///`-Zeilen (neu/korrigiert); Gate G1.
   **Keine Leerzeilen zwischen Membern einfügen/verschieben, keine Trailing-Spaces trimmen**
   (das Edit-Tool neigt dazu → G1-Bruch; nach Edits prüfen, ggf. per `perl` byte-genau restaurieren).
2. **Nur belegbare Aussagen** (Code, Erzeugungsstelle, `doc/Errors.md`, Tests). Unsicheres offen lassen.
3. **Typ-/Member-Verweise als `cref`** (Compiler prüft, Gate G2). **Ordner ≠ Namespace** und
   **bei geerbten Membern die deklarierende Schnittstelle** treffen (nicht die Basis raten).
   Scope: `public` Pflicht; `protected`/`internal`/`private` wo verhaltenstragend (v.a. die
   `protected virtual Format*`-Methoden); triviale Durchreicher auslassen. **Enum-/Konstanten-Member
   einzeln** dokumentieren.
4. **Deutsch, echte Umlaute, UTF-8 mit BOM, Zeilenenden unverändert (CRLF).** Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku.**
6. **Nichts committen.**

## 3. Arbeitsmodus

Orchestrator-Session hält den Plan; Doku-Arbeit **pro Batch in einem Subagenten** (Vorlage
Abschnitt 6). Batches bearbeiten **disjunkte Dateien** → laufen **parallel**; Gates fährt der
Orchestrator **einmal zentral** über den Ordner.

## 4. Verifikations-Gates (zentral, alle Pflicht)

**G1 — Doku-only-Diff** (Git Bash):

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/Diagnostic/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (**`--no-incremental`** Pflicht; `sort -u`):

```powershell
dotnet build Nav.Language\Nav.Language.csproj -c Debug `
  -p:GenerateDocumentationFile=true -p:WarningsAsErrors= --no-incremental 2>&1 > build.log
```

- **CS1570–CS1584**: Baseline **0** — jeder Treffer ist ein Fehler des Batches → beheben.
- **CS1591**: Baseline **70** (unique) → monoton auf **0**.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -iE "[\\/]Diagnostic[\\/]" | sort -u
grep -E "CS1591" build.log | grep -iE "[\\/]Diagnostic[\\/]" | sed 's/^[[:space:]]*//' | sort -u | wc -l
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM da, kein `U+FFFD`, EOL alle `w/crlf`.

```bash
find Nav.Language/Diagnostic -name '*.cs' | grep -v obj | while read -r f; do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD: $f"
done
git ls-files --eol 'Nav.Language/Diagnostic/*.cs' | grep -v 'w/crlf'   # erwartet: leer
```

**G4 — Build grün** (der G2-Aufruf genügt; am Ende zusätzlich `nav build` + `nav test`).

## 5. Batch-Plan & Status

| Batch | Inhalt | CS1591 | Status |
|---|---|---:|---|
| **B1 — Kern-Modell** (3) | `Diagnostic`, `DiagnosticDescriptor`, `DiagnosticDescriptors` (Katalog; `DiagnosticId` + `.Syntax`/`.Semantic`/`.DeadCode` als Kontext) | 29 | **fertig** |
| **B2 — Kategorie, Schweregrad & Extensions** (4) | `DiagnosticCategory`, `DiagnosticSeverity`, `DiagnosticSeverityExtension`, `DiagnosticExtensions` | 18 | **fertig** |
| **B3 — Formatter** (2) | `DiagnosticFormatter`, `UnitTestDiagnosticFormatter` | 15 | **fertig** |

`Diagnostic`/`DiagnosticDescriptor` (B1) sind die Doku-Autorität für die Typen, auf die B2/B3
per `cref` verweisen. `DiagnosticFormatter` ist die Basis; `UnitTestDiagnosticFormatter` nutzt
`<inheritdoc/>` an `override`-Membern.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\Diagnostic\` (Repo: D:\git\Nav.Language.Extensions)
> mit C#-XML-Doku. **Dateien dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen. Keine Umformatierung/using/Umsortieren/
>   `//`-Kommentare, **keine Leerzeilen zwischen Membern einfügen/verschieben, keine
>   Trailing-Spaces trimmen** (Edit-Tool neigt dazu → doku-only-Gate bricht; nach Edits per `perl`
>   byte-genau restaurieren, falls doch passiert).
> - Lies zuerst `doc/archive/nav-diagnostic-xmldoc-status.md`, Abschnitte 1 (fachlicher Kontext), 2 und 4,
>   und `Nav.Language\Syntax\SyntaxTrivia.cs` als Stil-Referenz.
> - Belege je Typ an der Erzeugungsstelle (`SemanticAnalyzer\`, Parser), an `doc/Errors.md` und den
>   Tests. Roslyn-Analogien (`Diagnostic`/`DiagnosticDescriptor`) dürfen tragen, wo sie zutreffen.
> - Deutsch, echte Umlaute, `<see cref="…"/>`. **Vor `cref` echten Namespace prüfen**
>   (Ordner ≠ Namespace); bei geerbten Membern die deklarierende Schnittstelle treffen.
> - Basis/abstrakte Member = Doku-Autorität; `<inheritdoc/>` an `override`-Membern wo identisch.
>   Enum-Werte / `NavXXXX`-Konstanten **einzeln** dokumentieren.
> - **Scope:** `public` Pflicht; `protected`/`internal`/`private` wo verhaltenstragend; triviale
>   Durchreicher auslassen. Unsicheres offen lassen und im Report melden.
> - Zeilenenden unverändert (CRLF), UTF-8 mit BOM. Nach den Edits Gates G1 + G3 ausführen (Git Bash),
>   Ausgabe in den Report. **Nicht selbst bauen** (G2/G4 zentral).
>
> **Report:** je Datei 1 Zeile (dokumentierte Member-Anzahl), „offen"-Liste mit Grund, G1 + G3.

## 7. Commit-Konvention

```
Nav-Engine: XML-Doku für Diagnostic/<Bereich> (Batch <n>/3) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-15 | — | Plan erstellt, Audit durchgeführt (5 von 13 Dateien bereits sauber); Gate G2 verifiziert (Baseline **70× CS1591 unique**, 0× CS157x unter `Diagnostic\`); Kodierung geprüft (überall BOM, kein `U+FFFD`, alle `w/crlf`) |
| 2026-07-15 | B1–B3 | Kampagne **abgeschlossen**: 4 angefangene Dateien (`Diagnostic`, `DiagnosticFormatter`; `DiagnosticCategory`/`DiagnosticSeverity` waren schon fertig) und alle unberührten (`DiagnosticDescriptor`, `DiagnosticDescriptors` + `.Syntax`/`.Semantic`/`.DeadCode`, `DiagnosticExtensions`, `DiagnosticSeverityExtension`, `UnitTestDiagnosticFormatter`) dokumentiert. Zentrale Gates: G1 doku-only ✔, G3 BOM/EOL ✔, G2 Build grün mit **CS1591 unter `Diagnostic\` = 0**, CS157x = 0. Falle wiedergesehen: Edit-Tool verschluckte eine Blindzeile mit 12 Trailing-Spaces in `DiagnosticFormatter.FormatSpan` → per `perl` byte-genau restauriert (G1 fing es). |
