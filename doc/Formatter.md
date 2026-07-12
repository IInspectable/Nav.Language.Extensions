# Nav Code-Formatter

Der Formatter bringt `.nav`-Dateien in eine einheitliche, kanonische Form — vergleichbar mit
`dotnet format`/Prettier, aber maßgeschneidert für die Nav-DSL.

## Eine Engine, alle Hosts

Der Formatter sitzt VS-frei in der Engine (`Nav.Language`) und wird von **allen Hosts geteilt**
(Visual Studio, LSP/VS Code, CLI). Eine Engine, ein Formatierungsergebnis — überall byte-identisch.

## Voraussetzung: ein vollständiger Syntaxbaum

Möglich ist das nur, weil der Parser einen **lückenlosen, verlustfreien Syntaxbaum** (full-fidelity)
liefert: **jedes** Zeichen der Quelle ist erfasst, auch der komplette Whitespace und alle Kommentare —
als **Leading-/Trailing-Trivia** an den Token. Der Baum ist damit zeichengenau zurück in den
Originaltext übersetzbar, und *das* ist die Grundlage des Formatters.

## Reiner Gap-Rewriter — die Sicherheitsgarantie

Technisch ist der Formatter als **reiner Gap-Rewriter** gebaut: signifikante Token werden **nie**
verändert, es werden ausschließlich die Trivia-Lücken dazwischen neu geschrieben. Weil der Baum jede
Lücke explizit als Trivia führt, weiß der Formatter exakt, *was* er anfassen darf und *was nicht* — das
gibt eine harte Sicherheitsgarantie (der Code kann semantisch nicht kaputtgehen) und liefert
**minimale, disjunkte Text-Changes** (nur was sich wirklich ändert), ideal für saubere Diffs und
inkrementelle LSP-Edits. Er arbeitet allein auf dem Syntaxbaum (kein Semantik-Build) und ist damit
schnell und auch auf fehlerhaftem Code robust (verbatim-Fallback für Handgelegtes).

## Funktionsumfang

Funktional deckt der Formatter neben Einzug und Whitespace-Hygiene die Nav-typischen
**Spaltenausrichtungen** ab (Transitions-Pfeile, Trigger, `if`/`else`-Bedingungen, Node-Raster,
Task-Kopf-Blöcke, Trailing-Kommentare), voll konfigurierbar.

## Qualitätssicherung

Das Verhalten ist **idempotent** (ein zweiter Lauf ändert nichts) und über Golden-/Regression-Tests
abgesichert, inklusive optionalem Selbst-Wächter, der das Ergebnis re-parst und den Token-Strom
gegenprüft.

## Nutzen fürs Team

Einheitlicher Stil ohne manuelle Handarbeit, keine Formatierungs-Diskussionen in Reviews, saubere
Diffs — und dank geteilter Engine identisches Verhalten in IDE, Editor und Build/CLI.
