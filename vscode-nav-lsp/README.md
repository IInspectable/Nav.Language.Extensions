# Nav Language

Sprachunterstützung für **Nav**-Dateien (`.nav`) in Visual Studio Code. Die Extension bringt einen
eingebetteten Sprachserver mit (kein separates Setup, keine Laufzeit-Installation) und stellt die
gleiche Sprachintelligenz bereit wie die Visual-Studio-Erweiterung.

## Features

- **Diagnostics** — Fehler und Warnungen live beim Tippen, inklusive datei­übergreifender Prüfungen
  (Änderungen in inkludierten Dateien wirken auf die inkludierenden).
- **Syntaxhervorhebung** — TextMate-Grammatik plus semantische Tokens vom Server.
- **Hover** — Signaturen als `nav`-Codeblock; bei `choice`/Edges die transitiv erreichbaren Knoten.
- **Code-Vervollständigung** — Nav-Schlüsselwörter, Edges (`-->`, `o->`, `*->`, `==>`) und Pfade in
  `taskref`-Direktiven.
- **Gehe zu Definition** und **Alle Verweise suchen** (solution-weit über alle `.nav` im Ordner).
- **Symbolhervorhebung** — Vorkommen des Symbols unter dem Cursor.
- **Gliederung** — Document Symbols für Outline und Breadcrumbs.
- **CodeLens** — „N Verweise" über Deklarationen, klickbar zur Referenzliste.
- **Umbenennen** (`F2`) — symbolweit mit Namensvalidierung.
- **Code-Aktionen** (Lightbulb) — QuickFixes (z. B. fehlende Exit-Transition ergänzen, ungenutzte
  Knoten/Deklarationen/Includes entfernen) und Refactorings (z. B. *Introduce Choice*).
- **Codefaltung** für Blöcke.

## Voraussetzungen

- **Windows x64.** Der eingebettete Sprachserver ist als self-contained `win32-x64`-Build paketiert
  und bringt die .NET-Runtime selbst mit — es ist **kein** separat installiertes `dotnet` nötig.

## Installation

1. `.vsix` über **Extensions ▸ ⋯ ▸ „Install from VSIX…"** installieren (oder
   `code --install-extension nav-language-<version>-win32-x64.vsix`).
2. Eine `.nav`-Datei öffnen — die Extension aktiviert sich automatisch und startet den Server.

## Konfiguration

| Einstellung | Beschreibung |
| --- | --- |
| `navLanguageServer.serverPath` | Optionaler Pfad zu einem eigenen Server. Endung `.exe` → wird direkt gestartet, `.dll` → via `dotnet <dll>`. Leer = der eingebettete Server wird verwendet. |
| `navLanguageServer.trace.server` | LSP-Verkehr zwischen VS Code und dem Server protokollieren (`off` / `messages` / `verbose`) — zur Diagnose. |

## Mitwirken / Aus dem Quellcode bauen

Bau, Debugging (F5) und Paketierung der Extension sind in [`Development.md`](./Development.md) beschrieben.
