<#
.SYNOPSIS
    Erhöht die Major-Version: X.Y.Z → (X+1).0.0.

.DESCRIPTION
    Bumpt die Produktversion in Version.props und vscode-nav-lsp\package.json synchron
    (siehe Update-Version). Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root).

.FUNCTIONALITY
    incmajor
#>
function Invoke-IncreaseMajor {
    [CmdletBinding()]
    param()

    $root = Resolve-Root
    if (-not $root) { return }

    [void](Update-Version -Root $root -Update {
        param([Version] $old)
        New-Object System.Version -ArgumentList ($old.Major + 1), 0, 0
    })
}
