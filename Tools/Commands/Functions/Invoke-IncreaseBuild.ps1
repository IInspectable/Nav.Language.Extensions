<#
.SYNOPSIS
    Erhöht die Build-Nummer (Patch): X.Y.Z → X.Y.(Z+1).

.DESCRIPTION
    Bumpt die Produktversion in Version.props und vscode-nav-lsp\package.json synchron
    (siehe Update-Version). Der Repo-Root wird zur Aufruf-Zeit aufgelöst (Resolve-Root).

.FUNCTIONALITY
    incbuild
#>
function Invoke-IncreaseBuild {
    [CmdletBinding()]
    param()

    $root = Resolve-Root
    if (-not $root) { return }

    [void](Update-Version -Root $root -Update {
        param([Version] $old)
        New-Object System.Version -ArgumentList $old.Major, $old.Minor, ($old.Build + 1)
    })
}
