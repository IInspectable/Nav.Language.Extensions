# Nav Language Extensions

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/IInspectable/Nav.Language.Extensions/master/images/Logo-dark.png">
  <img alt="Nav Language Extensions" src="https://raw.githubusercontent.com/IInspectable/Nav.Language.Extensions/master/images/Logo.png" width="340">
</picture>

Sprachunterstützung für die **Nav Language** in **Visual Studio** und **Visual Studio Code** —
mit Syntaxhervorhebung, semantischer Analyse, IntelliSense, Navigation und Refactorings aus einer
gemeinsamen Sprach-Engine.

Die **Nav Language** ist eine DSL zur Beschreibung von Workflows im Stil von
UML-Aktivitätsdiagrammen. Sie beschreibt Zustände (Views, Tasks, Choices) und die Übergänge
(Edges) dazwischen — und enthält zugleich Sprachelemente, die das **Erzeugen von C#-Code**
steuern: Aus `.nav`-Dateien wird beim Build generierter C#-Code.

```nav
#version 2

[namespaceprefix MyApp]

task LoginFlow [base StandardWFS: IWFServiceBase]
               [result bool]
{
    init    Start;
    exit    Done;
    view    Home;
    choice  Retry [params string reason];

    Start --> Home;
    Home  --> Retry on OnDecide;
    Home  --> Done  on OnClose;

    Retry --> Home;              // erneut anzeigen
    Retry --> Done if "Abbruch"; // abbrechen
}
```

## Features

- **Syntaxhervorhebung** — farbige Klassifizierung anhand des Sprachmodells.
- **Diagnostics** — Fehler und Warnungen live beim Tippen, inklusive dateiübergreifender Prüfungen
  (Änderungen in inkludierten Dateien wirken auf die inkludierenden).
- **Code-Vervollständigung** — Nav-Schlüsselwörter, Edges (`-->`, `o->`, `*->`, `==>`, `o-^`, `--^`)
  und Pfade in `taskref`-Direktiven.
- **QuickInfo / Hover** — Signaturen als `nav`-Codeblock; bei `choice` und Edges die transitiv
  erreichbaren Knoten.
- **Gehe zu Definition** und **Alle Verweise suchen** — solution-weit über alle `.nav` eines Ordners.
- **Symbolhervorhebung** — alle Vorkommen des Symbols unter dem Cursor.
- **Gliederung** — Document Symbols für Outline und Breadcrumbs.
- **Umbenennen** (`F2`) — symbolweit mit Namensvalidierung.
- **Code-Aktionen** (Lightbulb) — QuickFixes (z. B. fehlende Exit-Transition ergänzen, ungenutzte
  Knoten/Deklarationen/Includes entfernen) und Refactorings (z. B. *Introduce Choice*).
- **Codefaltung** für Blöcke.
- **Formatierung** — „Dokument formatieren" richtet Einrückung und Ausrichtung am Nav-Sprachmodell aus.

Die **Visual-Studio-Extension** bietet zusätzlich **Aufrufhierarchie**, **CodeLens** und die
**Navigation zwischen Nav und dem generierten C#-Code** (Sprung von der `.nav`-Definition zur
erzeugten Klasse und zurück).

## Installation & Nutzung

### Visual Studio 2026

Die Extension integriert sich in den Editor (Classification, IntelliSense, Outlining, QuickInfo,
Navigation) und bindet die `.nav`-Codegenerierung in den Build ein. Nach der Installation genügt es,
eine `.nav`-Datei zu öffnen.

### Visual Studio Code

Die VS-Code-Extension bringt einen **eingebetteten Sprachserver** mit — kein separates Setup, keine
Laufzeit-Installation. Sie stellt dieselbe Sprachintelligenz bereit wie die Visual-Studio-Erweiterung.

- **Voraussetzung:** Windows x64. Der Sprachserver ist als self-contained `win32-x64`-Build
  paketiert und bringt die .NET-Runtime selbst mit — es ist **kein** separat installiertes `dotnet`
  nötig.
- **Installation:** `.vsix` über **Extensions ▸ ⋯ ▸ „Install from VSIX…"** installieren (oder
  `code --install-extension nav-language-vscode-<version>-win32-x64.vsix`). Eine `.nav`-Datei öffnen —
  die Extension aktiviert sich automatisch und startet den Server.

#### Konfiguration (VS Code)

| Einstellung | Beschreibung |
| --- | --- |
| `navLanguageServer.serverPath` | Optionaler Pfad zu einem eigenen Server. Endung `.exe` → wird direkt gestartet, `.dll` → via `dotnet <dll>`. Leer = der eingebettete Server wird verwendet. |
| `navLanguageServer.trace.server` | LSP-Verkehr zwischen VS Code und dem Server protokollieren (`off` / `messages` / `verbose`) — zur Diagnose. |

## Links

- **Repository:** <https://github.com/IInspectable/Nav.Language.Extensions>
- **Aus dem Quellcode bauen (VS Code):** siehe [`vscode-nav-lsp/Development.md`](https://github.com/IInspectable/Nav.Language.Extensions/blob/master/vscode-nav-lsp/Development.md)
