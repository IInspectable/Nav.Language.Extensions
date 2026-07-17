# Visual-Studio-Features der Nav Language Extension

Diese Übersicht fasst zusammen, was die **Nav Language Extension** im Visual-Studio-Editor
beim Arbeiten mit `.nav`-Dateien bietet — aus Anwendersicht.

Die **Nav-Sprache** ist eine domänenspezifische Sprache (DSL) der Pharmatechnik zur
Beschreibung von Workflows im Stil von UML-Aktivitätsdiagrammen. Aus `.nav`-Dateien wird
beim Build C#-Code generiert. Die Extension macht aus dem reinen Texteditor eine
vollwertige Sprachumgebung mit Syntax-Highlighting, IntelliSense, Navigation, Fehleranzeige
und Refactorings — vergleichbar mit dem, was man von C# in Visual Studio gewohnt ist.

---

## Syntax-Highlighting

`.nav`-Dateien werden eingefärbt — sowohl rein **syntaktisch** (anhand der Token) als auch
**semantisch** (anhand der tatsächlichen Bedeutung der Symbole). Eigene Farben gibt es u.a.
für:

- Schlüsselwörter und Control-Keywords
- Task-Namen, GUI-/Form-Knoten und Choice-Knoten
- Connection Points (Init-/Exit-Punkte)
- Typnamen und Parameternamen
- String-Literale und Kommentare
- Preprocessor-Anweisungen
- nicht erreichbarer Code (Dead Code) wird abgesetzt dargestellt

Die Farben lassen sich über **Tools › Optionen › Umgebung › Schriftarten und Farben**
anpassen. Die semantische Hervorhebung kann in den Optionen abgeschaltet werden.

## IntelliSense / Code-Vervollständigung

Beim Tippen (oder mit `Strg+Leertaste`) bietet die Extension kontextabhängige Vorschläge:

- Nav-Schlüsselwörter und im Dokument bekannte Symbole
- Knoten und Kanten (Edges) sowie Trigger in Transitions
- Dateipfade innerhalb von String-Literalen (z.B. für Includes/`taskref`)

## QuickInfo / Tooltips

Beim Überfahren eines Symbols mit der Maus erscheint ein Tooltip mit Informationen zum
Symbol — inklusive einer Vorschau des zugehörigen Codes.

## Navigation

- **Gehe zu Definition** (`F12`) sowie Navigation per **Strg+Klick** auf ein Symbol.
- Sprung zur **nächsten/vorherigen Referenz** des markierten Symbols.
- **Bidirektionale Navigation zwischen Nav und generiertem C#-Code**: von einem Element in
  der `.nav`-Datei zur generierten C#-Klasse/-Methode und umgekehrt zurück zur Nav-Quelle.
  Im C#-Editor werden dafür klickbare **IntraText-GoTo-Links** direkt im Code eingeblendet.

## Alle Referenzen finden

Über **Alle Referenzen suchen** (`Umschalt+F12`) werden sämtliche Verwendungen eines Symbols
ermittelt und im Standard-Ergebnisfenster von Visual Studio angezeigt.

## Aufrufhierarchie

Über **Aufrufhierarchie anzeigen** (`Strg+K, Strg+T`) auf einer Task öffnet sich das eingebaute
**Aufrufhierarchie-Fenster** von Visual Studio — dasselbe Werkzeug, das man von C# kennt:

- **Calls To** — alle Tasks, die die gewählte Task aufrufen (eingehend, solution-weit ermittelt).
- **Calls From** — alle Tasks, die die gewählte Task aufruft (ausgehend).
- Jeder Knoten lässt sich **beliebig tief in beide Richtungen** weiter aufklappen — auch über
  Include-/`taskref`-Grenzen hinweg.
- Die einzelnen **Aufrufstellen** werden im Detailbereich angezeigt; ein Doppelklick springt direkt
  zur jeweiligen Stelle in der `.nav`-Datei.

## Referenz-Hervorhebung

Steht der Cursor auf einem Symbol, werden alle Vorkommen dieses Symbols im Dokument
hervorgehoben. Das Verhalten ist über die Optionen steuerbar und kann optional auch
Referenzen über Include-Dateien hinweg einbeziehen.

## Fehler & Warnungen (Diagnostics)

Syntaktische und semantische Probleme werden direkt beim Bearbeiten gemeldet:

- **Wellenlinien** unter der betroffenen Stelle im Editor
- ein **Fehler-Streifen** und eine **Fehler-Zusammenfassung** am Rand/Scrollbalken für den
  schnellen Überblick über alle Probleme im Dokument
- Integration in die **Fehlerliste** von Visual Studio

Die vollständige Liste der Fehler- und Warnungs-Codes ist in [Errors.md](Errors.md)
dokumentiert.

## Code-Fixes / Schnellaktionen (Glühbirne)

An passenden Stellen bietet die Glühbirne automatische Korrekturen und Aufräumaktionen an,
darunter:

- fehlende Exit-Transition ergänzen
- fehlende Semikolons bei Include-Direktiven ergänzen
- einen Choice einführen
- ungenutzte Include-Direktiven entfernen
- ungenutzte Knoten entfernen
- ungenutzte Task-Deklarationen entfernen

## Editier-Komfort

- **Kommentieren / Auskommentieren** der Auswahl
- automatische Vervollständigung von Klammern und Anführungszeichen: `{ }`, `( )`, `[ ]`,
  `" "`
- **Klammer-Matching**: zusammengehörige Klammern werden hervorgehoben
- **Umbenennen** (`F2`) eines Symbols inklusive aller Referenzen
- **Einfügen von Dateipfaden als `taskref`**: beim Einfügen eines Dateipfads wird daraus
  automatisch eine passende relative `taskref`-Anweisung erzeugt

## Code-Struktur & Übersicht

- **Outlining / Folding**: ein- und ausklappbare Bereiche für Task-Definitionen,
  Knoten-Blöcke, Transitions, mehrzeilige Kommentare, Include-Direktiven und Namespaces
- **Navigationsleiste** (Dropdowns oberhalb des Editors) zur schnellen Auswahl von
  Projekt und Task

## Generierten C#-Code anzeigen

Über einen eigenen Befehl lässt sich der aus einem Nav-Element generierte **C#-Code direkt
anzeigen**, ohne die generierten Dateien manuell suchen zu müssen.

## Optionen

Die Einstellungen der Extension finden sich unter
**Tools › Optionen › Text-Editor › Nav › Advanced**:

| Option | Beschreibung |
|---|---|
| Semantic Highlighting | semantische Syntax-Hervorhebung ein-/ausschalten |
| Highlight References Under Cursor | Referenzen des Symbols unter dem Cursor hervorheben |
| Highlight References Under Include | Referenzen auch über Includes hinweg hervorheben |
| Auto Insert Delimiters | automatisches Einfügen schließender Klammern/Anführungszeichen |

## Build-Integration

Über den reinen Editor hinaus werden `.nav`-Dateien beim Build automatisch in C#-Code
übersetzt — per MSBuild-Task bzw. dem Kommandozeilen-Werkzeug `nav.exe`. So bleiben Nav-
Definition und generierter Code synchron.
