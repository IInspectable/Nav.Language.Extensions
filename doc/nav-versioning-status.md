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

## Zwei AssemblyInfo-Pfade (Kern der Fragilität)

1. **GobalAssemblyInfo + WriteThisAssemblyFile** — net472/netstandard-Projekte (Nav.Language,
   Nav.Utilities, Nav.Cli, Nav.Language.CodeAnalysis, Nav.Language.BuildTasks,
   Nav.Language.Extension2026; alle `GenerateAssemblyInfo=false`):
   - `_build/WriteThisAssemblyFile.targets` generiert `<Projekt>/ThisAssembly.generated.cs` mit
     `static partial class MyAssembly { ProductVersion, AssemblyVersion,
     ProductVersionInformational, ProductName }`.
   - `_build/GobalAssemblyInfo.cs` setzt die Assembly-Attribute aus diesen Konstanten.
   - Laufzeit liest die Konstante `MyAssembly.ProductVersion`. Konsumenten u.a.:
     `Nav.Cli/CommandLine.cs`, `Nav.Cli/GrammarCommand.cs`, `Nav.Cli/Analyzer/CodeFixPipeline.cs`,
     `Nav.Language/Generator/NavCodeGeneratorPipeline.LoggerAdapter.cs`,
     `Nav.Language/CodeGen/CodeGeneratorContext.cs`, `Nav.Language.ExtensionShared/NavLanguagePackage.cs`.
   - **Schwäche: die generierte Datei liegt im PROJEKTVERZEICHNIS, geteilt zwischen allen
     Build-Prozessen.** (Dateiname-Tippfehler „Gobal" statt „Global" historisch.)
2. **SDK-Assembly-Info** — net10-SDK-Hosts (Nav.Language.Lsp, Nav.Language.Mcp;
   `GenerateAssemblyInfo` default true):
   - `SetSdkVersionFromGit` (in `_build/Version.targets`) speist `Version`/`FileVersion`/
     `InformationalVersion` aus `ComputeGitVersion` in die SDK-Pipeline; `AssemblyVersion` kommt
     ebenfalls aus `ComputeGitVersion`.
   - Generierte Assembly-Info landet in **obj**, pro Konfiguration getrennt. **Robust** — von der
     unten beschriebenen Falle nie betroffen.

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

**Ursache:** **Visual Studio.** Bei offener Solution startet VS im Hintergrund laufend eigene
**Design-Time-Builds**. In deren Kontext läuft der git-`Exec` in `ComputeGitVersion` leer, sodass die
Version auf den Fallback `0.0.0` fällt. Da `ThisAssembly.generated.cs` im **Projektverzeichnis**
liegt (geteilt zwischen allen Build-Prozessen), überschreibt VS' Design-Time-Build mitten im echten
(CLI-)Build den frisch gestempelten korrekten Wert mit `0.0.0` — der anschließende `csc` kompiliert
die `0.0.0`-Variante. Nachgewiesen per Datei-Polling während des Builds: `6.0.124` → `0.0.0` ~1,3 s
nach dem korrekten Write, aus einem Fremdprozess (`devenv`). MCP/LSP waren **nie** betroffen, weil
sie die Version über obj/SDK-Assembly-Info führen — kein geteiltes Projektverzeichnis-File.

**Fix (umgesetzt):** `WriteThisAssemblyFile` bei Design-Time-Builds nicht ausführen —
`Condition="('$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' != 'false') or
!Exists('$(AssemblyInfoFile)')"`. Fehlt die Datei auf einem frischen Clone noch ganz, wird sie
einmalig erzeugt (sonst schlüge der Compile auf die nicht existente Datei fehl).

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

## Zukunft: AssemblyInfo anders lösen

Idee: den fragilen **GobalAssemblyInfo/WriteThisAssemblyFile-Pfad ganz abschaffen** und — wie
MCP/LSP — **alles über SDK-Assembly-Info + MSBuild-Properties** führen (obj-basiert, von VS'
Design-Time-Builds strukturell nicht teilbar).

- **Vorteil:** keine geteilte Projektverzeichnis-Datei → VS-Falle strukturell weg; **eine** Mechanik
  statt zwei; Tippfehler-Dateiname `GobalAssemblyInfo.cs` verschwindet mit.
- **Hürde:** Die Laufzeit liest die Konstante `MyAssembly.ProductVersion`. Lösungen:
  1. zur Laufzeit das `AssemblyInformationalVersionAttribute` (bzw. `AssemblyFileVersion`) per
     Reflection lesen — Standardweg, entfernt `MyAssembly`/`ThisAssembly` komplett; oder
  2. die Konstante weiterhin generieren, aber **obj-lokal** (`$(IntermediateOutputPath)`) statt ins
     Projektverzeichnis — Achtung: `IntermediateOutputPath` ist zum Import-Zeitpunkt der Targets noch
     leer (SDK setzt ihn später), die Datei darf also nicht früh als Property fixiert, sondern muss
     spät (im Target / nach SDK-Props) gebunden werden.
- Variante 1 ist sauberer. Konsumenten (s.o.) umstellen; bei der VS-Extension (net472) zusätzlich
  `ProductName` beachten (Title/Product aus einer `$(ProductName)`-Property statt aus der
  MyAssembly-Konstante).

## Relevante Dateien

| Datei | Rolle |
|---|---|
| `_build/Version.targets` | `ComputeGitVersion` (Autorität) + `SetSdkVersionFromGit` (SDK-Hosts) |
| `_build/WriteThisAssemblyFile.targets` | GobalAssemblyInfo-Generierung + Design-Time-Guard |
| `_build/GobalAssemblyInfo.cs` | Assembly-Attribute aus `MyAssembly`-Konstanten |
| `Tools/Commands/Functions/Get-ProductVersion.ps1` | liest Version per `dotnet msbuild -getProperty` |
| `Tools/Commands/Functions/Invoke-Build.ps1`, `Publish-Cli/Mcp/VsCode.ps1` | bauen/publishen ohne `-p` |
| `Nav.Language.Extension2026/CustomBuild.targets` | VSIX-Manifest-Stempelung (DependsOn `ComputeGitVersion`) |
