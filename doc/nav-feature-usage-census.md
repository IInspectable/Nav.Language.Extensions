# Nav-Feature-Nutzungs-Zensus (Bestand `D:\tfs\Main`)

> **Momentaufnahme vom 2026-07-09.** Beantwortet die Frage: *Werden im gesamten `.nav`-Bestand alle
> Sprach-Features (außer den Version-2-Konstrukten) tatsächlich verwendet — oder gibt es komplett
> ungenutzte Keywords/Grammatiken?* Grundlage sind **1913** `.nav`-Dateien unter `D:\tfs\Main`.

## Methodik — echter Parser statt Textsuche

Gezählt wurde **nicht** per `grep`, sondern über den echten Nav-Parser: jede Datei wurde mit
`SyntaxTree.ParseText` geparst und der signifikante Token-Strom (`SyntaxTree.Tokens`) nach
`SyntaxTokenType` gruppiert. Damit zählen **Kommentare und String-Inhalte nicht mit** — Trivia hängt
an den Token, Zeichenketten sind `StringLiteral`. Das eliminiert genau die Fehltreffer, an denen eine
reine Textsuche scheitert. Beispiele aus diesem Bestand:

- `spontaneous` erscheint per Textsuche in 5 Dateien — **alle** in ein und demselben Kommentar
  („gedachtes Keyword spontaneous kann der Generator noch nicht generieren"). Als Token: **0**.
- `?` erscheint per Textsuche 44-mal — **alle** in Kommentaren/Strings („speichern?"). Als
  `Questionmark`-Token: **0**.
- `[code "…"]` vs. das Wort „code" in einem Kommentar — nur die echten Code-Blöcke zählen.

Alle 1913 Dateien wurden fehlerfrei geparst; **4** enthalten echte Syntaxfehler (siehe unten).

## Ergebnis in Kürze

**Nein — nicht alle Features werden verwendet.** Mehrere Keywords/Grammatiken sind im gesamten
Bestand **tot** (0 echte Tokens), weitere sind praktisch tot (nur in Test-/Framework-Dateien).

## Zensus (Token, nicht Textsuche)

Spalten: *Dateien* = Dateien mit mindestens einem echten Token, *Vorkommen* = Gesamtzahl der Token.

### Kern — flächendeckend genutzt

| Konstrukt | Token-Typ | Dateien | Vorkommen |
|---|---|---:|---:|
| `task` | `TaskKeyword` | 1864 | 10432 |
| `taskref` (inkl. Include `taskref "…";`) | `TaskrefKeyword` | 1522 | 5367 |
| `init` | `InitKeyword` | 1911 | 4560 |
| `exit` | `ExitKeyword` | 1909 | 4572 |
| `choice` | `ChoiceKeyword` | 1369 | 5639 |
| `view` | `ViewKeyword` | 1389 | 1443 |
| `on` (Trigger) | `OnKeyword` | 1395 | 11662 |
| `if` / `else` (Choice-Bedingung) | `IfKeyword` / `ElseKeyword` | 1291 / 482 | 10448 / 1171 |
| `do` (Do-Klausel) | `DoKeyword` | 392 | 2263 |
| `-->` (GoTo-Kante) | `GoToEdgeKeyword` | 1853 | 33629 |
| `o->` (Modal-Kante) | `ModalEdgeKeyword` | 1028 | 7760 |
| `using` | `UsingKeyword` | 1863 | 25318 |
| `namespaceprefix` | `NamespaceprefixKeyword` | 1906 | 2705 |
| `result` | `ResultKeyword` | 1893 | 2772 |
| `params` | `ParamsKeyword` | 1896 | 5050 |
| `base` | `BaseKeyword` | 1858 | 1945 |

Interpunktion: `{ }` `[ ]` `:` `;` `,` `< >` durchgehend genutzt (`Identifier` 198392, `StringLiteral`
17205).

### Selten — praktisch tot, aber echt verwendet

| Konstrukt | Token-Typ | Dateien | Vorkommen | Wo |
|---|---|---:|---:|---|
| `dialog` | `DialogKeyword` | 7 | 7 | verstreut (Verkauf/Faktura) |
| `[donotinject]` | `DonotinjectKeyword` | 6 | 7 | `task X [donotinject]` |
| `end` (End-Knoten) | `EndKeyword` | 3 | 9 | nur `framework…QuickTests` (Produktion nutzt `exit`) |
| `[notimplemented]` | `NotimplementedKeyword` | 2 | 3 | `taskref X [notimplemented]` (verstecktes Keyword) |
| `==>` (NonModal-Kante) | `NonModalEdgeKeyword` | 1 | 4 | nur `framework…QuickTests\WFL\MockWFS.nav` (verstecktes Keyword) |
| `[code "…"]` (inline C#) | `CodeKeyword` | 1 | 3 | nur `…\Application.Common.Shared\MessageBoxes.nav` |

### Komplett ungenutzt — 0 echte Tokens im gesamten Bestand

- **`spontaneous` / `spont`** (versteckte Keywords) — 0. Kommt nur im o.g. Kommentar vor; laut diesem
  „kann der Generator [es] noch nicht generieren".
- **`generateto`** — 0.
- **`abstractmethod`** — 0.
- **`Init`** (PascalCase-Alias von `init`, `InitKeywordAlt`) — 0. Alle 4560 Vorkommen sind
  klein geschrieben `init`.
- **Klammern `(` `)`** (`OpenParen`/`CloseParen`) — 0.
- **Fragezeichen `?`** (`Questionmark`) — 0.
- **Präprozessor-Direktiven** `#version`, `#pragma` (und jedes `#…`) — 0. Sprach-Versionierung wird
  nirgends genutzt (der gesamte Bestand läuft implizit auf Version 1).
- **Version-2-Konstrukte** (hier nur zur Bestätigung der Abwesenheit): Continuation-Kanten `--^` /
  `o-^` und `choice … [params]` — 0.

Ebenfalls 0: `SkippedTokensTrivia` (keine Panic-Mode-Recovery nötig) und `Unknown`.

## Nebenbefund — 4 Dateien mit echten Syntaxfehlern (Nav0002)

Unabhängig von der Feature-Frage; diese Dateien parsen schon unter Version 1 nicht sauber:

- `XTplusApplication\src\AlternativeMedizin.MI.Shared\AlternativeMedizin.nav` — `unexpected input '[namespaceprefix …]'`
- `XTplusApplication\src\Lagerbestand.MI.Shared\VerkaufsDisposition.nav` — `missing ';'`
- `XTplusApplication\src\Artikelstamm.MI.Shared\ArtikelBearbeiten\ArtikelBearbeiten.nav` — `unexpected input '[namespaceprefix …]'`
- `XTplusApplication\src\Artikelstamm.MI.Shared\ArtikelBearbeiten\LagerStatusMassenAendern.nav` — `unexpected input '[using …]'` sowie Tippfehler `[using using …]`

## Konsequenzen / mögliche Folgearbeit

- Kandidaten für eine **Deprecation-/Aufräum-Betrachtung** der Sprache: `spontaneous`/`spont`
  (nie generierbar gewesen), `generateto`, `abstractmethod`, der PascalCase-`Init`-Alias sowie —
  je nach Bewertung der reinen Test-/Framework-Nutzung — `end`, `==>` und inline `[code …]`.
- Der Befund ist eine **Momentaufnahme** eines konkreten Bestands; vor einem Sprach-Rückbau sollte er
  ggf. gegen weitere `.nav`-Quellen (andere Repos/Branches) gegengeprüft werden.
