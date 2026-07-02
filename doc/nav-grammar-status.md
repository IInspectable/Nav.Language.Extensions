# Nav-Grammatik: Rekonstruktion, Drift-Absicherung & Export — Statusdokument

Pflichtlektüre für die Weiterarbeit am Grammatik-Export. Schwesterdokument zu
`doc/nav-mcp-status.md` und `doc/nav-handwritten-parser.md`.

## Ziel

Mit dem Umbau von ANTLR auf den handgeschriebenen `NavParser` ist die zusammenhängende `.g4`-Grammatik
verschwunden (nur noch in Git-Historie: `git show b0b914b8^:Nav.Language/Grammar/NavGrammar.g4`). Diese
Arbeit baut sie zur Compile-Zeit aus den verstreuten EBNF-Fragmenten wieder zusammen, sichert per
Build-Diagnose **und** Test ab, dass die Grammatik zum Parser passt, und macht sie für Mensch (CLI/Deploy)
und Agent (MCP) abrufbar.

## Quelle der Wahrheit (unverändert)

Jede `Parse*`-Methode in `Nav.Language/Syntax/NavParser.cs` trägt ihre Produktion als **EBNF-Fragment**
im Doku-Kommentar (`<remarks><code><![CDATA[ … ::= … ]]></code></remarks>`), 56 Fragmente, Notations-
Legende in der `NavParser`-Klassendoku. Dazu: `NavParser.Rule`-Enum (Registry der Produktionen),
Terminale in `SyntaxFacts`/`SyntaxTokenType`, `[SampleSyntax]` je Knotenklasse. **Die Inline-EBNF bleibt
maßgeblich** — nichts davon wird verschoben.

## Architektur (entschiedene Forks — nicht erneut aufrollen)

- **Inline-EBNF bleibt Single Source of Truth** (kein `[GrammarRule]`-Attribut). Begründung: behält
  Hover-Hilfe, quote-freundlich unter C# 10, keine Migration; Laufzeitbedarf ist durch die generierte
  `const` gedeckt.
- **Roslyn-Source-Generator** liest die Fragmente, backt sie zu `const string NavGrammar.Ebnf` + meldet
  Drift als Compile-Fehler (NAV001/NAV002).
- **Shared-Projekt** eigenständig auf Top-Level `Build\Shared\Nav.CodeAnalysis.Shared` (Vorbild: die
  Generator-Infrastruktur in `C:\ws\git\Mfv-Peissenberg.Website\Build` — als Blaupause genutzt, keine
  Branding-Spuren übernommen). Ausgelegt für weitere Generatoren (perspektivisch ein Visitor-Generator,
  der die nur-in-VS laufenden T4-Templates ablöst).

Datenfluss: Inline-EBNF → `NavGrammarGenerator` (`const NavGrammar.Ebnf` + `Rules` + NAV001/NAV002)
→ Konsumenten: MCP `nav_grammar`, CLI `nav grammar`, Deploy-Artefakt `NavGrammar.ebnf`
→ Absicherung: `NavGrammarConsistencyTests`.

## Stand: erledigt (Schritte 1–3)

**Alles grün und committet** auf `feature/nav-parser` (Commits `0627975d` Schritt 1,
`82ea124c` SourceBuilder-Nachzug, `19585515` Schritt 2, `6d8f465f` Schritt 3).

### Schritt 1 — Infrastruktur + Generator (const)
- `Build\Shared\Nav.CodeAnalysis.Shared\` (Shared Project `.shproj`/`.projitems`,
  Namespace `Pharmatechnik.Nav.Language.CodeAnalysis.Shared`):
  - `XmlDocExtensions.cs` — extrahiert das EBNF-CDATA aus **rohem Leading-Trivia-Text**
    (`GetLeadingTrivia().ToFullString()`), NICHT aus dem strukturierten Doku-Baum. **Wichtig:** bei
    normalem Build ist `DocumentationMode` oft `None` → keine `XmlCDataSectionSyntax`-Knoten; der rohe
    Trivia-Text ist dagegen immer da. So kein `GenerateDocumentationFile`/CS1591-Lärm an der Engine.
  - `SourceBuilder.cs` — fluenter, einrückungsverwaltender Quelltext-Emitter (gemeinsame Generator-Basis).
  - `CompilerServicesAttributes.cs` — `IsExternalInit`-Polyfill für Records unter netstandard2.0.
- `Build\SourceGenerators\SourceGenerator.props` — gemeinsame Generator-Props (netstandard2.0,
  eigener `LangVersion latest`, `Microsoft.CodeAnalysis.CSharp`/`.Analyzers` mit `PrivateAssets=all`,
  Import der Shared-`.projitems`).
- `Build\SourceGenerators\Nav.Grammar.SourceGenerator\` (Namespace `Pharmatechnik.Nav.Language.Grammar.SourceGenerator`):
  - `NavGrammarGenerator.cs` — `IIncrementalGenerator`. Sammelt `Parse*`-Methoden in `NavParser` mit
    EBNF-Fragment ein, ordnet nach Quellposition, emittiert `partial class NavGrammar` mit
    `const string Ebnf` (alle Produktionen) und `IReadOnlyDictionary<string,string> Rules`. Emittiert via
    `SourceBuilder`; EBNF-Literale als C#-10-Verbatim (`@"…"`, Quotes verdoppelt) über `AppendRaw`.
  - `GrammarRule.cs` — Records `GrammarRule(RuleName, Ebnf, Order, Location)` und `RuleEnumMember(Name, Location)`
    (wertbasiert fürs Incremental-Caching; `Location` für Diagnose-Fundstellen).
- Verdrahtung: `Directory.Packages.props` (CodeAnalysis.CSharp 4.14.0, Analyzers 3.11.0);
  `Nav.Language.csproj` referenziert den Generator als `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`;
  `.slnx` um shproj + Generator ergänzt.
- Lokale `.gitignore` (`Build\Shared\`, `Build\SourceGenerators\`): `**/bin/`, `**/obj/`
  (no-BOM CRLF, wie das Geschwister `Nav.Language.BuildTasks.Tests\.gitignore`).

### Schritt 2 — NAV001/NAV002 + Generator-Tests
- `GrammarDiagnostics.cs` — NAV001 (ein `NavParser.Rule`-Wert ohne Produktion), NAV002 (referenziertes
  Nichtterminal ohne Definition). **Beide Error → Build bricht bei Drift.**
- `EbnfFacts.cs` — textuelle EBNF-Analyse: `DefinedNames` (alle LHS, auch Mehrfach-Produktionen wie
  `codeType`+`arrayType`), `ReferencedNonterminals` (RHS-camelCase nach Entfernen von `"…"`-Literalen und
  `(* … *)`-Kommentaren).
- Generator sammelt zusätzlich die `Rule`-Enum-Member ein und meldet via `rules.Combine(ruleEnumMembers)`.
- `Build\SourceGenerators\Nav.Grammar.SourceGenerator.Tests\` (net10, NUnit): Harness `GeneratorTestBase`
  (`CSharpGeneratorDriver`) + datei-basierte `SnapshotAssert` (Expected eingecheckt, Actual gitignored —
  `**/Snapshots/Actual/` ergänzt). 3 Tests: Snapshot eines Mini-Parsers, NAV001-Fall, NAV002-Fall.

### Schritt 3 — Konsistenz-Test (Engine)
- `Nav.Language.Tests\Syntax\NavGrammarConsistencyTests.cs` (net472 + net10, NUnit) — Laufzeit-Gegenstück
  zu NAV001/NAV002 über die echte Engine: Grammatik nicht leer; jede `Rule` hat eine Produktion in
  `NavGrammar.Ebnf`; geschlossene Grammatik; literale Terminale ∈ `SyntaxFacts` (Sonderfall `"?"`);
  kategorische Terminale ∈ {Identifier, StringLiteral, EOF}; 1:1-Triade Rule↔`{Rule}Syntax`↔`Syntax.Parse{Rule}`;
  Round-Trip je `[SampleSyntax]` (43 Fälle, diagnosefrei + zeichengetreu).
- Ergänzt (dupliziert nicht) die bestehenden `Generated Tests\SyntaxTest.cs` (Diagnose==0 je Beispiel)
  und `SyntaxTreeTests.TestAllSyntaxesPresent` (Knoten-Abdeckung).

### Verifikation (aktuell grün)
- `dotnet build Nav.Language\Nav.Language.csproj` → 0/0 (Generator läuft, keine Falsch-Positiven).
- `dotnet test Build\SourceGenerators\Nav.Grammar.SourceGenerator.Tests\…` → 3/3.
- `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0 --filter NavGrammarConsistencyTests` → 49/49.
- net472: `nunit3-console.exe Nav.Language.Tests\bin\Debug\Nav.Language.Tests.dll --where "class =~ NavGrammarConsistencyTests"` → 49/49.
- Generierte Grammatik einsehen: Build mit
  `/p:EmitCompilerGeneratedFiles=true /p:CompilerGeneratedFilesOutputPath=obj\GeneratedDbg`, dann
  `Nav.Language\obj\GeneratedDbg\…\NavGrammar.g.cs` (Inspektions-Ordner danach löschen — liegt unter obj).

## Stand: abgeschlossen

(Schritte 1–5 erledigt; der optionale zweite Generator ist ebenfalls umgesetzt, siehe unten.)

### Schritt 4 — MCP-Tool `nav_grammar` — ERLEDIGT (uncommittet)
Muster: `Nav.Language.Mcp\Tools\NavOutlineTool.cs` + `NavOutlineResult.cs`; Auto-Registrierung via
`WithToolsFromAssembly()` in `Program.cs` (keine weitere Verdrahtung).
- `NavGrammarTool.cs`: `[McpServerToolType]` + `[McpServerTool(Name="nav_grammar")]`, statische Methode
  **ohne** `NavMcpWorkspace`-Parameter. Parameter: `rule` (einzelne Produktion über `NavGrammar.Rules`),
  `includeTerminals` (Terminal-Tabelle). Beide optional.
- `NavGrammarResult.cs`: DTO mit voller `NavGrammar.Ebnf` bzw. einzelner Regel; bei unbekanntem `rule`
  `error` + `availableRules` (statt Exception). `NavGrammarTerminals` spiegelt `SyntaxFacts` (Keywords +
  Punctuations + kategorische Identifier/StringLiteral/EOF); `?` kommt direkt aus `SyntaxFacts.Punctuations`.
- `doc\nav-mcp-status.md`: Tool-Tabelle (§2) + Design-Notiz (§3) + Verifikation (§4) ergänzt.
- **Stolperstein (umgesetzt):** `NavGrammar.Rules` ist je Fragment-Haupt-LHS verschlüsselt — `arrayType`
  hat keinen eigenen Schlüssel (steckt im `codeType`-Fragment). Unbekanntes `rule` liefert daher Hinweis
  + `availableRules`.
- **Verifikation:** `dotnet build Nav.Language.Mcp` → 0/0; stdio-Smoke (volle Grammatik / `taskDefinition`
  + Terminals / `arrayType`→Fehler) grün.

### Schritt 5 — CLI `nav grammar` + Deploy-Artefakt — ERLEDIGT (uncommittet)
- `Nav.Cli` nutzt **`NDesk.Options`** (nicht FluentCommandLineParser — Statusdoc war hier falsch).
  Neuer Subcommand `nav grammar [--rule X]` in `Nav.Cli\GrammarCommand.cs`, in `Program.Main` als Verb
  vor der normalen Optionsauswertung abgefangen (`args[0] == "grammar"`). Druckt `NavGrammar.Ebnf` nach
  stdout, mit `--rule X` eine einzelne Produktion über `NavGrammar.Rules`; unbekannte Regel → Fehlertext
  + Liste der bekannten Regeln nach **stderr**, Exit ≠ 0. (Projekt ist nullable-**aus** → `string` statt
  `string?`.)
- Deploy: neuer interner Helfer `Tools\Commands\Functions\Export-Grammar.ps1` (Vorbild `Publish-Cli.ps1`),
  führt die publishte `nav.exe grammar` aus und schreibt nach **`deploy\Build Tools\NavGrammar.ebnf`**
  (UTF-8 **ohne** BOM — reines Daten-Artefakt; derselbe Zielort wie früher die `.g4`, Commit `ad32d451`).
  In `Invoke-Publish.ps1` als Schritt **1c** nach `Publish-Cli` eingehängt (braucht die `nav.exe` in
  `deploy\Build Tools`). Veraltete „Grammatik kommt aus DeployFiles"-Kommentare in `Publish-Cli.ps1` /
  `Invoke-Publish.ps1` richtiggestellt (DeployFiles liefert nur Task-DLL + Targets).
- **Verifikation:** `dotnet build Nav.Cli` → 0/0; `nav grammar` / `--rule taskDefinition` / `--rule
  arrayType`(→Fehler) geprüft; `Publish-Cli` + `Export-Grammar` isoliert ausgeführt → `NavGrammar.ebnf`
  (148 Zeilen, kein BOM, `codeGenerationUnit ::=` … `stringLiteral ::=`).

### Zweiter Generator — ERLEDIGT (uncommittet)
Visitor-/Walker-Generator unter `Build\SourceGenerators\Nav.Visitor.SourceGenerator\` (Namespace
`Pharmatechnik.Nav.Language.Visitor.SourceGenerator`), löst die drei nur-in-VS lauffähigen T4-Templates ab
(`SyntaxNodeVisitor.Generated.tt`, `SyntaxNodeWalker.Generated.tt`, `SymbolVisitor.tt` samt ihren
eingecheckten `.cs`). Zwei `IIncrementalGenerator` im selben Projekt (eine Analyzer-Referenz lädt beide),
teilen `SourceBuilder` aus dem Shared-Projekt — das validiert das Shared-Projekt als gemeinsame Basis.
- `SyntaxVisitorWalkerGenerator` — **semantische** Discovery (Basistyp-Kette auf `SyntaxNode`), emittiert
  `SyntaxNodeVisitor.g.cs` (Besucher invariant) + `SyntaxNodeWalker.g.cs`. Besuchsmethoden flach auf
  `DefaultVisit`.
- `SymbolVisitorGenerator` — interface-basiert (Klasse→`I{Name}`-Interface), **kovariantes**
  `ISymbolVisitor<out T>`, `public` Accept, Besuchsmethoden auf das **Interface** typisiert. **Stolperstein:**
  die eingecheckte `SymbolVisitor.cs` war NICHT die Ausgabe des dortigen `.tt` (das war veraltet/flach),
  sondern hatte einen **hierarchischen Fallback** — abgeleitete `*NodeReferenceSymbol`-Besuchsmethoden
  rufen standardmäßig `VisitNodeReferenceSymbol` statt `DefaultVisit`. Der Generator bildet das nach
  (nächstes besuchtes Basis-Interface), sonst brechen `SymbolVisito(rOfT)VisitNodeReferenceSymbolFallBack`
  + diverse Folge-Tests. Die eingecheckte `.cs` ist maßgeblich, nicht das `.tt`.
- Verdrahtung: zweite Analyzer-`ProjectReference` in `Nav.Language.csproj`; T4-Wiring (`<None Update>`/
  `<Compile Update>`/T4-`<Service>`) entfernt; `Nav.Visitor.SourceGenerator(.Tests)` in `.slnx`. `_Readme.txt`
  in `Syntax\Generated` auf den neuen Generator umgeschrieben.
- Tests: `Build\SourceGenerators\Nav.Visitor.SourceGenerator.Tests\` (Aufbau wie die Grammar-Tests,
  Snapshots; Harness referenzlos-semantisch über Mini-`SyntaxNode`-/`ISymbol`-Quelltexte). Der Symbol-
  Snapshot deckt den hierarchischen Fallback ab.
- **Verifikation:** Äquivalenz-Diff (Methoden-/Dispatch-Set identisch zu den alten `.cs`) vor dem Löschen;
  `dotnet build Nav.Language` 0/0; Generator-Tests 3/3; .NET-10-Engine 1062/1062; net472 1070 grün
  (NUnit-Runner); **Voll-Solution `MSBuild.exe` 0/0** (VS-Extension bezieht Visitor/Walker erstmals aus dem
  Generator).

## Fallstricke / Konventionen (für die neue Session)

- **UTF-8 mit BOM** für alle neuen `.cs`/`.md`/Projektdateien (CLAUDE.md). Schnellweg nach dem Schreiben:
  Datei mit `[System.IO.UTF8Encoding]::new($true)` neu schreiben. **Ausnahme:** lokale `.gitignore` sind
  no-BOM CRLF (Geschwister-Konvention).
- **Namespaces** durchgängig `Pharmatechnik.Nav.Language[.*]`; `RootNamespace` je Projekt setzen (sonst
  IDE0130). Shared = `…CodeAnalysis.Shared`, Generator = `…Grammar.SourceGenerator`.
- **CPM:** neue Pakete in `Directory.Packages.props` pinnen (PackageReference ohne Version).
- **Generatorprojekte** pinnen eigenes `LangVersion latest`; die Engine bleibt projektweit 10.0 (Roslyn
  läuft sowohl unter `dotnet build` als auch unter `MSBuild.exe`).
- **`NavGrammar`** ist generiert in `Pharmatechnik.Nav.Language` (Engine-Assembly): `public const string Ebnf`
  + `public static IReadOnlyDictionary<string,string> Rules`.
- **`NavParser` und `NavParser.Rule` sind internal** (IVT für `Nav.Language.Tests`). `NavParser.Rule` darf
  daher kein Parametertyp einer public Testmethode sein → Regel-Namen als `string` übergeben.
- **`?`-Terminal** (Questionmark) ist inzwischen in `SyntaxFacts.Punctuations` enthalten
  (`SyntaxFacts.Questionmark`) — die frühere Sonderbehandlung beim Terminal-Check ist entfallen.
- **Voll-Solution-Build** (`nav build`) braucht weiterhin `MSBuild.exe` (VS-Extension). Der .NET-Teil (inkl.
  Generator + Tests) baut mit `dotnet build`/`dotnet test`. Voll-Build mit MSBuild.exe ist noch nicht
  verifiziert (Generator ist Standard-netstandard2.0 — sollte laufen).

## Commit-Stand

Schritte 1–3 sind committet (siehe oben). Schritte 4 **und 5** sind im Working Tree fertig, aber
**uncommittet**:
- Schritt 4: `Nav.Language.Mcp\Tools\NavGrammarTool.cs`, `Nav.Language.Mcp\Tools\NavGrammarResult.cs`,
  `doc\nav-mcp-status.md`.
- Schritt 5: `Nav.Cli\GrammarCommand.cs`, `Nav.Cli\Program.cs` (Verb-Dispatch),
  `Tools\Commands\Functions\Export-Grammar.ps1`, `Tools\Commands\Functions\Invoke-Publish.ps1`,
  `Tools\Commands\Functions\Publish-Cli.ps1`.
- Beide: `doc\nav-grammar-status.md` (sowie ggf. die offene `.slnx`-Ergänzung aus 1–3).

Der Nutzer committet selbst.
