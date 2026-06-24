# Nav Language — VS-Code-LSP-PoC

Minimaler VS-Code-Client, der den Nav-LSP-Server (`Pharmatechnik.Nav.Language.Server`)
über stdio startet und `.nav`-Dateien mit Diagnostics versorgt.

## Voraussetzungen

- **.NET-10-Runtime** (`dotnet` im PATH)
- **Node.js / npm** (für `npm install`)
- **VS Code**

## Einrichten

1. Server bauen (Full-Framework-MSBuild, da die Engine `CodeTaskFactory` nutzt):

   ```
   MSBuild.exe Nav.Language.Server\Nav.Language.Server.csproj -t:Build -p:Configuration=Debug
   ```

   Ergebnis: `Nav.Language.Server\bin\Debug\net10.0\Pharmatechnik.Nav.Language.Server.dll`.

2. Client-Abhängigkeiten installieren:

   ```
   cd vscode-nav-lsp
   npm install
   ```

## Ausprobieren

- Ordner `vscode-nav-lsp` in VS Code öffnen und **F5** drücken
  („Nav LSP Extension (Extension Development Host)").
- Im Entwicklungs-Host einen **Ordner mit `.nav`-Dateien** öffnen.
- Eine `.nav`-Datei öffnen → Fehler/Warnungen erscheinen als Squiggles und im Problems-Panel.
  Beim Tippen aktualisieren sich die Diagnostics (Overlay), beim Schließen gilt wieder der Platteninhalt.

## Konfiguration

- `navLanguageServer.serverPath`: absoluter Pfad zur Server-DLL. Leer = Standard-Build-Ausgabe
  (`../Nav.Language.Server/bin/Debug/net10.0/...`). Für eine Release-/anderweitige Ausgabe hier
  den Pfad setzen.

## Hinweis Deployment

`dotnet publish --self-contained` funktioniert nicht direkt, weil die referenzierte Engine via
`CodeTaskFactory` nur mit der Full-Framework-`MSBuild.exe` baut. Für ein eigenständiges Paket:
`MSBuild.exe -t:Publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true`. Für den PoC genügt der
framework-dependent Lauf per `dotnet <dll>`.
