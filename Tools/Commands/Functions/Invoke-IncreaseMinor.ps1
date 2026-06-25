<#
.SYNOPSIS
    Erhöht die Minor-Version: X.Y.Z → X.(Y+1).0.

.DESCRIPTION
    Bumpt die Produktversion in Version.props und vscode-nav-lsp\package.json synchron
    (siehe Update-Version). Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root).

.FUNCTIONALITY
    incminor
#>
function Invoke-IncreaseMinor {
    [CmdletBinding()]
    param()

    $root = Resolve-Root
    if (-not $root) { return }

    [void](Update-Version -Root $root -Update {
        param([Version] $old)
        New-Object System.Version -ArgumentList $old.Major, ($old.Minor + 1), 0
    })
}
