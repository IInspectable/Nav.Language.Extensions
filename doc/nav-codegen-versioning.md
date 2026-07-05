# Versionsabhängige Codegenerierung — Analyse & Zielbild

> Vollständiger Überblick, was für eine **zusätzliche Nav-Sprachversion mit abweichendem Codegen**
> nötig ist: wo die Weiche zwischen den Generator-Generationen liegt, wo die Pfade wieder
> zusammenlaufen, warum die statischen `CodeGenFacts` in ihrer heutigen Form ein Show-Stopper sind,
> was mit der Nav↔C#-Navigation passiert und wie eine Ablösung von StringTemplate durch einen
> CodeBuilder einzuordnen ist. Baut auf `doc/nav-pragmas-versioning-status.md` auf (dort: `#version`,
> `NavLanguageVersion`, Feature-Gate `Nav5000`) — Pflichtlektüre, bevor hier gebaut wird.

## Ausgangslage

Die `#version`-Infrastruktur steht: jede `.nav`-Datei trägt eine wirksame `NavLanguageVersion`
(ohne Direktive `Default` = Version 1), die Version fließt bereits bis in den Codegen
(`CodeGeneratorContext.LanguageVersion`). Was fehlt, ist die **strukturelle Vorbereitung** darauf,
dass eine Version 2 **drastisch anderen Code** erzeugt — bis hin zu einem anders geschnittenen
CodeModel und einer anderen Artefakt-Menge. Randbedingungen:

- Der **Bestand (~1912 Dateien, Version 1) muss byte-identisch bleiben** — V1 ist eingefroren.
- Versionen sind ein **Per-Datei-Fakt**: ein Workspace/Build-Lauf enthält V1- und V2-Dateien
  gemischt. Alles, was „einen" Wertesatz pro Prozess annimmt, bricht.
- Die Nav↔C#-Navigation (GoTo, Annotations, QuickInfo) hängt an der **Gestalt** des generierten
  Codes und muss die Version mitdenken.

## Ist-Zustand: die Codegen-Pipeline heute

```
.nav ─Lexer/Parser→ SyntaxTree ─Semantik→ CodeGenerationUnit   (trägt LanguageVersion)
                                               │
        NavCodeGeneratorPipeline.Run           ▼            (Nav.Language/Generator/)
        1. ISyntaxProvider.GetSyntax(file)
        2. ISemanticModelProvider.GetSemanticModel(syntax)
        3. ICodeGenerator.Generate(unit)                    ← CodeGenerator (StringTemplate)
             je ITaskDefinitionSymbol:
               a) GenerateCodeModel → CodeModelResult       (IBeginWfs/IWfs/WfsBase/Wfs/TO-CodeModels)
               b) GenerateCode      → CodeGenerationResult  (5 CodeGenerationSpecs, via .stg)
        4. IFileGenerator.Generate(result)                  ← FileGenerator (OverwritePolicy, UTF-8)
```

- **Hosts der Pipeline:** `nav.exe` (`Nav.Cli/Generator/NavCodeGenerator.cs`) und darüber der
  MSBuild-Task (`Nav.Language.BuildTasks`, ruft `nav.exe` mit Manifest-basierter Inkrementalität);
  außerdem `Nav.Language.Tests/Regression/RegressionTests.cs` (Snapshot-Vergleich, `nav snapshot`).
- **Rendering:** je Artefakt eine StringTemplate-Group (`IBeginWFS.stg`, `IWFS.stg`, `WFSBase.stg`,
  `WFSOneShot.stg`, `TO.stg`), alle importieren `Common.stg` **und** `CodeGenFacts.stg`
  (`CodeGenerator.LoadTemplateGroup`). Templates erhalten `model` (CodeModel) und `context`
  (`CodeGeneratorContext` — enthält `ProductVersion`, `NullableContext` und **schon heute**
  `LanguageVersion`).
- **Dateischreiben:** `FileGenerator` kennt zwei Policies, heute **hart je Slot** verdrahtet:
  `WhenChanged` für die generierten Dateien (IWfs, IBeginWfs, WfsBase, TOs im `generated`-Ordner),
  `Never` für die Benutzer-Dateien (`{Task}WFS.cs`) — diese werden nur angelegt, nie überschrieben.
- **Semantik ist versions-vorbereitet:** `CodeGenerationUnit.LanguageVersion` steht; für neue
  Syntax-/Semantik-Features existiert das Gate `NavLanguageFeature`/`Nav5000` (Parser bleibt
  permissiv). Dieser Teil ist **orthogonal** zum Codegen-Umbau und hier nicht weiter Thema.

## Inventar: Was hängt an der Gestalt des generierten Codes?

Der „Vertrag" des Generators besteht aus vier Zutaten — jede davon bekommt mit V2 eine
Versions-Achse:

1. **Namens-Algebra** — wie aus Task-/Trigger-/Exit-Namen C#-Namen werden.
   Quelle: `CodeGenFacts` + die `*CodeInfo`-Klassen (`TaskCodeInfo`, `TaskDeclarationCodeInfo`,
   `TaskInitCodeInfo`, `TaskExitCodeInfo`, `SignalTriggerCodeInfo` in `Nav.Language/CodeGen/`).
   Beispiele: `{Task}WFS`/`I{Task}WFS`/`IBegin{Task}WFS`, `BeginLogic`, `After{Exit}Logic`,
   `{Trigger}Logic`, Namespace-Suffixe `WFL`/`IWFL`.
2. **Artefakt-Menge & Ablageorte** — welche Dateien wohin geschrieben werden.
   Quelle: `PathProvider` (nutzt dieselben Facts für Dateinamen) + die feste 5-Slot-Struktur von
   `CodeModelResult`/`CodeGenerationResult`.
3. **Datei-Inhalte** — CodeModels + Templates.
4. **Annotations** — die XML-Doku-Tags im generierten Code (`<NavFile>`, `<NavTask>`,
   `<NavTrigger>`, `<NavInit>`, `<NavExit>`, `<NavInitCall>`), geschrieben von `Common.stg`,
   gelesen von `AnnotationReader` (C#→Nav-Richtung).

### Konsumenten außerhalb des Generators (die stillen Abhängigen)

| Konsument | Nutzt | Richtung |
|---|---|---|
| `Nav.Language.CodeAnalysis/FindSymbols/LocationFinder.cs` | `*CodeInfo`-Namen, `BeginLogic` (= `BeginMethodPrefix`+`LogicMethodSuffix`) | Nav→C#: sucht Klassen/Methoden im Roslyn-Workspace |
| `Nav.Language.CodeAnalysis/Annotation/AnnotationReader.cs` | Annotation-Tag-Namen | C#→Nav: liest Tags aus generiertem Code |
| `Nav.Language.CodeAnalysis/FindReferences/WfsReferenceFinder.cs` | `TaskCodeInfo` | Nav→C# |
| `Nav.Language.ExtensionShared/GoTo/GoToSymbolBuilder.cs` + `GoToLocation/Provider/*` | erzeugt `*CodeInfo` je Nav-Symbol | Nav→C#-Sprungziele im Editor |
| `Nav.Language.ExtensionShared/CSharp/GoTo/IntraTextGoToTagSpanBuilder.cs` | `BeginMethodPrefix` | C#-Editor-Glyphs → Nav |
| `Nav.Language.ExtensionShared/Commands/ViewCSharpCodeCommandHandler.cs`, `RenameCommandHandler.cs` | `TaskCodeInfo` | Kommandos |
| `Nav.Language/Text/DisplayPartsBuilder.cs` | `TaskDeclarationCodeInfo` | QuickInfo zeigt generierte Namen (VS **und** LSP/MCP!) |
| `Nav.Language/Provider/PathProvider.cs` | Namespace-/Klassen-Suffixe | Dateinamen der Artefakte |
| `Nav.Language.Tests/CodeGenFactsTests.cs`, `Regression/` | Konstanten, Snapshots | Wächter |

Kernbeobachtung: **Namensbildung und Navigation teilen sich denselben Code** (`*CodeInfo`). Das ist
ein Glücksfall — wird die Namens-Algebra versionsbewusst, zieht die Navigation weitgehend
automatisch nach.

## Warum die statischen `CodeGenFacts` ein Show-Stopper sind

Die Facts existieren heute in **drei Erscheinungsformen desselben Datensatzes**:

1. `Nav.Language/CodeGen/Templates/CodeGenFacts.stg` — die Quelle (parameterlose ST-Templates).
2. `CodeGenFacts.generated.cs` — Build-Export via `GenerateStringTemplateExport`
   (`Nav.Language/CustomBuild.targets` → `dotnet run --file Build/CodeGen/GenerateCodeGenFacts.cs`)
   als **`public const string`** in `partial class CodeGenFacts`.
3. Zur Laufzeit importiert jede Template-Group die `.stg` erneut (`LoadTemplateGroup`).

Drei Gründe, warum das mit einer zweiten Version nicht mehr trägt:

- **`const` ist compile-time-eingebrannt** — auch in fremden Assemblies (`Nav.Language.CodeAnalysis`,
  `Nav.Language.ExtensionShared` kompilieren die Werte ein). Eine Laufzeit-Variation pro Datei ist
  damit prinzipiell unmöglich; selbst eine Wert-Änderung erforderte das Neukompilieren aller
  Konsumenten.
- **Statisch = genau ein Wertesatz pro Prozess.** Ein Workspace mit gemischten V1-/V2-Dateien
  braucht aber **beide Wertesätze gleichzeitig** (VS-Session, LSP-Server, ein `nav.exe`-Lauf über
  ein ganzes Projekt).
- **Der `.stg`-Import hat dieselbe Schwäche auf Template-Ebene:** die Facts erscheinen dort als
  parameterlose Templates — keine Versions-Achse, die Templates können nicht „andere Facts"
  bekommen, ohne dass man eine zweite komplette `.stg`-Familie baut.

Dazu kommt: der gesamte Build-Export (Custom-Target, file-based Generator, die frühere
MSB4801-Geschichte) existiert **nur**, um die ST-Quelle nach C# zu spiegeln. Er ist Infrastruktur
im Dienst von StringTemplate, nicht der Sprache.

## Zielbild: die Weiche

### Grundsätze

1. **Die Version ist ein Per-Datei-Fakt ⇒ die Weiche schaltet pro `CodeGenerationUnit`** — nicht
   pro Pipeline-Lauf, nicht pro Prozess, nicht per Option. Es gibt keinen „V2-Modus" des Tools;
   dieselbe Engine übersetzt in einem Lauf beide Generationen.
2. **Konvergenzpunkt ist `CodeGenerationSpec`** (Inhalt + Zielpfad + Schreib-Policy). Alles dahinter
   — `FileGenerator`, Logging/Statistik, das Outputs-/Deps-Manifest der BuildTasks, die
   Inkrementalität — bleibt **versionsfrei**. Alles davor — CodeModel, Rendering, Namen, Pfade —
   darf je Version beliebig anders sein.
3. **Die Annotations sind der versions-invariante Vertrag** der C#→Nav-Richtung. Auch ein V2-
   Generator emittiert `<NavFile>`/`<NavTask>`/… in XML-Doku-Kommentaren. Dann bleibt der
   `AnnotationReader` (und damit C#→Nav-GoTo, Rename-Verankerung, `FindReferences`) unverändert.
   Sollte V2 das Schema doch brechen müssen, braucht es ein zusätzliches `<NavVersion>`-Tag und
   einen versionsbewussten Reader — vermeidbar und zu vermeiden.

### Die Weiche im Bild

```
                          CodeGenerationUnit (LanguageVersion)
                                        │
                     ICodeGenerator (Dispatcher, wählt je Unit)
                          ┌─────────────┴─────────────┐
                          ▼                           ▼
                   CodeGeneratorV1              CodeGeneratorV2
                   (heutiger ST-Codepfad,       (neu; eigener CodeModel-
                    eingefroren, Facts V1)       Schnitt, Facts V2, CodeBuilder)
                          └─────────────┬─────────────┘
                                        ▼
                 ImmutableArray<CodeGenerationResult>            ← Konvergenzpunkt
                 = Liste CodeGenerationSpec {Content, FilePath, OverwritePolicy}
                                        │
                                        ▼
                 FileGenerator · Logger · Statistik · BuildTasks-Manifest   (versionsfrei)
```

Die Verzweigung liegt **in Pipeline-Schritt 3** (Codegen), und zwar *hinter* der `ICodeGenerator`-
Schnittstelle: die Pipeline, `nav.exe`, die BuildTasks und die Tests sehen weiterhin genau einen
`ICodeGenerator`. Ein `VersionDispatchingCodeGenerator` (der neue `CodeGeneratorProvider.Default`)
liest `unit.LanguageVersion` und delegiert an den Generator der Generation.

### Bausteine im Einzelnen

**(1) Facts de-statisieren: `ICodeGenFacts` + Registry.**
Ein Interface (bzw. eine abstrakte Klasse) mit den heutigen Werten als Instanz-Properties;
`NavCodeGenFacts.For(NavLanguageVersion)` liefert die Instanz der Generation (V1 = heutige Werte).
Die statische `CodeGenFacts`-Klasse bleibt übergangsweise als reine V1-Fassade bestehen (die
Konstanten delegieren nicht, sie *sind* die V1-Werte — `CodeGenFactsTests` pinnen sie), aber
**kein Konsument mit Versions-Bezug greift mehr direkt auf die Statik zu**. Der Build-Export
(`CodeGenFacts.generated.cs`) bleibt vorerst als V1-Wertequelle für die ST-Templates; er stirbt
mit dem letzten ST-Template.

**(2) Namens-Algebra versionsbewusst: `*CodeInfo` beziehen ihre Facts aus dem Symbol.**
`TaskCodeInfo.FromTaskDefinition(task)` kennt über `task.CodeGenerationUnit.LanguageVersion` die
Version selbst — die Factories holen sich intern `NavCodeGenFacts.For(version)`; **alle Aufrufer
(GoTo-Provider, LocationFinder, DisplayPartsBuilder, WfsReferenceFinder …) bleiben unverändert**
und werden dadurch automatisch versionsrichtig. Dasselbe für `TaskInitCodeInfo`,
`TaskExitCodeInfo`, `SignalTriggerCodeInfo`, `TaskDeclarationCodeInfo`.

**(2a) Include-Lücke schließen — der eine echte Modell-Eingriff.**
`ITaskDeclarationSymbol.CodeGenerationUnit` ist für **inkludierte** Deklarationen bewusst `null`
(fremde Syntaxbäume werden nicht gehalten). Die generierten Namen eines referenzierten Tasks
(z.B. `IBegin{Task}WFS` im `BeginWrapper` des Aufrufers) richten sich aber nach der Version der
**deklarierenden** Datei, nicht der konsumierenden. ⇒ Der `TaskDeclarationSymbolBuilder` muss beim
Include-Extrakt die `LanguageVersion` der Quelldatei als leichtgewichtigen Fakt am Symbol
mitführen (`ITaskDeclarationSymbol.DeclaringLanguageVersion` o.ä. — analog `CodeNamespace`).
Betrifft auch den Include-Extraktions-Cache (Prototypen/Klone). **Cross-Version-`taskref` ist
damit ein bewusst zu entscheidender Fall** — siehe offene Fragen.

**(3) Ergebnis-Form generalisieren: weg von den festen 5 Slots.**
`CodeModelResult`/`CodeGenerationResult` kodieren heute die V1-Artefakt-Menge strukturell
(IBeginWfs/IWfs/WfsBase/Wfs/TO). V2 kann mehr, weniger oder andere Dateien erzeugen.
`CodeGenerationResult` wird zur Liste von `CodeGenerationSpec { Content, FilePath,
OverwritePolicy }` — die Policy (heute in `FileGenerator.Generate` hart je Slot: `WhenChanged`
für Generate-Dateien, `Never` für Benutzer-Dateien) wandert als **Metadatum an den Spec**. Der
`FileGenerator` wertet nur noch aus. Konsumenten der heutigen benannten Slots
(`FileGenerator`, `LoggerAdapter`, `RegressionTests`) stellen auf die Liste um.

**(4) Pfade versionieren.**
`PathProvider` bildet Dateinamen aus den Facts — mit versionierten Facts (1) und einer per-Unit
gewählten Factory (`IPathProviderFactory` bekommt die Version bzw. der V2-Generator bringt seinen
eigenen Provider mit) ist auch die Ablage je Generation frei wählbar.

**(5) Generator-Generationen.**
`CodeGeneratorV1` ist der heutige `CodeGenerator` (ST-basiert) — unverändert, eingefroren, mit
V1-Facts. `CodeGeneratorV2` ist frei in CodeModel-Schnitt und Renderer (Empfehlung: CodeBuilder,
s.u.). Beide implementieren `ICodeGenerator`; der Dispatcher wählt je Unit. Neue Sprachversion
freischalten heißt dann: Konstante in `NavLanguageVersion.SupportedVersions` + Facts-Instanz +
Generator-Zweig im Dispatcher.

### Was ausdrücklich **nicht** umgebaut werden muss

- **Pipeline-Gerüst** (`NavCodeGeneratorPipeline`, Provider-Fabriken): die Weiche liegt hinter
  `ICodeGenerator`; Schritt 1/2/4 bleiben.
- **BuildTasks-Inkrementalität:** die Version steht **in** der `.nav`-Datei ⇒ eine Versions-
  Änderung ist eine Inhalts-Änderung, der bestehende Manifest-Mechanismus greift; `nav.exe` ist
  ohnehin selbst Input (Generator-Upgrade ⇒ Rebuild).
- **LSP/MCP:** generieren nicht; sie zeigen generierte Namen nur an (QuickInfo via
  `DisplayPartsBuilder`) — das läuft über (2) automatisch versionsrichtig.
- **Syntax/Semantik-Versionierung:** `Nav5000`-Gate, `Nav5001`, Completion — steht bereits
  (`doc/nav-pragmas-versioning-status.md`).

## StringTemplate vs. CodeBuilder

Befund zu StringTemplate (ST4 4.0.8) im Licht der Versionierung:

- **Modellbindung ist reflektiv/stringly:** `<model.WfsBaseTypeName>` bricht bei C#-Renames still
  zur Laufzeit; kein Compile-Zeit-Netz, schwer zu debuggen.
- **Versions-Verzweigung in ST skaliert nicht:** kleine Deltas gingen über
  `<if(context.LanguageVersion…)>`, aber „drastisch anderer Code" hieße faktisch eine **zweite
  komplette `.stg`-Familie** — dann bietet ST gegenüber C#-Code keinen Vorteil mehr, kostet aber
  weiter den statischen Facts-Export samt Build-Sonderweg.
- **Der Facts-Export existiert nur wegen ST.** Mit einem CodeBuilder sind die Facts gewöhnlicher,
  versionierbarer C#-Code; `CustomBuild.targets`-Export und `CodeGenFacts.stg` entfallen.
- Dagegen spricht für V1-Bestandsschutz: die **byte-identische** V1-Ausgabe ist über den gesamten
  Bestand bewiesen; ST-Eigenheiten (`anchor`-Einrückung, Separatoren, CRLF) sind nichttrivial
  exakt nachzubauen. Jede Neuimplementierung von V1 trägt Parity-Risiko.

**Empfehlung — Variante A („V1 einfrieren, V2 neu"):** Der ST-Codepfad bleibt exklusiv der
V1-Generator und wird nicht mehr angefasst; `CodeGeneratorV2` entsteht auf einem handgeschriebenen
**CodeBuilder** (indent-bewusster Emitter im Stil von Roslyn-Source-Generatoren: `WriteLine`,
`Indent`-Scopes, Region-/XML-Doku-Helfer — als eigener Baustein `Nav.Language/CodeGen/CodeBuilder/`,
gern mit Unterklassen-Verzweigung für spätere V3-Deltas). Kein Parity-Risiko, keine
Big-Bang-Migration; ST verschwindet mit dem Lebensende von V1 von selbst.

**Variante B (Voll-Migration auf CodeBuilder, auch V1)** bleibt möglich, falls man mittelfristig
nur eine Technologie will: dann ist der Byte-Identitäts-Beweis Pflicht (Regression-Snapshots +
Lauf über den kompletten Bestandskorpus mit Diff = leer), am besten *nachdem* der CodeBuilder in
V2 gereift ist. Nicht als erster Schritt.

## Schritt-Plan (Vorschlag)

Reihenfolge so gewählt, dass jeder Schritt für sich baubar/testbar ist und V1-Neutralität
(Regression-Snapshots + Korpus) nach jedem Schritt gilt:

| Step | Inhalt | Fertig, wenn |
|---|---|---|
| 1 | `ICodeGenFacts` + `NavCodeGenFacts.For(version)`; V1-Instanz; Statik wird V1-Fassade | Engine baut; `CodeGenFactsTests` pinnen V1-Werte; kein Verhaltens-Diff |
| 2 | `*CodeInfo` versionsbewusst (Facts aus Symbol-Version); Include-Lücke: `DeclaringLanguageVersion` am `ITaskDeclarationSymbol` | Navigation/QuickInfo-Tests grün; V1-Snapshots byte-identisch |
| 3 | `CodeGenerationResult` → Spec-Liste mit `OverwritePolicy`-Metadatum; `FileGenerator`/Logger/RegressionTests umgestellt | `nav test` beide TFMs grün; Regression byte-identisch |
| 4 | Dispatcher `VersionDispatchingCodeGenerator` als `CodeGeneratorProvider.Default`; heutiger `CodeGenerator` wird `CodeGeneratorV1` | Pipeline-Verhalten für V1 unverändert (Korpus-Diff leer) |
| 5 | CodeBuilder-Grundgerüst (`CodeGen/CodeBuilder/`) + `CodeGeneratorV2`-Skelett (zunächst hinter `SupportedVersions`, d.h. ohne freigeschaltete Version 2) | Unit-Tests des Builders; V2 noch nicht öffentlich |
| 6 | Erste echte V2-Inhalte: Facts V2, CodeModel-/Emitter-Schnitt, `PathProvider`-V2; **Version 2 in `SupportedVersions` freischalten** | neue Snapshot-Fixtures `Regression/Tests-V2/`; `nav snapshot` beherrscht beide |
| 7 | Navigation end-to-end für V2 verifizieren (GoTo Nav→C#, C#→Nav via Annotations, Rename, FindReferences) | VS-Smoke + Testabdeckung |

Nach jedem Step: Code-Review + `nav test` (net472 **und** net10.0), Commit-Message liefern —
Commit macht der Nutzer (Arbeitsweise siehe `CLAUDE.md`).

## Offene Design-Fragen (vor Step 6 zu entscheiden)

1. **Was ändert sich in V2 konkret?** Nur Inhalte/Namen — oder auch die Artefakt-Menge
   (mehr/weniger Dateien, andere Benutzer-Datei-Konvention)? Bestimmt, wie viel vom V1-CodeModel
   sich wiederverwenden lässt (bewusst *nicht* teilen wäre der sauberere Default).
2. **Cross-Version-`taskref`:** darf eine V1-Datei einen Task aus einer V2-Datei referenzieren
   (und umgekehrt)? Technisch lösbar über (2a) — aber wenn V2 z.B. das `IBegin`-Muster ändert,
   muss der V1-Generator fremde V2-Namen emittieren können. Alternativ: Mischreferenzen zunächst
   per Diagnose verbieten (neue `Nav5xxx`), bis der Bedarf real ist. **Empfehlung: verbieten,
   bewusst freischalten wenn nötig.**
3. **Annotations-Vertrag fixieren:** Zusage, dass jede Generation die heutigen Tags emittiert
   (Grundsatz 3)? Wenn ja, als Regel in dieses Dokument bzw. `CLAUDE.md`-Nähe heben.
4. **CodeBuilder Variante A vs. B** (oben) — Empfehlung A.
5. **`context.ProductVersion`/Header:** schreibt V2 denselben `<auto-generated>`-Header? (Kein
   Blocker, nur Konsistenz-Entscheidung.)

## Verifikation (Wiederholrezept)

- V1-Neutralität: `nav snapshot` erzeugt keine Diffs; zusätzlich Lauf über den Bestandskorpus
  (`nav.exe` gegen Kopie, `git diff` leer).
- Tests: `nav test` (net472) und `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj
  -f net10.0` — Engine-Tests müssen auf **beiden** TFMs grün sein.
- Navigation: VS-Extension (`nav build` + `nav install`) Smoke — GoTo aus `.nav` in generierten
  Code und zurück, für je eine V1- und (ab Step 6) V2-Datei.
