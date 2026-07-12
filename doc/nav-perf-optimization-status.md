# Nav Performance-Optimierung — Arbeitsdokument

> **Status:** Analyse abgeschlossen, Backlog priorisiert. **#1 umgesetzt** (Include-Cache-Default),
> **#2 umgesetzt** (Dictionary-Presizing), #3 offen (erst gegenmessen). Lebendes Dokument zum Abarbeiten
> in Folge-Sessions. Setup-Details: Memory `nav-perf-profiling-setup`.
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

### #2 — Collection-Presizing im Parser  ✔ ERLEDIGT
- **Umgesetzt:** Der Trivia-`Dictionary<int, TriviaRange>` in `NavParser.BuildTrivia` wird jetzt vorab
  mit `_tokens.Count` dimensioniert. Das ist die **exakte** Endgröße: die Map bekommt genau einen Eintrag
  je konsumiertem signifikanten Token (der signifikante Trenner-Arm der `BuildTrivia`-Schleife setzt jeden
  einmal), und `_tokens` enthält zu diesem Zeitpunkt genau diese Token (EOF hängt noch nicht im Strom).
  Damit entfällt das wiederholte Rehashen beim Wachsen.
- **Bereits presized (kein Handlungsbedarf):** Lexer-`_tokens` (`_length/4`), Parser-`_tokens`
  (`_raw.Length/2+1`), der `all`-Trivia-Builder (`_raw.Length/2`).
- **Nicht presizt:** das `consumedStarts`-`HashSet<int>` in `FinalizeTrivia` — der
  `HashSet<T>(int)`-Ctor fehlt auf **netstandard2.0** (Ziel-TFM der Engine); `Dictionary<K,V>(int)` gibt es
  dort. Bewusst so belassen (Kommentar im Code), damit es niemand erneut versucht.
- **Wirkung (gemessen, Release, `LargeNav.nav` 191 KB, Best-of-15×300, A/B via `git stash`):**
  Parse-**Allokation 3359 → 3076 KiB/Iter (−283 KiB, ~8,4 %)**; Zeit 1684 → 1672 µs/Iter = im Rauschen
  (die im Debug/ANTLR-Altstand gemessenen „~25 % der Parse-Zeit" schlagen im aktuellen Release nur als
  Alloc-Ersparnis durch, nicht als Wanduhr).
- **Risiko:** null. Reiner Kapazitäts-Hinweis, Ausgabe unverändert — **1731 net10-Tests grün** (Syntax-,
  Regression-, Formatting-Golden-Snapshots pinnen Token/Baum/Trivia/generierten Code).

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
2. ~~**#2** (breit wirksam, billig, hash-abgesichert).~~ ✔ erledigt — Dictionary-Presizing, ~8,4 %
   weniger Parse-Alloc, ausgabe-neutral.
3. **#3** nur, wenn eine frische Release-Messung ihn noch lohnt. Die #2-Messung zeigt: der Token-Sort
   (`TakeSortedTokens`) ist im aktuellen Release **nicht** mehr der Wanduhr-Treiber, den der veraltete
   Debug-Stand nahelegte — vor Inangriffnahme also erst gegenmessen (Trace/Alloc), sonst überflüssig.

Jede Änderung: `sweep` grün + Hash unverändert → messen → Commit-Message vorschlagen (nicht selbst
committen).
