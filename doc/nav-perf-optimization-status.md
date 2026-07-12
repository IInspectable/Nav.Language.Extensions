# Nav Performance-Optimierung — Arbeitsdokument

> **Status:** Analyse abgeschlossen, Backlog priorisiert. **#1 umgesetzt** (Include-Cache-Default),
> #2/#3 offen. Lebendes Dokument zum Abarbeiten in Folge-Sessions. Setup-Details: Memory
> `nav-perf-profiling-setup`.
> Voller Analyse-Befund (flüchtig, Scratchpad): `scratchpad/PERF-BEFUND.md`.

## 1. Ziel & Fokus

Perf der VS-freien Engine `Nav.Language`: **Syntax (Parse/Lexer), Semantic Model, Formatter**.
Referenz-Szenario: alle `.nav` unter `d:\tfs\main` (1913 Dateien, ~7,5 Mio. Zeichen) parsen / Semantic
Model bauen / neu formatieren.

## 2. Mess-Setup (zum Reproduzieren)

- **Korpus** pfadgetreu in den Session-Scratchpad kopieren (`navcorpus/`), Originale nie anfassen.
- **Harness** (net10-Konsole im Scratchpad, referenziert die gebaute
  `Nav.Language\bin\{Debug|Release}\Pharmatechnik.Nav.Language.dll` per `<Reference>`, NICHT in der
  Solution). Modi: `sweep` (Korrektheit + SHA-256 über alle Ausgaben), `perf` (Phasen-Split best-of-N,
  Zeit + `GC.GetTotalAllocatedBytes`), `trace` (langer Einzelphasen-Lauf für dotnet-trace). Entry-Points:
  `SyntaxTree.ParseText`, `CodeGenerationUnit.FromCodeGenerationUnitSyntax(root, ct, provider)`,
  `NavFormattingService.FormatDocument(tree, settings, options)` + `TextChangeWriter.ApplyTextChanges`.
- **Profiler:** `dotnet-trace collect --format speedscope` → Node-Skript aggregiert Self/Total je Frame.
  **Self landet auf CPU_TIME-Pseudoframes → Total-Time ist das brauchbare Signal.** (JetBrains `dottrace`
  ist installiert, braucht aber die Rider-GUI → nicht agent-tauglich.)

**Wichtige Mess-Vorbehalte:**
- Die bisherigen Zahlen sind aus dem **Debug**-Build und aus einem **inzwischen veralteten Stand**
  (Formatter wurde seither umgebaut, s. #4). Vor/nach jeder Optimierung **auf aktuellem HEAD und auf
  Release neu messen** — Debug überzeichnet u.a. `[Conditional("DEBUG")]`-Code.
- **Korrektheits-Netz zuerst:** `sweep` muss vor UND nach jeder Änderung 0 Crashes, disjunkte/sortierte
  Changes, Idempotenz, Bedeutungserhalt und **identische SHA-256** (Tabs+Spaces) liefern. Der Hash ist
  der Äquivalenz-Beweis für alle ausgabe-neutralen Optimierungen (#2/#3).

## 3. Ausgangs-Befund (Debug, veralteter Stand — nur als Größenordnung)

| Phase | ns/Zeichen | MB alloc |
|---|---:|---:|
| Parse | ~44 | 249 |
| Semantic (uncached) | 347 | 1795 |
| **Semantic (cached)** | **52** | **549** |
| Format | ~180–240 | 1377 |
| Apply | ~10 | 49 |

Semantic uncached ≈ 85 % Include-Reparse; mit `CachedSyntaxProvider` **6,7× schneller, ⅓ Alloc**.

## 4. Backlog (priorisiert)

### #1 — Include-Cache als Default am Build  ✔ ERLEDIGT
- **Was (umgesetzt):** `NavUseSyntaxCache`-Default `false` → `true` in
  `Nav.Language.BuildTasks/Pharmatechnik.Nav.Language.targets`. Damit gibt der MSBuild-`Nav`-Task bei
  jedem Build `/c` an `nav.exe` weiter (`Nav.cs:71`) und aktiviert den `CachedSyntaxProvider`.
- **CLI-Default bewusst NICHT mitgezogen:** `-c` in `Nav.Cli/CommandLine.cs:63` bleibt Opt-in
  (dokumentierter Schalter unverändert). Der MSBuild-Pfad — der volumenstärkste — läuft ohnehin über
  `/c`; nur direkte `nav.exe`-Handaufrufe cachen weiterhin erst mit explizitem `-c`.
- **Wirkung:** größter Hebel (Semantic 6,7×), trifft den volumenstärksten Pfad (jeder Build, jede `.nav`).
- **Risiko:** praktisch null. Ausgabe-**byte-identisch**; Cache ist prozess-scoped
  (`NavCodeGeneratorPipeline.Run` → `using var syntaxProvider`); fließt nicht in den Inkremental-Hash
  (`NavParamsContent` listet nur content-relevante Parameter, `UseSyntaxCache` gehört nicht dazu).
- **Kontext:** Default stammte aus 2017 (legacy-konservativ), keine bewusste Korrektheits-Entscheidung.
- **Belegt:** `nav.exe /d` auf den taskref-lastigen Regression-Fixtures (V1 `Test.nav` + V2-Flows),
  einmal ohne und einmal mit `/c`, `/f` (Force) → **21/21 generierte `.cs` SHA-256-identisch**, 0 Diffs.

### #2 — Collection-Presizing im Parser
- **Was:** Der Trivia-`Dictionary<int, TriviaRange>` resized ständig (Resize + set_Item zusammen ~25 %
  der Parse-Zeit); dazu unpresizte Token-Listen. Kapazität vorab aus Zeichen-/Tokenzahl schätzen; ggf.
  Dictionary durch sortiertes Array ersetzen. Umfeld: `NavParser.BuildTrivia`/`FinalizeTrivia`,
  `TakeSortedTokens`, `Nav.Language/Syntax/`.
- **Wirkung:** trägt Parse — und damit Formatter **und** (uncached-)Semantic gleichzeitig.
- **Risiko:** gering, durch `sweep`-Hash abgesichert. **Aufwand:** klein.

### #3 — Vollen Token-Sort vermeiden
- **Was:** `NavParser.TakeSortedTokens` → `List.Sort`/`IntroSort` via `SyntaxTokenComparer` (~10 %).
  Tokens fallen weitgehend positionsgeordnet an → geordnetes Einfügen/Merge statt Vollsortierung.
- **Risiko:** mittel (Reihenfolge-Invariante der Token-/Trivia-Zuordnung), `sweep` deckt es ab.
- **Aufwand:** klein–mittel.

### #4 — AlignmentMapBuilder bündeln  ✔ ERLEDIGT
Der Formatter wurde bereits umgebaut: `AlignmentMapBuilder.Build` nimmt eine vorab berechnete
`StatementFacts.Map`; die früher getrennten Condition-/Trigger-/Trailing-Comment-Pässe sind zu einem
gemeinsamen `AddTightClauseColumns` zusammengezogen. Wenn gewünscht: mit frischem Trace der Gewinn
gegenmessen. Sonst abgeschlossen.

## 5. Empfohlene Reihenfolge

1. ~~**#1** sofort (größter Hebel, kein Risiko, ausgabe-neutral).~~ ✔ erledigt — nur Target-Default,
   CLI blieb Opt-in.
2. **#2** (breit wirksam, billig, hash-abgesichert). ← als Nächstes
3. **Release + aktueller HEAD gegenmessen**, dann **#3** nur, wenn die Messung ihn noch lohnt.

Jede Änderung: `sweep` grün + Hash unverändert → messen → Commit-Message vorschlagen (nicht selbst
committen).
