<#
.SYNOPSIS
    Spiegelt die Build Tools und den MCP-Server flach in ein TFS-gemapptes Zielverzeichnis und
    registriert die Änderungen als Pending-Changes (tf checkout/add/delete).

.DESCRIPTION
    Kopiert den Inhalt von `deploy\Build Tools` UND `deploy\mcp` als flachen Merge (beide Ordner-
    Inhalte direkt nebeneinander) in ein Zielverzeichnis, das einem TFS-Workspace zugeordnet ist.
    Weil die Zieldateien TFS-versioniert (und damit schreibgeschützt) sind, wird jede Änderung
    korrekt als Pending-Change angemeldet:

      * Datei in Quelle UND Ziel, Inhalt verschieden → `tf checkout` (hebt Read-only auf), dann kopieren.
      * Datei in Quelle, aber nicht im Ziel          → kopieren, dann `tf add`.
      * Datei im Ziel, aber nicht mehr in der Quelle → `tf delete` (entfernt Pending + lokale Datei).
      * Inhalt identisch                             → übersprungen (kein No-op-Checkout).

    Der eigentliche Check-in bleibt manuell — dieses Command erzeugt nur die Pending-Changes.

    Das zuletzt verwendete Ziel wird in `%LOCALAPPDATA%\nav\last-tfs-deploy-target.txt` gemerkt und
    beim nächsten Aufruf ohne -Target wiederverwendet. Ist noch keins gemerkt, gilt der Default
    `D:\tfs\z_nav-mcp\build\Script\Nav`.

.PARAMETER Target
    Ziel-Workspace-Verzeichnis. Ohne Angabe: zuletzt gemerktes Ziel, sonst der Default.

.PARAMETER Configuration
    Build-/Publish-Konfiguration. Default: Debug.

.PARAMETER SkipPublish
    Überspringt den vorgeschalteten `nav publish`-Lauf und kopiert den vorhandenen `deploy\`-Stand.

.PARAMETER DryRun
    Zeigt nur die geplanten add/edit/delete-Änderungen, ohne TFS oder Dateien anzufassen (impliziert
    SkipPublish — es wird gegen den vorhandenen `deploy\`-Stand verglichen).

.FUNCTIONALITY
    tfsdeploy
#>
function Invoke-TfsDeploy {
    [CmdletBinding()]
    param(
        [string] $Target,
        [string] $Configuration = 'Debug',
        [switch] $SkipPublish,
        [switch] $DryRun
    )

    $ErrorActionPreference = 'Stop'

    $root = Resolve-Root
    if (-not $root) { return }

    # --- Ziel bestimmen (Parameter → gemerkt → Default) ---------------------------------------
    $settingsDir   = Join-Path $env:LOCALAPPDATA 'nav'
    $settingsFile  = Join-Path $settingsDir 'last-tfs-deploy-target.txt'
    $defaultTarget = 'D:\tfs\z_nav-mcp\build\Script\Nav'

    if (-not $Target) {
        if (Test-Path $settingsFile) {
            $Target = (Get-Content -Path $settingsFile -Raw).Trim()
        }
        if (-not $Target) { $Target = $defaultTarget }
    }

    Write-Host "TFS-Ziel: $Target" -ForegroundColor Cyan

    # --- tf.exe auflösen ----------------------------------------------------------------------
    $tf = Resolve-Tf
    if (-not $tf) { return }

    # Kleiner Wrapper: tf-Aufruf mit Exit-Code-Prüfung (nested → sieht $tf aus dem Elternscope).
    function Invoke-Tf {
        param([Parameter(Mandatory = $true)][string[]] $Arguments)
        $output = & $tf @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            $output | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkYellow }
            throw "tf $($Arguments -join ' ') fehlgeschlagen (Exit $LASTEXITCODE)."
        }
        return $output
    }

    # --- Ziel prüfen: existiert + einem Workspace zugeordnet ----------------------------------
    if (-not (Test-Path $Target)) {
        throw "Zielverzeichnis existiert nicht: '$Target'. Erst im TFS-Workspace anlegen/mappen."
    }
    $wf = & $tf @('workfold', $Target) 2>&1
    if ($LASTEXITCODE -ne 0) {
        $wf | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkYellow }
        throw "'$Target' ist keinem TFS-Workspace zugeordnet (tf workfold fehlgeschlagen)."
    }

    # --- Ziel merken --------------------------------------------------------------------------
    if (-not (Test-Path $settingsDir)) { New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null }
    Set-Content -Path $settingsFile -Value $Target -Encoding utf8BOM -NoNewline

    # --- Publish (sofern nicht übersprungen) --------------------------------------------------
    $doPublish = -not $SkipPublish -and -not $DryRun
    if ($doPublish) {
        Invoke-Publish -Configuration $Configuration
    }

    # --- Quell-Set (flacher Merge beider Ordner) ----------------------------------------------
    $sourceDirs = @(
        (Join-Path $root 'deploy\Build Tools'),
        (Join-Path $root 'deploy\mcp')
    )
    $sourceFiles = @{}   # Dateiname (klein) → voller Quellpfad
    foreach ($dir in $sourceDirs) {
        if (-not (Test-Path $dir)) {
            throw "Quelle nicht gefunden: '$dir'. Erst 'nav publish' laufen lassen (oder -SkipPublish weglassen)."
        }
        $subdirs = @(Get-ChildItem -Path $dir -Directory)
        if ($subdirs.Count -gt 0) {
            throw "Quelle '$dir' enthält Unterordner ($($subdirs.Name -join ', ')) — der flache Merge unterstützt nur Dateien direkt im Ordner."
        }
        foreach ($file in Get-ChildItem -Path $dir -File) {
            $key = $file.Name.ToLowerInvariant()
            if ($sourceFiles.ContainsKey($key)) {
                throw "Namenskollision im flachen Merge: '$($file.Name)' kommt in mehreren Quellordnern vor."
            }
            $sourceFiles[$key] = $file.FullName
        }
    }

    # --- Ziel-Ist (nur Top-Level-Dateien) -----------------------------------------------------
    $targetFiles = @{}   # Dateiname (klein) → voller Zielpfad
    foreach ($file in Get-ChildItem -Path $Target -File) {
        $targetFiles[$file.Name.ToLowerInvariant()] = $file.FullName
    }

    # --- Diff berechnen -----------------------------------------------------------------------
    $toAdd = @(); $toEdit = @(); $toDelete = @(); $unchanged = 0
    foreach ($key in $sourceFiles.Keys) {
        $src = $sourceFiles[$key]
        if ($targetFiles.ContainsKey($key)) {
            $dst = $targetFiles[$key]
            if ((Get-FileHash -Path $src).Hash -eq (Get-FileHash -Path $dst).Hash) {
                $unchanged++
            }
            else {
                $toEdit += [pscustomobject]@{ Source = $src; Target = $dst }
            }
        }
        else {
            $toAdd += [pscustomobject]@{ Source = $src; Target = (Join-Path $Target (Split-Path $src -Leaf)) }
        }
    }
    foreach ($key in $targetFiles.Keys) {
        if (-not $sourceFiles.ContainsKey($key)) { $toDelete += $targetFiles[$key] }
    }

    # --- Bericht ------------------------------------------------------------------------------
    Write-Host ""
    Write-Host "Geplante Änderungen:" -ForegroundColor Cyan
    foreach ($e in $toEdit)   { Write-Host "  [edit]   $($e.Target)"  -ForegroundColor Yellow }
    foreach ($a in $toAdd)    { Write-Host "  [add]    $($a.Target)"  -ForegroundColor Green }
    foreach ($d in $toDelete) { Write-Host "  [delete] $d"            -ForegroundColor Red }
    Write-Host ("  ({0} unverändert)" -f $unchanged) -ForegroundColor DarkGray

    if ($toEdit.Count -eq 0 -and $toAdd.Count -eq 0 -and $toDelete.Count -eq 0) {
        Write-Host ""
        Write-Host "Nichts zu tun — Ziel ist bereits aktuell." -ForegroundColor Green
        return
    }

    if ($DryRun) {
        Write-Host ""
        Write-Host "DryRun — keine Änderungen vorgenommen." -ForegroundColor DarkGray
        return
    }

    # --- Ausführen ----------------------------------------------------------------------------
    # Edits: erst auschecken (macht beschreibbar), dann überschreiben.
    foreach ($e in $toEdit) {
        Invoke-Tf @('checkout', '/noprompt', $e.Target) | Out-Null
        Copy-Item -Path $e.Source -Destination $e.Target -Force
    }
    # Adds: erst kopieren, dann zur Versionsverwaltung hinzufügen.
    foreach ($a in $toAdd) {
        Copy-Item -Path $a.Source -Destination $a.Target -Force
        Invoke-Tf @('add', '/noprompt', $a.Target) | Out-Null
    }
    # Deletes: tf delete entfernt Pending-Change und lokale Datei zugleich.
    foreach ($d in $toDelete) {
        Invoke-Tf @('delete', $d) | Out-Null
    }

    Write-Host ""
    Write-Host ("Fertig — {0} hinzugefügt, {1} geändert, {2} gelöscht." -f $toAdd.Count, $toEdit.Count, $toDelete.Count) -ForegroundColor Green
    Write-Host "Pending-Changes im TFS angelegt — Check-in bewusst manuell." -ForegroundColor DarkGray
    Write-Host "Ziel: $Target" -ForegroundColor DarkGray
}
