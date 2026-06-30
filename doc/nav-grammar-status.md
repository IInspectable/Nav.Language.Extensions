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
- **Shared-Projekt** eigenständig auf Top-Level `_build\Shared\Nav.CodeAnalysis.Shared` (Vorbild: die
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
- `_build\Shared\Nav.CodeAnalysis.Shared\` (Shared Project `.shproj`/`.projitems`,
  Namespace `Pharmatechnik.Nav.Language.CodeAnalysis.Shared`):
  - `XmlDocExtensions.cs` — extrahiert das EBNF-CDATA aus **rohem Leading-Trivia-Text**
    (`GetLeadingTrivia().ToFullString()`), NICHT aus dem strukturierten Doku-Baum. **Wichtig:** bei
    normalem Build ist `DocumentationMode` oft `None` → keine `XmlCDataSectionSyntax`-Knoten; der rohe
    Trivia-Text ist dagegen immer da. So kein `GenerateDocumentationFile`/CS1591-Lärm an der Engine.
  - `SourceBuilder.cs` — fluenter, einrückungsverwaltender Quelltext-Emitter (gemeinsame Generator-Basis).
  - `CompilerServicesAttributes.cs` — `IsExternalInit`-Polyfill für Records unter netstandard2.0.
- `_build\SourceGenerators\SourceGenerator.props` — gemeinsame Generator-Props (netstandard2.0,
  eigener `LangVersion latest`, `Microsoft.CodeAnalysis.CSharp`/`.Analyzers` mit `PrivateAssets=all`,
  Import der Shared-`.projitems`).
- `_build\SourceGenerators\Nav.Grammar.SourceGenerator\` (Namespace `Pharmatechnik.Nav.Language.Grammar.SourceGenerator`):
  - `NavGrammarGenerator.cs` — `IIncrementalGenerator`. Sammelt `Parse*`-Methoden in `NavParser` mit
    EBNF-Fragment ein, ordnet nach Quellposition, emittiert `partial class NavGrammar` mit
    `const string Ebnf` (alle Produktionen) und `IReadOnlyDictionary<string,string> Rules`. Emittiert via
    `SourceBuilder`; EBNF-Literale als C#-10-Verbatim (`@"…"`, Quotes verdoppelt) über `AppendRaw`.
  - `GrammarRule.cs` — Records `GrammarRule(RuleName, Ebnf, Order, Location)` und `RuleEnumMember(Name, Location)`
    (wertbasiert fürs Incremental-Caching; `Location` für Diagnose-Fundstellen).
- Verdrahtung: `Directory.Packages.props` (CodeAnalysis.CSharp 4.14.0, Analyzers 3.11.0);
  `Nav.Language.csproj` referenziert den Generator als `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`;
  `.slnx` um shproj + Generator ergänzt.
- Lokale `.gitignore` (`_build\Shared\`, `_build\SourceGenerators\`): `**/bin/`, `**/obj/`
  (no-BOM CRLF, wie das Geschwister `Nav.Language.BuildTasks.Tests\.gitignore`).

### Schritt 2 — NAV001/NAV002 + Generator-Tests
- `GrammarDiagnostics.cs` — NAV001 (ein `NavParser.Rule`-Wert ohne Produktion), NAV002 (referenziertes
  Nichtterminal ohne Definition). **Beide Error → Build bricht bei Drift.**
- `EbnfFacts.cs` — textuelle EBNF-Analyse: `DefinedNames` (alle LHS, auch Mehrfach-Produktionen wie
  `codeType`+`arrayType`), `ReferencedNonterminals` (RHS-camelCase nach Entfernen von `"…"`-Literalen und
  `(* … *)`-Kommentaren).
- Generator sammelt zusätzlich die `Rule`-Enum-Member ein und meldet via `rules.Combine(ruleEnumMembers)`.
- `_build\SourceGenerators\Nav.Grammar.SourceGenerator.Tests\` (net10, NUnit): Harness `GeneratorTestBase`
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
- `dotnet test _build\SourceGenerators\Nav.Grammar.SourceGenerator.Tests\…` → 3/3.
- `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0 --filter NavGrammarConsistencyTests` → 49/49.
- net472: `nunit3-console.exe Nav.Language.Tests\bin\Debug\Nav.Language.Tests.dll --where "class =~ NavGrammarConsistencyTests"` → 49/49.
- Generierte Grammatik einsehen: Build mit
  `/p:EmitCompilerGeneratedFiles=true /p:CompilerGeneratedFilesOutputPath=obj\GeneratedDbg`, dann
  `Nav.Language\obj\GeneratedDbg\…\NavGrammar.g.cs` (Inspektions-Ordner danach löschen — liegt unter obj).

## Stand: offen (Schritte 4–5) — hier weitermachen

### Schritt 4 — MCP-Tool `nav_grammar`  ← NÄCHSTER SCHRITT
Muster: `Nav.Language.Mcp\Tools\NavOutlineTool.cs` + `NavOutlineResult.cs`; Auto-Registrierung via
`WithToolsFromAssembly()` in `Program.cs` (keine weitere Verdrahtung).
- Neu `NavGrammarTool.cs`: `[McpServerToolType]` + `[McpServerTool(Name="nav_grammar")]`, statische
  Methode **ohne** `NavMcpWorkspace`-Parameter (kein Datei-/Solution-Zustand nötig — `NavGrammar` ist ein
  `public const`/`static` in der Engine). Optionale Parameter: `rule` (einzelne Produktion über
  `NavGrammar.Rules`), `includeTerminals` (Terminal-Tabelle).
- Neu `NavGrammarResult.cs`: DTO mit voller `NavGrammar.Ebnf` bzw. einzelner Regel, optional Terminal-
  Tabelle aus `SyntaxFacts` (Keywords + Punctuations + `"?"`).
- `doc\nav-mcp-status.md`: Zeile in der Tool-Tabelle (§2) + Design-Notiz.
- **Stolperstein:** `NavGrammar.Rules` ist je Fragment-Haupt-LHS verschlüsselt — `arrayType` hat keinen
  eigenen Schlüssel (steckt im `codeType`-Fragment). Für die Einzelregel-Abfrage entweder das so
  dokumentieren oder bei Fehlschlag in `NavGrammar.Ebnf` nachsehen.

### Schritt 5 — CLI `nav grammar` + Deploy-Artefakt
- `Nav.Cli` (FluentCommandLineParser): Subcommand `nav grammar [--rule X]`, druckt `NavGrammar.Ebnf`
  (bzw. eine Regel) nach stdout.
- `Tools\Commands\Functions\Invoke-Publish.ps1` (Vorbild `Publish-Cli.ps1`): `nav grammar` ausführen und
  nach **`deploy\Build Tools\NavGrammar.ebnf`** schreiben — derselbe Zielort wie früher die `.g4`
  (Commit `ad32d451`).

### Optional/später — zweiter Generator
Visitor-/Walker-Generator unter `_build\SourceGenerators\`, der `SyntaxNodeVisitor`/`SyntaxNodeWalker` aus
den `*Syntax`-Typen erzeugt und die VS-only-T4-Templates ablöst (dann auch unter `dotnet build`
reproduzierbar). Validiert das Shared-Projekt als gemeinsame Basis.

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
- **`?`-Terminal** (Questionmark) ist NICHT in `SyntaxFacts.Punctuations` — beim Terminal-Check
  gesondert zulassen.
- **Voll-Solution-Build** (`n build`) braucht weiterhin `MSBuild.exe` (VS-Extension). Der .NET-Teil (inkl.
  Generator + Tests) baut mit `dotnet build`/`dotnet test`. Voll-Build mit MSBuild.exe ist noch nicht
  verifiziert (Generator ist Standard-netstandard2.0 — sollte laufen).

## Commit-Stand

Schritte 1–3 sind committet (siehe oben). Offen im Working Tree: nur dieses Statusdokument
(`doc/nav-grammar-status.md`) und die zugehörige `.slnx`-Ergänzung — vor Schritt 4 committen.
Der Nutzer committet selbst.
