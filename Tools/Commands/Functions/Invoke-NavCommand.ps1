<#
.SYNOPSIS
    Dispatcher für die Nav-Repo-Commands — Alias `nav`.

.DESCRIPTION
    `nav <command>` führt den passenden Repo-Command aus (z. B. `nav build`, `nav test`); der
    Sub-Command (Token) wird generisch über Get-NavCommandInfo aufgelöst. Restliche
    Argumente werden über $args unverändert an die Zielfunktion durchgereicht
    (`nav build -Configuration Release`, `nav newbranch foo -Force`).

    Bewusst KEINE advanced function (kein [CmdletBinding], keine [Parameter]-/[ArgumentCompleter]-
    Attribute): nur dann befüllt PowerShell $args mit den nicht an $Command gebundenen
    Argumenten, und nur das automatische $args reicht beim Splatten (@args) benannte Parameter
    und Switches korrekt durch. Ein per ValueFromRemainingArguments selbst gesammeltes Array
    würde `-Name`-Token beim Splatten positional binden (`-Force` landete als Wert auf dem
    ersten positionalen Parameter). Die Tab-Completion hängt deshalb an Register-ArgumentCompleter
    (am Dateiende) statt an einem Parameter-Attribut.

    Ohne Argument (`nav` + Enter) erscheint eine interaktive Auswahlliste (Pfeiltasten-Menü) der
    Repo-Commands. Ohne echte Konsole (Input umgeleitet) wird stattdessen die statische Übersicht
    (Show-Commands) ausgegeben.

.PARAMETER Command
    Der Sub-Command (Token), z. B. build, test, incbuild, newbranch, rmbranch, publish.
#>
function Invoke-NavCommand {
    param(
        [string] $Command
    )

    if (-not $Command) {
        # Bare `nav` → interaktive Auswahlliste der Repo-Commands.
        $repo = @(Get-NavCommandInfo | Where-Object { $_.Kind -eq 'Repo' } | Sort-Object Token)
        # Token-Spalte dynamisch an den längsten Token ausrichten (Tokens variabler Länge).
        $tokenWidth = ($repo.Token | Measure-Object -Maximum -Property Length).Maximum
        $pick = Show-SelectionMenu -Items $repo -Header 'Command wählen  (↑/↓ · Enter · Esc)' `
            -Label { "{0,-$tokenWidth} {1}" -f $_.Token, $_.Synopsis }
        if ($null -eq $pick) {
            # Nicht-interaktiv (Input umgeleitet) → statische Übersicht; bei Esc stiller Abbruch.
            if ([Console]::IsInputRedirected) { Show-Commands }
            return
        }
        & $pick.Name
        return
    }

    $info = Get-NavCommandInfo |
        Where-Object { $_.Kind -eq 'Repo' -and $_.Token -eq $Command } |
        Select-Object -First 1
    if (-not $info) {
        Write-Host "Unbekannter Command '$Command'." -ForegroundColor Yellow
        Show-Commands
        return
    }

    # $args = alle nicht an $Command gebundenen Argumente; @args reicht sie (inkl. -Switches und
    # benannten Parametern) korrekt an die Zielfunktion durch.
    & $info.Name @args
}

Set-Alias nav Invoke-NavCommand

# Tab-Completion für den Sub-Command. Per Register-ArgumentCompleter statt Inline-Attribut,
# damit Invoke-NavCommand eine einfache Funktion bleibt und $args verfügbar ist (s. o.).
Register-ArgumentCompleter -CommandName Invoke-NavCommand -ParameterName Command -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)
    Get-NavCommandInfo |
        Where-Object { $_.Kind -eq 'Repo' -and $_.Token -like "$wordToComplete*" } |
        Sort-Object Token |
        ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_.Token, $_.Token, 'ParameterValue', $_.Synopsis)
        }
}
