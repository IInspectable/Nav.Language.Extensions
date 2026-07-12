# NRE-/Exception-Robustheits-Analyse der Nav-Pipeline — Lauf-Playbook

## Zweck

Wiederverwendbares Prompt für Läufe, die systematisch prüfen, ob **syntaktisch und/oder
semantisch fehlerhafter** `.nav`-Source beim Ermitteln von **Highlighting (Classification),
Diagnostics oder QuickInfo** eine Exception (insbesondere `NullReferenceException`) auslösen kann.

Leitszenario: Ein Anwender schreibt/ergänzt/korrigiert ein `.nav` in Visual Studio und erzeugt
dadurch ein kaputtes oder nicht vorgesehenes Syntax-/Semantic-Model. Die Feature-Services dürfen
daran **niemals** abstürzen, sondern müssen degradieren (leere/teilweise Ergebnisse), nicht werfen.

## Ausführung (Kurzfassung)

- **Modell:** Opus steuert (Discovery, Diagnose, Integration); optionaler Fan-out je Crash-Signatur
  läuft auf Fable.
- **Korpus:** pfadgetreue Kopie des realen Bestands aus `d:\tfs\main` (analog zum Perf-Harness-Setup).
- **Nachvollziehbarkeit:** Quelle der Wahrheit ist ein `findings.json`-Artefakt auf Platte
  (Signatur → minimaler Repro → Stacktrace) — nicht der Chat-Verlauf.
- **Ergebnis:** Report + Repro-Fixtures + Guard-Fix-Vorschläge; **kein** Commit.

---

## Prompt

````text
# Auftrag: NRE-/Exception-Analyse der Nav-Pipeline bei kaputtem Source

## Ziel
Finde systematisch alle Fälle, in denen syntaktisch und/oder semantisch **fehlerhafter**
`.nav`-Source dazu führt, dass beim Ermitteln von **Highlighting (Classification), Diagnostics
oder QuickInfo** eine Exception (insbesondere NullReferenceException) fliegt. Leitszenario:
Ein Anwender schreibt/ergänzt/korrigiert ein `.nav` in Visual Studio und erzeugt dadurch ein
kaputtes oder nicht vorgesehenes Syntax-/Semantic-Model — die Feature-Services dürfen daran
**niemals** abstürzen, sondern müssen degradieren (leere/teilweise Ergebnisse), nicht werfen.

## Scope (genau diese Oberflächen, nichts anderes)
Härte ausschließlich die **VS-freien Engine-Kerne** in `Nav.Language`:
1. **Parser/Syntax**: Lexer/Parser → `SyntaxTree`.
2. **SemanticModel**: `CodeGenerationUnit`/Symbole (inkl. der semantischen Diagnostik `NavXXXX`).
3. **Feature-Services**: Classification, Diagnostics und QuickInfo — die konkreten Erzeuger, die
   VS/LSP live aufrufen, während der Anwender tippt.

LSP-Host und VS-Extension-Host sind **nicht** Teil dieses Laufs. Wenn ein Feature (z.B.
Classification) seinen Kern teils erst im Host hat, treibe die **zugrundeliegende Engine-API,
die der Host aufruft** — nicht den Host selbst. Finde diese Einstiegspunkte durch Lesen des
Host-Codes, erfinde keine Signaturen.

## Kritische Vorgabe zur Mutationsstrategie — NICHT naiv sein
Die Analyse soll **grammatik-bewusst** mutieren, nicht mechanisch. Ausdrücklich **verboten** ist
die naive „wachsende-Präfix"-Strategie (korrektes File von Zeichen 1 bis Ende immer um ein Zeichen
verlängern) — die ist praxisfern und übersieht die interessanten Fälle (z.B. ein Knoten **mitten**
in der Grammatik fehlt, während der Rest steht).

Arbeite stattdessen auf **Token-/Knoten-Granularität** über den geparsten Bäumen der Seed-Files.
Wende mindestens diese Mutationsoperatoren systematisch an (jede Anwendungsstelle einzeln):
- **Interior-Deletion**: einen ganzen Teilbaum/Knoten aus der Mitte entfernen (eine Transition,
  ein Task-Decl, eine `[params]`-Liste, eine Direktive, ein einzelnes Statement) — der zentrale
  „Knoten fehlt in der Mitte"-Fall.
- **Token-Deletion**: jedes einzelne Token löschen.
- **Delimiter-Bruch**: je ein `{ } [ ] ( ) ;` entfernen oder verdoppeln → unbalancierte Klammern.
- **Keyword-/Identifier-Korruption**: Keyword → anderes Keyword / Identifier / Zahl / Müll ersetzen;
  Identifier → reserviertes Wort.
- **Leere Konstrukte**: `[]`, `{}`, leere Parameterliste, leerer Codeblock erzwingen.
- **Boundary-Truncation**: an **Konstrukt-Grenzen** abschneiden (Ende Statement / mitten im Knoten),
  nicht an beliebigen Zeichen — simuliert unfertiges Tippen an semantisch relevanten Stellen.
- **Adjazenz-Swap**: benachbarte Token vertauschen; **Stray-Insert**: Fremd-Token einstreuen.

Wichtig: **Nach dem Parsen NICHT abbrechen, wenn Parse-Fehler vorliegen.** Genau der kaputte Baum
soll in SemanticModel und Feature-Services weitergefüttert werden — dort entsteht der unvorgesehene
Zustand → NRE.

## Treiber — die Services so aufrufen, wie VS es tut
Für jede Mutation die volle Kette fahren und **jeden Schritt einzeln in try/catch** kapseln:
1. Parsen → SyntaxTree.
2. SemanticModel/`CodeGenerationUnit` bauen + Diagnostics einsammeln.
3. **Classification** über die gesamte Spanne.
4. **QuickInfo** an **jeder** Token-/Offset-Position (positionsgetrieben — genau wie Hover in VS).
5. Diagnostics-Abfrage so, wie der Host sie zieht.
Lies vorher im Host-Code (VS-Extension und/oder LSP) nach, **welche** Engine-Methoden mit **welchen**
Argumenten sie hier aufrufen, und spiegle das exakt.

## Harness (im Scratchpad, wiederverwendbar)
Baue ein kleines net10-Konsolen-/Testprojekt im Scratchpad, das `Nav.Language` referenziert
(Debug-Build der Engine) und:
- den realen Korpus aus `d:\tfs\main` **pfadgetreu** in den Scratchpad kopiert (analog zum
  bestehenden Perf-Harness-Setup) und alle `.nav` als Seeds lädt (`NavSolution.HasNavExtension`
  zum Filtern — `*.nav` matcht sonst auch `.navignore`),
- die Mutationsoperatoren oben anwendet und die Treiber-Kette fährt,
- jede Exception mit einer **Crash-Signatur** = `(ExceptionTyp + werfende Methode + Top-N Frames)`
  erfasst und **dedupliziert** (das ist der systematische Kern — nicht die Zahl der Crashes zählt,
  sondern die Zahl **distinkter** Signaturen),
- den auslösenden Input je **neuer** Signatur per Delta-Debugging auf ein **minimales
  reproduzierendes `.nav`** schrumpft.

## Schleife bis Konvergenz (Runden drehen, nicht brute force)
Arbeite in Runden. Pro Runde: Operatoren über den Korpus laufen lassen, neue distinkte Signaturen
sammeln, minimieren. **Nach** jeder Runde: welche Operatoren/Konstrukte haben zuletzt Neues
gebracht? Richte die nächste Runde gezielt dorthin (mehr Varianten dieses Operators / dieser
Grammatikstelle). Stoppe erst, wenn **zwei aufeinanderfolgende Runden keine neue Signatur** mehr
liefern. Optional als Konvergenz-Check: Branch-Coverage der Ziel-Namespaces messen (coverlet/
dotnet-coverage) und Mutationen gezielt auf noch **ungedeckte** Zweige lenken — nur als Steuerung,
nicht als Hauptantrieb. Falls du eine bewusste Obergrenze ziehst (Sampling, Top-N), **logge**, was
weggelassen wurde — keine stille Deckelung.

## Parallelisierung
- **Sweep NICHT auf Subagents verteilen** — die Mutations-Läufe sind Rechenarbeit: parallelisiere sie
  **im Harness** (Korpus partitionieren, mehrere Threads), deterministisch geordnet (feste
  Operator-Reihenfolge, Seeds sortiert), damit das Ergebnis reproduzierbar/nachvollziehbar bleibt.
- **Erst nach** vorliegender deduplizierter Signaturliste optional fan-out: **ein Subagent je
  distinkter Crash-Signatur**, aber **read-only** — nur Ursachenanalyse + Fix-*Entwurf* als Diff-Text.
  Nur lohnend bei vielen (>~10) distinkten Signaturen; darunter seriell bleiben.
- **Fan-out-Agents auf Fable**, der steuernde/integrierende Lauf auf **Opus**.
- **Kein paralleles Schreiben**: die entworfenen Guard-Fixes führt am Ende **ein** Agent seriell in
  Engine/Tests zusammen (parallele Edits an denselben Dateien = Clobbering).

## Token-Disziplin (harte Vorgaben)
- Der Harness schreibt Befunde als **`findings.json` auf Platte** (Signatur → minimaler Repro-Pfad →
  Top-N Frames, N≤8) plus die minimierten `.nav` als Dateien. **Crash-Dumps NIE in den Chat kippen.**
- Sweep-`stdout`/`stderr` in eine Datei umleiten; im Verlauf nur **Zähler** melden
  (X Seeds, Y Mutationen, Z **distinkte** Signaturen, davon W minimiert) — keine Roh-Ausgabe.
- **Immer erst deduplizieren, dann reasonen** — nie pro Roh-Crash analysieren.
- Jeder Fan-out-Agent bekommt **nur seinen einen `findings.json`-Eintrag** + Pfadzeiger und liest
  gezielt selbst nach — nicht den ganzen Korpus oder alle Befunde in den Kontext ziehen.
- Der Abschlussbericht referenziert `findings.json`/Repro-Dateien per Pfad, statt Inhalte einzubetten.

## Deliverables (kein Commit)
1. **Report** (`.md`, ins Repo unter `doc/` und in die Solution einhängen — siehe Konventionen):
   je Befund = Crash-Signatur, betroffener Service, minimaler auslösender Input, Kurz-Stacktrace,
   Ursachenanalyse (welcher unvorgesehene Zustand).
2. **Repro-Fixtures/Tests** je Befund in `Nav.Language.Tests` (NUnit, `net472;net10.0` beide grün),
   die den Absturz reproduzieren bzw. nach Fix das Degradieren belegen — `.nav`-Fixtures/Erwartungen
   als **Raw-Strings**, nicht als `\r\n`-Ketten.
3. **Guard-Fixes als Vorschlag**: die minimalen Absicherungen im Engine-Code, die den unvorgesehenen
   Zustand robust behandeln (Roslyn-Stil: eine treffende Diagnose statt Absturz, Folgefehler
   unterdrücken). Als Diff/Änderung anwenden, aber **nicht committen**.

## Randbedingungen (Projektkonventionen)
- Engine/Tests bauen mit Debug (`n test` für net472, `dotnet test -f net10.0`); die Solution
  nicht anfassen müssen — der Harness reicht Engine-Referenz.
- Echte Umlaute (ä ö ü ß) überall inkl. Doku/Kommentare; neue Textdateien UTF-8 **mit** BOM;
  bestehende Umlaut-Dateien vor Bearbeitung auf Win-1252/`U+FFFD` prüfen.
- Nach Abschluss: fertige Commit-Message vorschlagen — **nicht selbst einchecken**.

Beginne mit einer kurzen Bestandsaufnahme (Einstiegspunkte + Host-Aufrufe lesen), dann Harness bauen,
dann die Runden fahren. Halte mich nur bei einer echten Blockade an; ansonsten bis zur Konvergenz
durchlaufen.
````
