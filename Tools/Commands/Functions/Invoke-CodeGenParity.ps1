<#
.SYNOPSIS
    Beweist Byte-Identität des Nav-Codegens gegen den Bestandskorpus („Korpus-Diff leer").

.DESCRIPTION
    Verifikationswerkzeug für die Codegen-Versionierung (Step 4, ST→CodeBuilder-Migration): nach
    jedem Sub-Step muss der V1-Generator dasselbe C# erzeugen wie zuvor. Neben den
    Regression-Snapshots (`nav snapshot`) ist der Bestandskorpus (~1912 `.nav`) das breite Netz.

    Vorgehen — **single-exe**, kein Baseline-Binary nötig: der Korpus trägt die generierten `.cs`
    bereits eingecheckt, und diese sind byte-identisch zum bisherigen (StringTemplate-)Generator
    (siehe `doc/nav-kolibri.md`). Der eingecheckte Stand ist also die Referenz. Ablauf:

      1. Korpus in einen Scratch-Ordner spiegeln (Robocopy).
      2. Ist-Stand aller `.cs` festhalten (= eingecheckte Baseline == ST-Referenz).
      3. Kandidat-`nav.exe -d <scratch> -g All` darüber laufen lassen (überschreibt die `.cs` in-place);
         Wall-Zeit messen.
      4. Erneut erfassen und vergleichen. Jede geänderte, neu erzeugte oder verschwundene `.cs` ist
         eine Abweichung vom eingecheckten Generat.

    **Zwei Vergleichsebenen** (entscheidend ab der CodeBuilder-Migration): Der CodeBuilder ist
    clean-by-default und reproduziert die kosmetischen StringTemplate-Whitespace-Artefakte bewusst
    NICHT (Trailing-Whitespace, eingerückte Leerzeilen). Deshalb wird sowohl **roh** (Byte-für-Byte)
    als auch **normalisiert** (je Zeile rechts getrimmt, Zeilenenden vereinheitlicht) verglichen:

      * **Normalisiert** ist das maßgebliche Parity-Urteil (Default): 0 = der erzeugte Code ist
        semantisch/sichtbar identisch, erlaubte kosmetische Whitespace-Unterschiede herausgerechnet.
      * **Roh** dient dem Audit: solange eine Familie noch auf StringTemplate läuft, muss auch roh 0
        gelten; nach ihrer Migration darf roh > 0 sein, aber ausschließlich durch Whitespace — die
        Roh-Abweichungsliste ist genau die Menge, die manuell zu sichten ist.

    Mit `-Raw` wird strikte Byte-Identität gefordert (Roh = Urteil) — für den Vorab-Check vor einer
    Migration bzw. für noch nicht migrierte Familien.

    Die Kandidat-`nav.exe` wird standardmäßig frisch aus dem Arbeitsbaum gebaut (`dotnet build
    Nav.Cli`), damit wirklich der aktuelle Engine-Stand geprüft wird. Mit `-ExePath` lässt sich eine
    vorhandene exe verwenden (z.B. zum Vergleich gegen ein Alt-Binary).

    Der Korpuspfad ist maschinen-lokal (proprietäres TFS-Enlistment, nicht im Repo). Default ist
    `D:\tfs\Main`; auf anderen Rechnern per `-CorpusPath` überschreiben.

.PARAMETER CorpusPath
    Wurzel des Bestandskorpus (rekursiv nach `.nav` durchsucht). Default: D:\tfs\Main.

.PARAMETER ExePath
    Optional: bereits gebaute nav.exe. Ohne Angabe wird Nav.Cli frisch gebaut (aktueller Arbeitsbaum).

.PARAMETER Configuration
    Build-Konfiguration für den Kandidat-Build. Default: Debug.

.PARAMETER Raw
    Strikte Byte-Identität fordern (Roh-Diff ist das Urteil). Ohne den Schalter urteilt der
    normalisierte Diff, und Roh-Abweichungen werden nur als audit-pflichtiger Hinweis gemeldet.

.PARAMETER MaxReport
    Höchstzahl einzeln aufgelisteter Abweichungen. Default: 25.

.PARAMETER Keep
    Scratch-Ordner nach dem Lauf nicht löschen (zur Nachanalyse der Diffs).

.FUNCTIONALITY
    parity
#>
function Invoke-CodeGenParity {
    [CmdletBinding()]
    param(
        [string] $CorpusPath    = 'D:\tfs\Main',
        [string] $ExePath,
        [string] $Configuration = 'Debug',
        [switch] $Raw,
        [int]    $MaxReport     = 25,
        [switch] $Keep
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    # --- Korpus prüfen -------------------------------------------------------------------------
    if (-not (Test-Path -LiteralPath $CorpusPath)) {
        throw "Korpuspfad nicht gefunden: '$CorpusPath'. Per -CorpusPath den lokalen Pfad angeben."
    }
    $navCount = (Get-ChildItem -LiteralPath $CorpusPath -Recurse -Filter '*.nav' -File -ErrorAction SilentlyContinue |
                 Measure-Object).Count
    if ($navCount -eq 0) {
        throw "Im Korpus '$CorpusPath' wurden keine .nav-Dateien gefunden."
    }
    Write-Host "Korpus: $CorpusPath ($navCount .nav-Dateien)" -ForegroundColor Cyan

    # --- Kandidat-nav.exe bestimmen/bauen ------------------------------------------------------
    if ($ExePath) {
        if (-not (Test-Path -LiteralPath $ExePath)) { throw "nav.exe nicht gefunden: '$ExePath'." }
        $exe = (Resolve-Path -LiteralPath $ExePath).Path
        Write-Host "nav.exe (vorgegeben): $exe" -ForegroundColor Cyan
    }
    else {
        $project = Join-Path $root 'Nav.Cli\Nav.Cli.csproj'
        if (-not (Test-Path $project)) { throw "CLI-Projekt nicht gefunden: '$project'." }
        Write-Host "Baue Kandidat-nav.exe ($Configuration) aus dem Arbeitsbaum ..." -ForegroundColor Cyan
        & dotnet build $project -c $Configuration -v:m
        if ($LASTEXITCODE) { throw "Nav.Cli-Build fehlgeschlagen (Exit $LASTEXITCODE)." }
        # AppendTargetFrameworkToOutputPath=false → flacher Ausgabepfad, AssemblyName 'nav'.
        $exe = Join-Path $root "Nav.Cli\bin\$Configuration\nav.exe"
        if (-not (Test-Path $exe)) { throw "Gebaute nav.exe nicht gefunden: '$exe'." }
        Write-Host "nav.exe (gebaut): $exe" -ForegroundColor Cyan
    }

    # --- Scratch-Kopie -------------------------------------------------------------------------
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ("nav-parity\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $scratch -Force | Out-Null
    Write-Host "Spiegele Korpus nach: $scratch" -ForegroundColor Cyan
    # Robocopy: /MIR spiegelt, /NFL /NDL /NP /NJH /NJS = ruhig. Exit-Codes < 8 sind Erfolg.
    & robocopy $CorpusPath $scratch /MIR /NFL /NDL /NP /NJH /NJS /R:1 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Robocopy fehlgeschlagen (Exit $LASTEXITCODE)." }

    try {
        # --- Baseline-Manifest (eingecheckte .cs == ST-Referenz) -------------------------------
        $before = Get-CsHashMap -Base $scratch
        Write-Host "Baseline: $($before.Count) .cs-Dateien erfasst." -ForegroundColor Cyan

        # --- Kandidat-Codegen ------------------------------------------------------------------
        Write-Host "Führe Codegen aus: nav.exe -d <scratch> -g All ..." -ForegroundColor Cyan
        $sw = [Diagnostics.Stopwatch]::StartNew()
        & $exe -d $scratch -g All
        $genExit = $LASTEXITCODE
        $sw.Stop()
        Write-Host ("Codegen-Wall-Zeit: {0:n2} s (Exit {1})" -f $sw.Elapsed.TotalSeconds, $genExit) `
            -ForegroundColor Cyan

        # --- Nachher-Manifest + Vergleich ------------------------------------------------------
        $after = Get-CsHashMap -Base $scratch

        $rawChanged  = [Collections.Generic.List[string]]::new()
        $normChanged = [Collections.Generic.List[string]]::new()
        $added       = [Collections.Generic.List[string]]::new()
        $removed     = [Collections.Generic.List[string]]::new()

        foreach ($rel in $after.Keys) {
            if (-not $before.ContainsKey($rel)) {
                $added.Add($rel)
                continue
            }
            $b = $before[$rel]
            $a = $after[$rel]
            if ($b.Raw  -ne $a.Raw)  { $rawChanged.Add($rel) }
            if ($b.Norm -ne $a.Norm) { $normChanged.Add($rel) }
        }
        foreach ($rel in $before.Keys) {
            if (-not $after.ContainsKey($rel)) { $removed.Add($rel) }
        }

        # Nur-kosmetisch = roh geändert, aber normalisiert gleich.
        $cosmeticOnly = @($rawChanged | Where-Object { $normChanged -notcontains $_ })
        $normDivergent = $normChanged.Count + $added.Count + $removed.Count
        $rawDivergent  = $rawChanged.Count  + $added.Count + $removed.Count
        $verdictDivergent = if ($Raw) { $rawDivergent } else { $normDivergent }
        $parityOk = ($verdictDivergent -eq 0 -and $genExit -eq 0)

        $mode = if ($Raw) { 'roh/strikt' } else { 'normalisiert' }
        Write-Host ""
        Write-Host "=== Parity-Ergebnis ($mode) ===" -ForegroundColor Yellow
        Write-Host ("  .cs vorher            : {0}" -f $before.Count)
        Write-Host ("  .cs nachher           : {0}" -f $after.Count)
        Write-Host ("  geändert (roh)        : {0}" -f $rawChanged.Count)
        Write-Host ("  geändert (normalisiert): {0}" -f $normChanged.Count)
        Write-Host ("  davon nur kosmetisch  : {0}" -f $cosmeticOnly.Count)
        Write-Host ("  neu                   : {0}" -f $added.Count)
        Write-Host ("  entfernt              : {0}" -f $removed.Count)

        if ($parityOk) {
            if (-not $Raw -and $cosmeticOnly.Count -gt 0) {
                Write-Host ("PARITY OK (normalisiert) — {0} Datei(en) nur kosmetisch (Whitespace) abweichend; Roh-Audit empfohlen." -f $cosmeticOnly.Count) `
                    -ForegroundColor Green
            }
            else {
                Write-Host "PARITY OK — Codegen byte-identisch zum eingecheckten Korpus-Generat." `
                    -ForegroundColor Green
            }
        }
        else {
            Write-Host "PARITY VERLETZT — $verdictDivergent abweichende .cs (siehe unten)." -ForegroundColor Red
            $listChanged = if ($Raw) { $rawChanged } else { $normChanged }
            $show = @()
            $show += $listChanged | ForEach-Object { "  [geändert] $_" }
            $show += $added       | ForEach-Object { "  [neu]      $_" }
            $show += $removed     | ForEach-Object { "  [entfernt] $_" }
            $show | Select-Object -First $MaxReport | ForEach-Object { Write-Host $_ }
            if ($show.Count -gt $MaxReport) {
                Write-Host ("  ... und {0} weitere." -f ($show.Count - $MaxReport))
            }
        }

        [pscustomobject]@{
            Corpus         = $CorpusPath
            NavFiles       = $navCount
            CsBefore       = $before.Count
            CsAfter        = $after.Count
            RawChanged     = $rawChanged.Count
            NormChanged    = $normChanged.Count
            CosmeticOnly   = $cosmeticOnly.Count
            Added          = $added.Count
            Removed        = $removed.Count
            NormDivergent  = $normDivergent
            RawDivergent   = $rawDivergent
            GenExitCode    = $genExit
            CodegenSecs    = [math]::Round($sw.Elapsed.TotalSeconds, 2)
            ParityOk       = $parityOk
            Scratch        = if ($Keep) { $scratch } else { $null }
        }
    }
    finally {
        if ($Keep) {
            Write-Host "Scratch behalten: $scratch" -ForegroundColor DarkGray
        }
        else {
            Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# Erfasst alle *.cs unter $Base als Map (relativer Pfad → { Raw; Norm }). Raw = SHA256 der Rohbytes;
# Norm = SHA256 des whitespace-normalisierten Texts (je Zeile rechts getrimmt, Zeilenenden auf \n
# vereinheitlicht) — neutralisiert genau die kosmetischen Unterschiede, die der CodeBuilder
# clean-by-default gegenüber StringTemplate hat. Interner Helfer.
function Get-CsHashMap {
    param([Parameter(Mandatory)][string] $Base)

    $map      = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    $baseFull = (Resolve-Path -LiteralPath $Base).Path.TrimEnd('\') + '\'
    $sha      = [Security.Cryptography.SHA256]::Create()
    try {
        Get-ChildItem -LiteralPath $Base -Recurse -Filter '*.cs' -File | ForEach-Object {
            $rel   = $_.FullName.Substring($baseFull.Length)
            $bytes = [IO.File]::ReadAllBytes($_.FullName)
            $raw   = [BitConverter]::ToString($sha.ComputeHash($bytes))

            $text  = [IO.File]::ReadAllText($_.FullName)
            $lines = $text -split "\r\n|\n|\r"
            $norm  = ($lines | ForEach-Object { $_.TrimEnd() }) -join "`n"
            $normH = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($norm)))

            $map[$rel] = [pscustomobject]@{ Raw = $raw; Norm = $normH }
        }
    }
    finally {
        $sha.Dispose()
    }
    return $map
}
