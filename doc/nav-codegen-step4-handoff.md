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
- **Step 4, Sub-Step 2 (dieser Stand): `IBeginWFS`-Familie auf CodeBuilder migriert.**
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
- **Test-Strategie (wichtig für den Beweis):** Der CodeBuilder ist **clean-by-default** und reproduziert
  die kosmetischen StringTemplate-Whitespace-Artefakte **bewusst nicht** (Trailing-Whitespace,
  eingerückte Leerzeilen). Der Korpus-Beweis urteilt deshalb **nach Whitespace-Normalisierung**; ein
  leerer Roh-Byte-Diff ist ab einer migrierten Familie **nicht** mehr das Kriterium (bei `IBeginWFS`
  war er zufällig trotzdem 0 — diese Familie hat keine ST-Whitespace-Artefakte).

Aktuelle Template-Landschaft (unter `Nav.Language/CodeGen/Templates/`): `IBeginWFS.stg` ist durch den
Emitter abgelöst (die `.stg`-Datei bleibt bis zum letzten Sub-Step als toter Ballast liegen und fällt
mit dem ST-Sonderweg); noch ST: `Common.stg`, `IWFS.stg`, `WFSBase.stg`, `WFSOneShot.stg`, `TO.stg`
(+ `CodeGenFacts.stg`/`.generated.cs`, `Resources.cs`).

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
  `Align()` bleiben für dynamischen Inhalt (umbrochene Parameterlisten via `Align()`).
- **Kein literales `"\r\n"` im Emitter-Code.** Newline-haltige `WriteJoin`-Separatoren nutzen `cb.NewLine`
  (bzw. `$",{cb.NewLine}"`) — nie ein hartes `"\r\n"`. (Die `.stg`-Templates behalten ihr
  `separator="\r\n"`, sie fallen mit dem ST-Sonderweg.)
- **Mehrzeiliger Text + Einrückung.** Der `CodeBuilder` zerlegt jeden geschriebenen Text an
  Zeilenumbrüchen und stellt jeder Zeile die **aktuelle** Einrückung voran; relative Innen-Einrückung
  eines Raw-Strings bleibt additiv erhalten. Ein Raw-String-Block darf deshalb bedenkenlos innerhalb
  eines `Block()`/`Indent()`-Scopes stehen. Das Verhalten ist in `CodeBuilderTests` fixiert
  (`Write_MultiLineText_ReindentsEachLine`, `WriteLine_MultiLineRawString_*`).
- **`WriteLine` terminiert die Schlusszeile.** Ein mehrzeiliger `cb.WriteLine("""…""")` schreibt alle
  Zeilen **und** schließt die letzte mit Zeilenumbruch ab; ein separates `cb.WriteLine()` am Ende
  entfällt (eine gewünschte Leerzeile wird als letzte leere Zeile in den Raw-String gezogen).
- **Geteilte Bausteine in `EmitterCommon`.** Dateikopf, Using-Direktiven und Nav-Annotations liegen in
  `EmitterCommon` (C#-Pendant zu `Common.stg`) und werden von jeder Familie wiederverwendet.

## 3. Der nächste Sub-Step

Laut Plan (Grundsatz 9 in [`nav-codegen-versioning.md`](nav-codegen-versioning.md)): **eine
Template-Familie pro Sub-Step**, ST und CodeBuilder koexistieren während der Migration, der ST-Sonderweg
fällt erst mit dem letzten Sub-Step. Reihenfolge: **IBeginWFS ✓ → IWFS → WFSBase → WFSOneShot → TO**.

**Als Nächstes: die `IWFS`-Familie** (`IWFS.stg` → Emitter) auf denselben Weg bringen wie `IBeginWFS`:
`IWfsEmitter` neben `IBeginWfsEmitter` legen, die geteilten Bausteine aus `EmitterCommon` wiederverwenden
(ggf. um `IWFS`-spezifische Annotations/Trigger-Methoden ergänzen), `CodeGenerator.GenerateIWfsCodeSpec`
umhängen, `IWfsTemplateGroup` entfernen. `I{Task}WFS` ist wie `IBegin{Task}WFS` die versions-invariante
Schnittstelle (Grundsatz 3) → Namen aus `CodeGenInvariants`. Nach der Umstellung: Snapshots **und**
Korpus-Parity (s.u.) müssen grün sein, bevor `WFSBase` drankommt.

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
