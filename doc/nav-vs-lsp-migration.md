# Nav VS-LSP-Migration — Arbeitsdokument

> **Status:** Sondierung/Machbarkeit abgeschlossen, noch keine Umsetzung. Lebendes Dokument, wird in
> Folge-Sessions ausgebaut. Bezug: [nav-lsp-status.md](nav-lsp-status.md), [nav-mcp-status.md](nav-mcp-status.md).

## 1. Ziel

Den bestehenden, fertigen **`nav.lsp`**-Server (stdio, StreamJsonRpc) **in die Visual-Studio-IDE**
einbinden — statt die Editor-Features weiter in der nativen VS-Extension (`Nav.Language.ExtensionShared` +
`Nav.Language.Extension2026`) doppelt zu pflegen. Leitidee bleibt „**eine Engine, mehrere Hosts**": der
LSP ist das Gehirn, VS wird ein weiterer dünner Host — analog zum VS-Code-Client (`vscode-nav-lsp`) und
zum MCP.

Der eigentliche Antrieb: Das **Overlay-/Change-Tracking** des LSP (unsaved-Puffer als Overlay über
Platte, Datei-Invalidierung) ist der große Trumpf. Es in der nativen Extension nachzubauen wäre teuer —
also lieber den LSP anbinden und den Trumpf wiederverwenden.

## 2. Herkunft / Kontext

Der Strang entstand aus einer **Perf-Analyse** (Syntax/Semantic Model/Formatter, Korpus `d:\tfs\main`,
1913 `.nav`; Details in `scratchpad/PERF-BEFUND.md`, Setup-Memory `nav-perf-profiling-setup`). Dabei fiel
auf, wie zentral die **Provider-/Caching-Schicht** (`OverlaySyntaxProvider`, `CachedSyntaxProvider`,
`NavSolution`) für Performance **und** Korrektheit ist — und dass der LSP diese Schicht bereits
vollständig und invalidierungs-sicher nutzt, die native VS-Extension aber nur teilweise.

**Offener Perf-Nebenbefund (nicht Teil dieses Docs, aber nicht vergessen):** Der Build-Pfad
(`Pharmatechnik.Nav.Language.targets:16`) hat `NavUseSyntaxCache` per Default `false` → Includes werden
je `.nav` neu geparst. Ein geteilter `CachedSyntaxProvider` (Maschinerie existiert: `SyntaxProviderFactory.Cached`)
ist ~6,7× schneller im Semantic Model, reiner Perf-Schalter ohne Ausgabeänderung. Kandidat für einen
separaten, kleinen PR (Build-Default → `true`, CLI-Default optional). LSP/MCP sind bereits cached.

## 3. Ist-Stand: Provider-Verdrahtung je Host (verifiziert)

| Host / Pfad | Provider | Cached / invalidiert? |
|---|---|---|
| **LSP + MCP** (`NavWorkspaceCore`) | `OverlaySyntaxProvider` | **Ja** — Overlay bei `didOpen/didChange`, Invalidierung bei Datei-Änderung |
| `NavSolution` direkt | `new CachedSyntaxProvider()` | Ja |
| **VS solution-weit** (`NavSolutionProvider`, z.B. Call Hierarchy) | `NavSolution` → `CachedSyntaxProvider` | Ja (hat `IVsHierarchyEvents`) |
| **VS pro Editor-Buffer** (`SemanticModelService`) | `FromCodeGenerationUnitSyntax(syntax, ct)` | **Nein** — uncachter Komfort-Overload |
| **Nav.Cli / Build** | `-c`/`NavUseSyntaxCache` (Default aus) | Nein (Default) |

Kernpunkt für die Migration: Die **Cache-/Overlay-Maschinerie liegt im Engine-Kern**
(`Nav.Language/Workspace/`, Namespace `Pharmatechnik.Nav.Language`) — der LSP hat sie **nicht gebaut,
sondern konsumiert sie nur**. Die VS-Extension nutzt sie bereits für solution-weite Features; nur der
pro-Buffer-Editor-Pfad geht am Cache vorbei.

## 4. Recherche-Ergebnis: LSP in solution-basiertem VS (zitiert)

Deep-Research (20 Quellen, 25 Claims adversarial verifiziert, 18 bestätigt / 7 widerlegt). Volle zitierte
Fassung: `scratchpad/VS-LSP-RECHERCHE.md`.

### Bestätigt

- **Klassischer In-Proc-`ILanguageClient`** (`Microsoft.VisualStudio.LanguageServer.Client`, v17.14.60)
  funktioniert im **normalen Solution-Modus**. Aktivierung **rein über Content-Type**
  (`ContentTypeDefinition` + `FileExtensionToContentTypeDefinition`, MEF-Export), **kein Projektsystem
  nötig**. → [adding-an-lsp-extension](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)
- VS treibt den Lebenszyklus (`ActivateAsync` → `Connection` → VS macht `initialize`/`initialized`
  selbst → `OnServerInitializedAsync`).
- **VS besitzt den In-Memory-Puffer und macht `didOpen`/`didChange`/`didClose` automatisch, inkl.
  ungespeicherter Inhalte** — „the truth about the contents … is no longer on the file system but kept
  by the tool in memory". → **Overlay/Change-Tracking für offene Dateien gibt es geschenkt.**
  [language-server-protocol](https://learn.microsoft.com/en-us/visualstudio/extensibility/language-server-protocol)
- LSP-Support ist auf **Open Folder ausgerichtet** (Default-`rootUri` = Ordner; Settings/Tracing
  open-folder-only) — aktiviert aber im Solution-Modus; ordner-zentrische Defaults brauchen eigene
  Init-Logik.
- **Fester Nachrichten-Subset des klassischen Clients.** Unterstützt: didOpen/didChange/didClose/didSave,
  publishDiagnostics, completion, hover, signatureHelp, references, documentHighlight, documentSymbol,
  formatting, rangeFormatting, definition, codeAction, rename, workspace/symbol.
  **NICHT: `textDocument/codeLens`**, codeLens/resolve, documentLink, onTypeFormatting, willSave,
  telemetry, **`client/registerCapability`** (keine dynamische Registrierung → Capabilities statisch).
- **MS rät ab**, LSP **neben** nativem Language Service für denselben Content-Type zu betreiben →
  nativen `.nav`-Dienst für diesen Content-Type **stilllegen**, nicht parallel.
- `FilesToWatch` → `workspace/didChangeWatchedFiles` (glob) vorhanden; `InitializationOptions`/
  `ConfigurationSections` exponiert.
- Out-of-Proc-Alternative `VisualStudio.Extensibility.LanguageServerProvider` (VS 2026) existiert, hat
  aber Lücken (u.a. **kein** `workspace/configuration`).
- Server-Lebensdauer an die Solution gebunden (`shutdown`/`exit` beim Schließen, by design).

### Widerlegt (Mythen — nicht glauben)

- „VS-LSP nur für Open Folder, bricht mit Projektsystem" → **0-3 widerlegt**.
- „Nativer Language Service unterdrückt LSP-Routing (nur didOpen/didClose erreichen den Server)" →
  **0-3 widerlegt** (zweifach). Koexistenz ist ein Qualitäts-, kein Sperrthema.
- „Spurios `didClose` direkt nach `didOpen`" → widerlegt.

## 5. Architektur-Erkenntnisse

### 5a. VS-Feature-Lücken sind keine Decke — Self-Serve via Custom-RPC

Der feste Nachrichten-Subset ist nur das, was VS *automatisch* verdrahtet. Über
**`ILanguageClientCustomMessage2.AttachForCustomMessageAsync(JsonRpc rpc)`** bekommt man die
**JsonRpc-Verbindung selbst** (StreamJsonRpc, nebenläufige Requests möglich) und kann **jeden**
LSP-Request selbst absetzen — auch `semanticTokens`/`codeLens`, die VS nativ nicht anzeigt — und daraus
einen **VS-Tagger/Classifier/Adorner** bauen. Der LSP bleibt das Gehirn; VS-seitig nur ein dünner Adapter.

Vorbehalte: (1) für nicht-durchgereichte Features schreibt man weiterhin das **VS-Editor-Plumbing**
(Tagger etc.), nur eben LSP-gespeist, nicht mit dupliziertem Parser; (2) **Snapshot-/Versions-Skew**
(async LSP-Antwort vs. konkreter `ITextSnapshot`) muss behandelt werden (LSP-Ranges → Snapshot-Spans,
Neu-Anfrage bei `TagsChanged`). (3) Dieser Custom-RPC-Kanal ist der **Dreh- und Angelpunkt** und in der
Recherche **nicht bestätigt** → zuerst absichern.

### 5b. Workspace-Root + Change-Tracking ohne Folder-Mode

Wir machen uns von VS' Folder-Orientierung **unabhängig**:

- **Offene Dokumente:** `didOpen`/`didChange`/`didClose` laufen im Solution-Modus automatisch (bestätigt,
  content-type-getrieben). Das ist der Overlay-/unsaved-Teil.
- **Workspace-Root:** **nicht** auf VS' `rootUri` verlassen. Die Engine entdeckt ihren Solution-Root
  selbst aus einem Dateipfad (`NavSolution.FromDirectory`, läuft die Hierarchie hoch); zusätzlich kann
  der VS-Host den Solution-Pfad über **`InitializationOptions`** explizit mitgeben (kennt ihn via
  `NavSolutionProvider`/`IVsSolution`). Doppelt abgesichert, folder-unabhängig.
- **Geschlossene Include-Dateien:** kein `didChange`. Sauberster Weg: der **Server überwacht seinen
  selbst-entdeckten Root per `FileSystemWatcher`** (er liest Includes ohnehin von Platte). Genau das
  modelliert der `OverlaySyntaxProvider` schon: **Overlay (offen, von VS) über Platte (geschlossen, vom
  Server überwacht)**. Cross-File-Frische hängt damit an keiner VS-Folder-Funktion.

### 5c. Koexistenz / Rückbau

Nativen `.nav`-Dienst für den Content-Type stilllegen (MS-Empfehlung). Wie viel stillgelegt werden kann,
hängt an Punkt 6.1 (welche Features VS durchreicht). Classification/Folding ggf. via Self-Serve-Tagger
(5a) statt nativ — oder nativ belassen, wenn der Aufwand größer als der Nutzen ist.

## 6. Offene Fragen

1. **Reicht VS `foldingRange`, `semanticTokens`/Classification, `callHierarchy`, `inlayHint` an die UI
   durch?** (Fehlen in der bestätigten Support-Tabelle.) Entscheidet den Rückbau-Umfang. Primärquelle.
2. Was kommt im **Solution-Modus** exakt bei `initialize` an (`rootUri`/`workspaceFolders`/
   `InitializationOptions`)? Empfohlenes Custom-Init-Muster? — **empirisch im Spike**.
3. Funktioniert der **Custom-RPC-Kanal** (`ILanguageClientCustomMessage2`) zuverlässig im Solution-Modus?
   — Primärquelle + **Spike**. Linchpin für 5a **und** die Roslyn-Brücke.
4. **In-Proc `ILanguageClient` vs. Out-of-Proc `LanguageServerProvider`** für VS 2026? Zielkonflikt:
   In-Proc gäbe **Roslyn-Workspace-Zugriff** (für Nav↔generierter-C#-Navigation), Out-of-Proc isoliert
   aber ohne `workspace/configuration`.
5. **Roslyn-Brücke** (`Nav.Language.CodeAnalysis`: Nav ↔ generierter C#): ein stdio-LSP sieht den
   Roslyn-Workspace nicht. Bleibt nativ, oder via In-Proc-LSP + Custom-Requests? Der harte Brocken.

## 7. Nächster Schritt: Spike (de-riskt 6.2 + 6.3 auf einmal)

Minimaler `ILanguageClient` im VSIX, der:
- `nav.lsp` als Prozess startet und die `Connection` (stdio-Streams) zurückgibt,
- den Content-Type `.nav` per MEF (`ContentTypeDefinition` + `FileExtensionToContentTypeDefinition`,
  Basis `CodeRemoteContentDefinition.CodeRemoteContentTypeName`) registriert,
- das ankommende `initialize`-JSON **loggt** (Frage 6.2),
- **Diagnostics** zeigt (nativer Auto-Pfad) **und**
- **Classification** per selbst abgesetztem `textDocument/semanticTokens/full` in einen Test-Tagger holt
  (Custom-RPC-Beweis, Frage 6.3).

Wird im **reinen Solution-Modus** (kein Open Folder) getestet, neben bzw. mit stillgelegter nativer
Extension. Danach ist empirisch klar, ob die Strategie trägt.

## 8. Quellen (Auswahl)

- Adding an LSP extension — https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension
- Language Server Protocol (VS) — https://learn.microsoft.com/en-us/visualstudio/extensibility/language-server-protocol
- `ILanguageClient` API — https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.languageserver.client.ilanguageclient
- LanguageServerProvider (Out-of-Proc) — https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/language-server-provider/language-server-provider
- VSExtensibility #426 (workspace/configuration-Lücke) — https://github.com/microsoft/VSExtensibility/issues/426
