<#
.SYNOPSIS
    Erzeugt die Markdown-Tabelle der Diagnose-Fehler (Syntax + Semantic + DeadCode).

.DESCRIPTION
    Lädt die gebaute Pharmatechnik.Nav.Language.dll, ermittelt per Reflection alle
    DiagnosticDescriptors (Syntax, Semantic und DeadCode) und gibt eine Markdown-Tabelle
    (Id/Category/Severity/Message) auf die Pipeline aus — z. B. zum Umleiten in eine Datei:

        nav generateerrors > doc\diagnostics.md

    Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root). Hinweis: Add-Type sperrt die
    geladene DLL für die restliche PowerShell-Sitzung.

.PARAMETER Configuration
    Konfiguration der gebauten DLL. Default: Debug.

.FUNCTIONALITY
    generateerrors
#>
function Invoke-GenerateErrors {
    [CmdletBinding()]
    param(
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $dll = Join-Path $root "Nav.Language\bin\$Configuration\Pharmatechnik.Nav.Language.dll"
    if (-not (Test-Path $dll)) {
        Write-Host "Nav.Language.dll nicht gefunden: '$dll'" -ForegroundColor Red
        Write-Host "  Zuerst bauen (z. B. 'nav build')." -ForegroundColor Yellow
        return
    }

    Add-Type -Path $dll

    "**Nav Language diagnostic errors**"
    ""
    "|   |  Id    | Category | Severity |  Message |"
    "|---|--------|----------|----------|----------| "

    $sources = @(
        [Pharmatechnik.Nav.Language.DiagnosticDescriptors+Syntax].GetFields([Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
        [Pharmatechnik.Nav.Language.DiagnosticDescriptors+Semantic].GetFields([Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
        [Pharmatechnik.Nav.Language.DiagnosticDescriptors+DeadCode].GetFields([Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Static)
    )

    $nbr = 1
    $sources | ForEach-Object {
        $_.GetValue($null)
    } | Where-Object Id -ne $null | Sort-Object Id | ForEach-Object {
        $diag = $_
        "|$($nbr)|<a name=`"$($diag.Id)`">$($diag.Id)</a> | $($diag.Category)| $($diag.DefaultSeverity)| $($diag.MessageFormat)|"
        $nbr++
    }
}
