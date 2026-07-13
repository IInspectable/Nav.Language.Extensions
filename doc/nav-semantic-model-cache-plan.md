# Nav Semantik-Modell-Cache — Plan

> **Status:** Step 0 (Stempel-Frische) umgesetzt, Steps 1–4 offen. Am Code verifiziert; Messzahlen aus dem echten Korpus
> (`d:\tfs\main`, 1913 Dateien / 7,5 MB, **Debug**-Engine = ausgelieferter Stand). Setup-Details:
> Memory `nav-perf-profiling-setup`. Verwandt: `doc/nav-perf-optimization-status.md`, `doc/nav-mcp-status.md`.

## 1. Problem

Die Impact-Tools des MCP (`nav_call_hierarchy` incoming, `nav_exit_usages`, `nav_references`) und der
workspace-weite Diagnostics-Sweep des LSP (`PublishAllDiagnosticsAsync`) laufen alle über denselben Kern:
`NavSolution.ProcessCodeGenerationUnitsAsync` — ein linearer Voll-Scan über alle ~1900 `.nav`, der pro
Datei `SemanticModelProvider.GetSemanticModel(...)` aufruft.

`SemanticModelProvider` ist **zustandslos**: jeder Aufruf baut über
`CodeGenerationUnit.FromCodeGenerationUnitSyntax(...)` das komplette Semantikmodell neu
(Symboltabellen, Task-Definitionen, Nodes, Transitions, Referenz-Verdrahtung). Bei jedem Scan wird
diese Arbeit für **alle** Dateien wiederholt und wieder weggeworfen — auch wenn sich nichts geändert hat.

Was **bereits** gecacht ist (kein Handlungsbedarf):
- **Syntaxbäume** — `OverlaySyntaxProvider` (`Nav.Language/Workspace/`, Schlüssel = normalisierter Pfad).
- **Include-Extraktion** — `TaskDeclarationSymbolBuilder.IncludeExtractionCache`
  (`ConditionalWeakTable<CodeGenerationUnitSyntax, IncludeExtraction>`), keyed auf die Syntax-Instanz,
  mit Pro-Datei-Klonen der Deklarationen.

Was **nicht** gecacht ist und die Kosten trägt: der Semantikmodell-Aufbau der jeweils betrachteten Datei
selbst.

## 2. Warum Caching hier seiteneffektfrei möglich ist (verifiziert)

1. **Die `CodeGenerationUnit` ist nach dem Bau unveränderlich.** Alle `References.Add(...)`-Mutationen
   liegen ausschließlich im `TaskDefinitionSymbolBuilder` (Bauzeit). Jede Query-Seite
   (`FindReferencesVisitor`, `SemanticAnalyzer/*`, `NavCompletionService`, `CodeFixes/*`) **liest**
   `.References` nur. Ein gecachtes Modell darf daher gefahrlos über Queries hinweg geteilt werden.
2. **Das Modell ist eine reine Funktion von Syntax-Instanzen.** Das Semantikmodell von A hängt an
   A's Syntax-Instanz + den Syntax-Instanzen von A's **direkten** Includes.
3. **Includes sind nicht transitiv.** `IncludeExtraction.Create` ruft
   `FromCodeGenerationUnitSyntax(..., processAsIncludedFile: true, ...)`; das überspringt die
   Include-Direktiven-Verarbeitung. A hängt also nur an sich selbst + direkten Includes, nicht an deren
   Includes → kein transitiver Abschluss nötig.

## 3. Schichtung — drei Cache-Ebenen, alle auf Syntax-Instanzen pivotierend

```
Nav.Language (Engine — hier liegen ALLE Klassen; opt-in per Verdrahtung)

Provider/                    Workspace/                    SemanticModel/
  SyntaxProvider.Default       OverlaySyntaxProvider          TaskDeclarationSymbolBuilder
  (Disk-Read, kein Cache)        Tier 1: Syntax-Cache           Tier 1.5: IncludeExtractionCache
  CachedSyntaxProvider           + NEU: Stempel-Frische (Step 0)  (CWT, keyed auf Syntax-Instanz,
  (CLI-Einmalpfad)                                                 generell + immer an — existiert)
  CachedSemanticModelProvider  ◄── NEU, Tier 2: Semantik-Cache (Step 1)
    Decorator über SemanticModelProvider, keyed auf Pfad,
    selbstvalidierend gegen Syntax-Instanzen (Tier 1)
      │
      └─ eingehängt NUR in ─► NavWorkspaceCore
                                   ├── NavWorkspace     (LSP)
                                   └── NavMcpWorkspace  (MCP)
```

- **Nicht MCP-lokal:** Einhängepunkt ist die gemeinsame Host-Schicht `NavWorkspaceCore` → LSP **und** MCP
  profitieren. Die CLI (`Nav.Cli`, `NavSolution`-Default-Ctor, Einmal-Codegen) bleibt bewusst ungecacht.
- **Decorator statt Feld:** isoliert testbar, eine Verantwortung, folgt dem `CachedSyntaxProvider`-Muster.
- **Frische in Tier 1, nicht Tier 2:** Die Syntax-Instanz ist der Anker. Ein Stempel-Wechsel dreht die
  Instanz (Tier 1) → Tier 2 invalidiert sich automatisch über `ReferenceEquals`.

## 4. Invalidierung — Vorwärts-Snapshot statt Abhängigkeitsgraph

Kein Reverse-Abhängigkeitsgraph nötig. Jede gecachte Unit A merkt sich **selbst**, gegen welche
Include-Syntax-Instanzen sie gebaut wurde (abgeleitet aus `unit.Includes` bei jedem Neubau), und prüft das
beim Lookup:

```csharp
sealed class CacheEntry {
    public CodeGenerationUnitSyntax  PrimarySyntax;    // Instanz, aus der A gebaut wurde
    public CodeGenerationUnit        Unit;
    public (string Path, CodeGenerationUnitSyntax? Syntax)[] IncludeSnapshot; // je DIREKTEM Include
}

bool IsValid(CacheEntry e, CodeGenerationUnitSyntax currentPrimary) {
    if (!ReferenceEquals(e.PrimarySyntax, currentPrimary)) {
        return false;                                  // A selbst geändert
    }
    foreach (var (path, snapshot) in e.IncludeSnapshot) {
        if (!ReferenceEquals(_syntaxProvider.GetSyntax(path, ct), snapshot)) {
            return false;                              // ein Include geändert
        }
    }
    return true;
}
```

**Der Graph steckt implizit in `IncludeSnapshot` — nur vorwärts und lazy geprüft, statt rückwärts und
eager gepflegt.**

Durchgespielt (B ist Include von A, B ändert sich extern):
1. Nächstes `GetSyntax(B)` erkennt via Step-0-Stempel den neuen Stand → **neue B-Syntax-Instanz** (Tier 1).
2. `IncludeExtractionCache` (Tier 1.5, CWT auf der Instanz) liefert für die neue Instanz automatisch frische
   Extraktion.
3. A-Lookup: Snapshot vermerkt die **alte** B-Instanz → `ReferenceEquals` schlägt fehl → **A wird neu
   gebaut**, gegen frisches B, mit neuem Snapshot.

Eigenschaften: driftsicher (Snapshot bei jedem Neubau aus `unit.Includes` neu abgeleitet); nur eine Ebene
tief (Includes nicht transitiv); Kosten pro Lookup `1 + #direkteIncludes` Ref-Vergleiche gegen Tier 1;
Zyklen (A↔B) konvergieren (einstufige Ref-Vergleiche, keine Neubau-Kaskade).

Der LSP-`IncludeDependencyGraph` **bleibt** — aber für seine eigene Rolle (welche Dateien nach einer
Änderung Diagnostics **nachpublizieren**). Das ist orthogonal zur Cache-Gültigkeit: Graph = *wann* neu
gerechnet wird, Snapshot = *ob der Treffer gültig* ist. Sie komponieren.

## 5. Frische gegen Out-of-Band-Änderungen (Step 0) — Poll statt Watcher

**Ist-Zustand:** Der `NavMcpWorkspace` ist ein Prozess-Singleton (`Program.cs`: `AddSingleton`), ohne
File-Watcher. Der Syntax-Cache hat keine Frische-Prüfung. Einzige Invalidierung: `GetFreshUnit`/
`GetFreshSyntaxTree` — **nur für die eine direkt abgefragte Datei**. Folge: Scan-basierte Tools liefern für
alle **anderen** Dateien den gecachten Stand des ersten Scans; eine extern geänderte Datei wird dort
**nicht** bemerkt.

**Lösung:** Zeitstempel-Validierung (`LastWriteTimeUtc` + Dateilänge) am Disk-Pfad des
`OverlaySyntaxProvider`. Beim Cache-Treffer wird der Stempel gegen die Platte geprüft; bei Abweichung neu
geparst (→ neue Instanz → Tier 2 invalidiert automatisch). Overlays bleiben autoritativ (kein `stat`).

**Warum Poll statt `FileSystemWatcher`:** Der Watcher-Vorteil (kein Poll-Aufwand zwischen Lesevorgängen)
greift im request/response-Modell des Agenten nicht — zwischen Tool-Calls liest niemand. Dagegen bringt
der Watcher: verlorene Events (`InternalBufferOverflowException`) → **stille** Veralterung ohne
Selbstheilung; Unzuverlässigkeit auf Netz-/gemappten Laufwerken (Korpus ist TFS-Arbeitsordner);
Rename-/Temp-Event-Chaos beim Editor-Speichern; Threading/Lifecycle. Poll-beim-Lesen ist selbstkorrigierend,
funktioniert überall und liest zur Abfragezeit (Datei ruht). Falls je Push gewünscht: Watcher nur als
Hinweis, Poll als Korrektheits-Backstop.

## 6. Messzahlen (Debug-Engine, 1913 Dateien / 7,5 MB, in place)

| Phase | Zeit | Bedeutung |
|---|---:|---|
| A) Kaltscan (Syntax + Semantik bauen) | 3.749 ms | Erster Scan pro Serverprozess (Platte + JIT kalt). Einmalig. |
| B) Warmscan HEUTE (Syntax gecacht, Semantik neu) | ~440 ms | Kosten **jedes** weiteren Scan-Tools im Ist-Zustand. |
| C) Warmscan MIT Tier-2-Cache (Treffer) | 5–16 ms | 3826 Hits / 1913 Misses. |
| D) Stempel-Kosten (Step 0) | 8 ms | 1913 `stat` je Scan. |

**Ersparnis pro Wiederhol-Scan: ~440 ms → ~13–24 ms (C+D), grob 20–30×.** Der „Idealfall 0 s" ist nahezu
erreicht; der Boden ist ~13–24 ms (Validierung + Scan-Schleife + Stempel).

Ehrliche Abgrenzung:
- Der Cache eliminiert den **Wiederhol**-Aufwand, **nicht** den **Kaltstart** (~3,7 s, einmal pro Prozess) —
  dafür ist die Scan-Parallelisierung zuständig (§8).
- Die in der Ausgangs-Session beobachteten ~29 s erklärt der Semantik-Anteil **nicht** allein (1.+2. Aufruf ≈
  4,2 s reine Engine-Zeit). Der Rest steckt außerhalb des Modellbaus: MCP-Prozessstart, JSON-RPC und vor
  allem die Serialisierung des `full`-Payloads (`detail=full` war angefordert). Der Cache adressiert das nicht.

## 7. Umsetzungs-Schritte

- **Step 0 — Stempel-Frische** im `OverlaySyntaxProvider` (`Nav.Language/Workspace/OverlaySyntaxProvider.cs`),
  nur Disk-Pfad. `DiskStamp` = readonly record struct aus `LastWriteTimeUtc` + `Length`; fehlende (oder
  nicht stat-bare) Datei → `default` (Wiederauftauchen wird ebenfalls als Änderung erkannt). Overlay-Pfad
  unverändert (autoritativ, kein Stempel). **UMGESETZT** — mit einer bewussten Abweichung: statt eines
  parallelen `_stamps`-Dictionaries liegt der Stempel **mit im Cache-Eintrag** (`CacheEntry` =
  Syntax + Stempel). Zwei getrennte Dictionaries könnten bei nebenläufigen Neubauten einen frischen
  Stempel mit veralteter Syntax paaren (Schreibreihenfolge Cache↔Stempel verschränkt) — der Treffer
  bliebe dann dauerhaft stale; der atomare Eintrag schließt das aus. Der Stempel wird **vor** dem Lesen
  genommen (Änderung zwischen Stempel und Lesen ⇒ nächster Zugriff parst erneut, nie umgekehrt).
  Tests: `Nav.Language.Tests/Workspace/OverlaySyntaxProviderTests.cs` (Treffer-Identität, Out-of-Band-
  Inhalt, Nur-Zeitstempel, Löschen/Wiederauftauchen, Overlay-Autorität).
- **Step 1 — `CachedSemanticModelProvider`** (`Nav.Language/Provider/CachedSemanticModelProvider.cs`, neu):
  Decorator über einen inneren `ISemanticModelProvider`, hält denselben `ISyntaxProvider` (zur
  Include-Validierung). `ConcurrentDictionary<string, CacheEntry>` (Key = normalisierter Pfad). Reine
  Selbstvalidierung (§4), **kein** explizites `Invalidate` (bewusste Wahl: Korrektheit trägt die
  `ReferenceEquals`-Prüfung; geänderte Einträge werden beim nächsten Lookup ersetzt). Ungespeicherte Puffer
  ohne `FileInfo` → nicht cachebar, direkt durchreichen.
- **Step 2 — Einhängen** in `NavWorkspaceCore` (Ctor):
  `new CachedSemanticModelProvider(new SemanticModelProvider(_syntaxProvider), _syntaxProvider)`.
  Nicht in `NavSolution`-Default-Ctor (CLI bleibt ungecacht).
- **Step 3 — Tests** (`Nav.Language.Tests`, net472 + net10):
  - Trefferfall: zwei Scans → zweiter liefert referenzgleiche Units (`Is.SameAs`), kein Neubau.
  - Selbst-Invalidierung Primärdatei: A ändern → A neu, Nachbarn Treffer.
  - Selbst-Invalidierung über Include: B (Include von A) ändern, A nicht anfassen → A neu, spiegelt B.
  - Nicht-transitiv: C (Include von B, nicht A) ändern → A bleibt Treffer.
  - Out-of-Band-Frische (Step 0): Datei auf Platte ändern ohne `GetFreshUnit` → Scan sieht die Änderung
    (schlägt heute fehl).
  - Kein Dateibezug: reines Overlay ohne `FileInfo` → nicht gecacht, korrektes Modell.
- **Step 4 — Messen** mit dem Scratchpad-Harness (Kalt- vs. Warmscan) gegen die Zahlen aus §6.

Nach jedem Step: Review + Build/Test-Check + fertige Commit-Message (echte Umlaute) — Commit macht der
Nutzer.

## 8. Bewusst getrennter Folge-Step

**Scan-Parallelisierung** (`NavSolution.cs`, das faktisch sequenzielle `SolutionFiles.AsParallel()`-
`foreach`): ein `foreach` über eine `ParallelQuery` merged auf den aufrufenden Thread zurück → der teure
Rumpf läuft **nicht** parallel. Umstellen auf `Parallel.ForEachAsync` (bzw. Modelle per
`.AsParallel().Select(...)` bauen, dann einsammeln) skaliert den unvermeidbaren Kaltstart-Scan mit den
Kernen. Orthogonal zum Cache; eigener Durchgang nach stabilem/getestetem Cache. Erfordert Thread-Safety-
Review des Scan-Rumpfs (die Cache-Strukturen sind bereits `ConcurrentDictionary`).
