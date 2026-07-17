# NRE-/Exception-Robustheit auf kaputtem Source — Ergebnis-Report

Ergebnis des systematischen Robustheits-Laufs nach dem Playbook
[nav-nre-analysis-prompt.md](nav-nre-analysis-prompt.md): grammatik-bewusste Mutationen über den
realen `.nav`-Korpus, Treiber-Kette exakt wie die Hosts (VS-Extension, LSP), Crash-Signaturen
dedupliziert, Repros per Delta-Debugging minimiert.

## Kurzfassung

Der Lauf fand in drei Discovery-Runden über **~10 Mio. Mutanten** und **~265 000 Roh-Crashes**
genau **drei distinkte Crash-Signaturen** — alle drei mit **derselben Wurzel**; zwei
Validierungs-Runden auf der gefixten Engine (weitere ~7,1 Mio. Mutanten, inkl.
Zweitordnungs-Mutationen) liefen anschließend **crashfrei**: `SyntaxNode.ToString()` schnitt für
Knoten, die nach Parser-Recovery nur aus Missing-Token bestehen (Extent = `TextExtent.Missing`,
Start −1), ungeprüft in den Quelltext → `ArgumentOutOfRangeException` in
`StringSourceText.Slice`. `SyntaxToken.ToString()` hatte den Missing-Guard bereits;
dem Knoten-Gegenstück fehlte er.

**Fix (eine Stelle, heilt alle drei Signaturen):** `SyntaxNode.ToString()` liefert für
Missing-Extent `String.Empty` — Roslyn-Stil: der Text eines fehlenden Knotens ist leer,
die Konsumenten degradieren (leere Anzeige-Namen), statt abzustürzen.
Tests: `Nav.Language.Tests\Robustness\BrokenSourceRobustnessTests.cs` (je Befund ein
realistischer Tipp-Zwischenstand + der minimierte Original-Repro; grün auf net472 und net10.0).

## Befunde im Einzelnen

### S001 — QuickInfo: Hover über Signal-Trigger bei kaputter `[base]`-Deklaration

- **Signatur:** `ArgumentOutOfRangeException` in
  `StringSourceText.Slice ← SourceText.Substring ← SyntaxNode.ToString ← TaskCodeInfo.FromTaskDefinition`
- **Service:** QuickInfo (`NavHoverService.GetHover` → `DisplayPartsBuilder.VisitSignalTriggerSymbol`
  → `SignalTriggerCodeInfo` → `TaskCodeInfo`)
- **Minimaler Input:** `t l[b view X X on k` (aus `interior-del GenericTypeSyntax` in einer
  `[base]`-Deklaration; realistisch: Anwender löscht/tippt den Basisklassen-Typ)
- **Ursache:** `taskDefinition.Syntax.CodeBaseDeclaration?.WfsBaseType?.ToString()` — der
  `WfsBaseType`-Knoten existiert, besteht aber nur aus Missing-Token → Extent Missing →
  `Substring(-1, 0)`.

### S002 — SemanticModel: `[result` ohne Typ

- **Signatur:** wie S001, Konsument `CodeParameter.FromResultDeclaration`
- **Service:** SemanticModel-Bau (`TaskDeclarationSymbolBuilder`) — der Absturz riss in VS die
  **gesamte Semantik der Datei** mit (keine Diagnostics, kein Hover, nichts)
- **Minimaler Input:** `t D[r` (5 Zeichen — die angefangene `[result`-Klammer genügt)
- **Ursache:** `codeResult.Result.Type` ist non-null, aber all-missing → `Type.ToString()` wirft.

### S003 — SemanticModel: `[params` mit hängendem Komma (Analyzer Nav0119)

- **Signatur:** wie S001, Konsument `Nav0119InitNode0HasSameSignatureAsInitNode1.GetSignature`
- **Service:** SemanticModel-Bau (semantische Analyzer-Phase)
- **Minimaler Input:** `t B init[p e,`
- **Ursache:** Der Parameter hinter dem Komma besteht nur aus Missing-Token;
  `p.Type.ToString()` in der Überladungs-Signaturbildung wirft.

## Methodik / Nachvollziehbarkeit

- **Harness:** `NreSweep` (net10-Konsole im Session-Scratchpad, referenziert den Debug-Build von
  `Nav.Language`). Mutationsoperatoren auf Token-/Knoten-Granularität über den geparsten Bäumen:
  Interior-Deletion, Token-Deletion, Delimiter-Verdopplung, Keyword-/Identifier-Korruption,
  leere Konstrukte, Boundary-Truncation, Adjazenz-Swap, Stray-Insert — bewusst **keine** naive
  wachsende-Präfix-Strategie.
- **Treiber-Kette** je Mutant, jeder Schritt einzeln gekapselt (gespiegelt aus
  `SyntacticClassificationTagger`/`SemanticClassificationTagger` (VS), `SemanticTokensBuilder`
  (LSP), `NavWorkspaceCore.GetDiagnostics`/`DiagnosticsComputer`, `NavHoverService`):
  Parse → syntaktische Classification (Token-Strom, Kommentare, Direktiven, Skipped) →
  SemanticModel → Diagnostics → semantische Classification (DeadCode-/Knoten-/Code-Extents) →
  QuickInfo an jeder Token-Grenz-Position.
- **Signatur-Dedupe:** ExceptionTyp + Top-4-Engine-Frames; erst deduplizieren, dann analysieren.
- **Korpus:** pfadgetreue Kopie von `d:\tfs\main` (1913 `.nav`-Seeds, keine inhaltlichen Duplikate).
- **Deckelungen (geloggt, nicht still):** pro Operator und Seed max. 5000 Mutanten
  (Stride-Sampling; nur bei den größten Dateien wirksam), Hover-Positionen pro Mutant auf
  1200 Äquivalenzklassen-Positionen gesampelt.
- **Konvergenz** (Kriterium: zwei aufeinanderfolgende Runden ohne neue Signatur):
  - Runde 1 (voller Korpus, grober Signatur-Schlüssel): 1 Signatur.
  - Runde 2 (voller Korpus, Top-4-Frame-Schlüssel): 3,10 Mio. Mutanten, 87 098 Crashes,
    **3 Signaturen** (S001–S003).
  - Runde 3 (Caps auf 5000 angehoben): 3,80 Mio. Mutanten, 92 498 Crashes, **keine neue** Signatur.
  - Runde 4a (Erstordnung, volle Breite, **gefixte Engine**): 3,80 Mio. Mutanten,
    **0 Crashes, 0 Signaturen** — Fix korpusweit validiert, keine zuvor im Schatten der
    Semantik-Crashes liegenden Folge-Defekte (Code hinter der Absturzstelle lief vorher nie).
  - Runde 4b (Zweitordnung: gesampelte Mutanten erneut mutiert, gefixte Engine): 3,31 Mio.
    Mutanten, **0 Crashes, 0 Signaturen**.
- **Artefakte** (Session-Scratchpad, nicht eingecheckt): `out2/findings.json`
  (Signatur → Repro-Pfad → Stack-Frames), `out2/repros/S00x/{repro.nav, minimal.nav, stack.txt}`,
  `out2/sweep.log` bzw. `outfixed/sweep.log` (Zähler, Sampling-Auslassungen der
  Validierungs-Runden).

## Bewertung

Dass Millionen grammatik-bewusster Mutationen über den kompletten Real-Korpus nur **eine**
Wurzelursache freilegten, ist ein starkes Signal für die Fehler-Toleranz der auf
Roslyn-Prinzipien umgebauten Pipeline (Missing-Token, strukturierte Skip-Trivia, Recovery mit
präzisen Diagnosen): Der handgeschriebene Parser und das SemanticModel verkraften beliebige
Zerstörung des Inputs strukturell — übrig blieb genau die eine Text-Schnitt-Stelle, an der das
Token-Vorbild (`SyntaxToken.ToString()` → Missing-Guard) noch nicht auf Knoten übertragen war.

## Quercheck: statische Null-Analyse (ReSharper `jb inspectcode`)

Komplementär zum Fuzzing lief `jb inspectcode` (2026.1.4, Severity ab SUGGESTION) über
`Nav.Language`: **0 Treffer in der gesamten Null-Familie** — weder Danger-Inspektionen
(`PossibleNullReferenceException`, `AssignNullToNotNullAttribute`) noch redundante Guards
(die 363 verbliebenen Treffer sind ausschließlich Style-/Dead-Code-Haushalt wie
`MemberCanBePrivate.Global`). Das bestätigt zweierlei: Die Engine ist nach Nullable-Kampagne
und früheren Sweeps statisch null-sauber — und **keiner der drei Fuzzing-Befunde wäre statisch
gefunden worden**, denn dort war nirgends etwas `null`: Der Fehler steckte im Domänen-Sentinel
`TextExtent.Missing` (`Start = -1`), den eine generische Null-Analyse nicht kennt. Statische
Null-Analyse und Korpus-Fuzzing decken disjunkte Fehlerklassen ab; für die Sentinel-Klasse
bleiben Fuzzing-Läufe (Playbook) oder ein projekteigener Analyzer
(„`Substring(extent)` ohne `IsMissing`-Prüfung") die Werkzeuge.
