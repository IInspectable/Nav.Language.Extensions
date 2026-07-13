# Nav Semantik-Modell-Cache — Plan

> **Status: abgeschlossen.** Step 0 (Stempel-Frische), Step 1 (`CachedSemanticModelProvider` +
> Provider-Tests), Step 2 (Einhängen in `NavWorkspaceCore`), Step 3 (Tests inkl. Scan-Ebene) und
> Step 4 (Nachmessung gegen die fertige Implementierung, §6) umgesetzt; ebenso der Folge-Step
> Scan-Parallelisierung (§8, Kaltscan ~2×). Am Code verifiziert;
> Messzahlen aus dem echten Korpus
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

Design-Phase (Prototyp-Cache, vor Umsetzung):

| Phase | Zeit | Bedeutung |
|---|---:|---|
| A) Kaltscan (Syntax + Semantik bauen) | 3.749 ms | Erster Scan pro Serverprozess (Platte + JIT kalt). Einmalig. |
| B) Warmscan HEUTE (Syntax gecacht, Semantik neu) | ~440 ms | Kosten **jedes** weiteren Scan-Tools im Ist-Zustand. |
| C) Warmscan MIT Tier-2-Cache (Treffer) | 5–16 ms | 3826 Hits / 1913 Misses. |
| D) Stempel-Kosten (Step 0) | 8 ms | 1913 `stat` je Scan. |

**Step-4-Nachmessung (fertige Implementierung, 2026-07-13, zwei Läufe):** gemessen wurde die **echte**
Verdrahtung — `NavWorkspaceCore` (LoadAsync → `ProcessCodeGenerationUnitsAsync`), also
`OverlaySyntaxProvider` inkl. Stempel-Frische + Engine-`CachedSemanticModelProvider`; die Baseline ohne
Tier 2 lief im selben Prozess auf demselben Stand.

| Phase | Zeit | Bedeutung |
|---|---:|---|
| A) Kaltscan (ungecachter Provider) | 2.093–2.255 ms | Schneller als in der Design-Phase (Engine-Stand weiterentwickelt, OS-Cache wärmer). |
| B) Warmscan ohne Tier 2 | 728–1.086 ms | Baseline: Semantik-Neubau je Scan auf heutigem Stand. |
| C) Warmscan `NavWorkspaceCore` (Treffer) | 28–47 ms | **1913/1913 Units referenzgleich** über die Scans (100 % Treffer). Stempel-Prüfungen bereits enthalten. |
| D) Stempel isoliert | 7–8 ms | Zur Einordnung; in C) inbegriffen. |

**Ersparnis pro Wiederhol-Scan, verifiziert am Endstand: ~730–1.090 ms → ~30–45 ms, also ~20–30×.**
Der Boden liegt etwas über der Design-Prognose (~13–24 ms), weil die Treffer-Validierung pro Datei
zusätzlich je direktem Include ein `GetSyntax` (mit Stempel-`stat`) macht — der Preis der
Selbstvalidierung, im Gesamtbild vernachlässigbar.

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
  ohne `FileInfo` → nicht cachebar, direkt durchreichen. **UMGESETZT** — Details der Umsetzung:
  Snapshot-Pfade kommen aus `include.FileName` (aufgelöster Pfad, wie beim LSP-`IncludeDependencyGraph`);
  die `GetSemanticModel(syntax)`-Überladung schlüsselt über `syntax.SyntaxTree.SourceText.FileInfo`;
  liefert Tier 1 für eine Datei `null` (gelöscht), wird der Alteintrag entfernt. Bekanntes, bewusst
  akzeptiertes Rest-Fenster: ändert sich ein Include *während* des Baus einer Unit, kann der Snapshot
  schon die neuere Include-Instanz tragen (Kommentar an `CaptureIncludeSnapshot`). Die Provider-Tests
  aus Step 3 sind bereits mit umgesetzt: `Nav.Language.Tests/CachedSemanticModelProviderTests.cs`
  (Treffer, Primär-/Include-Invalidierung out-of-band via Step-0-Stempel, Nicht-Transitivität,
  Löschen/Wiederauftauchen, Puffer ohne Dateibezug).
- **Step 2 — Einhängen** in `NavWorkspaceCore` (Ctor):
  `new CachedSemanticModelProvider(new SemanticModelProvider(_syntaxProvider), _syntaxProvider)`.
  Nicht in `NavSolution`-Default-Ctor (CLI bleibt ungecacht). **UMGESETZT** — damit laufen LSP und MCP
  (alle Einzeldatei-Zugriffe wie auch die Scan-Tools über `NavSolution.ProcessCodeGenerationUnitsAsync`,
  das den Provider aus dem `NavSolution`-Ctor nutzt) über den Tier-2-Cache.
- **Step 3 — Tests** (`Nav.Language.Tests`, net472 + net10):
  - Trefferfall: zwei Scans → zweiter liefert referenzgleiche Units (`Is.SameAs`), kein Neubau.
  - Selbst-Invalidierung Primärdatei: A ändern → A neu, Nachbarn Treffer.
  - Selbst-Invalidierung über Include: B (Include von A) ändern, A nicht anfassen → A neu, spiegelt B.
  - Nicht-transitiv: C (Include von B, nicht A) ändern → A bleibt Treffer.
  - Out-of-Band-Frische (Step 0): Datei auf Platte ändern ohne `GetFreshUnit` → Scan sieht die Änderung
    (schlägt heute fehl).
  - Kein Dateibezug: reines Overlay ohne `FileInfo` → nicht gecacht, korrektes Modell.

  **UMGESETZT** — zweigeteilt: die Provider-Ebene deckt
  `Nav.Language.Tests/CachedSemanticModelProviderTests.cs` ab (mit Step 1 entstanden), die Scan-Ebene
  `Nav.Language.Tests/Workspace/NavWorkspaceCoreSemanticCacheTests.cs`: kompletter Host-Pfad
  (`NavWorkspaceCore.LoadAsync` → `ProcessCodeGenerationUnitsAsync`) mit Wiederhol-Scan-Treffern
  (`Is.SameAs`), Out-of-Band-Invalidierung Primärdatei/Include (nur Betroffene neu, Nachbarn Treffer),
  Nicht-Transitivität sowie Overlay-Edit/-Close (Overlay autoritativ, Close stellt Disk-Stand wieder her).
- **Step 4 — Messen** mit dem Scratchpad-Harness (Kalt- vs. Warmscan) gegen die Zahlen aus §6.
  **UMGESETZT** — Nachmessung gegen die fertige Implementierung statt des Design-Prototyps: das
  Harness treibt den echten Host-Pfad (`NavWorkspaceCore.LoadAsync` →
  `ProcessCodeGenerationUnitsAsync`) und zählt Treffer über Referenzgleichheit der Units zwischen
  zwei Scans (die Engine-Klasse exponiert bewusst keine Zähler). Ergebnis in §6: ~20–30× pro
  Wiederhol-Scan bei 100 % Treffern, damit ist die Design-Prognose bestätigt und der Plan
  abgeschlossen.

Nach jedem Step: Review + Build/Test-Check + fertige Commit-Message (echte Umlaute) — Commit macht der
Nutzer.

## 8. Folge-Step: Scan-Parallelisierung — UMGESETZT

**Ausgangsbefund** (`NavSolution.cs`): das frühere `foreach` über `SolutionFiles.AsParallel()` merged
nur die (identischen) Elemente auf den aufrufenden Thread zurück — der teure Rumpf lief faktisch
**sequenziell**.

**Umsetzung** (bewusst minimal, nur Schritt 3 von `ProcessCodeGenerationUnitsAsync`): die
Semantikmodelle werden per PLINQ-`Select` **parallel gebaut**, `asyncAction` läuft aber weiterhin
**sequenziell und in Datei-Reihenfolge** (`AsOrdered`) auf dem Aufrufer-Fluss — der Vertrag „Callback
läuft nicht nebenläufig" bleibt für alle Aufrufer erhalten, kein Host musste angefasst werden. Das
Datei-Dedup (`seenFiles`, nicht thread-sicher) passiert vollständig **vor** der Parallel-Stufe.

**Thread-Safety-Review des Bau-Pfads** (Voraussetzung, am Code verifiziert): Provider-Caches sind
`ConcurrentDictionary` (Overlay-/Cached-Syntax-Provider, Tier 2) bzw. `ConditionalWeakTable`
(Include-Extraktion; Prototypen unveränderlich, Konsumenten erhalten Klone); die Lazy-Initialisierungen
der Lese-Pfade sind benigne Races (`SyntaxNode._childTokens` idempotent mit atomarer
Referenz-Publikation, `SourceText._lastLineNumber` dokumentierter Hint, `Location._normalizedFilePath`
idempotentes `??=`); die Symbol-Builder sind rein instanzlokal, keine mutablen Statics.

**Messung** (gleicher Korpus/Harness wie §6, Debug-Engine, 24 logische Kerne):

| Phase | sequenziell (§6-Nachmessung) | parallel | Faktor |
|---|---:|---:|---:|
| A) Kaltscan | 2.093–2.255 ms | 1.061–1.195 ms | ~2× |
| B) Warmscan ohne Tier 2 | 728–1.086 ms | 455–686 ms | ~1,6× |
| C) Tier-2-Füllscan (`NavWorkspaceCore`) | 1.623–1.650 ms | 920–986 ms | ~1,7× |
| C) Warmscan Treffer | 28–47 ms | 8–36 ms | ≈ unverändert |

Bekannter, benigner Effekt: beim **parallelen** Füllscan können mehrere Builder dasselbe Include
gleichzeitig parsen (Tier 1 hält dann eine andere Instanz als der Snapshot) — im ersten Warmscan danach
werden bis zu ~2 % der Units einmalig neu gebaut, ab dem zweiten sind es wieder 1913/1913 Treffer. Die
Konvergenz ist genau das §4-Design.

**Skalierungs-Grenze = GC:** Der Bau ist allokationslastig; mit Workstation-GC (Auslieferungszustand
der Hosts) bleibt die Skalierung bei ~2×. Ein Probelauf mit **Server-GC** (`DOTNET_gcServer=1`) drückte
den Kaltscan auf **574 ms (~4×)**. Server-GC für `nav.lsp`/`nav.mcp` (net10, DATAS macht den
Speicher-Overhead adaptiv) ist damit ein bekannter, **nicht aktivierter** Folge-Hebel — bewusste
Entscheidung steht aus.
