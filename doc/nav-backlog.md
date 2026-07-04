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
- [ ] **Optional:** `LanguageVersion` in `nav_outline` / `nav_workspace` ausweisen.

## 2. Completion-Verfeinerungen (optional)

Quelle: `doc/nav-completion-status.md`.

- [ ] **C4 — baumbasierte Suppression** — zeilenbasierten `IsInQuotation`/`IsInTextBlock`-Scan durch
  baumbasierte Erkennung ersetzen. Regressionsgefahr; bewusst optionaler Folgeschritt.
- [ ] **Node-Deklarations-„Schwanz"** fällt aufs pauschale `Fallback` (Rauschen), obwohl grammatisch nur
  noch `;` (bzw. `do` beim init-Knoten) folgt → im Node-Tail `Suppress`.
- [ ] **Singleton-Code-Deklarationen** (`code`, `base`, `generateto`, `params`, `result`,
  `namespaceprefix`) werden erneut angeboten, obwohl am Wirt schon vorhanden → herausfiltern (braucht
  Wirt-Zustand).

## 3. LSP-Restpunkte

Quelle: `doc/nav-lsp-status.md §4`.

- [ ] **VS-`GoToSymbolBuilder` ruft den Engine-Kern noch nicht auf** — „eine Engine" ist für VS-GoTo noch
  nicht real (nur der Server nutzt den Kern). Einziger echter Architektur-Punkt.
- [ ] **`workspace/didChangeWorkspaceFolders`** (die übrigen Watch-Events sind erledigt).
- [ ] **Optional:** inkrementeller Doc-Sync statt Full; Severity-Mapping `Suggestion → Hint` (Geschmack).

## 4. MCP-Erweiterungen (optional)

Quelle: `doc/nav-mcp-status.md §5`.

- [ ] **Whole-File-Modus** für `nav_code_actions` (alle Fixes einer Datei ohne Symbolname).
- [ ] **`apply`-Flag** für die (derzeit read-only) mutierenden Tools.

## 5. Manuelle Verifikation offen

Quelle: `doc/nav-skiped-tokens-as-trivia.md`.

- [ ] **VS-Extension:** `init [];` öffnen — Klammern weiterhin rot, Tooltip korrekt?
- [ ] **LSP-Smoke:** Diagnose (Squiggle) für `init [];` unverändert?
