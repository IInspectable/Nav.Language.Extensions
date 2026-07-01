# Nav-Versionierung — Status & Entscheidungen

Statusdokument zur Versionsnummern-Infrastruktur (Pflichtlektüre vor Arbeiten daran).

## Modell (Ist-Stand)

- Version **git-abgeleitet** via `git describe` — kein `Version.props` mehr.
- Kern (3-teiliges SemVer): `Major.Minor.(Patch des letzten vX.Y.Z-Tags + Commits seit Tag)`.
  Major/Minor werden per Tag gesteuert (`vX.Y.0`, `n incminor`/`incmajor`), der Patch zählt
  automatisch.
- `AssemblyVersion` = `Major.Minor.0.0` (stabile Binding-Identität), `FileVersion` = Kern,
  `AssemblyInformationalVersion` = Kern + Branch + Kurz-SHA.
- **Einzige Autorität ist das MSBuild-Target `ComputeGitVersion`** (`_build/Version.targets`), über
  `Directory.Build.props` in **jedem** Build-Pfad aktiv (`n build`/MSBuild.exe, `dotnet build`,
  `dotnet publish`, VS). Es rechnet selbst aus git — **kein `-p:ProductVersion`-Durchreichen** mehr.
- PowerShell `Get-ProductVersion` rechnet **nichts**, sondern **liest** die Werte per
  `dotnet msbuild _build/Version.targets -t:ComputeGitVersion -getProperty:ProductVersion`
  (nur für vsce-/VSIX-Dateinamen). Damit gibt es keinen zweiten Parser.

## Die `MyAssembly`-Konstanten (`ProductVersion` & Co.)

`GobalAssemblyInfo.cs` (bleibt hand-geschrieben in `_build/`, je Projekt gelinkt) setzt die
Assembly-Attribute aus den Konstanten `MyAssembly.{ProductVersion, AssemblyVersion,
ProductVersionInformational, ProductName}`. Dieselben Konstanten liest die Laufzeit. Konsumenten u.a.:
`Nav.Cli/CommandLine.cs`, `Nav.Cli/GrammarCommand.cs`,
`Nav.Language/Generator/NavCodeGeneratorPipeline.LoggerAdapter.cs`,
`Nav.Language/CodeGen/CodeGeneratorContext.cs`,
`Nav.Language.ExtensionShared/NavLanguagePackage.cs` (Attribut-Argument → **braucht** die Konstante).

**Woher die Konstanten kommen (seit der Umstellung weg von der Projektverzeichnis-Datei):**

1. **Roslyn-Quellgenerator** `Nav.AssemblyInfo.SourceGenerator` — die fünf SDK-Projekte
   (Nav.Language, Nav.Utilities, Nav.Cli, Nav.Language.CodeAnalysis, Nav.Language.BuildTasks; alle
   `GenerateAssemblyInfo=false`). Der Generator liest `ProductVersion` & Co. als `build_property.*`
   (via `CompilerVisibleProperty`, gespeist aus `ComputeGitVersion`) und emittiert `static partial
   class MyAssembly`. **Opt-in mit einer Zeile:** `<UseAssemblyInfoGenerator>true</…>`; das zentrale
   Wiring (`_build/SourceGenerators/SourceGenerator.targets`, global über `Directory.Build.targets`)
   hängt Analyzer-`ProjectReference` + `CompilerVisibleProperty` an. Output ist **pro Compilation**
   (nie eine geteilte Datei) → strukturell immun gegen die unten beschriebene Falle.
2. **Physische obj-Datei** — die **erzwungene Ausnahme** für `Nav.Language.Extension2026` (legacy,
   non-SDK, WPF), **kein** Fallback. Die WPF-Markup-Compilierung baut ein temporäres Teilprojekt
   (`…_wpftmp.csproj`), das den gesamten C#-Code zu einer Wegwerf-Assembly übersetzt, damit XAML
   projekteigene Typen auflösen kann — in diesem Teilprojekt laufen Quellgeneratoren **nicht**. Mit dem
   Generator fehlt dort `MyAssembly`, der Compiler zieht die internen `MyAssembly` der referenzierten
   `Nav.*`-Assemblies (nicht zugänglich) → **CS0122**, der Build bricht ab. **Empirisch verifiziert**
   (kontrollierter Differenz-Test, gleicher `MSBuild.exe /t:Build`): physische obj-Datei → grün;
   Generator (`UseAssemblyInfoGenerator=true`, physische Datei unterdrückt) → `error CS0122` explizit im
   `…_wpftmp.csproj`. Deshalb schreibt hier `WriteMyAssemblyFile` (in der Projekt-`CustomBuild.targets`)
   die Konstanten in `$(IntermediateOutputPath)MyAssembly.g.cs` und nimmt sie als **physisches**
   `@(Compile)`-Item auf — nur so sind sie im wpftmp sichtbar (ein `<Compile>`-Item wird ins Temp-Projekt
   kopiert, Generator-Output nicht). **Wichtig — nicht strukturell trap-immun wie der Generator:** obj/ ist
   zwischen VS- und CLI-Build desselben Projekts genauso geteilt wie das Projektverzeichnis; der Schutz ist
   allein der Design-Time-Guard (+ funktionierendes git/`safe.directory`). Der Generator-Weg (Output pro
   Compilation, gar keine geteilte Datei) bleibt der robustere und ist überall dort das Mittel der Wahl, wo
   er läuft — die obj-Datei nur, wo er es nachweislich nicht tut (wpftmp).
3. **SDK-Assembly-Info** — net10-SDK-Hosts (Nav.Language.Lsp, Nav.Language.Mcp;
   `GenerateAssemblyInfo` default true, **kein** `MyAssembly`). `SetSdkVersionFromGit` (in
   `_build/Version.targets`) speist `Version`/`FileVersion`/`InformationalVersion` aus
   `ComputeGitVersion` in die SDK-Pipeline; `AssemblyVersion` kommt ebenfalls aus `ComputeGitVersion`.
   Generierte Assembly-Info landet in **obj**. **Robust** — von der Falle nie betroffen.

**Wichtig (Reihenfolge):** Der Generator liest die Werte aus der von
`GenerateMSBuildEditorConfigFileCore` erzeugten Analyzer-`.editorconfig` (das TASK heißt
`GenerateMSBuildEditorConfig`, das TARGET aber `…FileCore`). `ComputeGitVersion` MUSS davor laufen —
dafür sorgt das gated Target `_NavComputeGitVersionBeforeEditorConfig` in `_build/Version.targets`.
Sonst landen leere Werte in der `.editorconfig` und `MyAssembly.ProductVersion` wird `0.0.0`.

## Robustheit (umgesetzt)

- **Strikter Tag-Parser** in MSBuild (Regex `^v(\d+)\.(\d+)\.(\d+)-(\d+)-g([0-9A-Fa-f]+)$`,
  identisch zum PowerShell-Pendant) statt positionellem `Split`. Nicht-konforme Tags fallen sauber
  in den Fallback.
- **git-Ausfall-Guard**: Fallback `0.0.<count>` übernimmt die Commit-Anzahl nur bei `rev-list`-Exit
  0 **und** reiner Zahl. Damit kann ein git-Fehlertext (z.B. „fatal: detected dubious ownership")
  nie in die Version lecken.

## Die Visual-Studio-Design-Time-Falle (zentrale Lektion)

**Symptom:** Nach `n publish` trägt `nav.exe` (oder eine andere GobalAssemblyInfo-Assembly)
sporadisch Version `0.0.0`, obwohl der Build nachweislich `6.0.x` berechnet **und** schreibt — im
MSBuild-Log taucht `0.0.0` **nie** auf.

**Ursache (historisch):** **Visual Studio.** Bei offener Solution startete VS im Hintergrund laufend
eigene **Design-Time-Builds**. In deren Kontext läuft der git-`Exec` in `ComputeGitVersion` leer,
sodass die Version auf den Fallback `0.0.0` fällt. Da die damals genutzte `ThisAssembly.generated.cs`
im **Projektverzeichnis** lag (geteilt zwischen allen Build-Prozessen), überschrieb VS'
Design-Time-Build mitten im echten (CLI-)Build den frisch gestempelten korrekten Wert mit `0.0.0` —
der anschließende `csc` kompilierte die `0.0.0`-Variante. Nachgewiesen per Datei-Polling während des
Builds: `6.0.124` → `0.0.0` ~1,3 s nach dem korrekten Write, aus einem Fremdprozess (`devenv`).
MCP/LSP waren **nie** betroffen, weil sie die Version über obj/SDK-Assembly-Info führen — kein
geteiltes Projektverzeichnis-File.

**Fix (umgesetzt — Ursache strukturell entfernt):** Die Projektverzeichnis-Datei
(`WriteThisAssemblyFile.targets` → `ThisAssembly.generated.cs`) ist **abgeschafft**. Für die 5
SDK-Projekte kommt `MyAssembly` jetzt aus dem Roslyn-Quellgenerator — Output **pro Compilation, gar
keine geteilte Datei**, damit strukturell immun: Ein VS-Design-Time-Build kann nichts mehr
überschreiben, weil es nichts Geteiltes zum Überschreiben gibt. Das legacy WPF-Extension-Projekt kann
den Generator nicht nutzen (wpftmp, siehe Abschnitt oben) und behält als **erzwungene Ausnahme** eine
obj-lokale Datei mit Design-Time-Guard — die einzige verbliebene Stelle, die überhaupt noch auf einer
Datei beruht. Sie ist nicht mehr im eingecheckten Projektverzeichnis, aber (anders als der Generator)
weiterhin guard-abhängig, nicht strukturell immun.

**Diagnose-Rezept** (falls so etwas wiederkommt):
- Datei-Inhalt **während** des Builds pollen → fängt das `6.0.x` → `0.0.0`-Flip aus dem Fremdprozess.
- Datei mit korrektem Wert schreibgeschützt setzen, dann bauen → der schreibende Übeltäter fliegt
  mit `MSB3491` (Access denied) auf.
- `Get-Process devenv` prüfen; `-bl`-Binlog zeigt, dass MSBuild selbst nur den korrekten Wert
  schreibt.

**Historie:** Dasselbe Muster trat früher schon beim VSIX-Build auf.

## Umgebung

`.git` gehört unter Umständen den Administratoren → ohne
`git config --global --add safe.directory <repo>` meldet git „dubious ownership" und schlägt fehl.
Das betrifft auch VS' Build-Kontext: Der Design-Time-Guard schützt nur die **Hintergrund**-Builds;
baut man **explizit in VS** (Build-Menü) und git scheitert dort, könnte auch ein echter VS-Build
`0.0.0` schreiben. Dann Ownership/`safe.directory` korrigieren.

## Warum Quellgenerator statt Laufzeit-Reflection

Naheliegend wäre gewesen, `MyAssembly` ganz zu streichen und die Version zur Laufzeit per Reflection
aus `AssemblyInformationalVersionAttribute`/`AssemblyFileVersion` zu lesen. Das scheitert an **einer**
Stelle: `NavLanguagePackage.cs` nutzt `MyAssembly.ProductVersion` als **Attribut-Argument**
(`InstalledProductRegistration`) — das muss eine Compile-Zeit-Konstante sein, Reflection kann sie
nicht liefern. Der Quellgenerator liefert weiterhin **Konstanten** und lässt damit alle Konsumenten
(inkl. dieser Attribut-Stelle) unverändert — und ist zugleich trap-immun (Output pro Compilation).

## Relevante Dateien

| Datei | Rolle |
|---|---|
| `_build/Version.targets` | `ComputeGitVersion` (Autorität) + `SetSdkVersionFromGit` (SDK-Hosts) + `_NavComputeGitVersionBeforeEditorConfig` (Reihenfolge für den Generator) |
| `_build/SourceGenerators/Nav.AssemblyInfo.SourceGenerator/` | Quellgenerator: `MyAssembly`-Konstanten aus `build_property.*` |
| `_build/SourceGenerators/SourceGenerator.targets` | zentrales `UseAssemblyInfoGenerator`-Wiring (Analyzer-Ref + `CompilerVisibleProperty`) |
| `Directory.Build.targets` | importiert das Wiring global (auch im legacy VSIX-Projekt) |
| `_build/GobalAssemblyInfo.cs` | Assembly-Attribute aus `MyAssembly`-Konstanten (unverändert) |
| `Nav.Language.Extension2026/CustomBuild.targets` | `WriteMyAssemblyFile` (obj-lokale `MyAssembly.g.cs`, wpftmp-tauglich) + VSIX-Manifest-Stempelung |
| `Tools/Commands/Functions/Get-ProductVersion.ps1` | liest Version per `dotnet msbuild -getProperty` |
| `Tools/Commands/Functions/Invoke-Build.ps1`, `Publish-Cli/Mcp/VsCode.ps1` | bauen/publishen ohne `-p` |
