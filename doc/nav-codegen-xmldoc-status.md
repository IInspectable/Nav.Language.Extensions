# Nav.Language/CodeGen — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: IN ARBEIT (Start 2026-07-15).** Ziel: alle Dateien unter `Nav.Language\CodeGen\`
> durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne jede Code-Änderung**.
> Vorbild und Methodik: `doc/nav-syntax-xmldoc-status.md` (abgeschlossene Schwester-Kampagne).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-15)

- **60 Dateien, ~5.400 Zeilen** unter `Nav.Language\CodeGen\`.
- **Scope-Entscheidung des Nutzers: voller Ordner** (wie `Syntax\`) — nicht nur die
  CS1591-messbare `public`-Surface. Der CodeGen-Kern ist **überwiegend `internal`**
  (`internal sealed class …CodeModel`, Emitter, Builder), d.h. CS1591 sieht ihn nicht; die
  eigentliche Generator-Maschinerie (v.a. `V1\CodeModel\`) liegt sonst weiter im Dunkeln.
- **Drei Zonen (Audit):**
  - **V2** (`V2\…`) ist bereits weitgehend dokumentiert (Ertrag der V2-Design-Arbeit:
    `CallContextCodeModel` 114 `///`, Emitter/Builder gepflegt) — nur Lücken-Audit nötig.
  - **V1\CodeModel\** ist der große dunkle Block: **17 von 18 Dateien mit 0 Doku-Zeilen**
    (`CodeModel`, `CodeModelBuilder`, `CallCodeModel`, `WfsCodeModel`, `Transition*CodeModel`
    …), fast alle `internal sealed` → CS1591-unsichtbar, aber Herz des V1-Generators.
  - **Geteilte Schichten** (`Shared\Facts\`, `Shared\CodeInfo\`, Pipeline-Rahmen): teils
    dokumentiert (`CodeGenInvariants`, `CodeBuilder`), teils Lücken.
- **Messbare Ziellinie (Gate G2, verifiziert am 2026-07-15):**
  - **CS1591** (fehlende Doku an `public` Membern): Baseline unter `CodeGen\` ist **204**
    → Ziel **0**. Verteilung: `V1\CodeGenFacts.cs` 62, `Shared\CodeInfo\TaskCodeInfo.cs` 24,
    `FileGenerator.cs` 18, `Shared\CodeInfo\SignalTriggerCodeInfo.cs` 14,
    `Shared\CodeInfo\TaskDeclarationCodeInfo.cs` 12, `CodeGenerator.cs` 12,
    `Shared\CodeInfo\TaskInitCodeInfo.cs` 10, `FileGeneratorResult.cs` 10, `Shared\Generator.cs` 8,
    `Shared\CodeInfo\TaskExitCodeInfo.cs` 8, `V2\CodeGeneratorV2.cs` 6, `V1\CodeGeneratorV1.cs` 6,
    `Shared\CodeInfo\ChoiceCodeInfo.cs` 6, `FileGeneratorAction.cs` 6, `V1\CodeModel\CodeModel.cs` 2.
  - **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter
    `CodeGen\` ist **0**. Jeder neue Treffer ist ein Fehler des laufenden Batches.
- **CS1591 unterschätzt die reale Surface** (der `internal`-Kern zählt nicht). Deshalb ist die
  Ziellinie zweiteilig: **(a)** CS1591 unter `CodeGen\` = 0 **und** **(b)** der `internal`-Kern
  (v.a. `V1\CodeModel\`) durchgängig sinnvoll dokumentiert — geprüft per Stichproben-Review je Batch.
- **Stil-Referenz:** `Nav.Language\CodeGen\Shared\CodeBuilder.cs` und
  `Nav.Language\CodeGen\V2\CodeModel\CallContextCodeModel.cs` (dichte, korrekte Bestands-Doku:
  deutsche Doku mit echten Umlauten, `<see cref="…"/>` statt Klartext-Typnamen, Roslyn-Analogien
  wo tragfähig, `<param>`/`<returns>` an Methoden, knappe Ein-Zeilen-Summaries an trivialen Properties).

## 2. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder
   korrigiert). Mechanisch verifiziert durch Gate G1 (Abschnitt 4) — der Diff ohne
   `///`-Zeilen muss byte-identisch zu HEAD sein, inkl. Einrückung, Zeilenenden, BOM.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen
   (Emitter-Aufruf, StringTemplate, Semantikmodell, Tests, Regression-Snapshots) oder dem
   erzeugten C# ableitbar sein. Bei Unsicherheit: Member **unkommentiert lassen** und im
   Batch-Report als „offen" melden — eine Lücke ist besser als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591
   messbar). `internal`/`protected`/`private` Member werden hier **breit mitdokumentiert** —
   der CodeGen-Kern ist bewusst `internal`, und genau dort sitzt die zu erklärende Maschinerie
   (CodeModel-Aufbau, Emitter-Verträge, Fakten-Schicht, Version-Dispatch). Triviale
   Durchreicher-Properties und offensichtliche Felder brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF.** Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku
   beschreibt den Code, nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, pro Batch, nach Review.
7. **Win-1252-Falle beachten** (CLAUDE.md): vor dem Bearbeiten einer Umlaut-Datei die Kodierung
   prüfen; bei Win-1252/`U+FFFD` erst `git checkout` + `nav fixenc`, dann Edits.

## 3. Arbeitsmodus (Session-Ökonomie)

Eine **Orchestrator-Session** hält nur diesen Plan und den Status; die eigentliche Doku-Arbeit
läuft **pro Batch in einem Subagenten** mit eigenem, frischem Kontext (Vorlage in Abschnitt 6).
So liest die Haupt-Session nie die Dateiinhalte selbst und läuft nicht voll.

Ablauf je Batch:

1. Orchestrator startet den Subagenten mit der Auftrags-Vorlage (Abschnitt 6) + Dateiliste.
2. Subagent liest Dateien **und ihre Verwendungen**, schreibt die Doku, führt G1–G3 selbst aus.
3. Orchestrator verifiziert unabhängig: G1–G3 erneut ausführen, G4 (Build) einmal,
   dazu Stichproben-Review von 2–3 Dateien des Batches.
4. Orchestrator liefert Commit-Message-Vorschlag; **Nutzer committet**.
5. Status-Tabelle (Abschnitt 5) fortschreiben.

Nach einem Commit ist jeder Batch ein sauberer Wiederaufsetzpunkt.

## 4. Verifikations-Gates (pro Batch, alle Pflicht)

**G1 — Doku-only-Diff** (Herzstück, Git Bash): Der Diff darf ausschließlich aus
`///`-Zeilen bestehen. Byte-exakter Vergleich — schlägt das Gate flächig an, ist meist
ein Zeilenenden-/BOM-Schaden die Ursache; dann den Schaden beheben, **nie** das Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language/CodeGen/**/*.cs'); do
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

Auswertung gegen die **Baseline vom 2026-07-15** (nur `CodeGen\`-Treffer zählen):

- **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter
  `CodeGen\` ist **0**. Jeder neue Treffer ist ein Fehler des laufenden Batches → sofort beheben.
- **CS1591** (fehlende Doku an `public`): Baseline unter `CodeGen\` ist **204**; die Zahl muss
  mit jedem Batch monoton sinken und am Kampagnen-Ende **0** sein.

```bash
grep -E "CS15(7[0-9]|8[0-4])" build.log | grep -E "[\\\\/]CodeGen[\\\\/]" | sort -u
grep -E "CS1591" build.log | grep -cE "[\\\\/]CodeGen[\\\\/]"
```

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`, CRLF intakt.
Vor dem Bearbeiten außerdem die Win-1252-Falle prüfen (`iconv -f UTF-8 -t UTF-8 <file>`,
siehe CLAUDE.md); Edit/Write kann LF hinterlassen → dann CRLF+BOM wiederherstellen.

```bash
for f in $(find Nav.Language/CodeGen -name '*.cs'); do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
```

**G4 — Build grün** (im Orchestrator, einmal pro Batch): der G2-Aufruf genügt; am
Kampagnen-Ende zusätzlich einmal `nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

Reihenfolge ist bewusst **blatt-zuerst**: erst die Fakten-/Namens-/Datenschichten, auf die
Generatoren und Emitter per `cref` verweisen, dann die konsumierenden Schichten — so existieren
`cref`-Ziele samt Doku, wenn übergeordnete Klassen auf sie zeigen.

| Batch | Inhalt (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **B1 — Fakten-Schicht** (4) | `Shared/Facts/CodeGenInvariants`, `Shared/Facts/ICodeGenFacts`, `Shared/Facts/NavCodeGenFacts`, `V1/CodeGenFacts` | 62 | **fertig** (2026-07-15) |
| **B2 — CodeInfo (Namens-/Pfadschicht)** (6) | `Shared/CodeInfo/ChoiceCodeInfo`, `SignalTriggerCodeInfo`, `TaskCodeInfo`, `TaskDeclarationCodeInfo`, `TaskExitCodeInfo`, `TaskInitCodeInfo` | 74 | offen |
| **B3 — Shared-Infrastruktur** (5) | `Shared/CSharp`, `Shared/CodeBuilder`, `Shared/CodeGeneratorContext`, `Shared/Generator`, `Shared/Resilience` | 8 | offen |
| **B4 — Pipeline & Ergebnistypen** (9) | `CodeGenerator`, `VersionDispatchingCodeGenerator`, `CodeGenerationResult`, `CodeGenerationSpec`, `OverwritePolicy`, `GenerationOptions`, `FileGenerator`, `FileGeneratorAction`, `FileGeneratorResult` | 46 | offen |
| **B5 — V1 CodeModel: Basis & Struktur** (10) | `V1/CodeModel/CodeModel`, `CodeModelBuilder`, `FileGenerationCodeModel`, `WfsBaseCodeModel`, `WfsCodeModel`, `IWfsCodeModel`, `IBeginWfsCodeModel`, `BeginWrapperCodeModel`, `FieldCodeModel`, `V1/CodeModelResult` | 2 | offen |
| **B6 — V1 CodeModel: Transitionen & Calls** (9) | `V1/CodeModel/TransitionCodeModel`, `InitTransitionCodeModel`, `ExitTransitionCodeModel`, `TriggerTransitionCodeMode`, `CallCodeModel`, `CallCodeModelBuilder`, `ParameterCodeModel`, `TaskBeginCodeModel`, `TOCodeModel` | 0 | offen |
| **B7 — V1 Emitters + Generator** (7) | `V1/Emitters/EmitterCommon`, `IBeginWfsEmitter`, `IWfsEmitter`, `TOEmitter`, `WfsBaseEmitter`, `WfsOneShotEmitter`, `V1/CodeGeneratorV1` | 6 | offen |
| **B8 — V2 (Lücken-Audit)** (9) | `V2/CodeGeneratorV2`, `V2/CodeModel/CallContextCodeModel`, `ChoiceCallContextCodeModel`, `TransitionCallContextCodeModel`, `WfsBaseCodeModelV2`, `WfsCodeModelV2`, `CodeModelBuilderV2`, `V2/Emitters/WfsBaseEmitterV2`, `WfsOneShotEmitterV2` | 6 | offen |

Die CS1591-Spalte je Batch summiert auf 204 (Baseline). Batches mit niedriger CS1591-Zahl (B5,
B6) sind trotzdem inhaltlich groß — es ist der undokumentierte `internal`-Kern, den CS1591 nicht misst.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst Dateien unter `Nav.Language\CodeGen\` mit C#-XML-Doku. **Dateien dieses
> Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine
>   Umformatierung, keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare anfassen.
> - Lies zuerst `doc/nav-codegen-xmldoc-status.md`, Abschnitte 2 und 4 (Regeln + Gates), und
>   die Stil-Referenzen `Nav.Language\CodeGen\Shared\CodeBuilder.cs` +
>   `Nav.Language\CodeGen\V2\CodeModel\CallContextCodeModel.cs`.
> - Vor der Formulierung je Typ: die **Verwendung** ansehen — wer baut das CodeModel
>   (`CodeModelBuilder`/`CodeModelBuilderV2`), welcher Emitter/StringTemplate konsumiert es,
>   welches C# entsteht (Regression-Snapshots unter `Nav.Language.Tests`). Dokumentiere, welchen
>   Ausschnitt des erzeugten C#-Codes der Typ trägt.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `internal`/`protected`/`private` hier **breit
>   mitdokumentieren** (der CodeGen-Kern ist bewusst `internal`) — nur triviale Durchreicher/Felder auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Vor dem Bearbeiten je Datei die Kodierung prüfen (Win-1252-Falle, CLAUDE.md); nach den
>   Edits BOM/CRLF wiederherstellen, falls das Tooling LF hinterlassen hat.
> - Danach Gates G1–G3 aus dem Status-Dokument ausführen und die Ausgabe in den Report aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der
> „offen" gelassenen Member mit Grund, Gate-Ergebnisse G1–G3.

## 7. Commit-Konvention

Pro Batch ein Commit, Muster:

```
Nav-Engine: XML-Doku für CodeGen/<Bereich> (Batch <n>/8) — nur ///-Zeilen, doku-only-Diff verifiziert
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-15 | — | Plan erstellt, Audit durchgeführt (60 Dateien; V2 weitgehend fertig, V1\CodeModel\ fast komplett undokumentiert). Gate G2 verifiziert und kalibriert (Baseline: 204× CS1591, 0× CS1570–84 unter `CodeGen\`). Scope-Entscheidung des Nutzers: voller Ordner inkl. `internal`-Kern. |
| 2026-07-15 | B1 | Nur `V1\CodeGenFacts.cs` geändert (32 Member: 31 `const` + `BuildQualifiedName`); die 3 `Shared\Facts\`-Dateien waren bereits vollständig dokumentiert. G1–G4 grün, CS1591 unter `CodeGen\` 204→142 (−62, exakt B1-Anteil), keine neuen CS1570–84. Aussagen am erzeugten C# (`{Task}WFS`/Annotation-Tags) + `CodeGenInvariants`/`ICodeGenFacts` belegt. Notiert: `UnknownNamespace`-Sentinel hat keinen aktiven Konsumenten (nur wertpinnender Test) — Doku wert-belegt. |
