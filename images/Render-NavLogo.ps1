<#
.SYNOPSIS
    Rendert die README-Wortmarke (Icon + Schriftzug „NavLanguage Extensions") aus dem
    Vektor-Icon-Master (NavActivityDiagram.xaml) in zwei hochauflösende PNGs — eine helle
    und eine dunkle Variante für GitHubs <picture>-Umschaltung (prefers-color-scheme).

.DESCRIPTION
    Eine gemeinsame Vektorquelle, kein dupliziertes Pfadmaterial: Die Strichgrafik des Icons
    stammt unverändert aus NavActivityDiagram.xaml; hier wird lediglich die „Tinte"
    (Strichgrafik + Schriftzug) pro Variante umgefärbt und der Schriftzug angesetzt.

        Light  → Logo.png       (dunkle Tinte, für hellen Hintergrund)
        Dark   → Logo-dark.png  (helle Tinte, für dunklen Hintergrund)

    Der Schriftzug setzt sich zusammen aus „Nav" (fett) direkt gefolgt von „Language"
    (leicht) — also die Wortmarke „NavLanguage" — und darunter, linksbündig unter „Language",
    einem Akzentstrich plus gesperrtem „EXTENSIONS". Die Akzentfarbe ist variantenabhängig
    (Teal im Hellen, Gold im Dunklen).

    Verwendete Schrift ist **Plus Jakarta Sans** (SIL Open Font License) — muss installiert sein
    (statische Schnitte Light/Regular/SemiBold/ExtraBold). Bezugsquelle: Google Fonts bzw.
    https://github.com/tokotype/PlusJakartaSans.

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

# Plus Jakarta Sans muss installiert sein (siehe .DESCRIPTION); Fallback auf Segoe UI verhindert
# nur einen harten Fehler, ergibt aber nicht das gewünschte Bild.
$fontFamily = 'Plus Jakarta Sans, Segoe UI'

# „EXTENSIONS" gesperrt setzen (WPF-TextBlock kennt kein Letter-Spacing): Buchstaben mit
# schmalem Leerraum (U+2009 THIN SPACE) verbinden.
$extensions = ('EXTENSIONS'.ToCharArray() -join ([char]0x2009))

# Pro Variante:
#   Ink     „Nav"-Textfarbe (kräftig)
#   Lang    gedämpfte „Language"-Textfarbe
#   IconInk Rahmen-/Strichfarbe des Icons — bewusst getrennt vom Text, damit im Dunklen ein
#           hellgrauer Rand auf hellem „Nav" möglich ist
#   Paper   Innenfüllung von Kreis + Raute; TRANSPARENT, damit der jeweilige Seiten-Hintergrund
#           durchscheint („Füllung des Hintergrunds") statt einer weißen Fläche
#   Accent  Akzentstrich + „EXTENSIONS"
$variants = @(
    @{ Path = 'images\Logo.png';      Ink = '#FF1A1A1A'; Lang = '#FF3D3D3D'; IconInk = '#FF1A1A1A'; Paper = '#00FFFFFF'; Accent = '#FF0097B6' }
    @{ Path = 'images\Logo-dark.png'; Ink = '#FFF5F5F5'; Lang = '#FFC2C2C2'; IconInk = '#FFBFBFBF'; Paper = '#00FFFFFF'; Accent = '#FFFFC000' }
)

$scale = 3
$dpi   = 96 * $scale

foreach ($v in $variants) {
    $out    = Join-Path $repoRoot $v.Path
    # Strichgrafik → IconInk umfärben; die weißen Innenflächen (Kreis + Raute) → transparentes Paper.
    $icon   = $viewboxXaml -replace '#FF202020', $v.IconInk -replace '#FFFFFFFF', $v.Paper

    $xaml = @"
<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Background="Transparent" Padding="10,8"
        TextOptions.TextFormattingMode="Ideal"
        TextOptions.TextRenderingMode="Grayscale">
  <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
    <Grid Width="56" Height="56" VerticalAlignment="Center">$icon</Grid>
    <Grid VerticalAlignment="Center" Margin="18,0,4,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>
      <TextBlock Grid.Row="0" Grid.Column="0" Text="Nav" FontFamily="$fontFamily"
                 FontWeight="ExtraBold" FontSize="38" Foreground="$($v.Ink)"
                 LineHeight="42" LineStackingStrategy="BlockLineHeight"/>
      <TextBlock Grid.Row="0" Grid.Column="1" Text="Language" FontFamily="$fontFamily"
                 FontWeight="Regular" FontSize="38" Foreground="$($v.Lang)"
                 LineHeight="42" LineStackingStrategy="BlockLineHeight"/>
      <StackPanel Grid.Row="1" Grid.Column="1" Orientation="Horizontal"
                  VerticalAlignment="Center" Margin="1,3,0,2">
        <Border Width="18" Height="2.5" CornerRadius="1.25" Background="$($v.Accent)"
                VerticalAlignment="Center" Margin="0,0,9,0"/>
        <TextBlock Text="$extensions" FontFamily="$fontFamily" FontWeight="SemiBold"
                   FontSize="15.5" Foreground="$($v.Accent)"/>
      </StackPanel>
    </Grid>
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
