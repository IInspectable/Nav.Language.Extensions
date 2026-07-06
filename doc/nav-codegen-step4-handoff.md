# Codegen-Versionierung — Step-4-Handoff (Korpus-Parity auf einer Korpus-Maschine)

> **Zweck.** Damit der nächste Sub-Step der ST→CodeBuilder-Migration (Step 4) in einer **frischen
> Session auf einem anderen Rechner** aufgesetzt werden kann. Dieses Dokument sagt, *wo wir stehen*,
> *was als Nächstes dran ist* und *wie der Byte-Identitäts-Beweis gefahren wird* — inklusive der einen
> maschinen-lokalen Voraussetzung, die nicht im Repo liegt: dem **Bestandskorpus**.
>
> Die **Design-Entscheidungen** (11 Grundsätze, Ziel-Architektur der „Weiche", die V2-Richtung) stehen
> unverändert in [`nav-codegen-versioning.md`](nav-codegen-versioning.md) — hier **nicht** dupliziert.
> Die Parser-/Korpus-Vorgeschichte (byte-identischer Cutover, Perf-Zahlen) steht in
> [`nav-kolibri.md`](nav-kolibri.md).

## 1. Wo wir stehen (committet auf `feature/nav-parser`)

- **Step 1–3 fertig & committet.** Ergebnis-Form generalisiert: `CodeGenerationResult` trägt eine
  `ImmutableArray<CodeGenerationSpec> Specs` (Inhalt + Zielpfad + `OverwritePolicy`); alles hinter
  `CodeGenerationSpec` ist versionsfrei, alles davor darf je Version variieren.
- **Step 4, Sub-Step 1 committet** (`18a91651`): der **`CodeBuilder`** steht — ein einrückungs- und
  spaltenbewusster Textbauer als technischer ST-Ersatz (`Nav.Language/CodeGen/CodeBuilder/CodeBuilder.cs`,
  21 Golden-Tests in `Nav.Language.Tests/CodeBuilderTests.cs`). Public-API: Props
  `IndentDepth`/`Column`/`Length`; `Write`/`WriteLine`; `Indent()`-, `Block()`-, `Align()`/`Align(int)`-
  Scopes (schachtelbar); `WriteJoin` mit newline-fähigem Separator.
- **Step 4, Sub-Step 2: `IBeginWFS`-Familie auf CodeBuilder migriert.**
  `Nav.Language/CodeGen/CodeBuilder/IBeginWfsEmitter.cs` emittiert die `IBegin{Task}WFS`-Interfaces;
  der Interface-Name kommt aus `CodeGenInvariants` (invariante Schnittstelle, Grundsatz 3), der
  Begin-Methodenname aus den versionierbaren `ICodeGenFacts` des Tasks; umbrochene Parameterlisten über
  `CodeBuilder.Align()`. Geteilte Bausteine (auto-generated-Header inkl. `#nullable`-Option,
  Using-Direktiven, Nav-Annotations) liegen in `Nav.Language/CodeGen/CodeBuilder/EmitterCommon.cs` — das
  C#-Pendant zu den wiederverwendbaren `Common.stg`-Templates und Fundament der folgenden Familien.
  `CodeGenerator.GenerateIBeginWfsCodeSpec` ruft den Emitter; `IBeginWfsTemplateGroup` entfernt. **Die
  übrigen Familien laufen weiter über ST** (Koexistenz). Der Emitter ist anschließend in den
  Raw-String-Stil überführt (LangVersion 11.0, `Block()`-Scopes, `cb.NewLine` statt literalem `"\r\n"`) —
  byte-identisch; der verbindliche Stil steht in §2. Verifiziert: `nav snapshot` ohne Diff, Korpus
  **roh** identisch (s.u.), Tests net472 1353/0 + net10 1345/0.
- **Step 4, Sub-Step 3: `IWFS`-Familie auf CodeBuilder migriert.**
  `Nav.Language/CodeGen/CodeBuilder/IWfsEmitter.cs` emittiert die `I{Task}WFS`-Interfaces; je
  Trigger-Transition entsteht eine `INavCommand {Trigger}({View}TO to)`-Methode. Der Interface-Name ist
  wie beim Begin-Interface versions-invariant (Grundsatz 3) und stammt aus `CodeGenInvariants` — hier
  über `model.InterfaceName` → `TaskCodeInfo.IWfsTypeName`, das Präfix/Suffix aus `CodeGenInvariants`
  zieht (die `IWFS.stg` nutzte bereits die Modell-Property, anders als `IBeginWFS.stg`, das den Namen
  inline baute). Die Trigger-Annotation (`NavTrigger`) ist neu in `EmitterCommon.WriteTriggerAnnotation`
  (Pendant zu `WriteNavInitAnnotation`); Dateikopf/Using/Task-Annotation kommen unverändert aus
  `EmitterCommon`. Der `ViewParameter` ist ein einzelner Parameter → inline geschrieben (kein
  `Align()`/Join nötig). `CodeGenerator.GenerateIWfsCodeSpec` ruft den Emitter; `IWfsTemplateGroup`
  entfernt. Stil wie in §2. Verifiziert: `nav snapshot` ohne Diff, Korpus **roh** identisch (9211/9211,
  `ParityOk=True`), Tests net472 1355/0 + net10 1347/0; Kandidat 30,3 s vs. 33,8 s Referenz.
- **Step 4, Sub-Step 4 (dieser Stand): `WFSBase`-Familie auf CodeBuilder migriert.**
  `Nav.Language/CodeGen/CodeBuilder/WfsBaseEmitter.cs` — der bisher größte Emitter. Erzeugt in einer
  Datei die abstrakte Maschinerie-Basisklasse `{Task}WFSBase` (Node-Name-Konstanten, `readonly`-Felder,
  zwei Konstruktoren, `BeforeTriggerLogic`, die Init-/Exit-/Trigger-Weichen mit ihren `switch(body)`-
  Blöcken über die erreichbaren Calls, die Begin-Wrapper, `TaskResult`) **und** die partielle
  Implementierungsklasse `{Task}WFS`. Anders als die Interface-Familien sind Klassenname/Namespace hier
  **versionierbar** (aus `ICodeGenFacts` via `TaskCodeInfo`); nur die in der Basisliste referenzierten
  `I{Task}WFS`/`IBegin{Task}WFS` bleiben invariant (`CodeGenInvariants`). Neu in `EmitterCommon`:
  `WriteNavExitAnnotation` (`NavExit`) und `WriteInitCallAnnotation` (`NavInitCall`). Der polymorphe
  `writeCall`-Dispatch der `.stg` (Call-Template je `TemplateName`) ist als `switch` über
  `CallCodeModel.TemplateName` nachgebildet. `CodeGenerator.GenerateWfsBaseCodeSpec` ruft den Emitter;
  `WfsBaseTemplateGroup` entfernt. Stil wie in §2.
  - **Zwei bewusst nicht byte-genau reproduzierte StringTemplate-Eigenheiten** (beide rein Whitespace):
    (1) die strukturelle Leerzeile, die der `[notimplemented]`-Zweig eines Task-Calls vor dem nächsten
    `case` hinterlässt, wird **doch** reproduziert (sie überlebt Normalisierung, ist also inhaltlich);
    (2) die **Einrückung von Fortsetzungszeilen mehrzeiliger Typen** (in der `.nav` über mehrere Zeilen
    deklarierte Ergebnistypen) weicht ab — der CodeBuilder reindentiert nach Struktur-Einrückung, ST
    nach seinem Interpolations-Stack. Das ist **nicht generell** nachbaubar (bei `case`-Bodies zufällig,
    bei Parameterlisten mit Fortsetzung auf Spalte 0 nicht) und rein kosmetisch → die Parity-
    Normalisierung trimmt Zeilen jetzt **beidseitig** (s. §5). Betroffen: 2 von 9211 Korpus-Dateien
    (`KundenverwaltungWFSBase`, `PharmazeutischeBetreuungWFSBase`, beide mit demselben mehrzeiligen
    `ITaskResultWithTOAndData<…>`).
  - Verifiziert: `nav snapshot` nur Whitespace-Diff (`TestWFSBase`, per `git diff --ignore-all-space`
    leer), Tests net472 1355/0 + net10 1347/0, Korpus-Parity `ParityOk=True` (normalisiert 0,
    kosmetisch 1410).
- **Step 4, Sub-Step 5 (dieser Stand): `WFSOneShot`-Familie auf CodeBuilder migriert.**
  `Nav.Language/CodeGen/CodeBuilder/WfsOneShotEmitter.cs` erzeugt die **Benutzer-Datei** (`{Task}WFS.cs`,
  `OverwritePolicy.Never`): die partielle Implementierungsklasse `{Task}WFS` mit je einem
  `throw new NotImplementedException();`-Stub für die abstrakten Init-/Exit-/Trigger-Methoden aus
  `{Task}WFSBase`. Anders als alle übrigen Familien schreibt sie **keinen** `<auto-generated>`-Header
  (bearbeitbare Benutzer-Datei) — `Emit` startet direkt mit `EmitterCommon.WriteUsingDirectives`.
  Klassenname/Namespace sind versionierbar (`ICodeGenFacts` via `TaskCodeInfo`), die Methodennamen
  stammen aus denselben Facts wie in `WfsBaseEmitter`. Die drei Stub-Signaturen bilden die `.stg`-Zweige
  1:1 nach: Init abstrakt `public override IINIT_TASK Begin(…)` bzw. Logic
  `protected override INavCommandBody BeginLogic(…)`; Exit abstrakt
  `protected override INavCommand {Exit}{Node}(…)` bzw. Logic `…INavCommandBody {Exit}{Node}Logic(…)`;
  Trigger `…INavCommandBody {Trigger}Logic(…)`. **Leerzeilen-Kadenz:** alle Stubs durch genau eine
  Leerzeile getrennt; **eine** strukturelle Schluss-Leerzeile vor `}` nur, wenn die Trigger-Sektion
  leer ist (die `.stg` beendet Init-/Exit-Sektion mit `<\\>` + Leerzeile, die Trigger-Sektion nicht).
  `CodeGenerator.GenerateWfsCodeSpec` ruft den Emitter; `WfsTemplateGroup` entfernt. Stil wie in §2.
  - Verifiziert: Regression-`.cs` frisch generiert, Diff der zwei Snapshots (`SimpleTaskWFS`, `TestWFS`)
    **nur Whitespace** (`git diff --ignore-all-space` leer) — entfernt der Tab hinter `namespace … {` und
    der 5-Space-`throw`-Indent des Exit-Logic-Zweigs (beides reine ST-Artefakte, clean-by-default).
    Tests net472 1355/0 + net10 1347/0, Korpus-Parity `ParityOk=True` (normalisiert 0, kosmetisch 3355);
    Kandidat 29,1 s vs. 34,5 s Referenz.
- **Test-Strategie (wichtig für den Beweis):** Der CodeBuilder ist **clean-by-default** und reproduziert
  die kosmetischen StringTemplate-Whitespace-Artefakte **bewusst nicht** (Trailing-Whitespace,
  eingerückte Leerzeilen, und die Einrückung von Fortsetzungszeilen mehrzeiliger eingebetteter Werte).
  Der Korpus-Beweis urteilt deshalb **nach Whitespace-Normalisierung** (je Zeile **beidseitig** getrimmt,
  s. §5); ein leerer Roh-Byte-Diff ist ab einer migrierten Familie **nicht** mehr das Kriterium (bei
  `IBeginWFS`/`IWFS` war er zufällig trotzdem 0 — diese Familien haben keine ST-Whitespace-Artefakte, ab
  `WFSBase` fällt er erwartbar auf > 0). Maßstab ist `NormChanged = 0` (plus nichts neu/entfernt).

Aktuelle Template-Landschaft (unter `Nav.Language/CodeGen/Templates/`): `IBeginWFS.stg`, `IWFS.stg`,
`WFSBase.stg` und `WFSOneShot.stg` sind durch Emitter abgelöst (die `.stg`-Dateien samt ihrer
`Resources`-Einträge bleiben bis zum letzten Sub-Step als toter Ballast liegen und fallen mit dem
ST-Sonderweg); noch ST: `Common.stg`, `TO.stg` (+ `CodeGenFacts.stg`/`.generated.cs`, `Resources.cs`).

## 2. Emitter-/CodeBuilder-Stil (verbindlich für die Folge-Familien)

Die `IBeginWFS`-Familie definiert den Stil, in dem **alle** folgenden Emitter (IWFS, WFSBase,
WFSOneShot, TO) geschrieben werden. Ziel ist Code, der sich liest wie das, was er erzeugt — bei
weiterhin **byte-identischer** Ausgabe (Beweis: `nav snapshot` ohne Diff). Referenz-Implementierung:
`IBeginWfsEmitter.cs` + `EmitterCommon.cs`.

- **Sprachversion.** `LangVersion` ist projektweit **11.0** (`Directory.Build.props`) — nötig für
  Raw-String-Literale. Reines Compiler-Feature ohne BCL-Abhängigkeit, trägt auf net472/netstandard2.0;
  die Full-Solution inkl. VS-Extension `Nav.Language.Extension2026` baut damit.
- **Zusammenhängende Ausgabe als Raw-String.** Aufeinanderfolgende `Write`/`WriteLine`-Ketten werden zu
  **einem** (ggf. interpolierten) Raw-String-Literal zusammengezogen — Dateikopf, `#region`-Annotationen,
  statische Präambeln. Einzeiler aus mehreren Teilen werden interpoliert
  (`$"public interface {…}: {…} "`).
- **Blöcke über `CodeBuilder.Block()`.** `{ … }`-Rümpfe (namespace, interface, class, Methoden) laufen
  über `using (cb.Block()) { … }`: den Kopf mit `cb.Write($"namespace {ns} ")` schreiben, `Block()` setzt
  die öffnende Klammer, rückt ein und schließt beim `Dispose` mit `}` auf eigener Zeile. **Kein
  `{{`-Escape** und keine manuellen `cb.WriteLine("}")`/`cb.Write("}")`-Schließer mehr. `Indent()`/
  `Align()` bleiben für dynamischen Inhalt.
- **Umbrochene, ausgerichtete Listen über `cb.WriteAlignedJoin(...)`.** Der häufige Fall „Kopf schreiben,
  dann eine an der öffnenden Klammer ausgerichtete Parameterliste joinen" läuft über die Kurzform
  `cb.WriteAlignedJoin(items, x => cb.Write(x), separator: $",{cb.NewLine}")` — sie kapselt den
  `using (cb.Align()) { cb.WriteJoin(…) }`-Rahmen. Der per-Element-Writer ist ein **`Action<T>`**
  (`p => cb.Write(p)`): er schreibt über den bereits im Scope liegenden Builder. Der Builder wird
  **bewusst nicht** zusätzlich als Delegat-Parameter durchgereicht — eine solche
  `Action<CodeBuilder, T>`-Variante erzwänge am Aufrufort einen **zweiten Namen** für den Builder
  (z.B. `bb`, weil `b`/`cb` schon belegt ist), ohne Mehrwert. Nur wo mehr als ein reiner Join in den
  Anker soll, den `using (cb.Align()) { … }`-Scope explizit öffnen.
- **Kein literales `"\r\n"` im Emitter-Code.** Newline-haltige `WriteJoin`-/`WriteAlignedJoin`-Separatoren
  nutzen `cb.NewLine` (bzw. `$",{cb.NewLine}"`) — nie ein hartes `"\r\n"`. (Die `.stg`-Templates behalten
  ihr `separator="\r\n"`, sie fallen mit dem ST-Sonderweg.)
- **Mehrzeiliger Text + Einrückung.** Der `CodeBuilder` zerlegt jeden geschriebenen Text an
  Zeilenumbrüchen und stellt jeder Zeile die **aktuelle** Einrückung voran; relative Innen-Einrückung
  eines Raw-Strings bleibt additiv erhalten. Ein Raw-String-Block darf deshalb bedenkenlos innerhalb
  eines `Block()`/`Indent()`-Scopes stehen. Das Verhalten ist in `CodeBuilderTests` fixiert
  (`Write_MultiLineText_ReindentsEachLine`, `WriteLine_MultiLineRawString_*`).
- **`WriteLine` terminiert die Schlusszeile.** Ein mehrzeiliger `cb.WriteLine("""…""")` schreibt alle
  Zeilen **und** schließt die letzte mit Zeilenumbruch ab; ein separates `cb.WriteLine()` am Ende
  entfällt (eine gewünschte Leerzeile wird als letzte leere Zeile in den Raw-String gezogen).
- **Angrenzende Leerzeilen gehören in den Raw-String, nicht daneben.** Das gilt für **beide** Enden:
  eine gewünschte Leerzeile **vor** einem Raw-String-Block wird als erste leere Zeile in den Raw-String
  gezogen, eine **danach** als letzte — kein separates `cb.WriteLine()` davor oder dahinter. Der
  Builder verwirft Trailing-Whitespace und rückt leere Zeilen nicht ein, daher ist das Ergebnis
  byte-identisch, aber die Formatierung steht sichtbar an einer Stelle statt verteilt über
  Aufruf-Zeilen. (Ein Raw-String darf mit einer Leerzeile *beginnen*: die erste Zeile nach dem
  öffnenden `"""` bleibt leer.) Das gilt auch für einen **dynamischen, einzeiligen Kopf** wie
  `namespace {ns}`: statt `cb.WriteLine(); cb.Write($"namespace {ns} ")` wird die Leerzeile in einen
  interpolierten Raw-String gezogen (`cb.Write($"""` · Leerzeile · `namespace {ns} ` · `""")`). Der
  Builder normalisiert die Zeilenenden ohnehin auf sein CRLF, das Quell-Zeilenende ist also egal. Das
  eine Leerzeichen **vor** dem `{` (das `Block()` setzt) steht dann als bewusster Trailing-Space auf der
  Inhaltszeile — abgesichert durch die Regression-Snapshots: würde es je (z.B. durch Format-on-Save)
  wegfallen, schlägt `nav snapshot` sofort an.
- **Geteilte Bausteine in `EmitterCommon`.** Dateikopf, Using-Direktiven und Nav-Annotations liegen in
  `EmitterCommon` (C#-Pendant zu `Common.stg`) und werden von jeder Familie wiederverwendet.

## 3. Der nächste Sub-Step

Laut Plan (Grundsatz 9 in [`nav-codegen-versioning.md`](nav-codegen-versioning.md)): **eine
Template-Familie pro Sub-Step**, ST und CodeBuilder koexistieren während der Migration, der ST-Sonderweg
fällt erst mit dem letzten Sub-Step. Reihenfolge: **IBeginWFS ✓ → IWFS ✓ → WFSBase ✓ → WFSOneShot ✓ → TO**.

**Als Nächstes: die `TO`-Familie** (`TO.stg` → Emitter) — der **letzte** Familien-Sub-Step. Das ist die
zweite Benutzer-Datei (TO-Stub je View, `OverwritePolicy.Never`; den vollen Inhalt pflegt der externe
GUI-Generator): `TOCodeModel` → `TOTemplate`, gerendert über die letzte verbliebene per-Familie-`.stg`.
`ToEmitter` neben die anderen legen, `EmitterCommon` wiederverwenden, `CodeGenerator.GenerateToCodeSpecs`
umhängen, `ToTemplateGroup` entfernen. Anders als die WFS-Familien wird die TO **pro View** erzeugt
(`GenerateToCodeSpecs` iteriert `TOCodeModel.FromTaskDefinition` → mehrere Specs). **Achtung Parity:** der
Roh-Diff ist erwartbar groß (Whitespace), das Urteil hängt allein an `NormChanged = 0` (Normalisierung
trimmt beidseitig, s. §5). Danach fällt der **komplette ST-Sonderweg**: `Common.stg` + alle `.stg`,
`Resources.cs`, der Facts-Export (`CustomBuild.targets`-Target, `GenerateCodeGenFacts.cs`,
`CodeGenFacts.generated.cs`) und die `Antlr4.StringTemplate`-Abhängigkeit der Engine (Step 4 Abschluss,
danach Step 5 = Dispatcher). Nach der Umstellung: Snapshots **und** Korpus-Parity müssen grün sein.

## 4. Voraussetzung auf der Zielmaschine: Korpus + Referenz-Generator

Der Korpus (~1900 `.nav`) ist ein **proprietäres TFS-Enlistment** und liegt **nicht im Repo**. Auf der
Referenzmaschine reicht der Teilbaum **`D:\tfs\Main\XTplusApplication\src`** (1909 `.nav`; das ganze
`D:\tfs\Main` hat 1912, die 3 Extras liegen woanders und sind für den Beweis entbehrlich).

Als **Referenz-Generator** dient die deployte `nav.exe` aus dem Enlistment:
**`D:\tfs\Main\build\Script\Nav\nav.exe`** — genau das Binary, das die eingecheckten Produktions-`.cs`
erzeugt. Weiter gebraucht: **.NET-SDK 10** (baut den Kandidaten) und die `nav`-Commands
(`. .\Tools\Commands\Import-NavCommands.ps1`). Liegt etwas anderswo: `-CorpusPath` / `-ReferenceExe`.

## 5. Der Parity-Beweis: `nav parity` (run-both-generators)

Der Command **`nav parity`** (`Tools/Commands/Functions/Invoke-CodeGenParity.ps1`) vergleicht **zwei
Generatoren**, die **dieselben `.nav`** übersetzen — Referenz vs. Kandidat. Bewusst schlank (**wenige
Minuten**), weil nur `.nav` kopiert und nur der Generator-Output verglichen wird:

1. Nur die `.nav` in **zwei** leere Scratch-Bäume spiegeln (Robocopy `*.nav`, ohne `bin`/`obj`/`.git`/
   `.vs`) — wenige MB statt des zig-GB-Enlistments.
2. **Referenz**-`nav.exe -d <ref> -g All` über den einen, **Kandidat** (frisch aus dem Arbeitsbaum
   gebaut) `-d <cand> -g All` über den anderen Baum; Wall-Zeit je Lauf.
3. Alle erzeugten `.cs` einsammeln und vergleichen.

**Warum kein Korpus-Baseline / kein TO-Sonderfall mehr:** Beide Läufe starten vom **identischen leeren
Baum** ⇒ die `OverwritePolicy.Never`-Dateien (`{Task}WFS.cs`, TO-Stubs) erzeugen beide mit
unverändertem Code byte-gleich → kein Falsch-Diff. (Der alte single-exe-Weg verglich gegen die
eingecheckten `.cs`; dort trugen die vom GUI-Generator **gefüllten** TOs Falsch-Diffs bei — deshalb
musste er das ganze Enlistment kopieren.)

Zwei Vergleichsebenen: **normalisiert** (je Zeile rechts getrimmt, Zeilenenden vereinheitlicht) ist das
Urteil (`ParityOk` = 0 normalisiert **und** keine `.cs` neu/entfernt); **roh** ist die Audit-Ebene
(`CosmeticOnly` = roh ≠, aber normalisiert = → nur Whitespace, manuell sichten).

```powershell
. .\Tools\Commands\Import-NavCommands.ps1

# No-Brainer: Kandidat frisch bauen, gegen die deployte Referenz vergleichen (Defaults wie oben)
nav parity

# Overrides
nav parity -CorpusPath D:\tfs\Main\XTplusApplication\src -ReferenceExe D:\tfs\Main\build\Script\Nav\nav.exe
nav parity -CandidateExe 'Nav.Cli\bin\Debug\nav.exe'   # vorhandene Kandidat-exe statt Build
nav parity -Keep                                       # Scratch behalten (CosmeticFiles nachsehen)
```

Rückgabe ist ein Objekt (`ParityOk`, `RawChanged`, `NormChanged`, `CosmeticOnly`, `CosmeticFiles`,
`Added`, `Removed`, `RefSecs`, `CandSecs`, `RefExit`, `CandExit`) — skriptbar. Beide `…Exit` sind auf dem
Bestandskorpus konsistent `1` (einige `.nav` tragen Diagnostics); das ist **kein** Fehler, solange beide
gleich sind und `Added`/`Removed` = 0. „**PARITY OK (normalisiert) — N Datei(en) nur kosmetisch**" ist
der erwartete Zustand nach einer Migration mit Whitespace-Artefakten; bei `IBeginWFS` war sogar roh 0.

## 6. Verifikations-Checkliste je Sub-Step

1. **Snapshots:** `nav snapshot` erzeugt keine Diffs (Regression byte-identisch). — maschinen-unabhängig,
   im Repo.
2. **Tests auf beiden TFMs:** `nav test` (net472) **und**
   `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`.
3. **Korpus-Parity:** `nav parity` → **`ParityOk = True`** (normalisiert identisch, nichts neu/entfernt);
   anschließend die `CosmeticFiles`-Menge roh auditieren (nur Whitespace zulässig, kein Zeichen-/
   Struktur-Unterschied) — bei `IBeginWFS` war `CosmeticOnly` = 0 (roh identisch), also entfiel das.
4. **Perf festhalten:** `RefSecs`/`CandSecs` aus dem `nav parity`-Objekt notieren (Erwartung: keine
   Regression; bei `IBeginWFS` war der Kandidat sogar schneller, 30,6 s vs. 37,3 s Referenz).
5. Erst wenn 1–4 grün: fertige **Commit-Message** liefern (nicht selbst committen).

## 7. Fallstricke

- **Der Korpus/die Referenz fehlt oder liegt anders** → `nav parity` bricht mit klarer Meldung ab;
  `-CorpusPath` / `-ReferenceExe` setzen.
- **`RefExit`/`CandExit` = 1 ist normal** — einige Korpus-`.nav` tragen Diagnostics, `nav.exe` liefert
  dann prozessweit Exit 1. Kein Fehler, solange **beide** gleich sind; das Urteil hängt an
  `NormChanged`/`Added`/`Removed`, nicht am Exit-Code.
- **Roh-Diff nach Migration ≠ 0 ist normal** — nur wenn er **über Whitespace hinausgeht** (d.h.
  `NormChanged` > 0), ist Parity verletzt.
- **`nav.exe` schreibt in-place** neben die `.nav` (in `generated/`-Unterordner, `*.generated.cs`);
  darum läuft der Beweis über zwei **Scratch-Bäume**, nie über das echte Enlistment.
- **Der Referenz-Generator ist ein fixes deploytes Binary** (`build\Script\Nav\nav.exe`), das die
  Produktions-`.cs` erzeugt — nicht der Arbeitsbaum-Stand. Solange er unverändert bleibt, prüft jeder
  Sub-Step gegen dieselbe „alte Wahrheit".
- **CLAUDE.md sagt Nav.Cli sei net472** — die csproj ist net10.0 (SDK-Single-File-Publish trägt die
  Runtime selbst). `nav parity` baut den Kandidaten entsprechend mit `dotnet build`.
- **Frische Shell:** zuerst `. .\Tools\Commands\Import-NavCommands.ps1`, sonst ist `nav` nicht geladen.
