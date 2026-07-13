# Nav-MCP — Agenten-Gap-Analyse

**Stand:** 2026-07-13 · **Status:** Analyse / Input für eine spätere Ausbau-Session.
Schwester-Dokumente: `doc/nav-mcp-status.md` (Tool-Wahrheit), `doc/nav-backlog.md` (offene Punkte),
`doc/nav-diagram-status.md` (Graph-Feature), `doc/nav-lsp-status.md`.

> **Frage, die dieses Dokument beantwortet:** Wenn ein KI-Agent (Claude Code) künftig möglichst viel
> Nav-Arbeit **über den MCP** erledigen soll — welche Funktionalität muss der MCP bieten, wo ist er
> bereits vollständig, und bietet er womöglich zu viel? Blickwinkel ist ausdrücklich der **Agent**,
> nicht der Editor-Nutzer.

---

## 0. Ausgangslage (zuerst, weil entscheidend)

Der Repo ist als **„eine Engine, mehrere Hosts"** gebaut: die VS-freie Sprachlogik liegt in
`Nav.Language`, darüber sitzen **VS-Extension, LSP, MCP und CLI (`nav.exe`)**. Ein **MCP-Server
existiert bereits** (`Nav.Language.Mcp`, Assembly `nav.mcp`) mit **11 Tools** — der Ausbau *ergänzt*,
er beginnt nicht bei Null.

**Voraussetzung Nr. 0 (Verdrahtung).** Damit ein Agent überhaupt „über den nav-MCP abfrühstücken"
kann, muss `nav.mcp.exe` als MCP-Server registriert und auf den **Workspace-Root des konsumierenden
Projekts** gezeigt werden (bei uns der IXOS-Monorepo-Root, **nicht** dieser Parser-Repo — dort liegen
keine produktiven `.nav`). Ohne diese Anbindung ist die Tool-Qualität irrelevant.

---

## 1. Was der MCP heute bietet (11 Tools)

Bewusste Designlinie: **name-basiert** (ein Agent hat keinen Cursor), **read-only** (mutierende Tools
liefern nur das Edit-Set), **gepaged** (Token-Limit ~25k pro Tool-Result).

| Phase | Tool | Zweck |
|---|---|---|
| **Discovery** | `nav_workspace` | Alle `.nav` der Solution (relativ+absolut), gefiltert/gepaged |
| | `nav_find_symbol` | Solution-weite **Präfix-Suche nach Definitionen** ohne Datei vorab (der Einstieg) |
| | `nav_outline` | Struktur einer Datei: Tasks + Knoten (Art/Position) + `languageVersion` |
| | `nav_goto` | Name → Definition(en), folgt `taskref`-Includes cross-file |
| | `nav_references` | Alle Vorkommen solution-weit (inkl. Deklaration) |
| **Diagnose** | `nav_validate` | Eine Datei validieren → Diagnostics inkl. Cross-File (nach jedem Edit) |
| | `nav_diagnostics` | **Workspace-weit** aggregiert, `severity`/`filter`, gepaged |
| **Edit/Refactor** | `nav_rename` | Umbenennungs-Edit-Set (file-local) |
| | `nav_code_actions` | Anwendbare QuickFixes/Refactorings + Edit-Set |
| | `nav_format` | Formatierung: Edit-Set **+ kompletter formatierter Text** |
| **Referenz** | `nav_grammar` | EBNF der Sprache (gesamt/Einzelregel), zustandslos |

**Bewertung: sauber kuratiert, kein Wildwuchs.** Discovery- und Diagnose-Phase sind vollständig
abgedeckt. `nav_validate`/`nav_diagnostics` sind dem bisherigen Agenten-Weg („`nav.exe -v` laufen
lassen, Warnungen lesen") **überlegen**: fresh pro Datei, **kein Build**, exakt der Nav-Fehlercode-
Katalog.

---

## 2. Deckungsgrad gegen die Agenten-Workflows (Skill `nav`)

| Workflow | MCP-Deckung |
|---|---|
| Discovery vor Änderung (MANDATORY) | ✅ **voll** (outline/find_symbol/references/goto/workspace) |
| Validierung nach Edit (Nav-Diagnostics) | ✅ **voll**, besser als CLI |
| Grammatik-/Syntax-Nachschlag | ✅ `nav_grammar` — *dupliziert aber die Skill-Spec, s. §5* |
| `.nav` mechanisch umformen (Rename/Format/Fix) | 🟡 **teilweise** — Edit-Sets, kein Disk-Write, kein „add node/edge" |
| **Sehen, welcher C# generiert wird** | ✅ **umgesetzt** — `nav_preview_codegen` (in-memory, ohne Build) |
| **Graph / Visualisierung** (Reachability, Diagramm) | ❌ **fehlt** — Engine-Modell entworfen, nicht implementiert |
| Call-Hierarchy / transitiv erreichbare Tasks | ❌ **fehlt im MCP** (Engine kann es, LSP hat es) |
| GD↔NAV-Event-Mapping / WFS-Params | ⬜ **außerhalb nav-MCP** — Roslyn-MCP auf der generierten C#-Seite |

---

## 3. Lücken — priorisiert

### ✅ A. Codegen-Preview — UMGESETZT (`nav_preview_codegen`)

Der gesamte Nav-Nutzwert für den Agenten dreht sich um **„welcher C# entsteht"**: Begin-Overloads,
`After<Node>`, exakte Event-Methodennamen, und vor allem die **transitiv erreichbaren DI-Parameter**
je Logic-Methode. Bisher nur über `nav.exe` + Build + `*.generated.cs`-Lesen verifizierbar — teuer,
langsam, buildabhängig.

**Umgesetzt** als `nav_preview_codegen(path, task?, includeUserFiles?, includeContent?, nullableContext?,
projectRoot?)`: generiert **in-memory** gegen die frisch von Platte gelesene `CodeGenerationUnit` über
**dieselbe** Pipeline wie `nav.exe`/MSBuild (`CodeGeneratorProvider.Default` →
`VersionDispatchingCodeGenerator` → V1/V2), **ohne** `IFileGenerator` — kein Plattenschreiben, kein Build.
Ergebnis je Task-Definition: die Artefakte mit **Rolle** (`base`/`iwfs`/`ibegin`/`user`/`to`),
Ziel-Dateiname, Zeilen-/Zeichenzahl, `OverwritePolicy` und (optional) Inhalt. Fehler-Gate wie der
Generator (Datei mit Fehlern → `error` + `diagnostics` statt Codegen), Benutzer-Stubs standardmäßig aus,
Token-Budget über `includeContent`/`task`/`contentOmitted`. Details: `doc/nav-mcp-status.md` §3.

> **Ausbaustufe offen:** die *kompakte* Signaturfläche (nur Interface-Member, abstrakte Logic-Signaturen
> inkl. DI-Params, NodeName-Konstanten statt Volltext). Die aktuelle Umsetzung liefert den generierten
> C# je Artefakt; die verdichtete Sicht ist ein späterer, additiver Modus.

### 🟠 B. Graph / Reachability — teilweise UMGESETZT

Beides von der Engine bereits getragen (`EdgeExtensions.GetReachableCalls`, `IEdge`/`EdgeMode`,
`INodeSymbol.IsReachable`, `NavCallHierarchyService`):

- **`nav_call_hierarchy` ✅ UMGESETZT** — Aufrufbeziehungen auf **Task-Ebene**: ausgehend (welche Tasks
  ruft X auf) und eingehend (welche Tasks rufen X solution-weit auf), cross-file via `taskref`. Für
  Impact-Analyse cross-task („was bricht, wenn ich diese Task ändere"). Wiederverwendung des VS-freien
  Engine-Kerns `NavCallHierarchyService` (dieselbe Basis wie die LSP-Call-Hierarchy,
  `Nav.Language.Lsp/CallHierarchy/`), **name-basiert** statt cursor-basiert. Details:
  `doc/nav-mcp-status.md` §3.
  - **Nebenbefund/Fix:** `NavSolution.ProcessCodeGenerationUnitsAsync` deduplizierte Dateien
    **case-sensitiv** (`HashSet<string>` mit `// TODO File/Path comparer`); bei abweichender Pfad-
    Schreibweise (normalisierter `startingUnit`-Pfad vs. Original-Casing der `SolutionFiles`) wurde
    dieselbe Datei doppelt verarbeitet → doppelte Referenzen/Aufrufe. Auf `OrdinalIgnoreCase` umgestellt.
- **`nav_diagram(path)`** — layout-freies Knoten/Kanten-Modell (oder direkt Mermaid/HTML). Laut
  `doc/nav-diagram-status.md` ist das Engine-Modell (S1) entworfen; das MCP-Tool ist danach „trivial"
  (dort §8 „Optionale Erweiterungen: MCP-Tool `nav_diagram`"). Ersetzt manuelle Mermaid-Handarbeit.
  **Noch offen.**
- **Intra-Task-Node-Reachability** („welche Knoten erreicht Init/Choice X transitiv" =
  `GetReachableCalls`) ist als eigenes Tool **nicht** umgesetzt — die daraus folgende DI-Param-Menge je
  Logic-Methode ist bereits über `nav_preview_codegen` (Basisklasse) sichtbar; niedrige Priorität.

### ⚪ C. Schreibender Pfad + BOM-Kapselung — VERWORFEN (2026-07-13)

Ursprünglich als optionales **`apply`-Flag** bzw. `nav_apply_edits`-Tool gedacht. Zwei denkbare
Schreibpfade, beide nach Prüfung verworfen:

- **Codegen materialisieren (`.cs` schreiben):** Wo die generierten Dateien landen und wie ihre
  Namespaces heißen, ist **vollständig build-gesteuert** — über `NavProjectRootDirectory`/
  `NavWflRootDirectory`/`NavIwflRootDirectory` (+ `NavGenerate*`, `NavNullableContext`, Inkremental-
  Manifest) in `Pharmatechnik.Nav.Language.targets`, deren Werte die **aufrufende Seite** setzt
  (IXOS: `build/Script/IxosCompile.targets`). Ein MCP-Tool kennt diesen Kontext nicht und würde die
  Autorität über Ablage + Inkremental-Tracking umgehen. Gehört in den Build, nicht in den MCP.
- **`.nav`-Quelle in-place schreiben (Edit-Sets anwenden):** Nutzen zu schmal — die Ablage ist
  unkritisch (exakt die übergebene Quelldatei), einziger Mehrwert wäre die garantierte BOM-Kodierung.
  Die kann der Agent selbst leisten; die Kapselung spart eine Fehlerquelle, ist aber kein neues Können.
  Die mutierenden Tools bleiben bewusst **read-only** (Edit-Sets), die Designlinie bleibt intakt.

### ⚪ D. Kleinere Backlog-Punkte

- **Whole-File-`nav_code_actions`** (alle Fixes einer Datei ohne Symbolname) — bereits im Backlog.
- **Voll-Text-Ausgabe auch bei `nav_rename`/`nav_code_actions`** (wie `nav_format` sie hat): Edit-Sets
  präzise anzuwenden ist für einen Agenten fehleranfällig; das Status-Doc erkennt das für `nav_format`
  bereits an.

---

## 4. „Zu viel"? — Nein, zwei kleine Anmerkungen

Echten Überhang gibt es nicht. Reine Editor-UI (**Completion, Hover/QuickInfo, Semantic Tokens,
Folding, CodeLens, DocumentHighlight**) wurde bewusst **ausgelassen** — für einen Agenten wertlos, die
richtige Entscheidung (`nav-mcp-status.md §5`).

- **`nav_goto`** ist funktional weitgehend von `nav_find_symbol` + `nav_outline` gedeckt (beide tragen
  die Location schon). Eigene Nische: Cross-`taskref`-Include-Auflösung → behalten, aber schwächster
  eigenständiger Nutzen der elf.
- **`nav_grammar`** ist billig und statisch (kein MCP-Problem), **dupliziert** aber große Teile des
  Skills — s. §5.

---

## 5. Seitenblick Skill (folgt aus dem MCP-Bild)

Der Skill `nav` trägt aktuell die **volle Grammatik + alle Codegen-Regeln** inline (~1350 Zeilen).
Sobald `nav_grammar` (existiert) und ein `nav_preview_codegen` (Lücke A) autoritativ liefern, kann der
Skill auf das schrumpfen, was der MCP *nicht* liefert: **Routing, Workflow-Reihenfolge
(GD→NAV→WFS), SharedLibrary-Pfadlogik, `nav.exe`-Aufruf, BOM-Regel, Pitfalls** — und die Fakten aus
dem MCP ziehen statt sie doppelt zu pflegen. Konsequente „eine Engine als Quelle der Wahrheit"-Linie
auf der Skill-Seite.

---

## 6. Fazit

Der MCP deckt **Discovery + Diagnose bereits vollständig und ballastfrei** ab. Um „möglichst viel über
nav abzufrühstücken", fehlen vor allem **(A) Codegen-Preview**, **(B) Graph/Reachability** und
**(C) ein sicherer, BOM-korrekter Schreibpfad** — alle drei engine-seitig vorbereitet und damit gute
Kandidaten für die Ausbau-Session. Voraussetzung Nr. 0 bleibt, den nav-MCP überhaupt an die
Agenten-Session / den konsumierenden Workspace-Root anzubinden.

### Empfohlene Reihenfolge für die Ausbau-Session

1. **Voraussetzung 0** — nav-MCP an Agenten-Session + IXOS-Root anbinden (sonst nichts testbar).
2. **A** — `nav_preview_codegen` ✅ **umgesetzt** (höchster Hebel pro Aufwand; Engine-Pipeline existiert).
3. **C** — ⚪ **verworfen** (Codegen-Ablage ist build-gesteuert; `.nav`-Schreibpfad zu schmal, s.o.).
4. **B** — `nav_reachability`/Call-Hierarchy ← **nächster Schritt**; `nav_diagram` (nach Diagram-S1) danach.
5. **D** — Whole-File-`nav_code_actions`, Voll-Text bei rename/code_actions.
6. **Skill straffen** — Grammatik/Codegen-Fakten aus dem MCP ziehen.
