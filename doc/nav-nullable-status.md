# Nav-Nullable — Status & Handoff

Stand-Dokument für die **Nullable-Reference-Types-Kampagne** in `Nav.Language` (netstandard2.0,
Assembly `Pharmatechnik.Nav.Language`). Der Engine-Kern wird **ordnerweise** auf NRT umgestellt —
bewusst **pro Datei** per `#nullable enable` (erste Codezeile, BOM davor), **ohne** projektweites
`<Nullable>enable</Nullable>`. Ziel: **messerscharfe** Annotationen — keine falschen not-null-Zusagen
(latente `NullReferenceException`), aber auch keine unnötigen `?`/Null-Checks, wo null de facto nie
auftritt.

> Dieses Dokument ist die **Quelle der Wahrheit für den Fortschritt**. Eine neue Session liest zuerst
> hier den Stand je Ordner (Abschnitt 2) und arbeitet den nächsten offenen Step ab. Der
> selbsttragende Gesamtplan (Wellen 0–5, Begründungen) liegt außerhalb des Repos in
> `~\.claude\plans\wir-haben-jetzt-bereits-staged-bonbon.md`.

## 1. Mechanik — warum das wasserdicht ist

- **Per-Datei-Direktiven überstimmen die Projekteinstellung.** Fertige Dateien behalten volle
  Prüfung, auch wenn das Projekt oblivious bleibt.
- **Prüfbau mit `-p:Nullable=warnings`** (via `nav nullaudit`, Abschnitt 4): Unkonvertierte Dateien
  bekommen nur den **Warning-Kontext ohne Annotations-Kontext** — eigene Deklarationen bleiben
  oblivious (kaum Rauschen), aber die Flussanalyse warnt, wo sie **annotierte APIs falsch
  konsumieren** (CS8602 auf `T?`-Return, CS8625 bei null-Literal in non-null-Parameter). Genau das
  sind die Vertragsverletzungs-Signale: entweder ist die Annotation falsch (fehlendes `?`) oder der
  Konsument hat einen latenten NRE → Repro-Test + Fix.
- **Eingecheckte Baseline** (`Build\nullaudit-baseline.txt`) neutralisiert das Restrauschen der noch
  offenen Ordner — nur Deltas (neues Datei/Warncode-Paar oder Zähler-Anstieg) zählen als Regression.
- **LSP/MCP als Konsum-Validierung**: beide sind bereits projektweit nullable und bauen die
  Public Surface der Engine mit — ihre CS86xx/87xx-Warnungen sind Erstklasse-Regressionen.
- **Unnötige `?`** findet kein Compiler → Playbook-Regeln + Review-Checkliste (Abschnitt 3).
- **Finaler Beweis (Welle 4):** Smoke-Build `-p:Nullable=enable` über das Gesamtprojekt muss
  warnungsfrei sein.

netstandard2.0-Fallen: Die NRT-Flussanalyse-Attribute fehlen in der BCL → Polyfill
`Nav.Language\Internal\NullableAttributes.cs` (ab Welle 0 vollständig, danach eingefroren; wer ein
Attribut neu nutzt, das dort fehlt, bekommt `CS0122`). Und: netstandard2.0 hat **keine** annotierten
BCL-Referenzen → `String.IsNullOrEmpty(s)` verengt `s` **nicht** auf non-null; nach so einem Guard
folgt `CS8602` → eigenen `is null`-Check schreiben oder Parameter gleich non-null lassen.

## 2. Fortschritt je Ordner

Legende Welle: **1** Fundament · **2a** CodeGen · **2b** SemanticAnalyzer · **3/Px** Feature-Paket ·
**✅** fertig (Vorarbeit vor dieser Kampagne).

| Ordner | Welle | gesamt | konvertiert | Stand |
|---|---|---:|---:|---|
| Syntax | ✅ | 66 | 66 | fertig |
| Text | ✅ | 20 | 20 | fertig |
| SemanticModel | ✅ | 49 | 49 | fertig (Revalidierung in 2b) |
| Symbols | ✅ | 1 | 1 | fertig |
| Internal | 1 | 4 | 4 | fertig |
| Common | 1 | 4 | 4 | fertig |
| (Projektwurzel) | 1 | 2 | 2 | fertig |
| Properties | 1 | 1 | 1 | fertig |
| CodeGen (+ CodeModel, Templates) | 2a | 37 | 19 | `CodeModel\` fertig, Rest (18) offen |
| SemanticAnalyzer | 2b | 45 | 0 | offen |
| CodeFixes (+ ErrorFix, Refactoring, StyleFix) | 3 / P1 | 32 | 0 | offen |
| Completion | 3 / P2 | 3 | 0 | offen |
| GoTo | 3 / P2 | 2 | 0 | offen |
| Rename | 3 / P2 | 1 | 0 | offen |
| Diagnostic | 3 / P3 | 13 | 0 | offen |
| Dependencies | 3 / P3 | 4 | 0 | offen |
| FindReferences | 3 / P4 | 10 | 0 | offen |
| References | 3 / P4 | 3 | 0 | offen |
| Workspace | 3 / P5 | 8 | 5 | Rest (3) offen |
| Provider | 3 / P5 | 13 | 0 | offen |
| Generator | 3 / P6 | 6 | 0 | offen |
| QuickInfo | 3 / P6 | 2 | 0 | offen |
| CallHierarchy | 3 / P6 | 1 | 0 | offen |
| CodeActions | 3 / P6 | 2 | 0 | offen |
| **Gesamt** | | **329** | **171** | ~52 % |

> Zahlen verifiziert am 2026-07-03 (Scan `Nav.Language\**\*.cs` ohne `bin`/`obj`/`*.generated.cs` auf
> `#nullable enable`). Nach jedem Step diese Tabelle aktualisieren (Vorbild: `nav nullaudit` gibt den
> Fortschritt maschinell aus).

## 3. Playbook-Regeln (Review-Checkliste je Ordner)

1. **Bestandsaufnahme:** ReSharper-Annotationen ablösen — `[CanBeNull]` → `?`, `[NotNull]` →
   default (weg), `[ItemCanBeNull]` → Element-`?`; `using JetBrains.Annotations;` entfernen.
2. **Datei für Datei** (UTF-8 **mit** BOM; `#nullable enable` als erste Codezeile, Stil wie
   `Syntax/`/`SemanticModel/`): öffentliche Signaturen zuerst, dann Felder/Locals. Flussattribute
   aktiv nutzen: `NotNullWhen(true)` für `TryGet…`/`Is…`, `MemberNotNull` für Init-Helper,
   `NotNullIfNotNull` für Pass-Through.
3. **Kein `!`** ohne Begründungskommentar (Beweis, warum non-null).
4. **Kein neues `?`** ohne benennbaren Null-Zufluss; im Zweifel non-null + Invariante an der Quelle
   herstellen (`??`-Normalisierung) statt `?.`-Kaskaden bei jedem Konsumenten. Tote `?.` auf
   beweisbar non-null entfernen.
4a. **String-Properties bestmöglich non-null** (Default): wo „abwesend" und „leer" dasselbe meinen,
   liefert die Property `string` (nie `null`) und normalisiert im Zweifel auf `String.Empty` — das
   erspart Konsumenten Null-Checks und umgeht die netstandard2.0-`IsNullOrEmpty`-Falle. **Ausnahme:**
   `null` bleibt, wenn es einen **eigenen, ausgewerteten Zustand** kodiert (z.B. `Location.FilePath` =
   „hat gar keine Datei" — bewusst `string?`, mehrere Presence-Zweige hängen daran). Der `""`-Fallback
   gehört dann an den **Ausgabe-Rand** (DTO/Serialisierung, z.B. `FilePath ?? ""`), nicht ins
   Domänenmodell.
5. **`ArgumentNullException`-Guards an public Einstiegspunkten bleiben** (oblivious-Konsumenten wie
   die VS-Extension!); interne Guards dürfen fallen, wenn die Annotation den Vertrag trägt.
6. **Keine Verhaltensänderung außer NRE-Fixes** — und die nur mit **Repro-Test vor dem Fix**
   (rot → Fix → grün), wenn die NRE über die öffentliche API mit konstruierbarem Input erreichbar ist
   (typisch: fehlerhafter .nav-Quelltext → Missing Token → null-Kind). Nur-Annotation reicht, wenn
   null nachweislich unerreichbar ist (Beweis als Kommentar).
7. **Polyfill nie lokal anfassen** (Welle 0 macht ihn vollständig); fehlt doch etwas → an Integrator.
8. **Abschluss:** 0 Nullable-Warnungen im eigenen Ordner (`nav nullaudit -Detail <Ordner>`),
   `nav test` (net472) **und** `dotnet test … -f net10.0` grün, `nav nullaudit -UpdateBaseline`,
   diese Statustabelle aktualisieren, Commit-Message liefern (Nutzer committet).

## 4. Tool `nav nullaudit`

> Wird in Welle-0-Step 3 angelegt (`Tools\Commands\Functions\Invoke-NullAudit.ps1`,
> `.FUNCTIONALITY nullaudit`). Spezifikation:

- **Fortschritt** (`-NoBuild`): Scan `Nav.Language\**\*.cs` (ohne bin/obj/`*.generated.cs`) auf
  `#nullable enable` → Tabelle Ordner × (konvertiert/gesamt/%).
- **Prüfbau**: je Projekt (Nav.Language, Nav.Language.Lsp, Nav.Language.Mcp)
  `dotnet build --no-incremental -p:Nullable=warnings -flp:"warningsonly;logfile=…"`
  (`--no-incremental` ist Pflicht: übersprungene Compiles emittieren keine Warnungen).
- **Aggregation**: Regex `warning CS8[67]\d\d` aus dem File-Log, Pfade repo-relativ normalisiert,
  gruppiert nach **(Datei, Warncode) → Anzahl** — bewusst ohne Zeilennummern (robust gegen Drift).
- **Baseline-Diff** gegen `Build\nullaudit-baseline.txt`: Tab-getrenntes Zeilenformat
  (`Nav.Language/CodeFixes/Foo.cs<TAB>CS8602<TAB>3`), sortiert. Neues Paar oder Zähler-Anstieg =
  **Regression → Exit ≠ 0**.
- **Parameter**: `-UpdateBaseline` (nach reviewtem Step), `-Detail <Ordner>` (Roh-Warnungen mit
  Zeilennummern für die Arbeit), `-NoBuild` (nur Fortschritt). Zusätzlich der **Suppression-Zähler**
  (`!`-Vorkommen pro Ordner).

## 5. Entscheidungslog

- **Keine Big-Bang-Umstellung, per-Datei-Direktiven statt projektweitem `<Nullable>`** — inkrementell,
  jede Datei einzeln grün, Reviews < ~35 Dateien.
- **Grober Ordner-Graph statt Typ-Graph** für die Reihenfolge (Internal/Text/Common → Syntax →
  CodeGen → SemanticAnalyzer → SemanticModel → Feature-Ordner). Bottom-up ist Qualitäts-, keine
  Korrektheitsfrage: falsch geratene Verträge werden automatisch aufgedeckt, sobald die tiefere
  Schicht annotiert ist.
- **SemanticModel/ (fertig) hängt über die einzige Kante `CodeGenerationUnitBuilder.cs` an
  SemanticAnalyzer** → Welle 2b re-validiert SemanticModel gratis; neue Warnungen dort im selben Step
  fixen.
- **String-Properties bestmöglich non-null (Default), `null` nur bei eigenem ausgewertetem Zustand**
  (Playbook-Regel 4a). Entschieden am 2026-07-03 anhand `Location.FilePath`: bleibt `string?`, weil
  `null` = „keine Datei" real ausgewertet wird (`CachedSyntaxProvider`, `NavValidateResult.crossFile`,
  `NormalizedFilePath`); `""`-Fallback sitzt am DTO-Rand (`NavEditDto`).
- **Endzustand (Welle 5) offen** — Empfehlung: nach 100 % auf projektweites `<Nullable>enable</…>`
  umschalten + `<WarningsAsErrors>Nullable</…>`, Direktiven raus. **Entscheidung trifft der Nutzer
  erst nach Welle 4.**
- **Welle 2a Teil 1 (`CodeGen\CodeModel\`, 18 Dateien):** `CodeModelBuilder.GetTaskBeginParameter`
  reichte `taskNode.Declaration` (nullable) ungefiltert an `GetTaskBeginsAsParameter` (erwartet
  non-null) weiter → CS8620 und, isoliert betrachtet, ein latenter NRE (`GetTaskBeginAsParameter`
  dereferenziert die Declaration). Über die öffentliche API ist der Fall aber **unerreichbar**:
  ein Task-Node mit unaufgelöster Declaration erzeugt `Nav0010CannotResolveTask0`
  (`DiagnosticSeverity.Error`), und `CodeGenerator.Generate` bricht bei `Diagnostics.HasErrors()`
  vorab ab. Fix daher verhaltensneutral per `.WhereNotNull()` — dieselbe Idiomatik wie im
  Schwester-Pfad `TransitionCodeModel.GetTaskDeclarations`. Kein Befundlog-Eintrag (nicht über die
  öffentliche API mit konstruierbarem Input auslösbar). String-Konstruktor-Parameter mit
  `?? String.Empty`-Normalisierung wurden auf `string?` gesetzt (Hausstil analog `CodeParameter`),
  `?? throw ArgumentNullException`-Guards für Objekt-Parameter blieben (non-null Vertrag, Schutz vor
  oblivious-Aufrufern).

## 6. Befundlog (NRE-Funde mit Testreferenz)

> Jeder über die öffentliche API erreichbare NRE-Befund wird hier mit Datei, Ursache und
> Testreferenz (`Nav.Language.Tests\Robustness\…`) dokumentiert. Noch keine Einträge.

_(leer)_
