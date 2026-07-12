# Nav-MCP-Tests — Plan, Status & Handoff

Step-Plan für ein automatisiertes Testpaket **`Nav.Language.Mcp.Tests`** über die MCP-eigene
Glue-Schicht. Dieses Dokument ist so geschrieben, dass jeder Step in einer **frischen Session ohne
Vorwissen** abgearbeitet werden kann. Schwester-Dokument (Architektur, Tools, bisheriger manueller
Smoke): `doc/nav-mcp-status.md`.

## Fortschritt

| Step | Inhalt | Status |
|---|---|---|
| 1 | Projekt-Setup + Test-Infrastruktur (`McpTestWorkspace`) + Grammar-Smoke | ☑ fertig |
| 2 | Namens-Auflösung (`NavNameResolution`) & `nav_find_symbol` | ☑ fertig |
| 3 | `nav_diagnostics` & `nav_validate` (inkl. Fresh-Read-Semantik) | ☐ offen |
| 4 | Koordinaten-Mapping (`NavEditDto`) & mutierende Tools (Rename, CodeActions) | ☐ offen |
| 5 | Format, GoTo, References, Outline, Workspace | ☐ offen |
| 6 | *(optional)* In-Memory-Protokolltest (Wiring-Wächter) | ☐ offen |
| 7 | Doku (`nav-mcp-status.md` §4) & `Invoke-Test.ps1`-Anbindung | ☐ offen |

Nach jedem Step: Code-Review + Check (Befehle siehe unten), dann **Commit-Message als Text liefern**
(der Commit selbst ist Sache des Nutzers). Status-Tabelle hier nachziehen.

## Warum / Ausgangslage

- Die Engine-Logik hinter den MCP-Tools ist über `Nav.Language.Tests` abgedeckt (inkl.
  `Symbols/NavSymbolSearchTests` für die Namens-/Präfix-Suche). Die **MCP-eigene Schicht
  (~2000 Zeilen)** — Disambiguierung, Paging, DTO-Mapping, Fresh-Read, Fehlerpfade — ist bisher nur
  per **manuellem stdio-Smoke** verifiziert (`doc/nav-mcp-status.md` §4): nicht wiederholbar, nicht
  regressionssicher. Dieses Testpaket überführt den Smoke-Katalog in automatisierte Tests.
- **Entscheidung: eigenes Testprojekt** (statt Einbettung in `Nav.Language.Tests`), weil
  1. die Repo-Konvention ohnehin „ein Testprojekt pro Host/Schicht" ist
     (`Nav.Language.CodeAnalysis.Tests`, `Nav.Language.Extension.Tests`),
  2. `Nav.Language.Mcp` **net10.0-only** ist, `Nav.Language.Tests` aber multi-target
     `net472;net10.0` — eine Einbettung bräuchte TFM-bedingte `ProjectReference` +
     `Compile Remove`-Quirks in einer ohnehin sonderfallreichen `.csproj`,
  3. `nav test` (fest verdrahtete net472-Assembly-Liste in
     `Tools\Commands\Functions\Invoke-Test.ps1`) die MCP-Tests **so oder so** nie ausführen könnte —
     sie laufen in jeder Variante über `dotnet test`.

## Testbarkeit (bereits gegeben, kein Umbau nötig)

- **Tools sind statische, direkt aufrufbare Methoden** — `[McpServerToolType]`/`[McpServerTool]`
  sind passive Attribute; `NavMcpWorkspace` ist ein normaler erster Parameter
  (`NavFindSymbolTool.FindSymbol(workspace, "Login", …)`). Kein stdio, kein JSON-RPC, kein
  DI-Container nötig.
- **`NavMcpWorkspace` hat einen public Konstruktor mit Root-Pfad** — Tests zeigen ihn auf ein
  Temp-Verzeichnis mit hingeschriebenen `.nav`-Dateien. Der MCP arbeitet rein request/response
  **gegen Platte** (kein Overlay-Modell) — Temp-Dateien *sind* der natürliche Fixture-Mechanismus,
  kein Mocking nötig.
- **Einzige Hürde:** `NavNameResolution` ist `internal static` → `InternalsVisibleTo` (Step 1).
- `nav_grammar` ist komplett zustandslos (einziges Tool ohne Workspace-Parameter) — trivial testbar.

## Querschnitts-Regeln (gelten für jeden Step)

- Neue Dateien **UTF-8 mit BOM**, echte Umlaute; XML-Doku auf den Test-Helfern.
- `.nav`-Fixtures als **Raw-Strings** (`"""…"""`) direkt in den Tests (Konvention, siehe CLAUDE.md);
  escapte Strings nur, wo unsichtbare Whitespace der Prüfgegenstand ist — mit Begründungskommentar.
- **NUnit 3.14, klassische Asserts** (`Assert.AreEqual` …) — keine NUnit-4-Syntax.
- Checks pro Step:
  - `dotnet test Nav.Language.Mcp.Tests\Nav.Language.Mcp.Tests.csproj` (single-TFM, kein `-f` nötig)
  - einmal `nav build` als Solution-Wächter (frische Shell: zuerst
    `. .\Tools\Commands\Import-NavCommands.ps1`)
- Zum Verständnis der Soll-Verhalten je Tool: `doc/nav-mcp-status.md` §2/§3 (Design-Entscheidungen
  sind dort begründet — z.B. Leerlauf-Regel des `kind`-Filters, `formattedText`-Semantik).

---

## Step 1 — Projekt-Setup + Test-Infrastruktur

**Anlegen:**

- `Nav.Language.Mcp.Tests\Nav.Language.Mcp.Tests.csproj`:
  - net10.0 (single-TFM — bewusst **kein** Multi-Target), `OutputType` Library,
    `RootNamespace`/`AssemblyName` `Nav.Language.Mcp.Tests`.
  - `PackageReference`: `NUnit`, `Microsoft.NET.Test.Sdk`, `NUnit3TestAdapter` — alle bereits in
    `Directory.Packages.props` gepinnt (CPM, **keine** Versionen in der `.csproj`).
  - `ProjectReference` auf `Nav.Language.Mcp\Nav.Language.Mcp.csproj` (zieht `Nav.Language`/
    `Nav.Utilities` transitiv).
- In `Nav.Language.Extensions.slnx` einhängen (Top-Level-`<Project>`, alphabetisch neben
  `Nav.Language.Mcp`).
- `InternalsVisibleTo("Nav.Language.Mcp.Tests")` in `Nav.Language.Mcp` (für den direkten Test von
  `NavNameResolution`; Ort analog zu bestehenden IVT-Deklarationen im Repo prüfen — sonst
  `Properties/AssemblyInfo`-frei per `<InternalsVisibleTo>`-Item oder Attribut-Datei).
- **Test-Infrastruktur `McpTestWorkspace : IDisposable`** (z.B. `Infrastructure\McpTestWorkspace.cs`):
  - legt ein eindeutiges Temp-Wurzelverzeichnis an (`Path.GetTempPath()` + GUID),
  - `WriteFile(relativePath, navContent)` — schreibt UTF-8 **mit BOM**, legt Unterordner an,
    liefert den absoluten Pfad zurück,
  - `Workspace`-Property: darauf zeigender `NavMcpWorkspace`,
  - `Dispose`: Temp-Verzeichnis rekursiv löschen (fehlertolerant).
- **Ein Smoke-Test als Proof** (`NavGrammarToolTests`): volle Grammatik enthält
  `codeGenerationUnit ::=`; Einzelregel (`taskDefinition`); unbekannte `rule` (z.B. `arrayType`,
  steckt im Fragment von `codeType`) → `error` + `availableRules` statt Exception;
  `includeTerminals` liefert die Terminal-Tabelle.

**Check:** `dotnet test …` grün; `nav build` weiterhin grün (slnx-Einhängung + IVT).

## Step 2 — Namens-Auflösung & `nav_find_symbol`

**`NavNameResolutionTests`** (direkt via IVT):

- Die drei Ausgänge `Resolved` / `NotFound` / `Ambiguous`.
- `task`-Scope: gleicher Knotenname in zwei Task-Definitionen → mit `task` eindeutig.
- `kind`-Achse: Task + gleichnamiger `gui`-Knoten **in derselben Task** — der Fall, den `task`
  nicht lösen kann; mit `kind:"task"` bzw. `kind:"gui"` eindeutig.
- **Leerlauf-Regel:** `kind`-Filter ohne Treffer → ursprüngliche Kandidaten bleiben erhalten
  (weiterhin `Ambiguous`, **kein** falsches `NotFound`).
- `KindMatches`: Sammelart `node` (jeder `INodeSymbol`) vs. konkrete Art (`gui`, `choice`, …),
  case-insensitiv.

**`NavFindSymbolToolTests`** (über die public statische Tool-Methode):

- Präfix-Suche case-insensitiv, cross-file; leerer Präfix = alle Definitionen; unbekannter
  Präfix = leer; nur **Definitionen** (keine `taskref`-Deklarationen).
- `kind`-Filter; Dedup über `(Datei, Startoffset)`; stabile Sortierung (Pfad, dann Offset).
- Paging-Kanten: `limit`-Clamping auf Max 200, `limit <= 0` → Default 100, negativer `offset` → 0,
  `truncated`-Kante (genau volle Seite ≠ truncated; `matchCount`/`returned`/`offset` konsistent).

## Step 3 — `nav_diagnostics` & `nav_validate`

Fixture: Temp-Workspace mit 3–4 Dateien bekannter Fehlerlage (mindestens je eine mit Error,
mit Warning, komplett sauber — z.B. unaufgelöster Task-Verweis, ungenutzte `taskref`).

- Summary-Konsistenz `error + warning + suggestion == count` (beide **vor** Paging);
  `filesScanned` / `filesWithDiagnostics` korrekt.
- `severity`-Filter inkl. `NormalizeSeverity`-Kanten: unbekannte Eingabe → **kein** Filter
  (dokumentiertes Verhalten, kein falsches „nichts gefunden"), Whitespace → alle.
- `filter` (Substring auf **relativem** Pfad, case-insensitiv, scoped auf Unterordner).
- Paging über die **Diagnostics** (nicht die Dateien).
- Quer-Check: `nav_validate` einer Einzeldatei == auf diese Datei gefiltertes `nav_diagnostics`
  (identische Codes/Counts) — das bisherige manuelle Smoke-Szenario, jetzt automatisiert.
- **Fresh-Read-Semantik** (`NavMcpWorkspace.GetFreshUnit`): Datei validieren → auf Platte ändern
  (Fehler einbauen bzw. fixen) → erneut abfragen → neuer Stand sofort sichtbar. Das ist der
  „Agent editiert → fragt ab"-Kernfluss des MCP.
- `nav_validate` auf fehlende Datei → `NotFound`-Result (kein Wurf).

## Step 4 — Koordinaten-Mapping & mutierende Tools

**`NavEditDtoTests`:**

- `From`/`FromChanges`: Offset → **1-basierte** Zeile/Spalte; mehrzeilige Extents.
- Skip-Regeln in `FromChanges`: leere Changes und Extents hinter dem Textende werden übersprungen.
- `NavLocationDto` (1-basiert, `IsDeclaration`), `NavSymbolRef` (`Task` nur bei Knoten gesetzt,
  bei Task-Symbolen `null`).

**`NavRenameToolTests` / `NavCodeActionsToolTests`:**

- Edit-Set korrekt und **file-local**; angewandtes Edit-Set ergibt den erwarteten Text
  (Anwendung im Test rückwärts über die Edits oder via `formattedText`-Analogon: Edits absteigend
  sortiert anwenden).
- **Read-only-Versprechen explizit testen:** Datei-Inhalt auf Platte nach dem Aufruf
  byte-identisch.
- Fehlerpfade: `NotFound` (mit Meldung `NavNameResolution.NotFoundMessage`), `Ambiguous` mit
  Kandidatenliste (je `kind` + enthaltender `task`).

## Step 5 — Format, GoTo, References, Outline, Workspace

**`NavFormatToolTests`:**

- Voll-Format (kanonischer Default: Tabs, Breite 4); Range **zeilenbasiert** (1-basiert, inklusiv),
  nur der gewählte Task ändert sich (Subset-Garantie).
- **Idempotenz:** bereits formatierte Datei → 0 Edits, `formattedText == null`.
- `includeFormattedText = false` → nur Edits; explizite `insertSpaces`/`tabSize` überschreiben den
  Default (z.B. Spaces, Breite 2).
- Fehlerfälle: Zeilenbereich hinter dem Dateiende, fehlende Datei.

**Übrige Tools:**

- `nav_goto`: same-file + cross-file (Task-Definition in inkludierter Datei).
- `nav_references`: alle Vorkommen inkl. Deklaration (`isDeclaration`), Paging + `filter`.
- `nav_outline`: Tasks + Knoten (Art, Position), `languageVersion`/`hasVersionDirective`.
- `nav_workspace`: relative + absolute Pfade, `filter`, Paging.

Damit ist der manuelle Smoke-Katalog aus `doc/nav-mcp-status.md` §4 vollständig in wiederholbare
Tests überführt.

## Step 6 *(optional, empfohlen)* — In-Memory-Protokolltest

Ein einziger Wiring-Wächter über das MCP-SDK (`ModelContextProtocol` 1.4.0): Client + Server im
selben Prozess über Stream-Paare (statt stdio) verbinden, dann `initialize` → `tools/list`
(**alle 11 Tools** entdeckt — sichert `WithToolsFromAssembly` + DI-Injektion des Workspace) →
ein `tools/call` (z.B. `nav_outline`) mit echter JSON-Parameterbindung. Das ist das Einzige, was
die direkten Methodenaufrufe der Steps 2–5 nicht abdecken (Discovery, Serialisierung, Binding).

**Abbruchkriterium:** Zeigt sich das SDK hier sperrig (Transport-API nicht öffentlich zugänglich
o.ä.), wird der Step bewusst gestrichen — der stdio-Smoke bleibt dann die Abdeckung. Kein
Festbeißen.

## Step 7 — Doku & Runner-Anbindung

- `doc/nav-mcp-status.md` §4 umschreiben: automatisierte Suite (`Nav.Language.Mcp.Tests`) statt
  manueller Smoke-Liste; Smoke bleibt nur noch für stdio-Transport/Single-File-Publish erwähnt.
- `Tools\Commands\Functions\Invoke-Test.ps1` um einen `dotnet test`-Schritt für
  `Nav.Language.Mcp.Tests` erweitern (Skip mit Hinweis, wenn nicht gebaut — Muster der
  bestehenden DLL-Liste), damit `nav test` wieder „alles" bedeutet.
- Dieses Dokument: Status-Tabelle finalisieren, ggf. Erkenntnisse/Abweichungen nachtragen.

## Bekannte Fallen

- **`nav test` deckt die MCP-Tests bis Step 7 nicht ab** — immer zusätzlich
  `dotnet test Nav.Language.Mcp.Tests\Nav.Language.Mcp.Tests.csproj` laufen lassen.
- In frischer Shell zuerst `. .\Tools\Commands\Import-NavCommands.ps1`, sonst gibt es kein `nav`.
- `Nav.Language.Mcp` ist net10.0-only — **nie** versuchen, das Testprojekt multi-target zu machen
  oder von `Nav.Language.Tests` (net472-Zweig) aus zu referenzieren.
- Ausgabe-Pfade der Tools sind normalisiert (klein, Backslashes, `PathHelper.NormalizePath`) —
  Pfad-Asserts entsprechend normalisieren, nicht auf Original-Casing vergleichen.
- Temp-Verzeichnisse können auf einem anderen Laufwerk liegen als das Repo — Pfad-Asserts nie
  gegen Repo-relative Annahmen schreiben.

## Grober Umfang

Step 1 ist Gerüst; Steps 2–5 je ~10–20 Tests; gesamt ~50–70 Tests.
