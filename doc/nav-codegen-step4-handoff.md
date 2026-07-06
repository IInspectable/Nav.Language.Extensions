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
  21 Golden-Tests in `Nav.Language.Tests/CodeBuilderTests.cs`). **Noch kein Emitter, ST ist unverändert
  produktiv.** Public-API: Props `IndentDepth`/`Column`/`Length`; `Write`/`WriteLine`; `Indent()`-,
  `Block()`-, `Align()`/`Align(int)`-Scopes (schachtelbar); `WriteJoin` mit newline-fähigem Separator.
- **Test-Strategie (wichtig für den Beweis):** Der CodeBuilder ist **clean-by-default** und reproduziert
  die kosmetischen StringTemplate-Whitespace-Artefakte **bewusst nicht** (Trailing-Whitespace,
  eingerückte Leerzeilen). Der Korpus-Beweis vergleicht deshalb **nach Whitespace-Normalisierung** — ein
  leerer Roh-Byte-Diff ist ab der ersten migrierten Familie **nicht** mehr das Kriterium.

Aktuelle Template-Landschaft (noch ST, unter `Nav.Language/CodeGen/Templates/`):
`Common.stg`, `IBeginWFS.stg`, `IWFS.stg`, `WFSBase.stg`, `WFSOneShot.stg`, `TO.stg`
(+ `CodeGenFacts.stg`/`.generated.cs`, `Resources.cs`).

## 2. Der nächste Sub-Step

Laut Plan (Grundsatz 9 in [`nav-codegen-versioning.md`](nav-codegen-versioning.md)): **eine
Template-Familie pro Sub-Step**, ST und CodeBuilder koexistieren während der Migration, der ST-Sonderweg
fällt erst mit dem letzten Sub-Step. Reihenfolge: **IBeginWFS → IWFS → WFSBase → WFSOneShot → TO**.

**Als Nächstes: die `IBeginWFS`-Familie** auf einen CodeBuilder-Emitter umstellen (die Interfaces
`IBegin{Task}WFS` sind laut Grundsatz 3 die versions-**invariante** Schnittstelle — ein guter, klar
abgegrenzter erster Emitter). Nach der Umstellung: Snapshots **und** Korpus-Parity (s.u.) müssen grün
sein, bevor die nächste Familie drankommt.

## 3. Voraussetzung auf der Zielmaschine: der Bestandskorpus

Der Korpus (~1912 `.nav`, ~7,2 MiB) ist ein **proprietäres TFS-Enlistment** und liegt **nicht im Repo**.
Auf der Referenzmaschine ist das `D:\tfs\Main` (siehe [`nav-kolibri.md`](nav-kolibri.md)). Er trägt die
generierten `.cs` **bereits eingecheckt**, und diese sind byte-identisch zum bisherigen ST-Generator —
also ist der eingecheckte Stand die **Referenz** (kein Alt-Binary nötig).

Auf der Zielmaschine muss dieses Enlistment vorhanden sein; liegt es anderswo, wird der Pfad per
`-CorpusPath` übergeben. Weiter gebraucht: **.NET-SDK 10** (baut `nav.exe`) und die `nav`-Commands
(`. .\Tools\Commands\Import-NavCommands.ps1`).

## 4. Der Parity-Beweis: `nav parity`

Neu in diesem Handoff: der Command **`nav parity`**
(`Tools/Commands/Functions/Invoke-CodeGenParity.ps1`). Er automatisiert den Korpus-Beweis **single-exe**:

1. Korpus in einen Scratch-Ordner spiegeln (Robocopy).
2. Ist-Stand aller `.cs` erfassen (= eingecheckte Baseline == ST-Referenz).
3. Kandidat-`nav.exe -d <scratch> -g All` darüberlaufen lassen (baut standardmäßig frisch aus dem
   Arbeitsbaum), Wall-Zeit messen.
4. Erneut erfassen und **auf zwei Ebenen** vergleichen:
   - **normalisiert** (je Zeile rechts getrimmt, Zeilenenden vereinheitlicht) = das maßgebliche
     Parity-Urteil. `0` heißt: erzeugter Code sichtbar/semantisch identisch.
   - **roh** (Byte-für-Byte) = Audit-Ebene. Solange eine Familie auf ST läuft, muss auch roh `0` sein;
     nach ihrer Migration darf roh `> 0` sein — aber ausschließlich durch Whitespace. Die Roh-Liste ist
     genau die manuell zu sichtende Menge.

```powershell
. .\Tools\Commands\Import-NavCommands.ps1

# Standard: normalisiertes Urteil, Kandidat frisch aus dem Arbeitsbaum gebaut
nav parity -CorpusPath D:\tfs\Main

# Strikte Byte-Identität (Vorab-Check VOR einer Migration bzw. für noch nicht migrierte Familien)
nav parity -CorpusPath D:\tfs\Main -Raw

# Scratch behalten, um Diffs nachzusehen; oder eine vorhandene exe vergleichen
nav parity -CorpusPath D:\tfs\Main -Keep
nav parity -CorpusPath D:\tfs\Main -ExePath 'deploy\Build Tools\nav.exe'
```

Rückgabe ist ein Objekt (`ParityOk`, `RawChanged`, `NormChanged`, `CosmeticOnly`, `Added`, `Removed`,
`CodegenSecs`, …) — skriptbar für einen Bisect bei Parity-Abweichungen. Ausgabe „**PARITY OK
(normalisiert) — N Datei(en) nur kosmetisch**" ist der **erwartete** Zustand direkt nach einer
CodeBuilder-Migration: normalisiert leer, roh nur Whitespace. Erst dann den Roh-Diff auditieren
(`-Keep`, dann die kosmetischen Dateien mit `git diff --no-index` o.ä. sichten).

## 5. Verifikations-Checkliste je Sub-Step

1. **Snapshots:** `nav snapshot` erzeugt keine Diffs (Regression byte-identisch). — maschinen-unabhängig,
   im Repo.
2. **Tests auf beiden TFMs:** `nav test` (net472) **und**
   `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0`.
3. **Korpus-Parity:** `nav parity` → **normalisiert `ParityOk = True`**; anschließend die
   `CosmeticOnly`-Menge roh auditieren (nur Whitespace zulässig, kein Zeichen-/Struktur-Unterschied).
4. **Perf festhalten:** die von `nav parity` gemeldete `CodegenSecs` vorher/nachher notieren (Erwartung:
   keine Regression; der Emitter ersetzt ST 1:1).
5. Erst wenn 1–4 grün: fertige **Commit-Message** liefern (nicht selbst committen).

## 6. Fallstricke

- **Der Korpus fehlt / falscher Pfad** → `nav parity` bricht mit klarer Meldung ab; `-CorpusPath` setzen.
- **Roh-Diff nach Migration ≠ 0 ist normal** — nur wenn er **über Whitespace hinausgeht**, ist Parity
  verletzt. Deshalb ist `-Raw` **nicht** das Migrations-Kriterium, sondern nur der Vorab-/ST-Check.
- **`nav.exe` schreibt in-place** neben die `.nav` (in `generated/`-Unterordner, `*.generated.cs`);
  darum läuft der Beweis über eine **Scratch-Kopie**, nie über das echte Enlistment.
- **CLAUDE.md sagt Nav.Cli sei net472** — die csproj ist net10.0 (SDK-Single-File-Publish trägt die
  Runtime selbst). `nav parity` baut entsprechend mit `dotnet build`.
- **Frische Shell:** zuerst `. .\Tools\Commands\Import-NavCommands.ps1`, sonst ist `nav` nicht geladen.
