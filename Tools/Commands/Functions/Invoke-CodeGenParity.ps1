<#
.SYNOPSIS
    Beweist die Identität des Nav-Codegens, indem zwei Generatoren dieselben `.nav` übersetzen und
    ihre Ausgaben verglichen werden (Referenz vs. Kandidat).

.DESCRIPTION
    Verifikationswerkzeug für die Codegen-Versionierung (Step 4, ST→CodeBuilder-Migration): nach
    jedem Sub-Step muss der migrierte Generator dasselbe C# erzeugen wie der Referenz-Generator.
    Neben den Regression-Snapshots (`nav snapshot`) ist der Bestandskorpus (~1900 `.nav`) das breite
    Netz.

    Vorgehen — **run-both-generators**, schlank und schnell (wenige Minuten):

      1. Nur die `.nav` des Korpus in ZWEI leere Scratch-Bäume spiegeln (Robocopy, `*.nav`) — nicht
         das umgebende Enlistment. Wenige MB statt zig GB.
      2. Referenz-`nav.exe -d <ref> -g All` über den einen, Kandidat-`nav.exe -d <cand> -g All` über
         den anderen Baum laufen lassen; Wall-Zeit je Lauf messen.
      3. Alle erzeugten `.cs` einsammeln und vergleichen.

    Weil beide Läufe von einem **identischen leeren Baum** starten, erzeugen sie die
    `OverwritePolicy.Never`-Dateien (`{Task}WFS.cs`, TO-Stubs) mit demselben (unveränderten) Code —
    byte-gleich, kein Falsch-Diff. Deshalb braucht es hier weder eine eingecheckte Korpus-Baseline
    (die die vom GUI-Generator gefüllten TOs trägt) noch das Mitkopieren des ganzen Enlistments.

    **Zwei Vergleichsebenen:** Der CodeBuilder ist clean-by-default und reproduziert kosmetische
    StringTemplate-Whitespace-Artefakte (Trailing-Whitespace, eingerückte Leerzeilen) bewusst nicht;
    zudem reindentiert er mehrzeilige eingebettete Werte (z.B. über mehrere Zeilen deklarierte
    Typen aus der `.nav`) nach eigener Struktur-Einrückung statt nach StringTemplates
    Interpolations-Stack — beides ist reiner Whitespace ohne C#-Bedeutung.

      * **Normalisiert** (je Zeile beidseitig getrimmt, Zeilenenden auf `\n` vereinheitlicht) ist das
        maßgebliche Urteil: 0 = der erzeugte Code ist Zeile für Zeile inhaltsgleich (Token, Reihenfolge,
        Leerzeilen), nur die Einrückung darf abweichen. Zusätzlich dürfen keine `.cs` hinzukommen oder
        verschwinden.
      * **Roh** (Byte-für-Byte) ist die Audit-Ebene: nach der Migration einer Familie darf roh > 0
        sein — aber ausschließlich durch Whitespace. `CosmeticOnly` ist genau diese zu sichtende Menge.

    Der Referenz-Generator ist standardmäßig die deployte `nav.exe` aus dem Enlistment (die die
    Produktions-`.cs` erzeugt). Der Kandidat wird standardmäßig frisch aus dem Arbeitsbaum gebaut
    (`dotnet build Nav.Cli`), damit wirklich der aktuelle Engine-Stand geprüft wird; mit
    `-CandidateExe` lässt sich eine vorhandene exe vorgeben.

    Korpus- und Referenzpfad sind maschinen-lokal (proprietäres TFS-Enlistment, nicht im Repo). Auf
    anderen Rechnern per `-CorpusPath` / `-ReferenceExe` überschreiben.

.PARAMETER CorpusPath
    Wurzel des Bestandskorpus (rekursiv nach `.nav`). Default: D:\tfs\Main\XTplusApplication\src.

.PARAMETER ReferenceExe
    Referenz-`nav.exe` (die „alte" Wahrheit). Default: D:\tfs\Main\build\Script\Nav\nav.exe.

.PARAMETER CandidateExe
    Optional: bereits gebaute Kandidat-nav.exe. Ohne Angabe wird Nav.Cli frisch gebaut.

.PARAMETER Configuration
    Build-Konfiguration für den Kandidat-Build. Default: Debug.

.PARAMETER MaxReport
    Höchstzahl einzeln aufgelisteter Abweichungen. Default: 25.

.PARAMETER Keep
    Scratch-Bäume nach dem Lauf nicht löschen (zur Nachanalyse der Diffs).

.FUNCTIONALITY
    parity
#>
function Invoke-CodeGenParity {
    [CmdletBinding()]
    param(
        [string] $CorpusPath    = 'D:\tfs\Main\XTplusApplication\src',
        [string] $ReferenceExe  = 'D:\tfs\Main\build\Script\Nav\nav.exe',
        [string] $CandidateExe,
        [string] $Configuration = 'Debug',
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
    if (-not (Test-Path -LiteralPath $ReferenceExe)) {
        throw "Referenz-nav.exe nicht gefunden: '$ReferenceExe'. Per -ReferenceExe angeben."
    }
    $ReferenceExe = (Resolve-Path -LiteralPath $ReferenceExe).Path

    # --- Kandidat-nav.exe bestimmen/bauen ------------------------------------------------------
    if ($CandidateExe) {
        if (-not (Test-Path -LiteralPath $CandidateExe)) { throw "Kandidat-nav.exe nicht gefunden: '$CandidateExe'." }
        $candExe = (Resolve-Path -LiteralPath $CandidateExe).Path
        Write-Host "Kandidat (vorgegeben): $candExe" -ForegroundColor Cyan
    }
    else {
        $project = Join-Path $root 'Nav.Cli\Nav.Cli.csproj'
        if (-not (Test-Path $project)) { throw "CLI-Projekt nicht gefunden: '$project'." }
        Write-Host "Baue Kandidat-nav.exe ($Configuration) aus dem Arbeitsbaum ..." -ForegroundColor Cyan
        & dotnet build $project -c $Configuration -v:m
        if ($LASTEXITCODE) { throw "Nav.Cli-Build fehlgeschlagen (Exit $LASTEXITCODE)." }
        # AppendTargetFrameworkToOutputPath=false → flacher Ausgabepfad, AssemblyName 'nav'.
        $candExe = Join-Path $root "Nav.Cli\bin\$Configuration\nav.exe"
        if (-not (Test-Path $candExe)) { throw "Gebaute Kandidat-nav.exe nicht gefunden: '$candExe'." }
        Write-Host "Kandidat (gebaut): $candExe" -ForegroundColor Cyan
    }
    Write-Host "Referenz: $ReferenceExe" -ForegroundColor Cyan

    # --- Scratch: zwei nav-only-Bäume ----------------------------------------------------------
    $stamp   = Get-Date -Format 'yyyyMMdd-HHmmss'
    $scratch = Join-Path ([IO.Path]::GetTempPath()) "nav-parity\$stamp"
    $refTree = Join-Path $scratch 'ref'
    $candTree = Join-Path $scratch 'cand'
    New-Item -ItemType Directory -Path $refTree, $candTree -Force | Out-Null

    Write-Host "Spiegele nur .nav aus $CorpusPath ..." -ForegroundColor Cyan
    # /XD schließt Build-/VCS-/IDE-Verzeichnisse aus (keine .nav-Quellen; lock-anfällig, blähen auf);
    # /XJ meidet Reparse-Point-/Junction-Schleifen. Nur *.nav wird kopiert.
    & robocopy $CorpusPath $refTree *.nav /S /XD bin obj .git .vs /XJ /NFL /NDL /NP /NJH /NJS /R:1 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Robocopy (Korpus→ref) fehlgeschlagen (Exit $LASTEXITCODE)." }
    $navCount = (Get-ChildItem -LiteralPath $refTree -Recurse -Filter '*.nav' -File).Count
    if ($navCount -eq 0) { throw "Im Korpus '$CorpusPath' wurden keine .nav-Dateien gefunden." }
    Write-Host "Korpus: $navCount .nav-Dateien; klone nach 'cand' ..." -ForegroundColor Cyan
    & robocopy $refTree $candTree /MIR /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Robocopy (ref→cand) fehlgeschlagen (Exit $LASTEXITCODE)." }

    try {
        # --- Beide Generatoren laufen lassen ---------------------------------------------------
        $refRun  = Invoke-NavGen -Label 'Referenz' -Exe $ReferenceExe -Dir $refTree
        $candRun = Invoke-NavGen -Label 'Kandidat' -Exe $candExe      -Dir $candTree

        # --- Ausgaben einsammeln + vergleichen -------------------------------------------------
        Write-Host "Sammle erzeugte .cs ein und vergleiche ..." -ForegroundColor Cyan
        $ref  = Get-CsHashMap -Base $refTree
        $cand = Get-CsHashMap -Base $candTree

        $rawChanged  = [Collections.Generic.List[string]]::new()
        $normChanged = [Collections.Generic.List[string]]::new()
        $added       = [Collections.Generic.List[string]]::new()
        $removed     = [Collections.Generic.List[string]]::new()

        foreach ($rel in $cand.Keys) {
            if (-not $ref.ContainsKey($rel)) { $added.Add($rel); continue }
            if ($ref[$rel].Raw  -ne $cand[$rel].Raw)  { $rawChanged.Add($rel) }
            if ($ref[$rel].Norm -ne $cand[$rel].Norm) { $normChanged.Add($rel) }
        }
        foreach ($rel in $ref.Keys) {
            if (-not $cand.ContainsKey($rel)) { $removed.Add($rel) }
        }

        # Nur-kosmetisch = roh geändert, aber normalisiert gleich.
        $cosmeticOnly = @($rawChanged | Where-Object { $normChanged -notcontains $_ })
        # Urteil: normalisiert identisch UND keine .cs kommt hinzu/fehlt. Roh-Diffs sind erlaubt,
        # solange sie kosmetisch (= nicht normalisiert) sind.
        $normDivergent = $normChanged.Count + $added.Count + $removed.Count
        $parityOk      = ($normDivergent -eq 0)

        Write-Host ""
        Write-Host "=== Parity-Ergebnis (run-both-generators) ===" -ForegroundColor Yellow
        Write-Host ("  .cs Referenz / Kandidat : {0} / {1}" -f $ref.Count, $cand.Count)
        Write-Host ("  geändert (roh)          : {0}" -f $rawChanged.Count)
        Write-Host ("  geändert (normalisiert) : {0}" -f $normChanged.Count)
        Write-Host ("  davon nur kosmetisch    : {0}" -f $cosmeticOnly.Count)
        Write-Host ("  neu / entfernt          : {0} / {1}" -f $added.Count, $removed.Count)
        Write-Host ("  Codegen-Sek. Ref/Kand   : {0:n2} / {1:n2}  (Exit {2}/{3})" -f `
                $refRun.Secs, $candRun.Secs, $refRun.Exit, $candRun.Exit)

        if ($parityOk) {
            if ($cosmeticOnly.Count -gt 0) {
                Write-Host ("PARITY OK (normalisiert) — {0} Datei(en) nur kosmetisch (Whitespace); Roh-Audit empfohlen." -f $cosmeticOnly.Count) `
                    -ForegroundColor Green
            }
            else {
                Write-Host "PARITY OK — Kandidat byte-identisch zur Referenz." -ForegroundColor Green
            }
        }
        else {
            Write-Host "PARITY VERLETZT — $normDivergent abweichende .cs (siehe unten)." -ForegroundColor Red
            $show = @()
            $show += $normChanged | ForEach-Object { "  [geändert] $_" }
            $show += $added       | ForEach-Object { "  [neu]      $_" }
            $show += $removed     | ForEach-Object { "  [entfernt] $_" }
            $show | Select-Object -First $MaxReport | ForEach-Object { Write-Host $_ }
            if ($show.Count -gt $MaxReport) {
                Write-Host ("  ... und {0} weitere." -f ($show.Count - $MaxReport))
            }
        }

        [pscustomobject]@{
            Corpus        = $CorpusPath
            NavFiles      = $navCount
            CsReference   = $ref.Count
            CsCandidate   = $cand.Count
            RawChanged    = $rawChanged.Count
            NormChanged   = $normChanged.Count
            CosmeticOnly  = $cosmeticOnly.Count
            CosmeticFiles = $cosmeticOnly
            Added         = $added.Count
            Removed       = $removed.Count
            RefSecs       = [math]::Round($refRun.Secs, 2)
            CandSecs      = [math]::Round($candRun.Secs, 2)
            RefExit       = $refRun.Exit
            CandExit      = $candRun.Exit
            ParityOk      = $parityOk
            Scratch       = if ($Keep) { $scratch } else { $null }
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

# Lässt einen Generator über einen nav-only-Baum laufen und misst die Wall-Zeit. Interner Helfer.
function Invoke-NavGen {
    param(
        [Parameter(Mandatory)][string] $Label,
        [Parameter(Mandatory)][string] $Exe,
        [Parameter(Mandatory)][string] $Dir
    )
    Write-Host "$Label : $Exe -d <$([IO.Path]::GetFileName($Dir))> -g All" -ForegroundColor Cyan
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $Exe -d $Dir -g All | Out-Null
    $exit = $LASTEXITCODE
    $sw.Stop()
    Write-Host ("  $Label : {0:n2} s (Exit {1})" -f $sw.Elapsed.TotalSeconds, $exit) -ForegroundColor Cyan
    return [pscustomobject]@{ Secs = $sw.Elapsed.TotalSeconds; Exit = $exit }
}

# Erfasst alle *.cs unter $Base als Map (relativer Pfad → { Raw; Norm }). Raw = SHA256 der Rohbytes;
# Norm = SHA256 des whitespace-normalisierten Texts (je Zeile BEIDSEITIG getrimmt, Zeilenenden auf \n
# vereinheitlicht) — neutralisiert genau die kosmetischen Unterschiede, die der CodeBuilder
# clean-by-default gegenüber StringTemplate hat: Trailing-Whitespace, eingerückte Leerzeilen UND die
# abweichende Einrückung von Fortsetzungszeilen mehrzeiliger eingebetteter Werte. Interner Helfer.
function Get-CsHashMap {
    param([Parameter(Mandatory)][string] $Base)

    $map      = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    $baseFull = (Resolve-Path -LiteralPath $Base).Path.TrimEnd('\') + '\'
    $sha      = [Security.Cryptography.SHA256]::Create()
    try {
        # Bewusst .NET-nah statt Pipeline: der Korpus erzeugt zehntausende .cs, und eine
        # `$lines | ForEach-Object { TrimEnd }`-Pipeline kostete pro Zeile eine Skriptblock-
        # Invocation → Minuten pro Lauf. Ergebnis (die gehashten Roh-/Normalisiert-Bytes) ist
        # identisch: TrimEnd je Zeile, Zeilenenden auf `n vereinheitlicht.
        foreach ($item in (Get-ChildItem -LiteralPath $Base -Recurse -Filter '*.cs' -File)) {
            $rel   = $item.FullName.Substring($baseFull.Length)
            $bytes = [IO.File]::ReadAllBytes($item.FullName)
            $raw   = [BitConverter]::ToString($sha.ComputeHash($bytes))

            $text  = [IO.File]::ReadAllText($item.FullName)
            $parts = $text -split "\r\n|\n|\r"
            for ($i = 0; $i -lt $parts.Length; $i++) { $parts[$i] = $parts[$i].Trim() }
            $norm  = [string]::Join("`n", $parts)
            $normH = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($norm)))

            $map[$rel] = [pscustomobject]@{ Raw = $raw; Norm = $normH }
        }
    }
    finally {
        $sha.Dispose()
    }
    return $map
}
