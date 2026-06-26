<#
.SYNOPSIS
    Erhöht die Produktversion in Version.props (einzige Quelle der Wahrheit).

.DESCRIPTION
    Liest <ProductVersion> aus Version.props, berechnet die neue Version über den
    übergebenen Update-ScriptBlock (bekommt die alte [Version], liefert die neue) und
    schreibt sie als UTF-8 mit BOM zurück.

    vscode-nav-lsp\package.json wird BEWUSST NICHT angefasst: Das VSIX bekommt seine Version
    beim Paketieren ohnehin aus Version.props (Invoke-PackageVsCode ruft
    `vsce package <version> --no-update-package-json`). Die `version` in package.json ist
    daher nur ein Platzhalter (0.0.0) und keine zweite Quelle, die gepflegt werden müsste.

    Liefert die neue Version als String. Interner Helper (Verb-Noun ohne .FUNCTIONALITY →
    kein nav-Command); genutzt von Invoke-IncreaseBuild/Minor/Major.

.PARAMETER Root
    Repo-Root.

.PARAMETER Update
    ScriptBlock: param([Version] $old) → [Version] $new.
#>
function Update-Version {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root,
        [Parameter(Mandatory = $true)]
        [scriptblock] $Update
    )

    $ErrorActionPreference = 'Stop'

    $propsFile = Join-Path $Root 'Version.props'
    if (-not (Test-Path $propsFile)) { throw "Version.props nicht gefunden: '$propsFile'." }

    $xml = [xml](Get-Content -Raw $propsFile)
    $node = $xml.Project.PropertyGroup.ChildNodes | Where-Object Name -eq 'ProductVersion'
    if (-not $node) { throw "Kein <ProductVersion> in '$propsFile' gefunden." }

    $oldVersion = [Version]::Parse($node.InnerText.Trim())
    $newVersion = & $Update $oldVersion
    $newText = $newVersion.ToString()

    # Version.props schreiben (UTF-8 mit BOM, wie das bisherige Versioning.ps1).
    $node.InnerText = $newText
    $utf8WithBom = New-Object System.Text.UTF8Encoding($true)
    $sw = New-Object System.IO.StreamWriter($propsFile, $false, $utf8WithBom)
    try { $xml.Save($sw) } finally { $sw.Close() }

    Write-Host "  Version: $oldVersion → $newText" -ForegroundColor Green
    return $newText
}
