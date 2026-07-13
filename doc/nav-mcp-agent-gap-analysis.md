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

### 🟠 B. Graph / Reachability

Beides von der Engine bereits getragen (`EdgeExtensions.GetReachableCalls`, `IEdge`/`EdgeMode`,
`INodeSymbol.IsReachable`):

- **`nav_diagram(path)`** — layout-freies Knoten/Kanten-Modell (oder direkt Mermaid/HTML). Laut
  `doc/nav-diagram-status.md` ist das Engine-Modell (S1) entworfen; das MCP-Tool ist danach „trivial"
  (dort §8 „Optionale Erweiterungen: MCP-Tool `nav_diagram`"). Ersetzt manuelle Mermaid-Handarbeit.
- **`nav_reachability` / Call-Hierarchy** — „welche Tasks/Knoten erreicht Init/Choice X transitiv" =
  genau die DI-Param-Menge des Codegens. Für Impact-Analyse cross-task wertvoll; Blaupause ist die
  LSP-Call-Hierarchy (`Nav.Language.Lsp/CallHierarchy/`).

### 🟡 C. Schreibender Pfad + BOM-Kapselung

Ein optionales **`apply`-Flag** (bereits im Backlog, `nav-backlog.md §4`) bzw. ein
`nav_apply_edits`-Tool bringt nicht nur Komfort, sondern **kapselt die UTF-8-mit-BOM-Falle**:
BOM-lose `.nav` → der Generator transliteriert Umlaute in Identifiern (`ä→ae`) → Consumer bricht mit
`CS1061`. Ein nav-eigenes Schreib-Tool könnte garantiert BOM-behaftet schreiben — heute ein
wiederkehrendes, echtes Fehlerrisiko bei `Write`-basierten Agenten-Edits.

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
3. **C** — `apply`-Flag / BOM-korrekter Schreibpfad (eliminiert eine ganze Fehlerklasse). ← **nächster Schritt**
4. **B** — `nav_diagram` (nach Diagram-S1) + `nav_reachability`/Call-Hierarchy.
5. **D** — Whole-File-`nav_code_actions`, Voll-Text bei rename/code_actions.
6. **Skill straffen** — Grammatik/Codegen-Fakten aus dem MCP ziehen.
