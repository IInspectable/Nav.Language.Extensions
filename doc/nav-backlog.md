# Nav — Offene Punkte / Backlog

> Stand: 2026-07-04, Branch `feature/nav-parser`. Sammel- und Priorisierungsliste der noch offenen
> Arbeiten. Detailwahrheit steht jeweils im verlinkten Statusdokument — hier nur die Kurzfassung zum
> Draufschauen und Bearbeiten. Erledigtes wandert raus (oder wird abgehakt), nicht kommentiert.

## Kontext

- Parser-Umbau „Kolibri", Nullable-Kampagne, Grammatik-Export, Versionierung, ReSharper-Sweep sind
  **abgeschlossen** — siehe die jeweiligen Statusdokumente.
- Was hier steht, zerfällt in **ein echtes Feature-Cluster** (Pragmas/Versionierung) und ansonsten
  **Politur/Optional**. Der einzige nennenswerte Architektur-Rest ist der VS-GoTo-Kern.

---

## 1. Pragmas / `#version` — größte inhaltliche Restbaustelle

Quelle: `doc/nav-pragmas-versioning-status.md` (Roadmap), `doc/nav-directive-subparser.md`.

- [ ] **Erstes echtes v2-Feature einziehen** — Wert in `NavLanguageFeature`, Mindestversion in
  `NavLanguageFeatures.RequiredVersion`, `ReportIfUnavailable(...)` im Semantik-Lauf. Erst damit wird
  `Nav5000` (Feature nicht verfügbar) real ausgelöst; die ganze Versions-Gate-Maschinerie ist sonst
  ungenutzt.
- [ ] **`#pragma warning disable <NavXXXX>`** (heißer Kandidat) — `WarningDirectiveSyntax` (analog
  `VersionDirectiveSyntax`) + Diagnose-Filterschicht als Cross-Run-Pass + Ebene-2-Lexing-Entscheidung
  (Komma/Zweitwörter als eigene Token vs. Text-Matching).
- [ ] **QuickInfo/Hover auf Direktiven.**
- [x] **Code-Fix** zu `Nav3002` / `Nav3003` / `Nav5001` — **komplett** (Steps 2–5). `Nav5001` →
  `SetSupportedLanguageVersionCodeFix`, `Nav3002` → `SetValidLanguageVersionCodeFix`, `Nav3003` →
  `MoveVersionDirectiveToTopCodeFix` (Direktive an den Dateikopf verschieben bzw. entfernen). Alle drei
  laufen über den LSP (`NavCodeActionService`) **und** als VS-Lightbulb (SuggestedActionProvider in
  `Nav.Language.ExtensionShared/CodeFixes/`). Status: `doc/nav-version-codefixes-status.md`.
- [ ] **Generische Direktiven** (`#region`, `#if`) — Weg-B-Fundament steht; offen: Placement-Regel lockern
  (Direktiven überall zulassen) + je Direktive ein Sub-Parser-Zweig samt Knotentyp.
- [x] **Lexer-LF-Fallstrick** — `ScanPreprocessor` terminiert jetzt bei jedem Zeilenende (LF/CR/NEL/LS/PS),
  nicht mehr nur `\r\n`; `inTextMode`-Sonderfall entfernt. Verhaltensneutral für CRLF. Tests:
  `LanguageVersionTests.LfTerminatedDirective_*`. Detail: `doc/nav-pragmas-versioning-status.md`.
- [x] **Optional:** `LanguageVersion` in `nav_outline` ausweisen — `NavOutlineResult` trägt jetzt
  `languageVersion` (numerisch) + `hasVersionDirective` (explizit vs. Default), gemappt aus
  `unit.LanguageVersion` / `unit.Syntax.LanguageVersionDirective`.
- [ ] **Optional:** dasselbe für `nav_workspace` — bewusst offen: das Tool listet Pfade **ohne** Parse; die
  Version je Datei zu ermitteln erforderte ein Parse/`#version`-Lesen pro Eintrag und steht im
  Spannungsverhältnis zum Paging-/Token-Budget.

## 2. Completion-Verfeinerungen (optional)

Quelle: `doc/nav-completion-status.md`.

- [x] **C4 — baumbasierte Suppression** — **additiv umgesetzt** (kein Vollersatz): Der Baum trägt nur
  für wohlgeformte, an einem Wirt hängende Code-Blöcke sauber (`CodeSyntax`-Knoten, auch mehrzeilig);
  malformte/übersprungene Blöcke liegen in `SkippedTokensTrivia`, unterminierte Strings bilden kein
  `StringLiteral`-Token. `Classify` unterdrückt Code-Block-Inhalt jetzt baumbasiert (`InCodeBlock`)
  **ODER** über den bisherigen Zeilen-Scan → mehrzeilige wohlgeformte Blöcke gefixt, Recovery-/String-
  Fälle unverändert. Test `InMultilineCodeBlock_OffersNothing`. Detail: `doc/nav-completion-status.md`
  (Workstream C, C4).
- [x] **Node-Deklarations-„Schwanz"** — **umgesetzt**: hinter Schlüsselwort/Name einer schlüsselwort-eingeleiteten
  Knoten-Deklaration (`exit e ▸`, `choice c ▸`, `dialog d ▸`, `view v ▸`, `task Sub ▸`) liefert `Classify` jetzt
  `Suppress` statt des pauschalen `Fallback`; beim init-Knoten (ohne bereits vorhandene `do`-Klausel) der neue
  Kontext `InitNodeTail` → nur das `do`-Keyword. Grammatik-Autorität: neuer Helfer
  `NavCompletionContext.EnclosingNodeDeclaration`. Tests: `InitNodeTail_*`, `NodeDeclarationTail_*`. Detail:
  `doc/nav-completion-status.md`.
- [x] **Singleton-Code-Deklarationen** (`code`, `base`, `generateto`, `params`, `result`,
  `namespaceprefix`) werden erneut angeboten, obwohl am Wirt schon vorhanden → **umgesetzt**: Die
  Completion filtert am Wirt bereits deklarierte Singletons heraus (alle Code-Deklarationen sind `?`,
  einzig `using` ist `*`/wiederholbar). `CodeBlockFacts.AvailableDeclarationKeywords(host, present)` als
  Grammatik-Autorität; der Wirt-Zustand kommt aus den direkten `CodeSyntax`-Kindern des Wirt-Knotens (den
  gerade bearbeiteten Block ausgenommen), getragen über `NavCompletionContext.PresentCodeKeywords`. Detail:
  `doc/nav-completion-status.md`.

## 3. LSP-Restpunkte

Quelle: `doc/nav-lsp-status.md §4`.

- [x] **VS-`GoToSymbolBuilder` ruft den Engine-Kern auf** — **umgesetzt**: Die Nav→Nav-Zielauflösung
  läuft jetzt für alle vier Zweige (Include, Task-Node, Node-Referenz, Exit-Referenz) über die geteilte
  Autorität `NavGoToService.GetGoToLocations(ISymbol)` (→ `GoToTargetResolver`); VS steuert nur noch
  Präsentation (Anzeigename/Icon) und die Sprünge in den generierten C#-Code bei. Damit ist „eine Engine"
  auch für VS-GoTo real — keine parallel gepflegte Zielsemantik mehr. Verhaltensneutral (net472-Suite
  1306 grün, `NavGoToServiceTests` auf beiden TFMs grün, Solution baut).
- [ ] **`workspace/didChangeWorkspaceFolders`** — **bewusst vertagt** (Analyse 2026-07-04). Drei Gründe:
  (1) Das Protokoll-Paket **17.2.8 kennt Workspace-Folders gar nicht** (weder `InitializeParams.workspaceFolders`,
  noch der `ServerCapabilities.workspace`-Zweig, noch ein `didChangeWorkspaceFolders`-DTO/`Methods`-Konstante) →
  alle Formen müssten von Hand nachgerollt werden (wie `callHierarchyProvider` in `NavServerCapabilities`).
  (2) Der **Engine-Kern ist strukturell Single-Root**: `NavWorkspaceCore` hält genau **eine** `NavSolution` mit
  **einem** `SolutionDirectory`; echtes Multi-Root berührt `NavWorkspaceCore`, `NavSolution` und das
  `.navignore`-Laden — kein reiner Host-Handler. (3) **Kein akuter Leidensdruck**: Der `vscode-languageclient`
  füllt `rootUri` aus Rückwärtskompatibilität immer mit dem *ersten* Workspace-Ordner (auch im Multi-Root-Fall) →
  der Server lädt heute schon (nur eben nur diesen ersten Ordner), statt still leer zu bleiben. Ein *minimaler*
  Umbau (Fallback „erster Ordner", Notification annehmen, Voll-Reload vom primären Root) wäre beim VS-Code-Client
  praktisch ein **No-Op** (der gepinnte `rootUri` ändert sich nicht) und brächte keinen sichtbaren Nutzen; echten
  Mehrwert gäbe nur **volles** Multi-Root (Union aller Wurzeln). Wieder aufgreifen, wenn ein konkreter
  Multi-Root-Bedarf entsteht oder das Protokoll-Paket auf eine Version mit nativer Folder-Unterstützung gehoben wird.
- [ ] **Optional:** inkrementeller Doc-Sync statt Full; Severity-Mapping `Suggestion → Hint` (Geschmack).

## 4. MCP-Erweiterungen (optional)

Quelle: `doc/nav-mcp-status.md §5`.

- [ ] **Whole-File-Modus** für `nav_code_actions` (alle Fixes einer Datei ohne Symbolname).
- [ ] **`apply`-Flag** für die (derzeit read-only) mutierenden Tools.

## 5. Manuelle Verifikation offen

Quelle: `doc/nav-skiped-tokens-as-trivia.md`.

- [ ] **VS-Extension:** `init [];` öffnen — Klammern weiterhin rot, Tooltip korrekt?
- [ ] **LSP-Smoke:** Diagnose (Squiggle) für `init [];` unverändert?
