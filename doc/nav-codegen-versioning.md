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
  `WhenChanged` für IWfs, IBeginWfs und WfsBase; `Never` für die Benutzer-Datei (`{Task}WFS.cs`)
  **und die TOs** — beide werden nur angelegt, nie überschrieben (`FileGenerator.Generate`).
  Befund (2026-07-05): Die von `nav.exe` geschriebenen TOs sind nur **Stubs** (leere
  `partial class XyzTO : TO` mit Platzhalter-Kommentar); den vollen TO-Inhalt erzeugt laut
  Stub-Kommentar der **GUI-Generator** (externes Tool), der die Datei überschreibt.
- **Semantik ist versions-vorbereitet:** `CodeGenerationUnit.LanguageVersion` steht; für neue
  Syntax-/Semantik-Features existiert das Gate `NavLanguageFeature`/`Nav5000` (Parser bleibt
  permissiv). Dieser Teil ist **orthogonal** zum Codegen-Umbau und hier nicht weiter Thema.

## Inventar: Was hängt an der Gestalt des generierten Codes?

Der „Vertrag" des Generators besteht aus vier Zutaten. Zwei davon sind per Design-Entscheidung
**generationsübergreifend invariant** (die Interfaces `I{Task}WFS`/`IBegin{Task}WFS` als
Schnittstelle zum Workflow-Code sowie die Annotations — Grundsätze 3 und 4); die übrigen bekommen
mit V2 eine Versions-Achse:

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

### Befunde der property-genauen CodeInfo-Analyse (2026-07-05)

Die Voranalyse zu Step 2 hat die Konsumenten jeder einzelnen `*CodeInfo`-Property erhoben
(Generator-CodeModels, ST-Templates inkl. reflektivem Zugriff, Navigation, Anzeige). Ergebnisse,
die das Bild oben präzisieren:

- **Die CodeInfos sind heute primär Navigations-/Anzeige-SSOT, nicht Generator-SSOT.** Die
  Methoden-Namen (`BeginLogicMethodName`, `AfterLogicMethodName`, `TriggerLogicMethodName`)
  konsumiert der Generator **gar nicht** — die Templates bauen dieselben Namen selbst aus den
  Facts zusammen (`<ExitMethodPrefix()><…NodeNamePascalcase><LogicMethodSuffix()>` in
  `WFSBase.stg`). Die „SSOT" existiert also **doppelt** (CodeInfo für Navigation, Templates für
  Generierung); konsistent nur, weil beide dieselben Facts-Konstanten verbauen. **Vorgabe für
  Step 4:** die CodeBuilder-Emitter konsumieren die CodeInfos direkt — erst dann ist die SSOT echt.
  Echt dual-use ist nur `TaskCodeInfo` (CodeModels delegieren ihre Typnamen daran) sowie
  `SignalTriggerCodeInfo.TriggerName`/`TOClassName` (füttern `TriggerTransitionCodeModel`).
- **Drei tote Properties:** `TaskExitCodeInfo.AfterMethodName`, `TaskInitCodeInfo.BeginMethodName`,
  `SignalTriggerCodeInfo.TriggerMethodName` haben keinen einzigen Konsumenten (Code und Templates
  geprüft) — in Step 2 streichen.
- **Namensquelle am CodeInfo vorbei:** `LocationFinder.cs` baut sich `BeginLogic` lokal aus
  `BeginMethodPrefix + LogicMethodSuffix` zusammen (Konstante `BeginLogicMethodName`, Zeile 44) —
  in Step 2 auf `TaskInitCodeInfo` umstellen, sonst versioniert diese Quelle nicht mit.
- **`WflNamespaceSuffix` ist doppelt belegt** (analog `WfsClassSuffix`): `IBegin{Task}WFS` liegt in
  `{ns}.WFL` (`TaskDeclarationCodeInfo.WflNamespace`) — der WFL-Suffix ist also **invariant als
  IBegin-Interface-Ablage** (Grundsatz 3) *und* versionierbar als Implementierungs-Namespace
  (`{ns}.WFL.{Task}WFS`). `CodeGenInvariants` fehlt dieser Baustein noch; in Step 2 entflechten.
- **Die Nav→C#-Navigation hat zwei Versions-Achsen:** (a) die **Namen** — löst Step 2 über
  versionierte Facts vollständig; (b) die **Struktur-Annahmen des Suchverfahrens** im
  `LocationFinder`: „es gibt eine generierte Basisklasse mit berechenbarem FQN"
  (`GetTypeByMetadataName(FullyQualifiedWfsBaseName)`), „User-Code liegt in abgeleiteten Klassen"
  (`FindDerivedClassesAsync`, `.generated.cs` wird übersprungen), „Logik-Member sind
  namensadressierbar". Erzeugt eine Generation keine separate Basisklasse mehr, bricht der
  **Algorithmus**, nicht nur ein Name — versionierte Facts allein reparieren das nicht.
  Die C#→Nav-Richtung ist davon unberührt (läuft über die invarianten Annotations, Grundsatz 4).

### Befunde der Step-3-Voranalyse (2026-07-05) — Ergebnis-Form generalisieren

Erhoben für den nächsten Schritt (`CodeGenerationResult` → Spec-Liste mit `OverwritePolicy`-Metadatum).
Die Ist-Gestalt und der **präzise** Blast-Radius (kleiner als die Step-3-Planzeile vermuten lässt):

- **Ist-Gestalt der Typen:**
  - `CodeGenerationSpec` (record, `CodeGen/CodeGenerationSpec.cs`): heute nur `{ Content, FilePath }` +
    Sentinel `Empty`/`IsEmpty` — **noch ohne** `OverwritePolicy`.
  - `CodeGenerationResult` (sealed class): `TaskDefinition` + **feste 5 Slots** (`IBeginWfsCodeSpec`,
    `IWfsCodeSpec`, `WfsBaseCodeSpec`, `WfsCodeSpec`, `ToCodeSpecs`).
  - `OverwritePolicy`: **privates nested enum in `FileGenerator`** (`{ Never, WhenChanged }`).
  - `CodeModelResult` (internal sealed): analoge 5 Model-Slots; nur von `CodeGenerator.GenerateCode`
    konsumiert.
- **Wer baut das Result:** `CodeGenerator.GenerateCode` (+ die fünf `Generate*CodeSpec`-Methoden,
  je Slot ein `CodeGenerationSpec` bzw. `CodeGenerationSpec.Empty` bei ausgeschaltetem Options-Flag).
- **Wer konsumiert die Slots:** nur **zwei** Stellen — `FileGenerator.Generate` (liest die Slots,
  vergibt **hart je Slot** die Policy: `WhenChanged` für IWfs/IBeginWfs/WfsBase, `Never` für Wfs
  **und** TOs; skippt `IsEmpty`) und **`CodeGenTests`** (prüft Option-Gating: welches Artefakt bei
  welchem Flag `IsEmpty` ist, plus `#nullable`-Inhalt je Slot).
- **NICHT betroffen — anders als die Planzeile suggeriert:** `LoggerAdapter` konsumiert
  `FileGeneratorResult` (schon Liste, hängt nicht an den Slots); `RegressionTests` läuft über
  Pipeline **+ Dateisystem** (`.expected.cs`-Vergleich) und ist von der Result-Gestalt entkoppelt;
  `NavCodeGeneratorPipeline.Run` reicht das Result nur an `fileGenerator.Generate(result)` weiter;
  `Statistic` zählt `FileGeneratorResult`. Keine weiteren Hosts (BuildTasks ruft `nav.exe` als
  Prozess). ⇒ **Der reale Umbau berührt vier Produktionsdateien:** `CodeGenerationSpec`,
  `CodeGenerationResult`, `CodeGenerator`, `FileGenerator` — plus die **Test**-Umstellung in
  `CodeGenTests`.

- **Empfohlener Umbau:**
  1. `OverwritePolicy` in ein **public top-level** enum ziehen (eigene Datei), Werte unverändert.
  2. `CodeGenerationSpec` bekommt `OverwritePolicy` (Ctor-Param + Property); `Empty` mit belanglosem
     Default (wird ohnehin gefiltert).
  3. `CodeGenerationResult` → `{ TaskDefinition, ImmutableArray<CodeGenerationSpec> Specs }`. Die
     `Generate*CodeSpec`-Methoden vergeben die Policy (Wissen wandert vom `FileGenerator` in den
     Generator) und werden in `GenerateCode` zu **einer** Liste zusammengeführt, **leere Specs beim
     Bau gefiltert** (Liste = echte Artefakte).
  4. `FileGenerator.Generate` iteriert nur noch `result.Specs` und ruft
     `WriteFile(taskDef, spec, spec.OverwritePolicy)`; `ShouldWrite`/`WriteFile`/`Resilience` bleiben.
  5. `CodeGenTests` auf Spec-Identität per **FilePath-Suffix** umstellen; Option-Gating als
     An-/Abwesenheit in der Liste prüfen (statt `IsEmpty` je Slot).

- **Offene Entscheidungen (zu Step-3-Beginn mit dem Nutzer klären):**
  - **A. Spec-Identität in den Tests:** FilePath-Suffix-Matching (empfohlen — hält `CodeGenerationSpec`
    minimal wie in Baustein 3) **vs.** ein neues `Kind`-Label am Spec (für `FileGenerator` redundant
    zur Policy, aber explizit für Tests/Diagnostik/Logging).
  - **B. Leere Specs beim Bau filtern** (empfohlen: ja — Liste enthält nur zu schreibende Artefakte;
    vereinfacht `FileGenerator` und die Gating-Tests) **vs.** leere Specs mitführen und weiter skippen.
  - **C. `CodeModelResult` bleibt** unverändert (emitter-internes V1-Konstrukt, nur `GenerateCode`
    liest es) — in Step 3 **nicht** flachklopfen. Bestätigen.
  - **D. Schreibreihenfolge:** wirkt nur auf Log/Statistik, nicht auf Dateiinhalt (RegressionTests
    vergleicht Inhalte auf Platte) — bestehende Reihenfolge beibehalten, um Log-Diffs zu vermeiden.

- **Byte-Identitäts-Gate:** Policy-Mapping 1:1 erhalten, keine Inhalts-/Pfadänderung. Verifikation
  wie gehabt: `nav test` (net472) + `dotnet test … -f net10.0`, Regression byte-identisch.

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
3. **Schnittstellen-Invariante (entschieden 2026-07-05):** Die generierten Interfaces
   **`I{Task}WFS` und `IBegin{Task}WFS` sind über alle Generationen identisch** — in Name, Gestalt
   und Ablage. Sie sind im Wortsinn die Schnittstelle zum Workflow-Code. Eine neue Generation darf
   die **Implementierung** (WFSBase & Co.) und die **Anzahl/Zuschnitt der generierten Dateien**
   beliebig ändern, nie die Interfaces. Konsequenzen:
   - Die **TO-Typnamen gehören transitiv zur invarianten Oberfläche**, soweit sie in
     Interface-Signaturen auftauchen (`{Trigger}(XyzTO to)` in `I{Task}WFS` — die `…Logic`-
     Methoden liegen in `WFSBase`, nicht im Interface). Invariant ist nur der **Name**: die
     Member-Gestalt der TOs besitzt ohnehin der GUI-Generator (s. Dateischreiben oben), und V2
     schreibt gar keine TO-Stubs mehr (entschieden 2026-07-05, s. Design-Frage 7).
   - Alles, was nur Interface-Namen ableitet (`TaskDeclarationCodeInfo`:
     `FullyQualifiedBeginInterfaceName`; die Interface-GoTo-Provider), bleibt **versionsfrei**.
   - Cross-Version-`taskref` wird trivial (s.u.).
4. **Die Annotations sind der versions-invariante Vertrag** der C#→Nav-Richtung
   (**entschieden 2026-07-05 — fixiert**): jede Generation emittiert die heutigen Tags
   `<NavFile>`/`<NavTask>`/`<NavTrigger>`/`<NavInit>`/`<NavExit>`/`<NavInitCall>` in
   XML-Doku-Kommentaren. Damit bleiben `AnnotationReader` und alles darauf Aufbauende (C#→Nav-GoTo,
   Rename-Verankerung, `FindReferences`) generationsübergreifend unverändert.
5. **Header-Kontinuität (entschieden 2026-07-05):** Auch V2 schreibt denselben
   `<auto-generated>`-Dateikopf (inkl. `#nullable enable`-Option) — kein eigenes Header-Format je
   Generation. Befund (2026-07-05): Der Header ist heute **versionsfrei** —
   `writeFileHeader(context)` in `Common.stg` emittiert keine Versionsnummer, und
   `context.ProductVersion` wird von **keinem** Template konsumiert (einziger Nutzer von
   `ProductVersion` ist der Konsolen-Logger). Das ist erwünscht (byte-stabile Ausgabe über
   Tool-Versionen hinweg) und bleibt so.

### Die Weiche im Bild

```
                          CodeGenerationUnit (LanguageVersion)
                                        │
                     ICodeGenerator (Dispatcher, wählt je Unit)
                          ┌─────────────┴─────────────┐
                          ▼                           ▼
                   CodeGeneratorV1              CodeGeneratorV2
                   (Facts V1, byte-identisch    (eigener CodeModel-Schnitt,
                    zum Bestand; CodeBuilder     Facts V2; emittiert identische
                    nach der ST-Migration)       I{Task}WFS/IBegin{Task}WFS)
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

**(1) Facts de-statisieren: Zweiteilung in Invarianten + `ICodeGenFacts` (entschieden 2026-07-05).**
Die Facts werden **explizit zweigeteilt**, sodass die Grundsätze 3+4 strukturell erzwungen sind
statt nur per Test-Konvention:

- **Invariante Facts bleiben statische Konstanten** in einer eigenen Klasse (Arbeitstitel
  `CodeGenInvariants`): die Annotation-Tags (Grundsatz 4) und die Bausteine der
  Interface-Namensbildung (`InterfacePrefix`, `BeginInterfacePrefix`, Interface-Suffix „WFS",
  `IwflNamespaceSuffix`, `ToClassNameSuffix`). Konsumenten wie `AnnotationReader` und
  `TaskDeclarationCodeInfo` hängen nur an dieser Statik und **können** gar nicht versionieren;
  die `const`-Einkompilierung in fremde Assemblies ist hier legal, weil die Werte per Grundsatz
  unveränderlich sind.
- **Nur versionierbare Werte** wandern in `ICodeGenFacts` (Instanz-Properties);
  `NavCodeGenFacts.For(NavLanguageVersion)` liefert die Instanz der Generation (V1 = heutige Werte).
- **Doppelgebrauch entflechten:** `WfsClassSuffix` („WFS") steckt heute **sowohl** im invarianten
  Interface-Namen `I{Task}WFS` **als auch** im versionierbaren Klassennamen `{Task}WFS`. Die
  Zweiteilung trennt das in einen invarianten Interface-Suffix und einen versionierten
  Klassen-Suffix (initial gleicher Wert).

Die statische `CodeGenFacts`-Klasse bleibt übergangsweise als reine V1-Fassade bestehen (die
Konstanten delegieren nicht, sie *sind* die V1-Werte — `CodeGenFactsTests` pinnen sie), aber
**kein Konsument mit Versions-Bezug greift mehr direkt auf die Statik zu**. Der Build-Export
(`CodeGenFacts.generated.cs`) bleibt vorerst als V1-Wertequelle für die ST-Templates; er stirbt
mit der ST-Migration (Step 4).

**(2) Namens-Algebra versionsbewusst: `*CodeInfo` beziehen ihre Facts aus dem Symbol.**
`TaskCodeInfo.FromTaskDefinition(task)` kennt über `task.CodeGenerationUnit.LanguageVersion` die
Version selbst — die Factories holen sich intern `NavCodeGenFacts.For(version)`; **alle Aufrufer
(GoTo-Provider, LocationFinder, DisplayPartsBuilder, WfsReferenceFinder …) bleiben unverändert**
und werden dadurch automatisch versionsrichtig. Dasselbe für `TaskInitCodeInfo`,
`TaskExitCodeInfo` und `SignalTriggerCodeInfo` (Implementierungs-Namen wie `BeginLogic`,
`After{Exit}Logic`, `{Trigger}Logic` dürfen je Generation abweichen). `TaskDeclarationCodeInfo`
bleibt dagegen **versionsfrei** — es leitet nur Interface-Namen ab, und die sind per Grundsatz 3
invariant.

**(2a) Include-Lücke — entfällt dank Schnittstellen-Invariante (Grundsatz 3).**
`ITaskDeclarationSymbol.CodeGenerationUnit` ist für **inkludierte** Deklarationen bewusst `null`
(fremde Syntaxbäume werden nicht gehalten) — die Version der deklarierenden Datei ist am Symbol
also nicht verfügbar. Das ist **unschädlich**, weil ein Aufrufer von einem fremden Task nur dessen
Interface-Namen braucht (`TaskDeclarationCodeInfo` liefert genau `WflNamespace` +
`FullyQualifiedBeginInterfaceName` — beides invariant): Cross-Version-`taskref` funktioniert damit
in beide Richtungen ohne jede Zusatzinformation, `TaskDeclarationCodeInfo` bleibt versionsfrei.
**Wächter-Hinweis:** Diese Entwarnung hängt vollständig an Grundsatz 3. Sollte eine künftige
Generation die Interface-Namen doch ändern, kommt exakt dieser Eingriff zurück
(`DeclaringLanguageVersion` beim Include-Extrakt am Symbol mitführen, analog `CodeNamespace`;
betrifft dann auch den Include-Extraktions-Cache mit Prototypen/Klonen).

**(3) Ergebnis-Form generalisieren: weg von den festen 5 Slots.**
`CodeModelResult`/`CodeGenerationResult` kodieren heute die V1-Artefakt-Menge strukturell
(IBeginWfs/IWfs/WfsBase/Wfs/TO). V2 kann mehr, weniger oder andere Dateien erzeugen.
`CodeGenerationResult` wird zur Liste von `CodeGenerationSpec { Content, FilePath,
OverwritePolicy }` — die Policy (heute in `FileGenerator.Generate` hart je Slot: `WhenChanged`
für IWfs/IBeginWfs/WfsBase, `Never` für `{Task}WFS.cs` **und** die TO-Stubs) wandert als
**Metadatum an den Spec**. Der
`FileGenerator` wertet nur noch aus. Konsumenten der heutigen benannten Slots
(`FileGenerator`, `LoggerAdapter`, `RegressionTests`) stellen auf die Liste um.

**(4) Pfade versionieren.**
`PathProvider` bildet Dateinamen aus den Facts — mit versionierten Facts (1) und einer per-Unit
gewählten Factory (`IPathProviderFactory` bekommt die Version bzw. der V2-Generator bringt seinen
eigenen Provider mit) ist auch die Ablage je Generation frei wählbar.

**(5) Generator-Generationen.**
`CodeGeneratorV1` ist der heutige `CodeGenerator` — nach der ST-Migration (s.u., Variante B)
CodeBuilder-basiert, mit bewiesener Byte-Identität zum Bestand, danach eingefroren.
`CodeGeneratorV2` ist frei in CodeModel-Schnitt und Emitter-Aufbau — **mit der einen harten
Auflage aus Grundsatz 3: `I{Task}WFS`/`IBegin{Task}WFS` (samt der in Signaturen sichtbaren TOs)
emittiert er identisch zu V1** (dieselben Emitter-Bausteine wiederverwenden, nicht duplizieren).
Beide implementieren `ICodeGenerator`; der Dispatcher wählt je Unit. Neue Sprachversion
freischalten heißt dann: Konstante in `NavLanguageVersion.SupportedVersions` + Facts-Instanz +
Generator-Zweig im Dispatcher.

**(6) Navigation: versionierte Such-Strategie als Zielbild (entschieden 2026-07-05 — „Option B").**
Die Nav→C#-Navigation wird perspektivisch hinter eine kleine, pro Generation implementierte
Schnittstelle gestellt (sinngemäß `INavToCSharpLocationStrategy`: „finde Task-Implementierung /
Trigger / Init / Exit"); die Weiche liest wie beim Codegen die Symbol-Version. Begründung:
saubere Separation of Concerns — die `*CodeInfo`-Klassen sind **Mittel zum Zweck** und damit
privates Vokabular ihrer Generation; sie dürfen vollständig versionsspezifisch werden, statt als
generationsübergreifender Property-Vertrag erstarren zu müssen. Konsequenzen:

- **Jetzt nicht bauen.** Die Schnittstelle entsteht erst in Step 6/7, wenn der V2-Schnitt
  feststeht — vorher ist unbekannt, welche Operationen sie braucht. Behält V2 das Muster
  „generierte abstrakte Basis + User-Derived" (die V2-Richtungs-Notiz unten ist damit verträglich),
  degeneriert die Strategie zur geteilten V1-Implementierung mit versionierten Namen und kostet
  nichts.
- **Step 2 markiert die Sollbruchstelle:** `FullyQualifiedWfsBaseName`/`WfsBaseTypeName` sind
  semantisch „der Navigations-Anker-Typ dieser Generation", nicht „die WfsBase" — das wird in der
  Doku der Properties explizit gemacht. Der Trichter ist schmal: `GoToSymbolBuilder` + die
  GoTo-Provider reichen CodeInfos nur an die vier `LocationFinder`-Suchen durch.
- Die (invariante) Annotations-Suche bleibt als **Fallback-Baustein innerhalb einer V2-Strategie**
  in der Hinterhand (gestalt-unabhängig, aber teurer als die Symboltabellen-Suche — als
  Voll-Ersatz für die Nav→C#-Richtung verworfen).

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

**Entscheidung (2026-07-05): Variante B — Voll-Migration auf CodeBuilder, auch für V1.**
Ein handgeschriebener, indent-bewusster Emitter im Stil von Roslyn-Source-Generatoren
(`WriteLine`, `Indent`-Scopes, Region-/XML-Doku-Helfer) als eigener Baustein
`Nav.Language/CodeGen/CodeBuilder/`, mit Unterklassen-Verzweigung für Generations-Deltas. Die
Migration ist ein **isolierter, früher Schritt** (vor jeder V2-Arbeit) mit hartem Abnahmekriterium:

- **Byte-Identitäts-Beweis ist Pflicht:** Regression-Snapshots unverändert **und** Lauf über den
  kompletten Bestandskorpus (~1912 Dateien) mit leerem Diff. Die ST-Eigenheiten sind die
  Parity-Checkliste: `anchor`-Einrückung (Parameterlisten!), Separatoren, CRLF, `<if>`-Ränder,
  Leerzeilen-Verhalten leerer Sub-Templates.
- **Danach entfällt der komplette ST-Sonderweg:** `Antlr4.StringTemplate`-Abhängigkeit der Engine,
  `Templates/*.stg` + `Templates/Resources.cs` (EmbeddedResources), der Facts-Export
  (`CustomBuild.targets`-Target `GenerateStringTemplateExport`, `Build/CodeGen/GenerateCodeGenFacts.cs`,
  `CodeGenFacts.generated.cs`) — die Facts sind dann gewöhnlicher, versionierbarer C#-Code.
- **Performance-Erwartung:** plausibel besser (ST rendert reflektiv/interpretativ pro Task; der
  Emitter schreibt direkt). Vorher/nachher am Bestandskorpus **messen** und festhalten — Erwartung,
  kein Versprechen.

Damit teilen sich V1 und V2 dieselbe Emitter-Technologie; die Weiche unterscheidet nur noch
CodeModel-Schnitt, Facts und Emitter-Klassen, nicht mehr die Render-Technik.

## Schritt-Plan (Vorschlag)

Reihenfolge so gewählt, dass jeder Schritt für sich baubar/testbar ist und V1-Neutralität
(Regression-Snapshots + Korpus) nach jedem Schritt gilt:

| Step | Inhalt | Fertig, wenn |
|---|---|---|
| 1 | Facts-Zweiteilung: `CodeGenInvariants` (statisch) + `ICodeGenFacts` mit `NavCodeGenFacts.For(version)`; V1-Instanz; Statik wird V1-Fassade | Engine baut; `CodeGenFactsTests` pinnen V1-Werte (Invarianten + V1-Instanz); kein Verhaltens-Diff |
| 2 | `*CodeInfo` versionsbewusst (Facts aus Symbol-Version; Interface-Ableitungen bleiben per Grundsatz 3 invariant, `TaskDeclarationCodeInfo` unverändert). Dazu aus der Voranalyse: `ICodeGenFacts` + `NavCodeGenFacts.For(version)` entstehen hier aus den echten Konsumenten; invariante Ableitungen (`IWfsTypeName`, `IwflNamespace`, `TaskDeclarationCodeInfo`) auf `CodeGenInvariants` umhängen; `WflNamespaceSuffix`-Doppelgebrauch entflechten (invariante IBegin-Ablage vs. versionierbarer Implementierungs-Namespace); tote Properties streichen (`AfterMethodName`, `BeginMethodName`, `TriggerMethodName`); `LocationFinder`-Konstante `BeginLogicMethodName` auf `TaskInitCodeInfo` umstellen; Anker-Rolle von `FullyQualifiedWfsBaseName` dokumentieren (Baustein 6) | Navigation/QuickInfo-Tests grün; V1-Snapshots byte-identisch |
| 3 | `CodeGenerationResult` → Spec-Liste mit `OverwritePolicy`-Metadatum; `FileGenerator`/Logger/RegressionTests umgestellt | `nav test` beide TFMs grün; Regression byte-identisch |
| 4 | **ST-Migration (Variante B), isolierter Schritt — Artefakt für Artefakt:** CodeBuilder-Grundgerüst (`CodeGen/CodeBuilder/`), dann je Template-Familie ein Sub-Step (IBeginWFS, IWFS, WFSBase, WFSOneShot, TO — ST und CodeBuilder koexistieren solange); zum Schluss ST-Sonderweg entfernen (`.stg`, `Resources.cs`, Facts-Export, `Antlr4.StringTemplate`) | **Byte-Identität je Sub-Step bewiesen** (Snapshots **und** Bestandskorpus-Diff leer, nach jedem Sub-Step); Perf vorher/nachher gemessen |
| 5 | Dispatcher `VersionDispatchingCodeGenerator` als `CodeGeneratorProvider.Default`; bisheriger Generator wird `CodeGeneratorV1` | Pipeline-Verhalten für V1 unverändert (Korpus-Diff leer) |
| 6 | V2-Inhalte: Facts V2, CodeModel-/Emitter-Schnitt, `PathProvider`-V2, **keine TO-Stubs mehr** — **Interfaces `I{Task}WFS`/`IBegin{Task}WFS` identisch zu V1 emittiert** (geteilte Emitter-Bausteine); **Version 2 in `SupportedVersions` freischalten** | neue Snapshot-Fixtures `Regression/Tests-V2/`; `nav snapshot` beherrscht beide; Interface-Identitäts-Test V1↔V2 |
| 7 | Navigation end-to-end für V2: falls der V2-Schnitt das V1-Suchverfahren bricht (kein Anker-Typ + Derived-Descent), Such-Strategie-Schnittstelle einziehen (Baustein 6, „Option B"); dann verifizieren (GoTo Nav→C#, C#→Nav via Annotations, Rename, FindReferences, Cross-Version-`taskref`) | VS-Smoke + Testabdeckung |

Nach jedem Step: Code-Review + `nav test` (net472 **und** net10.0), Commit-Message liefern —
Commit macht der Nutzer (Arbeitsweise siehe `CLAUDE.md`).

## Entschiedene Design-Fragen (mit dem Nutzer geklärt 2026-07-05 — nicht ohne Grund umwerfen)

1. **Was ändert sich in V2?** Die generierten Interfaces `I{Task}WFS`/`IBegin{Task}WFS` ändern
   sich **nicht** — sie sind der Vertrag zum Workflow-Code (⇒ Grundsatz 3, Schnittstellen-
   Invariante). Ändern dürfen sich die **Implementierung** und die **Anzahl/Zuschnitt der
   generierten Dateien**; so flexibel ist der Umbau auszulegen (⇒ Spec-Listen-Generalisierung,
   Baustein 3).
2. **Cross-Version-`taskref` ist erlaubt** — durch die Schnittstellen-Invariante braucht es weder
   Versions-Info am Include-Extrakt noch eine Verbots-Diagnose (⇒ Baustein 2a samt
   Wächter-Hinweis).
3. **Annotations-Vertrag ist fixiert** (⇒ Grundsatz 4): jede Generation emittiert die heutigen
   Tags.
4. **CodeBuilder: Variante B** — Voll-Migration auch für V1, als isolierter früher Schritt mit
   Byte-Identitäts-Beweis; erwarteter Nebeneffekt ist bessere Performance (messen).
5. **Header:** V2 schreibt denselben `<auto-generated>`-Header (⇒ Grundsatz 5; Befund: der Header
   ist heute versionsfrei — `ProductVersion` taucht in keinem Template auf, nur im Konsolen-Logger).
6. **Facts-Zweiteilung:** invariante Facts bleiben statische Konstanten (`CodeGenInvariants`:
   Annotation-Tags + Interface-Namensbausteine), nur versionierbare Werte wandern in
   `ICodeGenFacts`; der `WfsClassSuffix`-Doppelgebrauch (Interface- vs. Klassen-Suffix) wird
   entflochten (⇒ Baustein 1). Grundsätze 3+4 sind damit strukturell erzwungen, nicht bloß
   Test-Konvention.
7. **TO-Stub ist ein Relikt:** nav.exe schreibt TOs nur als einmalige Stubs
   (`OverwritePolicy.Never`); den Inhalt besitzt der GUI-Generator. V1 behält das Verhalten
   (eingefroren), **V2 schreibt keine TO-Dateien mehr** — `GenerateToClasses` läuft für
   V2-Dateien sinngemäß ins Leere. Die TO-Typen in den (invarianten) Interface-Signaturen
   liefert weiterhin der GUI-Generator.
8. **Options-Flags gelten generationsübergreifend, sinngemäß:** die MSBuild-/CLI-Flags
   (`NavGenerateToClasses`/`WflClasses`/`IwflClasses`, `Strict`) bleiben der öffentliche
   Vertrag; jede Generation interpretiert sie auf ihre Artefakt-Menge — kein neues
   Options-Schema für V2.
9. **Step-4-Vorgehen — Artefakt für Artefakt:** je Template-Familie ein Sub-Step (IBeginWFS,
   IWFS, WFSBase, WFSOneShot, TO); ST und CodeBuilder koexistieren während der Migration, der
   ST-Sonderweg fällt erst mit dem letzten Sub-Step. **Nach jedem Sub-Step** laufen Snapshots
   **und** der volle Korpus-Beweis (bester Bisect-Punkt bei Parity-Abweichungen).
10. **Korpus-Logistik:** der Bestandskorpus (~1912 Dateien) liegt lokal; den Pfad nennt der
    Nutzer, sobald Step 4 ansteht. Beweis-Lauf (Kopie → nav.exe → Diff) und Perf-Messung werden
    geskriptet und können von Claude selbst gefahren werden.
11. **Navigations-Zielbild ist „Option B" — versionierte Such-Strategie** (⇒ Baustein 6): die
    `*CodeInfo`-Klassen sind Mittel zum Zweck und dürfen vollständig versionsspezifisch werden;
    ein genereller „Anker-Vertrag" für alle Generationen (Option A: jede Generation garantiert
    Basisklasse + Derived-Descent) wird **nicht** zum Grundsatz erhoben, damit V2 z.B. auf eine
    separate Basisklasse verzichten darf. Die Strategie-Schnittstelle entsteht erst in Step 6/7;
    Step 2 markiert nur die Sollbruchstelle (Anker-Rolle dokumentieren). Eine annotations-basierte
    Nav→C#-Suche (Option C) ist als Voll-Ersatz verworfen, bleibt aber Fallback-Baustein einer
    V2-Strategie.

## V2-Richtungs-Notiz (unverbindlich, Stand 2026-07-05)

Keine Festlegung, aber die Richtung, in die V2 gedacht wird: Heute werden alle erlaubten
Subtask-Aufrufe als `IBegin{Task}WFS`-Parameter in die abstrakten `…Logic`-Methoden von
`WFSBase` gereicht — kommt in der `.nav`-Datei eine Choice bzw. ein Task-Knoten hinzu, ändert
sich die (oft ellenlange) Signatur, die der Benutzer-Code implementiert. Die V2-Idee: ein
**methodenspezifischer Kontext-Parameter**, der die Subtask-Aufrufe kapselt
(`context.BeginMessageBox(…)`), statt einzelner `IBegin*`-Parameter.

Verträglichkeits-Befund (code-verifiziert am Regression-Snapshot): die `IBegin*`-Parameter
existieren **nur** in `WFSBase` (abstrakte `…Logic`-Methoden, `Begin{Node}`-Helfer,
Konstruktoren/Felder) und im `{Task}WFS`-OneShot — **nicht** in `I{Task}WFS`/`IBegin{Task}WFS`.
Die Idee ist also mit Grundsatz 3 verträglich: die Task-Schnittstelle bleibt identisch; die
Umstellung einer Datei auf `#version 2` bricht bewusst den Benutzer-**Logic**-Code (andere
abstrakte Signaturen), nie die Aufrufer des Tasks.

## Verifikation (Wiederholrezept)

- V1-Neutralität: `nav snapshot` erzeugt keine Diffs; zusätzlich Lauf über den Bestandskorpus
  (`nav.exe` gegen Kopie, `git diff` leer).
- Tests: `nav test` (net472) und `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj
  -f net10.0` — Engine-Tests müssen auf **beiden** TFMs grün sein.
- Navigation: VS-Extension (`nav build` + `nav install`) Smoke — GoTo aus `.nav` in generierten
  Code und zurück, für je eine V1- und (ab Step 6) V2-Datei.
