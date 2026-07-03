<#
.SYNOPSIS
    Auditiert den Fortschritt und die Wasserdichtheit der Nullable-Kampagne in Nav.Language.

.DESCRIPTION
    Werkzeug der ordnerweisen NRT-Umstellung (per Datei `#nullable enable`, ohne projektweites
    <Nullable>). Drei Aufgaben:

      1. Fortschritt: Scan Nav.Language\**\*.cs (ohne bin/obj/*.generated.cs) auf `#nullable enable`,
         gruppiert je Top-Level-Ordner → Tabelle (konvertiert/gesamt/%) plus ein approximativer
         Suppression-Zähler (`!`-Null-forgiving) je Ordner.

      2. Prüfbau: baut Nav.Language mit `-p:Nullable=warnings` (der ganze Engine-Kern bekommt so den
         Warning-Kontext; per-Datei-`#nullable enable` überstimmt ihn und behält die volle Prüfung),
         sowie Nav.Language.Lsp und Nav.Language.Mcp in ihrer nativen Einstellung (bereits projektweit
         `enable`) als Konsum-Validierung der Engine-Public-Surface. Alle mit `--no-incremental`
         (übersprungene Compiles emittieren keine Warnungen).

         Bewusst KEIN `-p:Nullable=warnings` für LSP/MCP: das würde deren Annotations-Kontext
         abschalten und jedes `?` mit CS8632 fluten (statt echter Konsum-Regressionen).

      3. Baseline-Diff: aggregiert die Nullable-Warnungen (CS86xx/CS87xx) je (Datei, Warncode) — ohne
         Zeilennummern, robust gegen Zeilen-Drift — und vergleicht gegen Build\nullaudit-baseline.txt.
         Ein neues (Datei, Warncode)-Paar oder ein gestiegener Zähler ist eine Regression → Abbruch mit
         Fehler. Restrauschen der noch offenen Ordner steht in der eingecheckten Baseline und zählt
         nicht.

.PARAMETER UpdateBaseline
    Schreibt den aktuellen Warnungs-Stand als neue Baseline (nach einem reviewten Step). Kein Diff.

.PARAMETER Detail
    Ordnername (relativ zu Nav.Language, z. B. CodeFixes). Baut und listet die Roh-Warnungen dieses
    Ordners MIT Zeilennummern — der Arbeitsmodus beim Annotieren. Kein Baseline-Diff, kein Abbruch.

.PARAMETER NoBuild
    Nur der Fortschritts-Scan (Punkt 1), kein Build und kein Diff.

.PARAMETER Configuration
    Build-Konfiguration für den Prüfbau. Default: Debug.

.EXAMPLE
    nav nullaudit
    Fortschritt + Prüfbau + Baseline-Diff (Gate). Exit != 0 bei Regression.

.EXAMPLE
    nav nullaudit -Detail CodeFixes
    Roh-Warnungen des Ordners CodeFixes mit Zeilennummern (zum Abarbeiten).

.EXAMPLE
    nav nullaudit -UpdateBaseline
    Aktuellen Stand als neue Baseline einfrieren (nach Review eines Steps).

.FUNCTIONALITY
    nullaudit
#>
function Invoke-NullAudit {
    [CmdletBinding()]
    param(
        [switch] $UpdateBaseline,
        [string] $Detail,
        [switch] $NoBuild,
        [string] $Configuration = 'Debug'
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    $engineDir    = Join-Path $root 'Nav.Language'
    $baselineFile = Join-Path $root 'Build\nullaudit-baseline.txt'
    $rootPrefix   = $root.TrimEnd('\') + '\'

    function ConvertTo-RepoRelative([string] $p) {
        if ($p.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            $p = $p.Substring($rootPrefix.Length)
        }
        return ($p -replace '\\', '/')
    }

    # --- 1. Fortschritt ------------------------------------------------------------------------------

    $srcFiles = Get-ChildItem -Path $engineDir -Recurse -Filter *.cs -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.Name -notlike '*.generated.cs' }

    # Approximativer Null-forgiving-Zähler: Identifier/`)`/`]` gefolgt von `!` und einem Zugriffs-/
    # Abschluss-Token. Schließt `!=` aus (`=` nicht im Lookahead). Heuristik → als ≈ ausgewiesen.
    $suppressRegex = [regex] '[\w\)\]]\!(?=[\.\),;\s])'

    $folderGroups = $srcFiles | Group-Object {
        $rel = $_.FullName.Substring($engineDir.Length).TrimStart('\')
        $i   = $rel.IndexOf('\')
        if ($i -ge 0) { $rel.Substring(0, $i) } else { '(Projektwurzel)' }
    }

    $rows = foreach ($g in $folderGroups | Sort-Object Name) {
        $conv = @($g.Group | Where-Object {
            Select-String -Path $_.FullName -Pattern '#nullable enable' -SimpleMatch -Quiet
        }).Count
        $supp = 0
        foreach ($f in $g.Group) {
            $supp += $suppressRegex.Matches([System.IO.File]::ReadAllText($f.FullName)).Count
        }
        [pscustomobject]@{
            Ordner      = $g.Name
            Konvertiert = $conv
            Gesamt      = $g.Count
            Prozent     = if ($g.Count) { [math]::Round(100.0 * $conv / $g.Count) } else { 0 }
            Suppress    = $supp
        }
    }

    $totConv = ($rows | Measure-Object Konvertiert -Sum).Sum
    $totAll  = ($rows | Measure-Object Gesamt      -Sum).Sum
    $totSupp = ($rows | Measure-Object Suppress    -Sum).Sum

    Write-Host ""
    Write-Host "  Nullable-Fortschritt (Nav.Language)" -ForegroundColor Cyan
    Write-Host ("  {0,-22} {1,5} {2,6} {3,5}  {4,7}" -f 'Ordner', 'konv.', 'gesamt', '%', '~supp.') -ForegroundColor DarkGray
    foreach ($r in $rows) {
        $done  = $r.Konvertiert -eq $r.Gesamt
        $color = if ($done) { 'Green' } elseif ($r.Konvertiert -gt 0) { 'Yellow' } else { 'Gray' }
        Write-Host ("  {0,-22} {1,5} {2,6} {3,4}%  {4,7}" -f `
            $r.Ordner, $r.Konvertiert, $r.Gesamt, $r.Prozent, $r.Suppress) -ForegroundColor $color
    }
    $totPct = if ($totAll) { [math]::Round(100.0 * $totConv / $totAll) } else { 0 }
    Write-Host ("  {0,-22} {1,5} {2,6} {3,4}%  {4,7}" -f `
        'GESAMT', $totConv, $totAll, $totPct, $totSupp) -ForegroundColor White

    if ($NoBuild) { Write-Host ""; return }

    # --- 2. Prüfbau ----------------------------------------------------------------------------------

    $builds = @(
        [pscustomobject]@{ Proj = 'Nav.Language\Nav.Language.csproj';         Nullable = 'warnings'; Scope = 'Nav.Language/' }
        [pscustomobject]@{ Proj = 'Nav.Language.Lsp\Nav.Language.Lsp.csproj'; Nullable = '';         Scope = 'Nav.Language.Lsp/' }
        [pscustomobject]@{ Proj = 'Nav.Language.Mcp\Nav.Language.Mcp.csproj'; Nullable = '';         Scope = 'Nav.Language.Mcp/' }
    )

    # Der MSBuild-File-Logger stellt bei Multiprozess-Builds ein Knoten-Präfix ("  1:7>") voran — der
    # Dateipfad wird darum am Laufwerksbuchstaben verankert, damit das Präfix nicht mitgelesen wird.
    $warnRegex = [regex] '^(?:\s*\d+(?::\d+)?>)?\s*(?<file>[A-Za-z]:\\.+?)\((?<line>\d+),(?<col>\d+)\):\s+warning\s+(?<code>CS8[67]\d\d):\s*(?<msg>.*?)(\s+\[[^\]]*\])?$'
    $logDir    = Join-Path ([System.IO.Path]::GetTempPath()) ("nullaudit_" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    $parsed      = New-Object System.Collections.Generic.List[object]
    $seen        = New-Object System.Collections.Generic.HashSet[string]
    $buildFailed = $false

    try {
        Write-Host ""
        Write-Host "  Prüfbau (--no-incremental)…" -ForegroundColor Cyan
        foreach ($b in $builds) {
            $proj = Join-Path $root $b.Proj
            if (-not (Test-Path $proj)) { throw "Projekt nicht gefunden: '$proj'." }

            $log    = Join-Path $logDir ((Split-Path $b.Proj -Leaf) + '.log')
            $dnArgs = @('build', $proj, '-c', $Configuration, '--no-incremental', '-nologo')
            if ($b.Nullable) { $dnArgs += "-p:Nullable=$($b.Nullable)" }
            $dnArgs += "-flp:warningsonly;logfile=$log"

            Write-Host "    $($b.Proj)$(if ($b.Nullable) { " (-p:Nullable=$($b.Nullable))" })" -ForegroundColor DarkGray
            & dotnet @dnArgs | Out-Null
            if ($LASTEXITCODE) { $buildFailed = $true; Write-Host "      Build meldete Fehler (Exit $LASTEXITCODE) — Ergebnis evtl. unvollständig." -ForegroundColor Red }

            if (Test-Path $log) {
                foreach ($line in [System.IO.File]::ReadAllLines($log)) {
                    $m = $warnRegex.Match($line)
                    if (-not $m.Success) { continue }
                    $file = ConvertTo-RepoRelative $m.Groups['file'].Value
                    if ($file -like '*/obj/*') { continue }
                    if (-not $file.StartsWith($b.Scope, [StringComparison]::OrdinalIgnoreCase)) { continue }
                    $key = "$file|$($m.Groups['line'].Value)|$($m.Groups['col'].Value)|$($m.Groups['code'].Value)"
                    if (-not $seen.Add($key)) { continue }
                    $parsed.Add([pscustomobject]@{
                        File = $file
                        Line = [int] $m.Groups['line'].Value
                        Col  = [int] $m.Groups['col'].Value
                        Code = $m.Groups['code'].Value
                        Msg  = $m.Groups['msg'].Value
                    })
                }
            }
        }
    }
    finally {
        Remove-Item -Path $logDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    # --- Detail-Modus: Roh-Warnungen eines Ordners -------------------------------------------------

    if ($Detail) {
        $prefix = "Nav.Language/$($Detail.Trim('/','\').Replace('\','/'))"
        $hits   = $parsed |
            Where-Object { $_.File -eq "$prefix.cs" -or $_.File.StartsWith("$prefix/", [StringComparison]::OrdinalIgnoreCase) } |
            Sort-Object File, Line, Col
        Write-Host ""
        Write-Host "  Roh-Warnungen unter '$prefix' ($($hits.Count)):" -ForegroundColor Cyan
        if (-not $hits) {
            Write-Host "    (keine)" -ForegroundColor Green
        } else {
            foreach ($h in $hits) {
                Write-Host ("    {0}({1},{2}) {3}: {4}" -f $h.File, $h.Line, $h.Col, $h.Code, $h.Msg) -ForegroundColor Yellow
            }
        }
        Write-Host ""
        return
    }

    # --- 3. Aggregation + Baseline -----------------------------------------------------------------

    $agg = $parsed |
        Group-Object { "$($_.File)`t$($_.Code)" } |
        ForEach-Object {
            $p = $_.Name -split "`t"
            [pscustomobject]@{ File = $p[0]; Code = $p[1]; Count = $_.Count }
        } |
        Sort-Object File, Code

    $currentLines = @($agg | ForEach-Object { "{0}`t{1}`t{2}" -f $_.File, $_.Code, $_.Count })

    if ($UpdateBaseline) {
        $enc = New-Object System.Text.UTF8Encoding($true)   # UTF-8 mit BOM
        [System.IO.File]::WriteAllLines($baselineFile, $currentLines, $enc)
        Write-Host ""
        Write-Host "  Baseline aktualisiert: $($currentLines.Count) Einträge, $($parsed.Count) Warnungen." -ForegroundColor Green
        Write-Host "    $baselineFile" -ForegroundColor DarkGray
        Write-Host ""
        if ($buildFailed) { throw "Achtung: mindestens ein Build meldete Fehler — Baseline evtl. unvollständig." }
        return
    }

    $baseMap = @{}
    if (Test-Path $baselineFile) {
        foreach ($line in [System.IO.File]::ReadAllLines($baselineFile)) {
            if (-not $line.Trim()) { continue }
            $p = $line -split "`t"
            if ($p.Count -ge 3) { $baseMap["$($p[0])`t$($p[1])"] = [int] $p[2] }
        }
    }

    $regressions  = New-Object System.Collections.Generic.List[string]
    $improvements = New-Object System.Collections.Generic.List[string]

    foreach ($a in $agg) {
        $k    = "$($a.File)`t$($a.Code)"
        $base = if ($baseMap.ContainsKey($k)) { $baseMap[$k] } else { 0 }
        if ($a.Count -gt $base) {
            $regressions.Add(("{0}  {1}  {2} → {3}" -f $a.File, $a.Code, $base, $a.Count))
        } elseif ($a.Count -lt $base) {
            $improvements.Add(("{0}  {1}  {2} → {3}" -f $a.File, $a.Code, $base, $a.Count))
        }
    }
    $curKeys = @{}; foreach ($a in $agg) { $curKeys["$($a.File)`t$($a.Code)"] = $true }
    foreach ($k in $baseMap.Keys) {
        if (-not $curKeys.ContainsKey($k)) {
            $p = $k -split "`t"
            $improvements.Add(("{0}  {1}  {2} → 0" -f $p[0], $p[1], $baseMap[$k]))
        }
    }

    Write-Host ""
    Write-Host "  Nullable-Warnungen: $($parsed.Count) gesamt, $($agg.Count) (Datei,Code)-Paare (Baseline: $($baseMap.Count))." -ForegroundColor Cyan

    if ($improvements.Count) {
        Write-Host "  Verbesserungen ($($improvements.Count)) — mit -UpdateBaseline einfrieren:" -ForegroundColor Green
        foreach ($i in ($improvements | Sort-Object)) { Write-Host "    $i" -ForegroundColor DarkGreen }
    }

    if ($regressions.Count) {
        Write-Host "  REGRESSIONEN ($($regressions.Count)):" -ForegroundColor Red
        foreach ($r in ($regressions | Sort-Object)) { Write-Host "    $r" -ForegroundColor Red }
        Write-Host ""
        throw "Nullable-Regression: $($regressions.Count) neue/gestiegene Warnung(en) gegenüber der Baseline."
    }

    if ($buildFailed) { Write-Host ""; throw "Mindestens ein Build meldete Fehler — Audit unvollständig." }

    Write-Host "  Keine Regression gegenüber der Baseline." -ForegroundColor Green
    Write-Host ""
}
