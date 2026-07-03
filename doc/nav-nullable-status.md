# Nav-Nullable — Status & Handoff

Stand-Dokument für die **Nullable-Reference-Types-Kampagne** in `Nav.Language` (netstandard2.0,
Assembly `Pharmatechnik.Nav.Language`). Der Engine-Kern wurde **ordnerweise** auf NRT umgestellt —
ursprünglich **pro Datei** per `#nullable enable`, seit **Welle 5** projektweit. Ziel: **messerscharfe**
Annotationen — keine falschen not-null-Zusagen (latente `NullReferenceException`), aber auch keine
unnötigen `?`/Null-Checks, wo null de facto nie auftritt.

> **✅ ENDZUSTAND ERREICHT (Welle 5, 2026-07-03).** `Nav.Language` trägt projektweit
> `<Nullable>enable</Nullable>` + `<WarningsAsErrors>Nullable</WarningsAsErrors>` — der **Compiler**
> ist das harte Gate (jede Nullable-Verletzung ist ein Build-Fehler; empirisch per Sonde verifiziert).
> Die per-Datei-`#nullable enable`-Direktiven sind **alle entfernt** (329 Dateien). `nav nullaudit`
> bleibt als **Hygiene- und Diagnose-Wächter** (Abschnitt 4): prüft die Projekteinstellung + Direktiven-
> Drift und difft den (leeren) Warnungs-Stand gegen die Baseline. Abschnitte 1–3 dokumentieren die
> Umstellungs-Mechanik als **historischen Record**; der selbsttragende Gesamtplan liegt außerhalb des
> Repos in `~\.claude\plans\wir-haben-jetzt-bereits-staged-bonbon.md`.

## 1. Mechanik — warum das wasserdicht ist

- **Per-Datei-Direktiven überstimmen die Projekteinstellung.** Fertige Dateien behalten volle
  Prüfung, auch wenn das Projekt oblivious bleibt.
- **Prüfbau mit `-p:Nullable=warnings`** (via `nav nullaudit`, Abschnitt 4): Unkonvertierte Dateien
  bekommen nur den **Warning-Kontext ohne Annotations-Kontext** — eigene Deklarationen bleiben
  oblivious (kaum Rauschen), aber die Flussanalyse warnt, wo sie **annotierte APIs falsch
  konsumieren** (CS8602 auf `T?`-Return, CS8625 bei null-Literal in non-null-Parameter). Genau das
  sind die Vertragsverletzungs-Signale: entweder ist die Annotation falsch (fehlendes `?`) oder der
  Konsument hat einen latenten NRE → Repro-Test + Fix.
- **Eingecheckte Baseline** (`Build\nullaudit-baseline.txt`) neutralisiert das Restrauschen der noch
  offenen Ordner — nur Deltas (neues Datei/Warncode-Paar oder Zähler-Anstieg) zählen als Regression.
- **LSP/MCP als Konsum-Validierung**: beide sind bereits projektweit nullable und bauen die
  Public Surface der Engine mit — ihre CS86xx/87xx-Warnungen sind Erstklasse-Regressionen.
- **Unnötige `?`** findet kein Compiler → Playbook-Regeln + Review-Checkliste (Abschnitt 3).
- **Finaler Beweis (Welle 4):** Smoke-Build `-p:Nullable=enable` über das Gesamtprojekt muss
  warnungsfrei sein.

netstandard2.0-Fallen: Die NRT-Flussanalyse-Attribute fehlen in der BCL → Polyfill
`Nav.Language\Internal\NullableAttributes.cs` (ab Welle 0 vollständig, danach eingefroren; wer ein
Attribut neu nutzt, das dort fehlt, bekommt `CS0122`). Und: netstandard2.0 hat **keine** annotierten
BCL-Referenzen → `String.IsNullOrEmpty(s)` verengt `s` **nicht** auf non-null; nach so einem Guard
folgt `CS8602` → eigenen `is null`-Check schreiben oder Parameter gleich non-null lassen.

## 2. Fortschritt je Ordner

Legende Welle: **1** Fundament · **2a** CodeGen · **2b** SemanticAnalyzer · **3/Px** Feature-Paket ·
**✅** fertig (Vorarbeit vor dieser Kampagne).

| Ordner | Welle | gesamt | konvertiert | Stand |
|---|---|---:|---:|---|
| Syntax | ✅ | 66 | 66 | fertig |
| Text | ✅ | 20 | 20 | fertig |
| SemanticModel | ✅ | 49 | 49 | fertig (in 2b revalidiert — keine geratenen Verträge) |
| Symbols | ✅ | 1 | 1 | fertig |
| Internal | 1 | 4 | 4 | fertig |
| Common | 1 | 4 | 4 | fertig |
| (Projektwurzel) | 1 | 2 | 2 | fertig |
| Properties | 1 | 1 | 1 | fertig |
| CodeGen (+ CodeModel, Templates) | 2a | 37 | 37 | fertig |
| SemanticAnalyzer | 2b | 45 | 45 | fertig |
| CodeFixes (+ ErrorFix, Refactoring, StyleFix) | 3 / P1 | 32 | 32 | fertig |
| Completion | 3 / P2 | 3 | 3 | fertig |
| GoTo | 3 / P2 | 2 | 2 | fertig |
| Rename | 3 / P2 | 1 | 1 | fertig |
| Diagnostic | 3 / P3 | 13 | 13 | fertig |
| Dependencies | 3 / P3 | 4 | 4 | fertig |
| FindReferences | 3 / P4 | 10 | 10 | fertig |
| References | 3 / P4 | 3 | 3 | fertig |
| Workspace | 3 / P5 | 8 | 8 | fertig |
| Provider | 3 / P5 | 13 | 13 | fertig |
| Generator | 3 / P6 | 6 | 6 | fertig |
| QuickInfo | 3 / P6 | 2 | 2 | fertig |
| CallHierarchy | 3 / P6 | 1 | 1 | fertig |
| CodeActions | 3 / P6 | 2 | 2 | fertig |
| **Gesamt** | | **329** | **329** | **100 %** |

> Historische Fortschrittszahlen (Stand nach Welle 3, alle Ordner 100 %). **Seit Welle 5 ist die
> Spalte „konvertiert" gegenstandslos** — die Umstellung erfolgt projektweit, die per-Datei-Direktiven
> sind entfernt. Der Endzustand ist erreicht: **Welle 4** bewies den warnungsfreien Voll-Enable-Build
> (und fand die Lücke `GenerationOptions.cs`), **Welle 5** schaltete `Nav.Language` dauerhaft auf
> `<Nullable>enable</Nullable>` + `<WarningsAsErrors>Nullable</…>` und entfernte alle 329 Direktiven.
> `Build\nullaudit-baseline.txt` ist **leer**; `nav nullaudit` läuft als Hygiene-Wächter grün; net472
> (1271) + net10.0 (1263) grün.

## 3. Playbook-Regeln (Review-Checkliste je Ordner)

1. **Bestandsaufnahme:** ReSharper-Annotationen ablösen — `[CanBeNull]` → `?`, `[NotNull]` →
   default (weg), `[ItemCanBeNull]` → Element-`?`; `using JetBrains.Annotations;` entfernen.
2. **Datei für Datei** (UTF-8 **mit** BOM; `#nullable enable` als erste Codezeile, Stil wie
   `Syntax/`/`SemanticModel/`): öffentliche Signaturen zuerst, dann Felder/Locals. Flussattribute
   aktiv nutzen: `NotNullWhen(true)` für `TryGet…`/`Is…`, `MemberNotNull` für Init-Helper,
   `NotNullIfNotNull` für Pass-Through.
3. **Kein `!`** ohne Begründungskommentar (Beweis, warum non-null).
4. **Kein neues `?`** ohne benennbaren Null-Zufluss; im Zweifel non-null + Invariante an der Quelle
   herstellen (`??`-Normalisierung) statt `?.`-Kaskaden bei jedem Konsumenten. Tote `?.` auf
   beweisbar non-null entfernen.
4a. **String-Properties bestmöglich non-null** (Default): wo „abwesend" und „leer" dasselbe meinen,
   liefert die Property `string` (nie `null`) und normalisiert im Zweifel auf `String.Empty` — das
   erspart Konsumenten Null-Checks und umgeht die netstandard2.0-`IsNullOrEmpty`-Falle. **Ausnahme:**
   `null` bleibt, wenn es einen **eigenen, ausgewerteten Zustand** kodiert (z.B. `Location.FilePath` =
   „hat gar keine Datei" — bewusst `string?`, mehrere Presence-Zweige hängen daran). Der `""`-Fallback
   gehört dann an den **Ausgabe-Rand** (DTO/Serialisierung, z.B. `FilePath ?? ""`), nicht ins
   Domänenmodell.
5. **`ArgumentNullException`-Guards an public Einstiegspunkten bleiben** (oblivious-Konsumenten wie
   die VS-Extension!); interne Guards dürfen fallen, wenn die Annotation den Vertrag trägt.
6. **Keine Verhaltensänderung außer NRE-Fixes** — und die nur mit **Repro-Test vor dem Fix**
   (rot → Fix → grün), wenn die NRE über die öffentliche API mit konstruierbarem Input erreichbar ist
   (typisch: fehlerhafter .nav-Quelltext → Missing Token → null-Kind). Nur-Annotation reicht, wenn
   null nachweislich unerreichbar ist (Beweis als Kommentar).
7. **Polyfill nie lokal anfassen** (Welle 0 macht ihn vollständig); fehlt doch etwas → an Integrator.
8. **Abschluss:** 0 Nullable-Warnungen im eigenen Ordner (`nav nullaudit -Detail <Ordner>`),
   `nav test` (net472) **und** `dotnet test … -f net10.0` grün, `nav nullaudit -UpdateBaseline`,
   diese Statustabelle aktualisieren, Commit-Message liefern (Nutzer committet).

## 4. Tool `nav nullaudit` (Post-Welle-5-Rolle)

> `Tools\Commands\Functions\Invoke-NullAudit.ps1`, `.FUNCTIONALITY nullaudit`. Seit Welle 5 **kein**
> Fortschritts-Tracker mehr, sondern **Hygiene- und Diagnose-Wächter** über den Endzustand. Der
> harte Regressions-Schutz ist ab jetzt der **Compiler** (projektweit `enable` +
> `WarningsAsErrors=Nullable`); `nav nullaudit` ergänzt Drift-Erkennung, einen aggregierten Report
> und den `-Detail`-Arbeitsmodus.

- **Hygiene** (`-NoBuild`): (a) prüft, dass `Nav.Language.csproj` weiterhin `<Nullable>enable</Nullable>`
  trägt (fehlt es → **Abbruch**); (b) scannt `Nav.Language\**\*.cs` (ohne bin/obj/`*.generated.cs`)
  **zeilenbasiert** auf `#nullable`-**Direktiven-Drift** — `#nullable disable`/`restore` heben das Gate
  lokal aus (→ **Abbruch**), ein redundantes `#nullable enable` ist nur Hinweis; (c) Tabelle
  Ordner × (Dateien, ~Suppressions). *Wichtig:* die Direktiv-Erkennung ist **zeilenbasiert** (Trim ==
  Direktive), nicht String-Suche — ein Vorkommen in der XML-Doku (`<c>#nullable enable</c>`) zählt
  **nicht** (der frühere Substring-Scan zählte es fälschlich mit → die Lücke `GenerationOptions.cs`, s.
  Welle-4-Eintrag).
- **Prüfbau**: je Projekt (Nav.Language, Nav.Language.Lsp, Nav.Language.Mcp) `dotnet build
  --no-incremental` in **nativer** Einstellung (alle projektweit `enable`; **kein** `-p:Nullable=warnings`
  mehr, das würde den Annotations-Kontext abschalten und z. B. CS8618 verdecken). Für Nav.Language
  `-p:WarningsAsErrors=`, damit Verstöße als **Warnung** einsammelbar sind statt den Build hart
  abzubrechen. `-flp:"warningsonly;logfile=…"`, `--no-incremental` Pflicht (übersprungene Compiles
  emittieren keine Warnungen).
- **Aggregation**: Regex `warning CS8[67]\d\d` aus dem File-Log, Pfade repo-relativ normalisiert,
  gruppiert nach **(Datei, Warncode) → Anzahl** — bewusst ohne Zeilennummern (robust gegen Drift).
- **Baseline-Diff** gegen `Build\nullaudit-baseline.txt` (im Endzustand **leer**): Tab-getrenntes
  Zeilenformat, sortiert. Neues Paar oder Zähler-Anstieg = **Regression → Exit ≠ 0**.
- **Parameter**: `-UpdateBaseline`, `-Detail <Ordner>` (Roh-Warnungen mit Zeilennummern), `-NoBuild`
  (nur Hygiene). Der **Suppression-Zähler** (`!`-Vorkommen pro Ordner) läuft mit.

## 5. Entscheidungslog

- **Keine Big-Bang-Umstellung, per-Datei-Direktiven statt projektweitem `<Nullable>`** — inkrementell,
  jede Datei einzeln grün, Reviews < ~35 Dateien.
- **Grober Ordner-Graph statt Typ-Graph** für die Reihenfolge (Internal/Text/Common → Syntax →
  CodeGen → SemanticAnalyzer → SemanticModel → Feature-Ordner). Bottom-up ist Qualitäts-, keine
  Korrektheitsfrage: falsch geratene Verträge werden automatisch aufgedeckt, sobald die tiefere
  Schicht annotiert ist.
- **SemanticModel/ (fertig) hängt über die einzige Kante `CodeGenerationUnitBuilder.cs` an
  SemanticAnalyzer** → Welle 2b re-validiert SemanticModel gratis; neue Warnungen dort im selben Step
  fixen.
- **String-Properties bestmöglich non-null (Default), `null` nur bei eigenem ausgewertetem Zustand**
  (Playbook-Regel 4a). Entschieden am 2026-07-03 anhand `Location.FilePath`: bleibt `string?`, weil
  `null` = „keine Datei" real ausgewertet wird (`CachedSyntaxProvider`, `NavValidateResult.crossFile`,
  `NormalizedFilePath`); `""`-Fallback sitzt am DTO-Rand (`NavEditDto`).
- **Endzustand (Welle 5) offen** — Empfehlung: nach 100 % auf projektweites `<Nullable>enable</…>`
  umschalten + `<WarningsAsErrors>Nullable</…>`, Direktiven raus. **Entscheidung trifft der Nutzer
  erst nach Welle 4.**
- **Welle 2a Teil 1 (`CodeGen\CodeModel\`, 18 Dateien):** `CodeModelBuilder.GetTaskBeginParameter`
  reichte `taskNode.Declaration` (nullable) ungefiltert an `GetTaskBeginsAsParameter` (erwartet
  non-null) weiter → CS8620 und, isoliert betrachtet, ein latenter NRE (`GetTaskBeginAsParameter`
  dereferenziert die Declaration). Über die öffentliche API ist der Fall aber **unerreichbar**:
  ein Task-Node mit unaufgelöster Declaration erzeugt `Nav0010CannotResolveTask0`
  (`DiagnosticSeverity.Error`), und `CodeGenerator.Generate` bricht bei `Diagnostics.HasErrors()`
  vorab ab. Fix daher verhaltensneutral per `.WhereNotNull()` — dieselbe Idiomatik wie im
  Schwester-Pfad `TransitionCodeModel.GetTaskDeclarations`. Kein Befundlog-Eintrag (nicht über die
  öffentliche API mit konstruierbarem Input auslösbar). String-Konstruktor-Parameter mit
  `?? String.Empty`-Normalisierung wurden auf `string?` gesetzt (Hausstil analog `CodeParameter`),
  `?? throw ArgumentNullException`-Guards für Objekt-Parameter blieben (non-null Vertrag, Schutz vor
  oblivious-Aufrufern).
- **Welle 2a Teil 2 (CodeGen-Rest, 18 Dateien):** Der in der Baseline geführte `CS8602` in
  `CodeGenerator.GenerateCode` stammte aus `codeModelResult.TaskDefinition.CodeGenerationUnit`
  (`ITaskDefinitionSymbol.CodeGenerationUnit` ist `CodeGenerationUnit?` — nullable für importierte/
  noch nicht angehängte Symbole). Hier ist die Unit aber stets vorhanden: Das `TaskDefinition` stammt
  aus `codeGenerationUnit.TaskDefinitions`, und `TaskDefinitionSymbol.FinalConstruct` setzt die
  Rückreferenz beim Aufbau der Unit. Fix daher per `!` mit Invariantenkommentar (kein Befundlog —
  über die öffentliche API nicht als `null` konstruierbar). Die `[CanBeNull]`-Modellparameter der
  `Generate…CodeSpec`-Methoden und die `CodeModelResult`-Modelle wurden zu `?` (die Modelle sind je
  nach `GenerationOptions.Generate…`-Flags optional), `WriteFile`/`WriteFileImpl` liefern
  `FileGeneratorResult?` und werden per `WhereNotNull()` gefiltert. `?? String.Empty`-normalisierte
  String-Parameter → `string?`; tote `?? String.Empty` auf beweisbar non-null `ISymbol.Name` entfernt.
  netstandard2.0-BCL bleibt oblivious → `Path.GetDirectoryName`/`GetManifestResourceStream` erzeugen
  trotz „Lass krachen"-`null`-Durchreichung keine Warnung.
- **Welle 2b (SemanticAnalyzer, 45 Dateien):** Die uniformen `Nav####*.cs`-Analyzer konsumieren nur
  das (bereits fertige) SemanticModel und deklarieren kaum eigene Verträge → `#nullable enable` liess
  ausser den drei Baseline-Files (`Nav0024`, `Nav0025`, `Nav1002`, zusammen 6× `CS8602`) **keine**
  neuen Warnungen entstehen. Insbesondere blieb die Revalidierungs-Kante
  `SemanticModel\CodeGenerationUnitBuilder.cs` warnungsfrei — SemanticModel hatte hier **keine
  geratenen Verträge**. Alle drei Baseline-`CS8602` waren **Narrowing-Artefakte hinter bereits
  vorhandenen Null-Guards**, keine echten NREs: `.Where(x => x != null)` verengt in NRT nicht.
  Nav0024/Nav0025 daher verhaltensneutral per `.WhereNotNull()` (Element-Null, gleiche Idiomatik wie
  `TransitionCodeModel.GetTaskDeclarations`); Nav1002 per `!` mit Begründungskommentar (Null liegt auf
  der `Namespace`-**Property**, nicht dem Element → `WhereNotNull` greift nicht; das vorherige `Where`
  beweist non-null). Kein Befundlog-Eintrag (keine über die öffentliche API auslösbare NRE). Einzige
  neue Suppression: das eine begründete `!` in Nav1002.
- **Welle 3 / P1 (CodeFixes, 32 Dateien):** Beide Baseline-`CS8602` waren Narrowing-Artefakte hinter
  bestehenden Guards, keine echten NREs — verhaltensneutral aufgelöst: `AddMissingExitTransitionCodeFix`
  (2×) per `!` (Zeile mit `ExitConnectionPointReference` nach `.Where(…!=null)`; und `SourceReference`
  nach dem `CanApplyFix`-Guard), `IntroduceChoiceCodeFix` (1×) durch **null-toleranten Vertrag** von
  `TaskDefinitionSymbolExtensions.ValidateNewNodeName` (`this ITaskDefinitionSymbol?` — intern bereits
  über `GetDeclaredNodeNames` null-fest) statt eines `!`. Weitere Muster: die public `RenameCodeFix`-API
  (`ValidateSymbolName`/`GetTextChanges` + Leaf-Overrides) auf `string?`-Parameter/Rückgabe gesetzt
  (Validate liefert `null` = „gültig"); `IntroduceChoiceCodeFix.GetTextChanges` normalisiert den Namen
  jetzt per `?? String.Empty` (Hausstil der Rename-Fixes) — verhaltensgleich (leer/`null` wirft weiterhin
  `ArgumentException`). Zwei `FirstOrDefault`→Konstruktor-Flüsse in den StyleFix-Providern per
  `.WhereNotNull()` verengt (statt `.Where(x=>x!=null)`). Guard-gedeckte `!`: `SafeCreateTuple`
  (`GetLocation()!` — nicht-fehlende Tokens haben Parent → non-null), `Contains(nodeName!)`
  (`IsValidIdentifier` beweist non-null), `RemoveUnusedTaskDeclarationCodeFix` (`Syntax!` nach
  CanApplyFix). ReSharper-`[NotNull]`/`[CanBeNull]` (3 Dateien) abgelöst. **Encoding-Falle:** zwei
  Provider-Dateien (`RemoveUnusedNodesCodeFixProvider`, `RemoveUnusedTaskDeclarationCodeFixProvider`)
  waren im Repo **Windows-1252** (Bytes 0xF6/0xE4/0xFC); ein simples „BOM davorsetzen" per Edit hätte die
  Umlaut-Bytes durch den UTF-8-Read zu `U+FFFD` zerstört. Korrekt behandelt: aus HEAD zurückgeholt, mit
  `nav fixenc` Win-1252→UTF-8-BOM umkodiert, dann die Nullable-Änderungen erneut angewandt. Kein
  Befundlog-Eintrag.
- **Welle 3 / P2 (Completion+GoTo+Rename, 6 Dateien):** **Null neue Warnungen, null Suppressions** — die
  Feature-Kerne konsumieren durchweg bereits messerscharf annotierte Verträge. Kein NRE-Befund. Die
  Konsum-Kanten trugen sauber: `TaskDefinitionSymbolExtensions.TryFindNode` ist als **null-toleranter
  Extension-Vertrag** (`this ITaskDefinitionSymbol?`, `string? name`) geschrieben — daher kompiliert
  `context.Task.TryFindNode(context.ExitNodeName)` in `ExitConnectionPointItems` trotz nullbarer
  `Task`/`ExitNodeName` ohne CS8602/CS8604 (Extension-Aufruf dereferenziert `this` nicht), keine
  zusätzlichen Guards nötig. `[CanBeNull]`→`?`/`[NotNull]`→default in allen dreien abgelöst
  (`JetBrains.Annotations`-`using` raus). Zwei substanzielle Kontrakt-Präzisierungen: (1)
  `NavCompletionService.GetPathCompletions` liefert bewusst `IReadOnlyList<NavCompletionItem>?` — `null`
  ist das **Sentinel** „kein taskref-String-Kontext" (dann übernimmt die Nav-/Edge-Completion),
  unterscheidbar von der leeren Liste „String-Kontext, aber keine Treffer". (2)
  `GoToTargetResolver.One` nimmt jetzt `Location?` (Aufrufer reichen `…Declaration?.Location` durch) und
  filtert die abwesende Location zur leeren Sequenz — damit sind die von `Visit` gelieferten Elemente
  **beweisbar non-null**, weshalb in `NavGoToService.GetGoToLocations` der tote `location != null`-Guard
  vor dem `HashSet.Add` entfiel (Rule 4). Das Dedup-`HashSet` wurde auf `(string?, int)` gestellt, weil
  `Location.FilePath` bewusst `string?` bleibt (Playbook 4a). **Encoding:** GoTo/Rename lagen als
  UTF-8 **ohne** BOM vor (valide UTF-8, keine Win-1252-Falle); der Format-Hook hat beim Speichern den
  BOM ergänzt, Umlaute blieben intakt.
- **Welle 3 / P3 (Diagnostic 13 + Dependencies 4, 17 Dateien):** **Keine neuen Warnungen, 1 begründete
  Suppression, kein NRE-Befund.** Der Löwenanteil (Enums `DiagnosticCategory`/`DiagnosticSeverity`,
  `DiagnosticId`-Konstanten, die drei `DiagnosticDescriptors.*`-Tabellen, `DiagnosticExtensions`,
  `DiagnosticSeverityExtension`) trägt keine nullbaren Flüsse → reine `#nullable enable`-Direktive.
  Substanz: (1) `DependencyAnalyzer.CollectTasksDefinitionDependencies` — der `Where(tn => …
  tn.Declaration != null)`-Filter narrowt nicht in die anschließende `Select`-Lambda (Null sitzt auf der
  `Declaration`-**Property**, nicht auf dem Element → `WhereNotNull` greift nicht), daher
  `FromSymbol(taskNode.Declaration!)` mit Begründungskommentar — dieselbe Idiomatik wie Nav1002 in 2b.
  Einzige Suppression des Pakets. (2) `Diagnostic`/`DiagnosticDescriptor`/`DependencyItem`: `[NotNull]`→
  default, `[CanBeNull]`→`?`; `IEquatable<T>.Equals`, `object`-`Equals` und die `==`/`!=`-Operatoren auf
  nullbare Parameter gestellt (Standard-Muster wie `Location`/`Symbol`); tote `x != null ? … : 0`-Zweige in
  `GetHashCode` auf beweisbar non-null Feldern (`Name`/`Location`/`Descriptor`) entfernt (Rule 4).
  `Diagnostic(…, IEnumerable<Location>? additionalLocations, …)` bleibt bewusst nullbar — der `?.`/
  `?? EmptyAdditionalLocations`-Pfad plus der `Where(loc => loc != null)`-Elementfilter sind reale
  Boundary-Guards gegen oblivious-Aufrufer (Rule 5), keine tote Verteidigung. (3) `DiagnosticFormatter`:
  `WorkingDirectory`/`workingDirectory` → `string?` (der `FilePath`/`WorkingDirectory`-`null`-Doppelcheck in
  `FormatFilePath` ist echte Presence-Logik, `Location.FilePath` ist `string?`), alle
  `IFormatProvider formatter`-Parameter → `IFormatProvider?` (die Methoden reichen `formatter` an
  `String.Format` durch, das `null` akzeptiert); `UnitTestDiagnosticFormatter`-Overrides an die nullbare
  Basis angeglichen. `ArgumentNullException`-Guards an den public Konstruktoren/`Format` blieben (Rule 5).
  Alle 17 valides UTF-8 (drei ASCII-only-Dateien ohne BOM), Hook ergänzte den BOM.
- **Welle 3 / P4 (FindReferences 10 + References 3, 13 Dateien):** **Keine neuen Warnungen, 1 begründete
  Suppression, kein Befundlog-Eintrag.** Kernmuster: die `.Where(x => x != null)`-Filter narrowen in NRT
  nicht → durchgängig auf `.WhereNotNull()` umgestellt (Init-/Exit-/Node-Referenzen im
  `FindReferencesVisitor`, plus die lokalen `IEnumerable<ISymbol?> FindReferences()`-Generatoren, die nullbare
  `SourceReference`/`TargetReference` liefern); erst danach greift `SymbolOrderer.OrderByLocation<T> where
  T: ISymbol` sauber. `VisitExitConnectionPointSymbol` zieht `.WhereNotNull()` **vor** den
  `ep.Declaration == …`-Match (verhaltensgleich, null matchte die Declaration ohnehin nie). Die einzige
  Suppression ist `ReferenceItemBuilder.Invoke`: `reference.SyntaxTree!` mit Begründung — Referenz-Site-Symbole
  der aktuellen Unit tragen stets einen SyntaxTree (nur importierte TaskDeclarations sind `null`, die hier nie
  als `reference` ankommen); der bisherige defensive `SyntaxTree == null`→`return null`-Zweig entfiel, wodurch
  `Invoke` nun **non-null** `ReferenceItem` liefert (die `yield`+`OrderByLocation`-Pfade setzten Non-Null
  ohnehin voraus). Zwei nullbar-tolerante Verträge statt Suppressions: (1) `ReferenceItem`-Ctor-Parameter
  `Location? location` (das `?? throw ArgumentNullException` bleibt Rule-5-Guard und ist zugleich die
  Null-Behandlung des `CreateSimpleMessage`/`NoReferencesFoundTo`-Pfads, der `DefinitionItem.Location` — für
  eine location-lose `SimpleTextDefinition` `null` — durchreicht); (2) `FindReferencesAsync` (static) nimmt
  `ITaskDeclarationSymbol? taskDeclaration` **und** `CodeGenerationUnit? codeGenerationUnit` mit **einem**
  Kopf-Guard `if (taskDeclaration == null || codeGenerationUnit == null) return;` — deckt beide Aufrufkanten
  ab: `taskDefinition.AsTaskDeclaration` (nullbar bei location-uneindeutiger Definition) und
  `taskDeclaration.CodeGenerationUnit` (nullbar bei included Declaration). Beide sind über die cursor-basierten
  Hosts (LSP/MCP `FindSymbol` liefert stets ein Symbol der aktuellen, geladenen Unit) real non-null; der Guard
  wandelt den theoretisch-latenten NRE für malformed/included Eingaben verhaltensneutral in einen No-op (kein
  über die öffentliche API konstruierbarer Repro → kein Befundlog-Eintrag). Weitere Präzisierungen:
  `DefinitionItem.Symbol`→`ISymbol?`, `Location`→`Location?`, `sortKey`→`string?` (auf `String.Empty`
  normalisiert); die `[CanBeNull]`-Factory-Overloads (`CreateInitConnectionPointDefinition`,
  `CreateExitConnectionPointDefinitions`) auf `ITaskDeclarationSymbol?`/`DefinitionItem?` gesetzt;
  `CreateExitConnectionPointDefinitions` nutzt jetzt `exitConnectionPoint.Location` (non-null `ISymbol.Location`)
  als Dictionary-Key statt der nullbaren `exitDefinition.Location`. Tote Guards entfernt:
  `if (taskDefinitionItem == null) yield break;` in `FindTaskNodeReferences` (Parameter beweisbar non-null,
  Rule 4). Öffentliche Verträge geschärft ohne Konsum-Regression: `IFindReferencesContext.OnDefinition/
  OnReferenceFoundAsync` non-null (LSP/MCP-Collectors haben nur redundante `?.`), `NavReferenceService.FindSymbol`
  →`ISymbol?` (alle drei Hosts guarden `origin == null` bereits vor `new FindReferencesArgs`),
  `HighlightSymbolFinder` →`SymbolVisitor<IEnumerable<ISymbol?>>` (die Node-Referenzen sind element-nullbar —
  `NavReferenceService.GetHighlightSymbols` filtert per `symbol?.Location != null`, Dedup-`HashSet` auf
  `(string?, int)` wegen `Location.FilePath` = `string?`, Playbook 4a). In `ReferenceRootFinder` die zwei
  `x?.IsIncluded == false`-Bedingungen auf `x != null && !x.IsIncluded` (Locals) umgestellt — sauberes
  Narrowing statt fragiler Member-Slot-Analyse, verhaltensgleich. Encoding: FindReferences/ war UTF-8 **mit**
  BOM, References/ UTF-8 **ohne** BOM (valide, keine Win-1252-Falle) — Hook ergänzte den BOM.

- **Welle 3 / P5 (Provider 13 + Workspace-Rest 3, 16 Dateien):** **Keine neuen Warnungen, keine neue
  Suppression, kein Befundlog-Eintrag.** Die Provider-Schicht ist die Nav↔Datei-Grenze: `ISyntaxProvider`
  war schon per `[CanBeNull]` als „liefert `null` bei nicht existierender Datei" markiert → jetzt
  compiler-erzwungen `CodeGenerationUnitSyntax? GetSyntax(…)`, analog `ISemanticModelProvider.GetSemanticModel(string)`
  → `CodeGenerationUnit?` (der zweite Overload `GetSemanticModel(CodeGenerationUnitSyntax)` bleibt non-null).
  `CachedSyntaxProvider` cacht das **negative** Ergebnis mit → Dictionary-Wert bewusst
  `ConcurrentDictionary<string, CodeGenerationUnitSyntax?>` (mit Kommentar); ctor nimmt `ISyntaxProvider?`
  (der parameterlose ctor reicht `null` durch, `?? SyntaxProvider.Default` normalisiert). `PathProvider`-ctor
  `string? generateTo = null, GenerationOptions? options = null` (Default-`null`, intern `??=`/CombinePath-Filter);
  `CombinePath` auf `params string?[]` (die `Where(!IsNullOrEmpty)`-Filterung + netstandard2.0-oblivious
  `Path.Combine` tragen den Rest). `PathProviderFactory`: `[NotNull]`/`ArgumentNullException`-Guard bleibt (Rule 5,
  oblivious VS-Aufrufer), `syntaxFile`/`generateToInfo` folgen der nullbaren `SourceText.FileInfo`.
  **Konsum-Validierung:** LSP/MCP (nativ nullable) bauen die jetzt compiler-erzwungenen `?`-Rückgaben von
  `GetSyntax`/`GetSemanticModel` **warnungsfrei** — beide guardeten die vormaligen `[CanBeNull]`-Rückgaben schon.
  Workspace-Rest: `NavSolution`-ctor/`SolutionDirectory` → `DirectoryInfo?`, ctor-Provider-Parameter `?`
  (`??`-normalisiert), `ProcessCodeGenerationUnitsAsync(Func<…>, CodeGenerationUnit? startingUnit, …)`,
  `FromDirectoryAsync(DirectoryInfo? directory, …)` — `directory` bewusst nullbar, weil der `null`/leer-Zweig als
  `Empty`-Sentinel real ausgewertet wird (Rule 5). Der einzige neue **Warnungsfund** war dabei die
  netstandard2.0-`IsNullOrEmpty`-Falle: `String.IsNullOrEmpty(directory?.FullName)` verengt `directory` nicht →
  `directory.FullName` warnte CS8602. Verhaltensneutral gefixt zu `if (directory == null ||
  String.IsNullOrEmpty(directory.FullName))` (der alte Ausdruck lieferte für `directory == null` via
  `directory?.FullName` ohnehin `IsNullOrEmpty(null) == true`). Dadurch fällt der bisherige Baseline-`CS8602` in
  `NavSolution` weg — Baseline jetzt **1 Eintrag** (nur noch `CallHierarchy`, dran in P6).
  `IncludeDependencyGraph.SetIncludes(…, IEnumerable<string>? includedFiles)` (`!= null`-Guard vorhanden),
  `GetDependentsClosure` `[NotNull]`→default; `DiagnosticsComputer.BelongsToDocument(Diagnostic, string?
  normalizedPath)` (nur an null-tolerante `string.Equals`/`IsNullOrEmpty` gereicht). Encoding: teils UTF-8 mit,
  teils ohne BOM, alle valides UTF-8 (keine Win-1252-Falle) — Hook ergänzte den BOM.

- **Welle 3 / P6 (Generator 6 + QuickInfo 2 + CallHierarchy 1 + CodeActions 2, 11 Dateien) — Welle 3
  komplett, 329/329 (100 %):** **Keine neuen Warnungen, +3 begründete Suppressions, kein Befundlog-Eintrag.**
  Der bisher **letzte** Baseline-`CS8602` (in `NavCallHierarchyService.GetOutgoingCalls`) fällt weg → Baseline
  jetzt **leer (0 Einträge)**. Er war das gewohnte Narrowing-Artefakt: `.Where(tn => tn.Declaration != null)`
  verengt die `Declaration`-**Property** nicht (Null sitzt auf der Property, nicht dem Element) → verhaltensneutral
  per `!` in `GroupBy(tn => tn.Declaration!.Location)` **und** `group.First().Declaration!` mit gemeinsamem
  Begründungskommentar (gleiche Idiomatik wie Nav1002/DependencyAnalyzer). Die `AsTaskDeclaration?.Location`- und
  `taskNode.Declaration?.Location`-Pfade waren bereits `?.`-sauber; `startingUnit: task.CodeGenerationUnit`
  (nullbar) passt jetzt exakt auf `ProcessCodeGenerationUnitsAsync(…, CodeGenerationUnit? startingUnit, …)` aus P5.
  **Generator:** die Pipeline konsumiert die in P5 geschärfte Provider-Grenze sauber — `syntaxProvider.GetSyntax(…)`
  → `if (syntax == null) continue;` (Nav0004-Fehler), der `GetSemanticModel(syntax)`-Overload bleibt non-null, daher
  kein Guard nötig. Einzige Suppression: `include.FileLocation.FilePath!` beim Sammeln der taskref-Include-Pfade
  (`IncludedFiles`) — ein Include wird stets aus dem aufgelösten non-null Pfad erzeugt (`TaskDeclarationSymbolBuilder`:
  `new Location(filePath)` nach `Path.GetFullPath`), `Location.FilePath` bleibt aber `string?` (Playbook 4a), daher
  `!` mit Kommentar. `NavCodeGeneratorPipeline`-ctor/`Create`-Parameter alle `?` (per `??`-Default normalisiert),
  `[CanBeNull] ILogger Logger`→`ILogger?`, `LoggerAdapter._logger`/ctor→`ILogger?` (die `_logger?.`-Aufrufe waren schon
  null-safe), `FileInfo?.DirectoryName`-Pfad bereits guarded. `FileSpec`: `ArgumentNullException`-Guards bleiben (Rule 5).
  **QuickInfo:** reine Vertrags-Präzisierung ohne neue Guards — `NavHoverService.GetHover`→`NavHoverInfo?`,
  `NavHoverInfo`-Ctor/Props `Location?`/`string? Documentation`; `NavSymbolDocumentation.GetDocumentation`/
  `GetDeclarationSyntax`→`string?`/`SyntaxNode?` (die `{ Prop: { } x }`-Pattern und `taskNode.Declaration?.Syntax`
  tragen die Nullbarkeit sauber). **CodeActions:** reine `#nullable enable`-Direktive — `CodeFix.Name` (abstract
  non-null), `SuggestCodeFixes`/`SuggestChoiceName` non-null, `GetTextChanges(string?)` null-tolerant; `NavCodeAction`-
  Ctor-Guards bleiben (Rule 5). Encoding: teils mit/ohne BOM, alle valides UTF-8 — Hook ergänzte den BOM.

- **Welle 4 (Voll-Enable-Smoke + LSP/MCP-Konsumcheck) — abgeschlossen, 1 echte Lücke gefunden & gefixt:**
  Der Smoke-Build deckte **eine** Datei auf, die die gesamte Kampagne unentdeckt durchlaufen hatte:
  `CodeGen\GenerationOptions.cs` hatte **keine** `#nullable enable`-Direktive. `nav nullaudit` zählte sie
  dennoch als „konvertiert", weil der **Fortschritts-Scan** auf den bloßen String `#nullable enable` grept
  und dieser in der **XML-Doku** der Datei vorkommt (`<c>#nullable enable</c>`, beschreibt die
  `NullableContext`-Option). ⇒ **Latenter Tooling-Fehler:** der Scanner sollte die Direktive am
  Zeilenanfang (BOM-tolerant) verlangen, nicht als Teilstring irgendwo. Unter `nav nullaudit`
  (`-p:Nullable=warnings`, Annotations-Kontext für diese oblivious-Datei **aus**) blieb die Lücke
  unsichtbar — die drei uninitialisierten non-null String-Properties (`ProjectRootDirectory`,
  `IwflRootDirectory`, `WflRootDirectory`) feuern `CS8618` **nur** mit vollem Annotations-Kontext
  (`enable`). Fix: Direktive + `using System;` ergänzt, die drei Properties auf `= String.Empty`
  (Playbook 4a — „abwesend" == „leer", deckt sich mit `GenerationOptions.Default`, der sie nicht setzt).
  **Verhaltensneutral verifiziert:** der einzige Nav.Language-Konsument `PathProvider` reicht sie an
  `PathHelper.TryTranslateToDirectory` durch, das `null` und `""` per `IsNullOrEmpty` **identisch**
  behandelt (`return fileName`); der CLI-Pfad nutzt durchweg `IsNullOrEmpty`-Guards. Kein NRE, kein
  Befundlog-Eintrag.
  **Methodik-Befund (Skopierung des Beweises):** `dotnet build -p:Nullable=enable` als **globale**
  MSBuild-Property ist zu grob — sie schlägt auch ins Fremd-Projekt `Nav.Utilities` durch (nie Teil der
  Kampagne), das dann seine eigene Nullable-Schuld (`PathHelper` CS8603/CS8625) zeigt **und** über
  geänderte Signaturen induzierte Downstream-Warnungen in Nav.Language erzeugt (`NavIgnore.cs` CS8604 —
  `PathHelper.GetFullPathNoThrow(string)` wird non-null, plus die netstandard2.0-`IsNullOrEmpty`-Falle).
  Der **echte** Endzustand (Welle 5) ist projekt-lokales `<Nullable>enable</Nullable>` **nur** in
  Nav.Language; das leckt **nicht** in Dependencies. Der Welle-4-Beweis wurde daher projekt-skopiert
  geführt (temporär `<Nullable>enable</Nullable>` in `Nav.Language.csproj`, danach zurückgenommen):
  **Nav.Language 0 Warnungen/0 Fehler**, die induzierte `NavIgnore`-CS8604 ist damit erwartungsgemäß
  **weg**. LSP + MCP (`-f net10.0`, nativ nullable) bauen je **0 CS86xx/87xx**. `nav nullaudit` bleibt
  nach dem Fix grün (329/329, Baseline 0); net472 (1271) + net10.0 (1263) grün. Die `Nav.Utilities`-
  Eigenschuld ist **kein** Kampagnen-Regress, sondern separater Scope (eigene Nullable-Umstellung, falls
  je gewünscht).

- **Welle 5 (Endzustand) — abgeschlossen (2026-07-03, Nutzerentscheid „durchziehen, nullaudit behalten"):**
  Drei Teile. **(1) Scanner-Fix vorab:** die `nav nullaudit`-Fortschritts-Erkennung suchte den String
  `#nullable enable` per `Select-String -SimpleMatch` — matchte damit auch die XML-Doku-Erwähnung und
  ließ so die Welle-4-Lücke `GenerationOptions.cs` durch. Umgestellt auf **zeilenbasierte** Erkennung
  (`Trim() -eq '#nullable enable'`, BOM-fest via `ReadAllLines`); gegen den bekannten 329/329-Stand
  verifiziert (unverändert 329, aber aus dem richtigen Grund). **(2) csproj-Umschaltung:** in
  `Nav.Language.csproj` `<Nullable>enable</Nullable>` + `<WarningsAsErrors>$(WarningsAsErrors);Nullable</…>`.
  Das WAE-Gate ist **empirisch per Sonde** bewiesen: eine temporäre `string? s = null; return s;`-Methode
  erzeugt `error CS8603` (nicht warning), Build bricht ab — der `Nullable`-Kategorie-Token wird also
  wirklich wirksam, nicht still ignoriert. **(3) Direktiven entfernt:** alle **329** per-Datei-
  `#nullable enable` (+ jeweils die Folge-Leerzeile) per Skript entfernt — BOM-fest (raw-Bytes gelesen,
  UTF-8-**mit**-BOM zurückgeschrieben), EOL-erhaltend, zeilenbasiert (die Doku-Zeile in
  `GenerationOptions.cs` blieb unberührt). Verifiziert: 0 Rest-Direktiven, **0 `U+FFFD`** (keine
  Encoding-Korruption, vgl. Win-1252-Falle), 0 Dateien ohne BOM (5 vormals BOM-lose Dateien dabei
  gleich CLAUDE.md-konform gemacht). Nav.Language baut nativ **0 Warnungen/0 Fehler**; net472 (1271) +
  net10.0 (1263) grün — **verhaltensneutral** (die Direktiven waren unter projektweitem `enable`
  redundante No-ops). **nullaudit umgebaut** (Nutzerwunsch, bleibt erhalten): Fortschritts-Tabelle →
  **Hygiene-Wächter** (Projekteinstellung vorhanden? `#nullable`-Direktiven-Drift? — `disable`/`restore`
  fatal), Prüfbau **nativ** statt `-p:Nullable=warnings` (mit `-p:WarningsAsErrors=` fürs nicht-fatale
  Einsammeln), Baseline bleibt leer, `nav nullaudit` grün. Damit ist die Kampagne **vollständig
  abgeschlossen** — kein NRE-Befund über alle Wellen (Befundlog leer).

## 6. Befundlog (NRE-Funde mit Testreferenz)

> Jeder über die öffentliche API erreichbare NRE-Befund wird hier mit Datei, Ursache und
> Testreferenz (`Nav.Language.Tests\Robustness\…`) dokumentiert. Noch keine Einträge.

_(leer)_
