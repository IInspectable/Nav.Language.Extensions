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

    Ergänzend läuft Nav.Language.Mcp.Tests (net10.0-only) über `dotnet test` — der net472-
    NUnit-Console-Runner kann diese Assembly nicht ausführen. Auch dieser Schritt wird nur
    ausgeführt, wenn die Assembly bereits gebaut ist (sonst Hinweis statt Fehler).

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

    # Nav.Language.Mcp.Tests ist net10.0-only und läuft nicht über den net472-NUnit-Console-Runner,
    # sondern über `dotnet test` (die csproj ist eigens so gesetzt, dass die `nav build`-Ausgabe direkt
    # ausführbar ist → --no-build). Analog zur DLL-Liste oben: nur ausführen, wenn die Assembly bereits
    # gebaut ist, sonst ein Hinweis statt eines Fehlers. Die NUnit-spezifischen $RemainingArgs (z. B.
    # --where) werden hier bewusst NICHT durchgereicht — VSTest kennt eine andere Filtersyntax.
    $mcpTestsDll = Join-Path $root "Nav.Language.Mcp.Tests\bin\$Configuration\net10.0\Nav.Language.Mcp.Tests.dll"
    if (Test-Path $mcpTestsDll) {
        $mcpTestsProject = Join-Path $root 'Nav.Language.Mcp.Tests\Nav.Language.Mcp.Tests.csproj'
        & dotnet test $mcpTestsProject -c $Configuration --no-build --nologo
        if ($LASTEXITCODE) { throw "MCP-Tests fehlgeschlagen (Exit $LASTEXITCODE)." }
    } else {
        Write-Host "  Übersprungen (nicht gebaut): $mcpTestsDll" -ForegroundColor DarkYellow
    }
}
