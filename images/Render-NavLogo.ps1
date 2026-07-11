<#
.SYNOPSIS
    Rendert die README-Wortmarke (Icon + Schriftzug „Nav Language Extensions") aus dem
    Vektor-Icon-Master (NavActivityDiagram.xaml) in zwei hochauflösende PNGs — eine helle
    und eine dunkle Variante für GitHubs <picture>-Umschaltung (prefers-color-scheme).

.DESCRIPTION
    Eine gemeinsame Vektorquelle, kein dupliziertes Pfadmaterial: Die Strichgrafik des Icons
    stammt unverändert aus NavActivityDiagram.xaml; hier wird lediglich die „Tinte"
    (Strichgrafik + Schriftzug) pro Variante umgefärbt und der Schriftzug angesetzt.

        Light  → Logo.png       (dunkle Tinte, für hellen Hintergrund)
        Dark   → Logo-dark.png  (helle Tinte, für dunklen Hintergrund)

    Die Nav-Akzentknoten (#FFC000 / #0097B6) und die weißen Innenflächen des Icons bleiben in
    beiden Varianten erhalten. Gerendert wird in 3× (288 DPI) für gestochen scharfe Darstellung;
    das README skaliert die Grafik per width-Attribut auf die logische Größe herunter.

    Läuft in Windows PowerShell 5.1 (.NET Framework, native WPF). Aufruf aus dem Repo:
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File images\Render-NavLogo.ps1

    Nach einer Änderung an NavActivityDiagram.xaml dieses Skript erneut ausführen.
#>
[CmdletBinding()]
param()

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$repoRoot = Split-Path $PSScriptRoot -Parent
$master   = Join-Path $PSScriptRoot 'NavActivityDiagram.xaml'

# Icon-Master einlesen und die reine <Viewbox>…</Viewbox> extrahieren (Kommentar davor abschneiden).
$masterText  = [System.IO.File]::ReadAllText($master)
$viewboxXaml = $masterText.Substring($masterText.IndexOf('<Viewbox'))

# Zwei Varianten: Dateiname, Tintenfarbe (Strichgrafik + „Nav Language"), Akzent für „Extensions".
$variants = @(
    @{ Path = 'images\Logo.png';      Ink = '#FF1B1B1B'; Accent = '#FF0097B6' }
    @{ Path = 'images\Logo-dark.png'; Ink = '#FFEDEDED'; Accent = '#FF2BB8D6' }
)

$scale = 3
$dpi   = 96 * $scale

foreach ($v in $variants) {
    $out    = Join-Path $repoRoot $v.Path
    $icon   = $viewboxXaml -replace '#FF202020', $v.Ink

    $xaml = @"
<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Background="Transparent" Padding="10,8"
        TextOptions.TextFormattingMode="Ideal"
        TextOptions.TextRenderingMode="Grayscale">
  <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
    <Grid Width="56" Height="56" VerticalAlignment="Center">$icon</Grid>
    <StackPanel Orientation="Vertical" VerticalAlignment="Center" Margin="16,0,4,2">
      <TextBlock Text="Nav Language" FontFamily="Segoe UI" FontWeight="SemiBold"
                 FontSize="37" Foreground="$($v.Ink)" LineHeight="40"
                 LineStackingStrategy="BlockLineHeight"/>
      <TextBlock Text="Extensions" FontFamily="Segoe UI" FontWeight="SemiBold"
                 FontSize="19" Foreground="$($v.Accent)" Margin="1,1,0,0"/>
    </StackPanel>
  </StackPanel>
</Border>
"@

    $element = [System.Windows.Markup.XamlReader]::Parse($xaml)
    $element.Measure((New-Object System.Windows.Size([double]::PositiveInfinity, [double]::PositiveInfinity)))
    $w = $element.DesiredSize.Width
    $h = $element.DesiredSize.Height
    $element.Arrange((New-Object System.Windows.Rect(0, 0, $w, $h)))
    $element.UpdateLayout()

    $pxW = [int][math]::Ceiling($w * $scale)
    $pxH = [int][math]::Ceiling($h * $scale)

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $pxW, $pxH, $dpi, $dpi, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($element)

    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $fs = [System.IO.File]::Create($out)
    try { $enc.Save($fs) } finally { $fs.Close() }

    Write-Host ("{0,4}x{1,-4} (logisch {2:N0}x{3:N0}) -> {4}" -f $pxW, $pxH, $w, $h, $v.Path)
}
