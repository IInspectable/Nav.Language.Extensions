<#
.SYNOPSIS
    Rendert das Nav-Icon aus dem Vektor-Master (NavActivityDiagram.xaml) in die PNG-Zielgrößen
    für die Visual-Studio- und die VS-Code-Extension.

.DESCRIPTION
    Eine gemeinsame Vektorquelle, mehrere PNG-Kopien: Da VSIX (VS) und vsce (VS Code) jeweils
    ihre eigene Icon-Datei mitpacken und Ordnergrenzen nicht überschreiten können, wird der Master
    hier reproduzierbar in beide Extension-Ordner gerendert. Nach einer Änderung an
    NavActivityDiagram.xaml dieses Skript erneut ausführen.

    Läuft in Windows PowerShell 5.1 (.NET Framework, native WPF). Aufruf aus dem Repo:
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File images\Render-NavIcon.ps1
#>
[CmdletBinding()]
param()

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$repoRoot = Split-Path $PSScriptRoot -Parent
$master   = Join-Path $PSScriptRoot 'NavActivityDiagram.xaml'

# Ziel-PNGs: Pfad (relativ zum Repo-Root) → Kantenlänge in px
$targets = @(
    @{ Path = 'vscode-nav-lsp\images\icon.png';                 Size = 256 }
    @{ Path = 'Nav.Language.Extension2026\Resources\Icon.png';  Size = 256 }
)

$xamlText = [System.IO.File]::ReadAllText($master)

foreach ($t in $targets) {
    $out  = Join-Path $repoRoot $t.Path
    $size = [int]$t.Size

    $element = [System.Windows.Markup.XamlReader]::Parse($xamlText)
    $element.Width  = $size
    $element.Height = $size
    $element.Measure((New-Object System.Windows.Size($size, $size)))
    $element.Arrange((New-Object System.Windows.Rect(0, 0, $size, $size)))
    $element.UpdateLayout()

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($element)

    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $fs = [System.IO.File]::Create($out)
    try { $enc.Save($fs) } finally { $fs.Close() }

    Write-Host ("{0,4}px -> {1}" -f $size, $t.Path)
}
