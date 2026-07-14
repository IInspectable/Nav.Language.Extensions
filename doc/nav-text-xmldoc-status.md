# Nav.Language/Text — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-14).** Alle 3 Batches fertig, `Text\` ist doku-warnungsfrei
> (0× CS1591, 0× CS1570–CS1584); doku-only-Diff über 15 geänderte von 20 Dateien mechanisch
> verifiziert (501 Insertions, 75 Deletions — ausschließlich `///`-Zeilen, die 75 Deletions sind
> auf Deutsch umgestellte englische Alt-Summaries), Build grün (0 Fehler). Ziel war: alle Dateien
> unter `Nav.Language\Text\` durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne
> jede Code-Änderung**. Vorgehen war die Blaupause der vorangegangenen Kampagnen
> (`doc/nav-syntax-xmldoc-status.md`, `doc/nav-semanticanalyzer-xmldoc-status.md`,
> `doc/nav-semanticmodel-xmldoc-status.md`).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-14)

- 20 Dateien unter `Nav.Language\Text\`. **5 sind bereits doku-warnungsfrei**
  (`SourceText`, `SourceTextExtensions`, `SourceTextLine`, `DisplayPartsBuilder`,
  `StringSourceText`) — nur die restlichen 15 tragen Warnungen.
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-14):** **234 CS1591**-Warnungen
  (eindeutige Treffer; fehlende XML-Doku an öffentlichen Membern) unter `Text\` → Ziel ist **0**.
  **Keine CS1570–CS1584-Vorbelastung (0 Treffer)** — wie zuletzt in `SemanticModel\`.
- Kodierungs-Lage: alle 20 Dateien UTF-8 **mit** BOM, kein `U+FFFD`, keine Win-1252-Altlast.
  Zeilenenden: **alle `w/crlf`** (kein LF-Sonderfall in diesem Ordner).
- **Stil-Referenz bleibt `Nav.Language\Syntax\SyntaxTrivia.cs`** sowie die fertig dokumentierten
  `SemanticModel\`-Dateien: deutsche Doku mit echten Umlauten, `<see cref="…"/>` statt
  Klartext-Typnamen, Roslyn-Analogien wo tragfähig (viele Text-Primitive spiegeln
  `Microsoft.CodeAnalysis.Text`), `<param>`/`<returns>` an Methoden, knappe Ein-Zeilen-Summaries
  an trivialen Properties.
- **Belege für Aussagen** liegen hier in den Konsumenten der Text-Primitive: `Syntax\`
  (Positionen/Extents an Tokens & Knoten), `Formatting\` (`TextChange`/`TextChangeWriter`),
  `QuickInfo\`/`Completion\` (`ClassifiedText`/`DisplayPartsBuilder`), sowie in den Tests.

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder korrigiert).
   Mechanisch verifiziert durch Gate G1 — der Diff ohne `///`-Zeilen muss identisch zu HEAD sein.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen und den
   Tests ableitbar sein. Bei Unsicherheit: Member **unkommentiert lassen** und im Batch-Report
   als „offen" melden — eine Lücke ist besser als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `protected`/`internal`/`private` Member werden **mitdokumentiert, wo es Sinn ergibt**
   — überall dort, wo der Member eine Invariante, eine Entwurfsentscheidung oder nicht
   offensichtliches Verhalten trägt; triviale private Felder/Durchreicher brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM.** Zeilenenden der Datei **unverändert
   belassen** (CRLF-Bestand bleibt CRLF). Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku beschreibt
   den Code, nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, nach Review.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan und den Status; die eigentliche Doku-Arbeit
läuft **pro Batch in einem Subagenten** mit eigenem, frischem Kontext (Vorlage in Abschnitt 6).

Ablauf je Batch:

1. Orchestrator startet den Subagenten mit der Auftrags-Vorlage (Abschnitt 6) + Dateiliste.
2. Subagent liest Dateien **und ihre Verwendungen**, schreibt die Doku, führt G1 + G3 selbst aus.
3. Orchestrator verifiziert unabhängig: G1–G3 erneut ausführen, G4 (Build) einmal, dazu
   Stichproben-Review von 2–3 Dateien des Batches inkl. Nachschlagen der stärksten Behauptung.
4. Status-Tabelle (Abschnitt 5) fortschreiben.

## 4. Verifikations-Gates (pro Batch, alle Pflicht)

**G1 — Doku-only-Diff** (Herzstück, Git Bash): Der Diff darf ausschließlich aus `///`-Zeilen
bestehen. Die Git-Bash-`grep` liest textmodus-bedingt CR-tolerant, daher ist der Vergleich
EOL-unempfindlich; schlägt das Gate an, ist es eine echte Code-Änderung → beheben, **nie** das
Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/Text/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; ändert keine Datei;
**`--no-incremental`** ist Pflicht, sonst verschluckt der inkrementelle Build die Warnungen —
und MSBuild listet Warnungen doppelt, daher immer `sort -u` über die bereinigten Zeilen):

```powershell
dotnet build Nav.Language\Nav.Language.csproj -c Debug `
  -p:GenerateDocumentationFile=true -p:WarningsAsErrors= --no-incremental 2>&1 > build.log
```

Auswertung gegen die **Baseline vom 2026-07-14** (nur `Text\`-Treffer zählen, eindeutig gemacht):

- **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter `Text\`
  ist **0**. Jeder Treffer ist ein Fehler des laufenden Batches → sofort beheben.
- **CS1591** (fehlende Doku): Baseline unter `Text\` ist **234** (unique); die Zahl muss mit jedem
  Batch monoton sinken und am Kampagnen-Ende **0** sein.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -iE "[\\/]Text[\\/]" | sort -u
grep -E "CS1591" build.log | grep -iE "[\\/]Text[\\/]" | sed 's/^[[:space:]]*//' | sort -u | wc -l
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`; EOL-Bild laut
`git ls-files --eol` unverändert (alle `w/crlf`). Edit/Write kann LF hinterlassen → dann CRLF
(+BOM) wiederherstellen.

```bash
for f in Nav.Language/Text/*.cs; do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
git ls-files --eol Nav.Language/Text/ | grep -v 'w/crlf'   # erwartet: leer
```

**G4 — Build grün** (im Orchestrator, einmal pro Batch): der G2-Aufruf genügt; am Kampagnen-Ende
zusätzlich einmal `nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

| Batch | Inhalt (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **B1 — Geometrie-Primitive** (4) | `IExtent`, `TextExtent`, `LinePosition`, `LineRange` | 50 | **fertig** (2026-07-14) |
| **B2 — Klassifizierter Text & Anzeige** (5) | `ClassifiedText`, `ClassifiedTexts`, `ClassifiedTextExtensions`, `SyntaxTokenClassification`, `SymbolExtensions` | 96 | **fertig** (2026-07-14) |
| **B3 — Textänderung, Zeilen- & String-Helfer** (6) | `TextChange`, `TextChangeWriter`, `TextEditorSettings`, `StringExtensions`, `SourceTextLineList`, `SourceTextLineExtensions` | 88 | **fertig** (2026-07-14) |

Reihenfolge B1→B3 ist bewusst: erst die reinen Geometrie-Primitive (Position/Extent), auf die
die späteren Batches per `cref` verweisen (z.B. `TextChange` trägt eine `TextExtent`), dann die
Klassifizierungs-/Anzeige-Familie, zuletzt Textänderung und die verstreuten Helfer. Interface +
Implementierung (`IExtent`/`TextExtent`) liegen im **selben** Batch (Doku-Autorität am Interface;
Implementierung verweist per `<inheritdoc/>`, wo C# das trägt).

Bereits doku-warnungsfrei und **nicht** Teil eines Batches: `SourceText`, `SourceTextExtensions`,
`SourceTextLine`, `DisplayPartsBuilder`, `StringSourceText` (nur bei G-Läufen mitgeprüft).

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\Text\` mit C#-XML-Doku. **Dateien dieses Batches:**
> `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine
>   Umformatierung, keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen.
> - Lies zuerst `doc/nav-text-xmldoc-status.md`, Abschnitte 2 und 4 (Regeln + Gates), und
>   `Nav.Language\Syntax\SyntaxTrivia.cs` als Stil-Referenz.
> - Vor der Formulierung je Typ: die **Verwendungen** ansehen (wo wird das Primitiv erzeugt und
>   konsumiert — `Syntax\` für Positionen/Extents, `Formatting\` für `TextChange`,
>   `QuickInfo\`/`Completion\` für `ClassifiedText`/Display-Parts, Tests). Viele Typen spiegeln
>   Roslyns `Microsoft.CodeAnalysis.Text` — die Analogie darf die Doku tragen, aber nur wo sie
>   wirklich zutrifft.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - Interface ist die Doku-Autorität; an Implementierungs-Membern `<inheritdoc/>` nutzen, wo die
>   Aussage identisch ist — eigenen Text nur, wo die Implementierung Zusätzliches trägt.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `protected`/`internal`/`private` überall dort
>   mitdokumentieren, wo der Member eine Invariante, Entwurfsentscheidung oder nicht
>   offensichtliches Verhalten trägt — triviale Felder/Durchreicher auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Zeilenenden der Datei unverändert belassen (CRLF-Bestand); nach den Edits BOM prüfen und
>   Gates G1 + G3 aus dem Status-Dokument ausführen, Ausgabe in den Report aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der „offen"
> gelassenen Member mit Grund, Gate-Ergebnisse G1 + G3.

## 7. Commit-Konvention

Pro Batch ein Commit (oder ein Sammel-Commit am Ende — Entscheidung des Nutzers), Muster:

```
Nav-Engine: XML-Doku für Text/<Bereich> (Batch <n>/3) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-14 | — | Plan erstellt, Audit durchgeführt (5 von 20 Dateien bereits doku-warnungsfrei); Gate G2 verifiziert (Baseline: 234× CS1591 unique, 0× CS157x unter `Text\`); Kodierung geprüft (überall BOM, kein `U+FFFD`, alle `w/crlf`) |
| 2026-07-14 | B1 | 4 Dateien (`IExtent`, `TextExtent`, `LinePosition`, `LineRange`), 48 Member; englische Alt-Summaries auf Deutsch umgestellt; halboffene Intervall-Semantik (Start inklusiv/End exklusiv, `End => Start + Length`) und 0-Basierung belegt; `IExtent` als Doku-Autorität, `TextExtent.Start`/`End` per `<inheritdoc/>` |
| 2026-07-14 | B2 | 5 Dateien (`ClassifiedText`, `ClassifiedTexts`, `ClassifiedTextExtensions`, `SyntaxTokenClassification`, `SymbolExtensions`), 48 Member inkl. aller 21 `TextClassification`-Enum-Werte einzeln; keine offenen Punkte; Notiz: `TextClassification.FormName` hat im Engine-Code keinen Producer (nur im LSP-`SemanticTokensBuilder` als Map-Key gelesen) — faktentreu minimal dokumentiert |
| 2026-07-14 | B3 | 6 Dateien (`TextChange`, `TextChangeWriter`, `TextEditorSettings`, `StringExtensions`, `SourceTextLineList`, `SourceTextLineExtensions`), 45 Member; `TextChange`-Semantik (Extent+Ersatztext = Einfügen/Löschen/Ersetzen) an `TextChangeWriter`/`NavFormattingService` belegt; sibling-Namespace-`cref` auf `Formatting.NavFormattingService` qualifiziert (unqualifiziert → CS1574) |
| 2026-07-14 | Gates | Zentral über ganz `Text\`: G1 doku-only-Diff grün (501 Ins/75 Del, nur `///`); G3 BOM/EOL grün (alle `w/crlf`, kein `U+FFFD`); G2/G4-Build **0 Fehler, CS1591 unter `Text\` = 0**. Ein vom Batch eingeführter CS1574 (`cref="…Common.Location"` — `Location` liegt in `Pharmatechnik.Nav.Language`, nicht `.Common`) korrigiert → CS157x wieder 0. Stichprobe verifiziert: `TextExtent.End => Start + Length` + `Contains` halboffen; `TextChangeWriter` sortiert nach `Extent.Start`, prüft Overlap, wendet mit `offset += ReplacementText.Length - Extent.Length` an. **Kampagne abgeschlossen** (Schluss-`nav build`/`nav test` steht dem Nutzer nach Review frei). |
