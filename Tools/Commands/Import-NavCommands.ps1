<#
.SYNOPSIS
    Einziger Einstiegspunkt für die Nav-Commands — per Dot-Sourcing aus dem
    PowerShell-Profil laden.

.DESCRIPTION
    Lädt alle Command-Funktionen aus dem Unterordner `Functions` (eine Datei pro Command).
    Jede Funktion löst ihren Repo-/Worktree-Root selbst zur Aufruf-Zeit auf (siehe
    Resolve-Root), daher lassen sich die Befehle von jedem Ort im Repo aufrufen.

    Profil-Setup (einmalig, außerhalb des Repos), z. B. in $PROFILE:

        . "C:\ws\git\Nav.Language.Extensions\Tools\Commands\Import-NavCommands.ps1"

    Eine Übersicht der bereitgestellten Funktionen und Aliase steht in README.md.
#>

Get-ChildItem -Path (Join-Path $PSScriptRoot 'Functions') -Filter *.ps1 |
    ForEach-Object { . $_.FullName }
