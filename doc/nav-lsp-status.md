# Nav-LSP — Status & Handoff

Stand-Dokument für die Weiterarbeit am LSP-Server (Branch `feature/nav-lsp`,
Worktree `D:\git\Nav.Language.Extensions-nav-lsp`). Ergänzt die ursprüngliche Plandatei
`C:\Users\mnhaenel\.claude\plans\mache-dir-mal-ein-enumerated-kazoo.md` (Bewertung + 3-Phasen-Fahrplan).

> **Nächste Session-Aufgabe:** Option **B** analysieren — die Nav→Nav-GoTo-Auflösung aus dem
> VS-Extension-Layer in die Engine (`Nav.Language`) extrahieren, damit VS-Extension **und**
> LSP-Server dieselbe Logik nutzen. Details unten unter „Option B".

---

## 1. Build / Run / Test — Fallstricke zuerst

- **Engine baut nur mit Full-Framework `MSBuild.exe`, NICHT `dotnet build`.** Grund: `CodeTaskFactory`
  in `Nav.Language\CustomBuild.targets` (StringTemplate-Export). `dotnet build` → Fehler MSB4801.
  ```
  "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" \
      Nav.Language.Server\Nav.Language.Server.csproj -t:Build -p:Configuration=Debug
  ```
- **Server starten (framework-dependent):** `dotnet Nav.Language.Server\bin\Debug\net10.0\Pharmatechnik.Nav.Language.Server.dll`
  (.NET-10-Runtime ist installiert).
- **Tests:**
  - net472: gebündelter NUnit-Console-Runner — `Run-Tests.bat` (`_build\NUnit.ConsoleRunner\3.8.0\tools\nunit3-console.exe`).
  - .NET 10: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0 --no-build` (Projekt ist multi-target `net472;net10.0`).
- **VS-Code-PoC:** `cd vscode-nav-lsp && npm install`, dann Ordner in VS Code öffnen → **F5**.
  **Achtung:** Solange der PoC läuft, ist die Server-DLL gesperrt → vor einem Server-Rebuild den
  Extension-Development-Host **schließen** (der LSP-Client startet den Server sonst automatisch neu).
- **URI-Test-Fallstrick:** LSP-Clients (VS Code) prozent-kodieren den Laufwerks-Doppelpunkt
  (`file:///d%3A/...`). `System.Uri.LocalPath` liefert dafür einen kaputten Pfad → siehe `NavUri`.
  Node `url.pathToFileURL` kodiert das **nicht** — Test-Harnesse müssen die `%3A`-Form explizit verwenden.

---

## 2. Erledigt (committet auf `feature/nav-lsp`)

**Phase 1 — Engine portabel** (`005c54a5`):
- `Nav.Language` + `Nav.Utilities` → `netstandard2.0` (ein Build für net472-Extension UND .NET-10-Server).
- `System.CodeDom`-Paket ergänzt (für `Microsoft.CSharp.CSharpCodeProvider.IsValidIdentifier`).
- `Nav.Language.Tests` multi-target `net472;net10.0`; Testsuite grün auf **beiden** TFMs (624 passed).
- Testdaten-Pfadauflösung tiefenunabhängig gemacht (`TestDataDirectory`).

**Phase 2 — LSP-Server** (`Nav.Language.Server`, .NET 10, `StreamJsonRpc` + `Microsoft.VisualStudio.LanguageServer.Protocol` 17.2.8):
- Lebenszyklus `initialize`/`initialized`/`shutdown`/`exit`; Dokument-Sync `didOpen`/`didChange`/`didClose` (Full-Sync).
- **Diagnostics** (`publishDiagnostics`): einzelnes Dokument + **Workspace-Discovery** (`NavSolution`, globt `**/*.nav`, Cross-File-Modell).
- **Overlay-Modell** „offenes Dokument schlägt Platte" (`OverlaySyntaxProvider`) als gemeinsamer Provider für Scan + offene Docs; **URI-Normalisierung** am Rand (`NavUri`).
- **Semantic Tokens** (`textDocument/semanticTokens/full`): reuse der Engine-`TextClassification` (`SemanticTokensBuilder`).
- **Document Symbols** (`textDocument/documentSymbol`): Outline aus dem semantischen Modell (`DocumentSymbolBuilder`).
- **VS-Code-PoC** (`vscode-nav-lsp`): startet den Server per stdio, registriert Sprache `nav`.
- Serialisierungs-Fix: Newtonsoft-Serializer camelCased unbenannte LSP-DTO-Properties (sonst PascalCase → Client verwirft z.B. die Semantic-Tokens-Legend).

**Server-Dateien** (`Nav.Language.Server/`): `Program.cs` (stdio + JSON-RPC + camelCase-Resolver + stdout-Hardening),
`NavLanguageServer.cs` (Handler + Capabilities), `NavWorkspace.cs` (Workspace + Overlay + Unit/Tree-Zugriff),
`OverlaySyntaxProvider.cs`, `NavUri.cs`, `DiagnosticsComputer.cs`, `LspMapper.cs`, `SemanticTokensBuilder.cs`, `DocumentSymbolBuilder.cs`.

---

## 3. Option B — GoTo-Resolution in die Engine extrahieren (nächste Session)

**Ziel:** Eine wiederverwendbare Nav→Nav-Navigationslogik in `Nav.Language`, die sowohl die VS-Extension
als auch der LSP-Server nutzen (Ideal „eine Engine"). Aktuell liegt GoTo nur VS-gekoppelt vor.

**Ist-Zustand (VS-gekoppelt, NICHT serverfähig):** `Nav.Language.ExtensionShared/GoToLocation/`
- `GoToLocationService.cs` (UI), `GoToTag.cs` (Tagger, `Microsoft.VisualStudio.Text.Tagging`), `LocationInfo.cs`.
- `Provider/*` (~16 Provider). Kopplung: `Microsoft.VisualStudio.Text` (`ITextBuffer`), `…Imaging` (`ImageMoniker`),
  `Microsoft.CodeAnalysis` (Roslyn) und `Nav.Language.CodeAnalysis.FindSymbols.LocationFinder` —
  etliche Provider springen in den **generierten C#-Code**/Call-Sites (Roslyn) → das bleibt VS-/Roslyn-only.

**Wiederverwendbarer Kern (Engine, schon vorhanden):**
- `CodeGenerationUnit.Symbols[TextExtent]` → Symbole an einer Position (Indexer in `SymbolList`/`SymbolCollection`).
- Referenz → Deklaration: `INodeReferenceSymbol.Declaration` (→ `INodeSymbol`), `ITaskNodeSymbol.Declaration`
  (→ `ITaskDeclarationSymbol`, cross-file via `IsIncluded`/`Origin`/`Location`), `IExitConnectionPointReferenceSymbol.Declaration`.
- `ISymbol.Location` (Name), `SyntaxNode.GetLocation()` (voller Bereich), `SyntaxToken.GetLocation()`.
- `Nav.Language/FindReferences/ReferenceFinder` (`FindReferencesVisitor`) — engine-level Referenzsuche (für References/Highlight).

**Vorschlag:** Ein VS-freier Service in `Nav.Language`, z.B. `NavNavigationService` / `GoToResolver` mit
`Resolve(CodeGenerationUnit unit, int position) → IEnumerable<Location>` (Nav→Nav). Die VS-Provider rufen
diesen Kern auf und ergänzen nur UI/Codegen; der LSP-Server nutzt ihn direkt für `textDocument/definition`.

**Zu analysierende offene Fragen:**
1. Provider-Kategorisierung: welche sind reines Nav→Nav (extrahierbar) vs. Codegen/Roslyn (bleiben VS)?
   Kandidaten Nav→Nav: `TaskDeclarationLocationInfoProvider`, `TaskExitDeclarationLocationInfoProvider`,
   `TriggerDeclarationLocationInfoProvider`, `TaskBeginDeclarationLocationInfoProvider`. Codegen/Roslyn:
   `CodeAnalysisLocationInfoProvider` (Basis), `*CodeFile*`, `Nav*Annotation*`, `NavInitCall*`/`NavExitBeginCaller*`.
2. Cross-File: liefert `ITaskDeclarationSymbol.Location` bei `IsIncluded==true` einen brauchbaren `FilePath`+Position?
   (Server hat den Workspace, kann die Zieldatei öffnen — VS nutzt Includes anders.)
3. Wie löst der VS-Tagger heute „Caret → welcher Provider"? (Muss der Kern nachbilden: `unit.Symbols[caretExtent]`.)
4. Mapping mehrerer Treffer (das VS-„Go To…"-Menü) → LSP `Location[]`.

---

## 4. Weitere offene TODOs (priorisiert)

**LSP-Features (Phase 2, additiv, risikoarm):**
- **GoTo Definition** (`textDocument/definition`) — hängt an Option B (oder dünner Server-Resolver als Variante A).
- **Hover** (`textDocument/hover` ↔ `SymbolQuickInfoSource`).
- **References / documentHighlight** (`ReferenceFinder` der Engine nutzen).
- **Folding** (`foldingRange` ↔ `OutliningTagger`), **Completion** (`textDocument/completion` ↔ `AsyncCompletionSource`),
  **Rename**, **Code Actions** (`*SuggestedAction`/`CodeFix`).

**Korrektheit / Robustheit:**
- **Dependency-aware Re-Diagnose:** Bei `didChange` werden derzeit nur die Diagnostics der geänderten Datei
  neu publiziert, NICHT die der Abhängigen (Dateien, die sie inkludieren). → Abhängigkeitsgraph + Re-Publish.
- `workspace/didChangeWatchedFiles` (externe Änderungen geschlossener Dateien) + `didChangeWorkspaceFolders`.
- Inkrementeller Doc-Sync statt Full (optional).
- Severity-Mapping `Suggestion → Information` ggf. auf `Hint` ändern (Geschmack).

**Toolchain / Deployment:**
- **2a:** `CodeTaskFactory → RoslynCodeTaskFactory` in `Nav.Language\CustomBuild.targets`, damit die Engine
  auch mit `dotnet build`/`dotnet publish` baut (netstandard-Refs für StringTemplate4/Antlr3.Runtime nötig;
  unter beiden MSBuild-Varianten verifizieren). Entsperrt self-contained Server-Deployment & komfortableres Debugging.
- **Self-contained Publish:** geht (noch) nur via `MSBuild.exe -t:Publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true`;
  Ausgabe in die Extension bündeln, `navLanguageServer.serverPath` setzen. Extension paketieren: `npx @vscode/vsce package`.
- **Security:** `StreamJsonRpc` zieht transitiv `MessagePack 2.5.192` (GHSA-hv8m-jj95-wg3x, High). Wird nicht
  genutzt (nur JSON), aber auf gepatchte Version pinnen.

**Phase 3 (eigentliches Ziel 1 — Last raus aus `devenv.exe`):**
- VS als RPC-Client: `ParserService`/`SemanticModelService` → Proxy gegen den Server. Großer Posten
  (Objektgraph → serialisierbare DTOs, ~38 Konsumenten). Plan empfiehlt zuerst einen **Spike** (nur Diagnostics
  end-to-end in VS) zur Aufwandsschätzung. Option B passt gut hier hinein (gemeinsame Engine-Services).

---

## 5. Memories (laden automatisch in neue Sessions)
- `nav-lsp-build-toolchain` — Build/Test-Toolchain-Constraints.
- `nav-lsp-uri-windows-gotcha` — `System.Uri.LocalPath`-Fallstrick bei `%3A`.
- `generic-xaml-assembly-name-coupling` — (älter, Extension-Theming).
