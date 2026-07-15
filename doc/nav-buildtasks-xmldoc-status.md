# Nav.Language.BuildTasks — XML-Doku (Status & Umsetzung)

> **Status: ABGESCHLOSSEN (2026-07-15, uncommittet).** Alle 3 handgeschriebenen Dateien dokumentiert;
> **24× CS1591 → 0**, **0× CS1570–84**. Glossar um **§10 MSBuild-Task / Build-Integration** ergänzt
> (1 Eintrag + A–Z-Index). Commit macht der Nutzer.
>
> **Zuschnitt-Entscheid (Nutzer).** Das Projekt ist mit nur **3 kleinen C#-Dateien** (~155 LOC, eine
> reale Klasse) deutlich zu klein für eine mehrwellige Multi-Subagent-Kampagne (Vorkampagnen: 9–254
> Dateien). Nutzer-Entscheid: **ein Durchgang mit voller Gate-Disziplin** (G1–G3, Glossar, Status-Doc,
> Commit-Message) statt Wellen/Subagenten.
>
> **Abweichung von „doku-only" (Nutzer-Entscheid).** Eine extern geöffnete IDE (Rider/VS mit
> Reformat/Cleanup-on-change) hat **alle drei Dateien beim Edit nebenbei umformatiert** (Spalten-Alignment
> kollabiert, `if(`→`if (`, Leerzeilen zwischen Membern, Trailing-Whitespace entfernt). Der Repo-BOM-Hook
> (`Ensure-Utf8Bom.ps1`) tut das **nicht** — es ist die IDE. Ein byte-exaktes Zurücksetzen ist nicht
> stabil (jeder Restore ist selbst eine Dateiänderung → die IDE formatiert erneut, Endlosschleife).
> **Nutzer-Entscheid: Reformatting akzeptieren.** Der Diff enthält daher **XML-Doku (105 `///`-Zeilen)
> + IDE-Reformatting (74 Nicht-`///`-Zeilen)** gemischt; **G1 (doku-only) entfällt bewusst**. G2/G3 grün.

## 1. Ziel & Ausgangslage (Audit 2026-07-15)

`Nav.Language.BuildTasks` (net472, Assembly `Pharmatechnik.Nav.Language.BuildTasks`) ist der Host, der
die `.nav`→C#-Übersetzung in den **MSBuild-Build** einklinkt. Er referenziert **weder** `Nav.Language`
**noch** `Nav.Cli` als Assembly (nur Build-Reihenfolge via `ReferenceOutputAssembly=false`) — der
net472-Task darf die net10-self-contained `nav.exe` nicht laden. Referenzen sind allein
`Microsoft.Build.Utilities.Core` (+ `Microsoft.Build.Framework` transitiv) und `System.Memory`.

**Handdoku-Dateien (3):**

- `Generator/Nav.cs` (121 LOC) — die eigentliche Substanz: `public class Nav : ToolTask`, der
  MSBuild-Task. 14 public Task-Parameter (`Force`/`Strict`/`Sources`/`Generate{To,Wfl,Iwfl}Classes`/
  `UseSyntaxCache`/`FullPaths`/`NullableContext`/`{Project,Iwfl,Wfl}RootDirectory`/`ManifestFile`/
  `DependencyManifestFile`), 9 `protected`-`ToolTask`-Overrides, die private `GetCodeGenerationOptions`.
- `CodeGenerationOptions.cs` (19 LOC) — `internal [Flags] enum`, lokale Kopie des `/g:`-Kontrakts der
  `nav.exe` (interoperabel über die Enum-**Namen**, nicht die Zahlenwerte).
- `CommandLineBuilderExtensions.cs` (15 LOC) — eine `static`-Extension-Methode `AppendSwitchIfPresent`.

**Nicht Gegenstand:** `..\Build\GobalAssemblyInfo.cs` (compile-eingebunden, geteilte Assembly-Attribute),
die `.targets`-Dateien (MSBuild-XML — anderes Doku-Format, `///` greift nicht) und `obj\**`.

**Parameter-Abbildung (aus `Pharmatechnik.Nav.Language.targets` verifiziert):**
`Force`→`/f`, `Strict`→`/t`, `UseSyntaxCache`→`/c`, `FullPaths`→`/fullpaths`, `NullableContext`→`/n`,
stets `/v`, `Generate*Classes`→`/g:` (via `CodeGenerationOptions`-Namen), `ProjectRootDirectory`→`/r:`,
`WflRootDirectory`→`/w:`, `IwflRootDirectory`→`/i:`, `ManifestFile`→`/m:`, `DependencyManifestFile`→`/dm:`,
`Sources`→`/s:` (je Datei).

**Baseline (Gate G2, verifiziert 2026-07-15):** **24× CS1591**, alle in `Nav.cs` (die einzige `public`
Klasse: Klasse + 14 public Props + 9 `protected` Overrides = 24). `CodeGenerationOptions` (`internal`)
und `CommandLineBuilderExtensions` (`static`) zählen 0 CS1591, werden aber laut Konvention breit
mitdokumentiert. **0× CS1570–84.**

## 2. Eiserne Regeln

Wie in den Vorkampagnen (`doc/nav-cli-xmldoc-status.md` §3), mit **einer** dokumentierten Ausnahme:
die **doku-only-Regel (G1) wurde vom Nutzer für dieses Projekt ausgesetzt** (IDE-Reformatting, s.o.).
Weiterhin gilt: nur belegbare Aussagen, Deutsch mit echten Umlauten, UTF-8 mit BOM, CRLF,
keine Plan-Step-Verweise, **nichts committen**.

**cref-Falle (projektspezifisch, wichtig):** BuildTasks referenziert Nav.Language/Nav.Cli **nicht** als
Assembly → crefs dorthin würden **CS1574** werfen. Referenzierbar sind nur **eigene Typen** (`Nav`,
`CodeGenerationOptions`, `CommandLineBuilderExtensions`) und **Microsoft.Build.*** (`ToolTask`,
`CommandLineBuilder`, `ITaskItem`, `MessageImportance`). Engine-Begriffe (`nav.exe`, `/g:`,
`GenerationOptions`, WFL/IWFL/TO) bleiben **Klartext / `<c>…</c>`**. (Das ist der Gegensatz zu Nav.Cli,
wo crefs in die Engine erlaubt waren.)

**CS1587-Falle:** `///` auf der lokalen Funktion `GetGetCodeGenerationArg` (in
`GenerateResponseFileCommands`) würde CS1587 werfen → dort bewusst keine XML-Doku.

## 3. Verifikations-Gates

**G1 — doku-only:** **ausgesetzt** (Nutzer-Entscheid, IDE-Reformatting).

**G2 — XML wohlgeformt + cref auflösbar** (Compiler, `--no-incremental` Pflicht):

```bash
dotnet build Nav.Language.BuildTasks/Nav.Language.BuildTasks.csproj -c Debug \
  -p:GenerateDocumentationFile=true --no-incremental 2>&1 > /tmp/btdoc.txt
grep -F ': warning CS15' /tmp/btdoc.txt \
  | grep -F '\Nav.Language.BuildTasks\' | grep -Fv '\obj\' | sort -u \
  | grep -oE 'CS15[0-9][0-9]' | sort | uniq -c
```

Ergebnis: **CS1591 24→0**, **CS1570–84 0→0**, Build fehlerfrei. ✅

**G3 — Kodierung:** BOM vorhanden, kein `U+FFFD`, CRLF intakt — für alle 3 Dateien ✅.

## 4. Ergebnis je Datei

| Datei | CS1591 vorher | nachher | dokumentiert |
|---|---:|---:|---|
| `Generator/Nav.cs` | 24 | 0 | Klasse + 14 public Params + 9 `protected` Overrides + `GetCodeGenerationOptions` |
| `CodeGenerationOptions.cs` | 0 | 0 | Enum-Typ + 5 Werte (Konvention: `internal` breit mitdokumentiert) |
| `CommandLineBuilderExtensions.cs` | 0 | 0 | `static`-Klasse + `AppendSwitchIfPresent` |

## 5. Glossar-Ergänzung

Neuer **§10 MSBuild-Task / Build-Integration** (`doc/Glossar.md`): Eintrag **MSBuild-Task / Build-Task**
(`Nav` `ToolTask` als externer `nav.exe`-Aufrufer, `<UsingTask>`/`GenerateNavCode`, bewusst ohne
Engine-Assembly-Referenz → lokale `/g:`-Kopie). A–Z-Index um den Begriff erweitert. Eigener Commit.

## 6. Commit-Konvention

Zwei Commits (Doku + Glossar), Muster:

```
Nav-BuildTasks: XML-Doku (nav.exe-MSBuild-Task) — inkl. IDE-Format-Cleanup

Alle 3 handgeschriebenen Dateien mit C#-XML-Doku versehen (24× CS1591 → 0,
0× CS1570–84, Doku-Build grün). Der Diff enthält zusätzlich von einer offenen
IDE angewandtes Format-Cleanup (Alignment/if-Spacing/Leerzeilen) — daher nicht
strikt doku-only (bewusst so abgenommen).
```

```
Nav-Engine: Glossar um §10 MSBuild-Task / Build-Integration ergänzt
```

## 7. Fortschritts-Log

| Datum | Ergebnis |
|---|---|
| 2026-07-15 | Projektwahl `Nav.Language.BuildTasks` (Nutzer). Audit: 3 Dateien, Baseline **24× CS1591** (alle in `Nav.cs`), 0× CS1570–84. Zuschnitt-Entscheid: **ein Durchgang** statt Multi-Subagent-Kampagne (zu klein). |
| 2026-07-15 | Alle 3 Dateien dokumentiert (105 `///`-Zeilen). **G2** CS1591 24→0, 0× CS1570–84, Build grün; **G3** BOM/CRLF sauber. Offene IDE reformatierte alle 3 Dateien beim Edit → **G1 doku-only ausgesetzt** (Nutzer-Entscheid „Reformatting akzeptieren"). Glossar §10 ergänzt. **Abgeschlossen** — Commit durch Nutzer. |
