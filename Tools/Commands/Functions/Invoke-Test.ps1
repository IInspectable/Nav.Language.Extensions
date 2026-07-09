<#
.SYNOPSIS
    Führt die Tests über den NUnit-Console-Runner aus.

.DESCRIPTION
    Ruft den gebündelten NUnit-Console-Runner (Build\nunit.consolerunner\3.8.0\tools\
    nunit3-console.exe) auf die Test-Assemblies von Nav.Language.Tests,
    Nav.Language.CodeAnalysis.Tests und Nav.Language.Extension.Tests
    (`<Projekt>\bin\<Configuration>\<Projekt>.dll`). Nur
    tatsächlich vorhandene DLLs werden übergeben — fehlt eine, gibt es einen Hinweis (z. B.
    weil sie noch nicht gebaut wurde). Der Repo-Root wird zur Aufruf-Zeit aufgelöst
    (Resolve-Root).

.PARAMETER Configuration
    Build-Konfiguration der Test-Assemblies. Default: Debug.

.PARAMETER RemainingArgs
    Zusätzliche, unverändert an nunit3-console durchgereichte Argumente (z. B. --where ...).

.FUNCTIONALITY
    test
#>
function Invoke-Test {
    [CmdletBinding()]
    param(
        # RemainingArgs MUSS der positionale Auffangkorb (Position 0) sein, damit an nunit3-console
        # durchgereichte Optionen wie `--where` erhalten bleiben: PowerShell erkennt `--where`
        # (Doppelstrich) NICHT als Parameternamen und würde es sonst positional an ein positionales
        # $Configuration binden — der Filter ginge lautlos verloren. Configuration bleibt deshalb
        # named-only (`-Configuration Release`).
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $RemainingArgs,
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $runner = Join-Path $root 'Build\nunit.consolerunner\3.8.0\tools\nunit3-console.exe'
    if (-not (Test-Path $runner)) {
        throw "NUnit-Console-Runner nicht gefunden: '$runner'."
    }

    $projects = 'Nav.Language.Tests', 'Nav.Language.CodeAnalysis.Tests', 'Nav.Language.Extension.Tests'
    $assemblies = foreach ($p in $projects) {
        $dll = Join-Path $root "$p\bin\$Configuration\$p.dll"
        if (Test-Path $dll) { $dll }
        else { Write-Host "  Übersprungen (nicht gebaut): $dll" -ForegroundColor DarkYellow }
    }

    if (-not $assemblies) {
        Write-Host "Keine Test-Assemblies gefunden — zuerst 'nav build' ausführen." -ForegroundColor Yellow
        return
    }

    & $runner @assemblies @RemainingArgs
    if ($LASTEXITCODE) { throw "Tests fehlgeschlagen (Exit $LASTEXITCODE)." }
}
