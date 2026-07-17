#Requires -Version 5.1
<#
.SYNOPSIS
    PostToolUse-Hook: erzwingt UTF-8 *mit* BOM für die zuletzt geschriebene Textdatei.

.DESCRIPTION
    Claude Codes Write-Tool legt neue Dateien grundsätzlich als UTF-8 *ohne* BOM an
    (Edit bewahrt nur einen vorhandenen Zustand). Da das Repo durchgängig UTF-8 *mit*
    BOM verlangt, korrigiert dieser Hook das deterministisch nach jedem Write/Edit.

    Die Hook-Payload kommt als JSON über stdin; daraus wird der Dateipfad gelesen.
    Hat die Datei eine Text-Endung aus der Whitelist und (noch) kein BOM, wird das
    BOM-Präfix EF BB BF byte-genau vorangestellt — der übrige Inhalt inklusive
    Zeilenenden bleibt unverändert. Dateien, die nicht sauber als UTF-8 dekodieren
    (z. B. UTF-16 oder Binärdaten), werden in Ruhe gelassen.

    Ausgenommen ist Markdown mit YAML-Frontmatter (erste Zeile '---'): dessen Parser
    erkennt den öffnenden Delimiter hinter einem BOM nicht mehr und liest die Datei
    als frontmatter-los. Betroffen sind u. a. SKILL.md und die Memory-Dateien, deren
    'description' damit unsichtbar würde. Frontmatter-Parser verlangen UTF-8 ohne BOM —
    das ist keine Aufweichung der Repo-Regel, sondern ein fremdes Dateiformat.

    Der Hook darf Edits nie blockieren: Fehler werden geschluckt, Exit-Code immer 0.
#>

$ErrorActionPreference = 'Stop'
try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

    $json = $raw | ConvertFrom-Json
    $path = $null
    if ($json.tool_input -and $json.tool_input.file_path) {
        $path = [string]$json.tool_input.file_path
    } elseif ($json.tool_response -and $json.tool_response.filePath) {
        $path = [string]$json.tool_response.filePath
    }
    if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { exit 0 }

    $ext = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
    $textExt = @(
        '.cs', '.md', '.csproj', '.props', '.targets', '.tasks',
        '.sln', '.slnx', '.config', '.xml', '.nav', '.stg',
        '.ps1', '.psm1', '.txt'
    )
    if ($textExt -notcontains $ext) { exit 0 }

    $bom = [byte[]](0xEF, 0xBB, 0xBF)

    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -eq 0) { exit 0 }
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF

    # Nur eingreifen, wenn der Inhalt sauberes UTF-8 ist — sonst würde ein
    # vorangestelltes BOM eine andere Kodierung (z. B. UTF-16) zerstören.
    try {
        $strict = New-Object System.Text.UTF8Encoding($false, $true)
        $text = $strict.GetString($bytes)
    } catch {
        exit 0
    }

    # Markdown mit YAML-Frontmatter: kein BOM setzen — und ein vorhandenes entfernen,
    # damit der Hook einmal beschädigte Dateien beim nächsten Edit wieder heilt.
    if ($ext -eq '.md' -and $text.TrimStart([char]0xFEFF) -match '^---\r?\n') {
        if ($hasBom) {
            $out = New-Object byte[] ($bytes.Length - $bom.Length)
            [System.Array]::Copy($bytes, $bom.Length, $out, 0, $out.Length)
            [System.IO.File]::WriteAllBytes($path, $out)
        }
        exit 0
    }

    if ($hasBom) { exit 0 }

    $out = New-Object byte[] ($bom.Length + $bytes.Length)
    [System.Array]::Copy($bom, 0, $out, 0, $bom.Length)
    [System.Array]::Copy($bytes, 0, $out, $bom.Length, $bytes.Length)
    [System.IO.File]::WriteAllBytes($path, $out)
} catch {
    # bewusst still: ein Hook darf den Edit nie scheitern lassen
}
exit 0
