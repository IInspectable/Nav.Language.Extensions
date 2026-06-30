# NavCommands

Deklaratives PowerShell-Command-System für `Nav.Language.Extensions` — ein Dispatcher mit
Alias **`nav`**, der seine Sub-Commands generisch aus den Dateien in `Functions/` ableitet.
Jede Funktion mit einer `.FUNCTIONALITY <token>`-Help wird automatisch zu `nav <token>` —
inklusive Tab-Completion, interaktivem Auswahlmenü und Übersicht. Keine zentrale Registry:
Eine neue Funktion mit `.FUNCTIONALITY` genügt.

## Setup (einmalig)

Im PowerShell-Profil (`$PROFILE`) dot-sourcen:

```powershell
. "C:\ws\git\Nav.Language.Extensions\Tools\Commands\Import-NavCommands.ps1"
```

Danach steht der Alias `nav` in jeder Session bereit. Die Commands lösen ihren Repo-/Worktree-Root
zur Aufruf-Zeit selbst auf (`git rev-parse --show-toplevel`) — sie funktionieren also aus jedem
Unterordner und treffen bei mehreren Worktrees automatisch den, in dem man gerade steht.

## Benutzung

```powershell
nav                     # interaktives Menü (↑/↓ · Enter · Esc)
nav help                # statische Übersicht aller Commands
nav <TAB>               # Tab-Completion der Tokens
nav build               # Command ausführen
nav build -Configuration Release   # benannte Parameter/Switches werden durchgereicht
```

## Commands

| Token            | Funktion               | Zweck                                                                 |
|------------------|------------------------|----------------------------------------------------------------------|
| `build`          | Invoke-Build           | Solution per MSBuild bauen (Restore + Build).                        |
| `test`           | Invoke-Test            | Tests über den NUnit-Console-Runner ausführen.                      |
| `incminor`       | Invoke-IncreaseMinor   | Minor-Tag `vX.(Y+1).0` auf HEAD anlegen (`-Push`/`-Force`).         |
| `incmajor`       | Invoke-IncreaseMajor   | Major-Tag `v(X+1).0.0` auf HEAD anlegen (`-Push`/`-Force`).         |
| `publish`        | Invoke-Publish         | Solution (Debug) bauen und alles unter `deploy\` bereitstellen: Build Tools, VS-Code-Extension (mit LSP), MCP-Single-File. |
| `install`        | Install-Extension      | VS-2026-Extension (VSIX) in Visual Studio installieren.              |
| `deploy`         | Invoke-Deploy          | Bauen und Build Tools ins XTplus-Verzeichnis kopieren.              |
| `snapshot`       | Invoke-Snapshot        | Regression-Snapshots (`.expected.cs`) neu erzeugen.                 |
| `generateerrors` | Invoke-GenerateErrors  | Markdown-Tabelle der Diagnose-Fehler erzeugen.                       |
| `undo`           | Invoke-Clean           | Alle lokalen Änderungen verwerfen + `git clean`.                    |
| `newbranch`      | New-Branch             | Branch + danebenliegenden Worktree anlegen und hineinwechseln.      |
| `rmbranch`       | Remove-Branch          | Branch + Worktree + Remote-Branch löschen (mit Schutzmechanismen).  |
| `help`           | Show-Commands          | Übersicht aller Commands.                                            |

Die Tabelle ist nur eine Lese-Hilfe — Quelle der Wahrheit sind die `.FUNCTIONALITY`-Tokens in
`Functions/`. Detail-Hilfe je Command via `Get-Help <Funktion> -Full`, z. B. `Get-Help New-Branch -Full`.

## Navigation

Eigenständige Shortcut-Funktionen (kein `nav`-Dispatcher, kein Token):

| Funktion | Zweck                                                                       |
|----------|-----------------------------------------------------------------------------|
| `nav:`   | In den Root eines Worktrees wechseln. Bei mehreren erscheint ein Pfeiltasten-Menü; optionaler Branch-Filter (`nav: lsp`). |

## Versionierung

Die Version wird **git-abgeleitet** (`git describe`) — kein `Version.props` mehr. Der 3-teilige Kern
ist `Major.Minor.(Patch des letzten vX.Y.Z-Tags + Commits seit Tag)`. Major/Minor werden über Tags
gesteuert: `incminor`/`incmajor` legen das nächste `vX.Y.0`-Tag auf HEAD an (Clean-Tree-Check,
Monotonie-Absicherung, Bestätigung, optional `-Push`); der Patch zählt automatisch — ein `incbuild`
gibt es nicht mehr. Die Berechnung liefert `Get-ProductVersion` (Autorität); `build`/`publish`
reichen sie als `-p:ProductVersion=…` an MSBuild durch, `_build\Version.targets` ist der Fallback.
Die `version` in `vscode-nav-lsp\package.json` bleibt Platzhalter (`0.0.0`): `publish` ruft
`vsce package <version> --no-update-package-json` mit der git-abgeleiteten Version.

## Branching (Worktree-basiert)

`newbranch foo` legt den Branch `feature/foo` und einen Geschwister-Ordner
`Nav.Language.Extensions-feature-foo` neben dem Haupt-Repo an und wechselt hinein
(`-Type bugfix|hotfix`, vollständiger `foo/bar`-Name verbatim). `rmbranch` entfernt Branch,
Worktree und Remote-Branch in einem Rutsch — `master` ist gesperrt, dirty Worktrees und der
Aufruf aus dem zu löschenden Worktree werden hart abgewiesen.

## Eigene Commands hinzufügen

1. Neue Datei `Functions/Verb-Noun.ps1` mit einer Funktion `Verb-Noun`.
2. Comment-based Help mit `.SYNOPSIS` (erscheint in der Übersicht) und `.FUNCTIONALITY <token>`
   (macht sie als `nav <token>` aufrufbar).
3. Root via `Resolve-Root` auflösen. Fertig — Übersicht und Tab-Completion ziehen automatisch nach.

Funktionen **ohne** `.FUNCTIONALITY` mit Bindestrich-Namen (Verb-Noun) gelten als interne Helper
(z. B. `Resolve-Root`, `Resolve-MsBuild`, `Get-Worktree`, `Get-ProductVersion`, `Set-VersionTag`) und
tauchen nicht als Command auf.
