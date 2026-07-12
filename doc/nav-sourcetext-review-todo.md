# Nav SourceText — Review-TODO (Abarbeitungsliste)

> **Lebendes Arbeitsdokument.** Ergebnis des SourceText-Reviews (Juli 2026): `Nav.Language\Text\SourceText.cs`
> plus direkte Nachbarn (`StringSourceText`, `SourceTextLineList`, `SourceTextLine`,
> `Internal\ExtentExtensions`, `SourceTextExtensions`). Das Review fand **keine Korrektheitsbugs im
> Produktivpfad** — die Befunde sind eine unnötige Allokation pro `Empty`-Zugriff, falsche/irreführende
> Dokumentation (inkl. eines nachweislich verwirrenden Methodennamens in `ExtentExtensions`),
> asymmetrische Eingabe-Validierung mit irreführenden Exception-Parameternamen, ein sinnfreier
> Konstruktor-Guard, Enumerator-Randfälle sowie insgesamt lückenhafte XML-Doku. Alle Punkte werden
> hier in **commit-großen Steps** abgearbeitet — pro Session ein Step.
>
> **Kein Befund (geprüft und bewusst so, nicht anfassen):**
> - Der Hint-Cache `_lastLineNumber` ist trotz fehlender Synchronisation korrekt: int-Writes sind
>   atomar, der Wert ist immer eine gültige Zeilennummer, ein veralteter Hint kostet schlimmstenfalls
>   die Binärsuche. Der vorhandene Kommentar beschreibt das zutreffend.
> - `StringSourceText.Text => _memory.ToString()` ist allokationsfrei, weil `ReadOnlyMemory<char>.ToString()`
>   bei String-Backing über `string.Substring(0, Length)` läuft und das die Original-Instanz
>   zurückgibt (Fast-Path existiert in .NET Framework, .NET und im System.Memory-Package).
>   S8 macht das lediglich explizit.
> - `GetLineRange`: `extent.End` (exklusiv) auf die *Folgezeile* mit Character 0 abzubilden, wenn der
>   Extent exakt an einem Zeilenende endet, ist die übliche LSP-Semantik — kein Fehler.

## Arbeitsweise (gilt für jeden Step)

- In frischer Shell zuerst `. .\Tools\Commands\Import-NavCommands.ps1` dot-sourcen.
- Pro Step: umsetzen → Code-Review → `nav build` + `nav test` (net472) **und**
  `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0` (beide TFMs grün) →
  fertige **Commit-Message liefern** (Commit macht ausschließlich der Nutzer).
- `Nav.Language.Tests` ist multi-target (`net472;net10.0`) — neue Tests müssen auf **beiden** TFMs
  grün sein. Test-Framework ist NUnit; mehrzeilige Fixtures als Raw-Strings (`"""…"""`).
- Nach jedem Step: Status-Spalte hier fortschreiben.

## Übersicht

| # | Step | Größe | Status |
|---|---|---|---|
| S1 | Sicherheitsnetz: Boundary-Unit-Tests für `SourceText`/`GetTextLineAtPositionCore` | mittel | **erledigt** |
| S2 | `SourceText.Empty` als Singleton (`static readonly` statt `=> new`) | klein | **erledigt** |
| S3 | Doku-/Kommentar-Korrekturen (`GetTextLineAtPosition`-XML-Doku, Typos, Hint-Fenster-Kommentar) | klein | **erledigt** |
| S4 | Eingabe-Validierung `GetLocation` + ehrliche Exception-Parameternamen in `FindElementAtPosition` | klein | offen |
| S5 | `FindIndexAtOrAfterPosition` → `FindIndexAtOrBeforePosition` (Name + Doku + TODO auflösen) | klein | offen |
| S6 | `SourceTextLine`-Konstruktor: sinnfreien Guard `line > lineEnd` bereinigen | klein | offen |
| S7 | `SourceTextLineList.Enumerator`: `Current` außerhalb des Bereichs härten | klein | offen |
| S8 | Stil-Refactorings (Slice-Default-Impl, `GetTextLine`-Ternary, explizites `string`-Feld, Spacing) | klein | offen |
| S9 | Vollständige XML-Doku über die gesamte SourceText-Fläche | mittel | offen |

Reihenfolge: **S1 zuerst** (Sicherheitsnetz für alles Folgende), danach sind S2–S8 unabhängig
voneinander und einzeln committbar. S5 und S6 liegen außerhalb von `SourceText.cs`, gehören aber zum
Befund-Umfeld. **S9 als Abschluss**, nachdem S2–S8 die öffentliche Fläche stabilisiert haben —
sonst dokumentiert man Signaturen, die sich noch ändern (S3 korrigiert nur *falsche* Doku und
bleibt davon unberührt).

---

## S1 — Sicherheitsnetz: Boundary-Unit-Tests

**Befund:** `SourceTextTests` deckt die Happy-Paths gut ab (leerer Text, trailing Newline,
Spalten-Arithmetik), aber nicht die Grenzen und nicht den Hint-Cache-Pfad von
`GetTextLineAtPositionCore` (`SourceText.cs:80-108`). Bevor an Validierung, Exceptions und
Enumerator geschraubt wird (S2–S7), braucht es ein Netz, das heutiges Verhalten festnagelt.

**Umsetzung:** Neue Tests in `Nav.Language.Tests\SourceTextTests.cs`:

1. **`GetTextLineAtPosition`-Grenzen:** `position == Length` liefert die letzte Zeile (mit und ohne
   trailing Newline — einmal letzte Inhaltszeile, einmal die leere Schlusszeile); `position == -1`
   und `position == Length + 1` werfen `ArgumentOutOfRangeException`.
2. **Hint-Cache-Äquivalenz:** ein mehrzeiliger Text (≥ 10 Zeilen, gemischte `\r\n`/`\n`-Enden);
   alle Positionen `0 … Length` einmal **sequenziell vorwärts** (trifft den Hint-Pfad, Fenster +4),
   einmal in **zufälliger/rückwärtiger Reihenfolge** (erzwingt die Binärsuche) abfragen — beide
   Durchläufe müssen für jede Position dieselbe Zeile liefern. Zusätzlich gezielt: Position in Zeile
   `lastLineNumber + 3` (knapp außerhalb des effektiven Fensters) und Sprung weit zurück nach
   vorherigem Vorwärtslauf.
3. **`GetLocation` über Zeilengrenzen:** Extent, der mehrere Zeilen überspannt; Extent, der exakt an
   einem Zeilenende endet (`End` == Zeilenende → `EndLinePosition` = Folgezeile, Character 0);
   Extent `[Length, Length]` (EOF-Punkt).
4. **`Substring`/`Slice`-Grenzen:** voller Text, leerer Extent, Extent am EOF; out-of-range wirft.
5. **Zeilenstruktur bei `\r` allein und `\n` allein** (nicht nur `\r\n`): Zeilenzerlegung und
   `GetTextLineAtPosition` auf dem Trennzeichen selbst.

**Betroffen:** nur `Nav.Language.Tests\SourceTextTests.cs` (keine Produktänderung).

**Fertig, wenn:** alle neuen Tests auf beiden TFMs grün sind, ohne dass Produktcode angefasst wurde.

---

## S2 — `SourceText.Empty` als Singleton

**Befund:** `public static SourceText Empty => new StringSourceText(null, null);`
(`SourceText.cs:42`) ist eine Expression-Bodied-Property — **jeder** Zugriff allokiert eine neue
`StringSourceText` samt `Lazy` und `StringTextLineList`. Nebenwirkung: Referenzvergleiche
(`sourceText == SourceText.Empty`) schlagen immer fehl. Roslyn cached `SourceText.Empty` als
statische Instanz.

**Umsetzung:** `public static SourceText Empty { get; } = new StringSourceText(null, null);`
(oder `static readonly` Feld + Getter). Kurz prüfen, ob irgendwo im Code auf Referenzgleichheit mit
`Empty` verglichen wird (dann ist das ab jetzt verlässlich, vorher war es ein latenter Bug).

**Unit-Tests:** `Assert.That(SourceText.Empty, Is.SameAs(SourceText.Empty));` plus die bestehenden
`TestEmpty`-Asserts (Text leer, Länge 0, eine Zeile) bleiben grün. Thread-Aspekt: statischer
Initializer ist vom Runtime-Modell her sicher, kein eigener Test nötig.

**Betroffen:** `Nav.Language\Text\SourceText.cs`, `Nav.Language.Tests\SourceTextTests.cs`.

**Fertig, wenn:** Singleton-Test grün auf beiden TFMs, keine weiteren Verhaltensänderungen.

---

## S3 — Doku-/Kommentar-Korrekturen

**Befund (nur Doku/Kommentare, kein Verhalten):**

1. XML-Doku von `GetTextLineAtPosition` (`SourceText.cs:54-57`) beschreibt den falschen Parameter:
   „Liefert die Zeileninformation für die angegebene **Zeile** (zero based)" — der Parameter ist
   eine **Position** (Zeichen-Offset). Außerdem unerwähnt: `position == Length` ist bewusst erlaubt
   (EOF → letzte Zeile).
2. Typo `lineInformaton` (`SourceText.cs:74`), abgehackter Kommentar „…durchsucht-"
   (`SourceText.cs:92`).
3. Hint-Fenster-Kommentar ergänzen: das Fenster `lastLineNumber + 4` ist **effektiv +3**, weil die
   Schleife den *Start der Folgezeile* im Fenster braucht, um fündig zu werden — liegt die Position
   in Zeile `lastLineNumber + 3`, fällt sie trotz Fensters in die Binärsuche. Kein Bug (S1-Test 2
   beweist die Äquivalenz), aber die Konstante soll ehrlich dokumentiert sein. Alternativ die
   Konstante benennen (`const int HintWindow = 4`) und dort kommentieren.

**Unit-Tests:** keine neuen (Doku-only); S1-Tests sichern, dass sich nichts ändert.

**Betroffen:** `Nav.Language\Text\SourceText.cs`.

**Fertig, wenn:** Doku stimmt mit Verhalten überein, beide TFMs grün (unverändert).

---

## S4 — Eingabe-Validierung `GetLocation` + ehrliche Exception-Parameternamen

**Befund:**

1. **Asymmetrische Validierung:** `GetTextLineAtPosition` validiert die Position sauber
   (`SourceText.cs:57-63`), der zweite öffentliche Einstieg `GetLocation(TextExtent)`
   (`SourceText.cs:46-48`) reicht ungeprüft an `GetLinePositionAtPosition` →
   `GetTextLineAtPositionCore` durch. Ein Missing-Extent (`Start == -1`) oder `End > Length` fällt
   erst tief in `ExtentExtensions.FindElementAtPosition` und wirft dort eine
   `ArgumentOutOfRangeException` mit Parametername „index".
2. **Irreführende Parameternamen:** `FindElementAtPosition` (`Internal\ExtentExtensions.cs:112,123`)
   wirft `ArgumentOutOfRangeException(nameof(index))` — `index` ist eine **lokale Variable**, kein
   Parameter. Für den Aufrufer wertlos; der eigentliche Parameter heißt `position`.

**Umsetzung:**

- `GetLocation(TextExtent)` validiert am Eingang: `extent.IsMissing` sowie
  `extent.End > Length` → `ArgumentOutOfRangeException(nameof(extent))` mit sprechender Message.
  (`extent.Start ≥ 0` folgt aus `!IsMissing`; `Start ≤ End` garantiert `TextExtent` selbst.)
- In `FindElementAtPosition` beide `nameof(index)` → `nameof(position)`.
- Kurzer Blick auf Aufrufer, die sich heute auf die (falsch benannte) Exception verlassen — nicht zu
  erwarten, da Missing-Tokens `SyntaxTree == null` haben und `SyntaxToken.GetLocation()` dann gar
  nicht hier ankommt (`Syntax\SyntaxToken.cs:80-82`).

**Unit-Tests:**

- `GetLocation(TextExtent.Missing)` wirft `ArgumentOutOfRangeException` mit `ParamName == "extent"`.
- `GetLocation(TextExtent.FromBounds(0, Length + 1))` wirft ebenso.
- `GetLocation` mit gültigem Extent inkl. `[Length, Length]` funktioniert weiter (Überschneidung mit
  S1-Test 3).
- In `ExtentTests.TestFindElementAtPosition`: `ParamName == "position"` mitprüfen.

**Betroffen:** `Nav.Language\Text\SourceText.cs`, `Nav.Language\Internal\ExtentExtensions.cs`,
`Nav.Language.Tests\SourceTextTests.cs`, `Nav.Language.Tests\ExtentTests.cs`.

**Fertig, wenn:** ungültige Extents am Eingang mit sprechendem Parameternamen scheitern, beide TFMs grün.

---

## S5 — `FindIndexAtOrAfterPosition` → `FindIndexAtOrBeforePosition`

**Befund:** Name **und** XML-Doku behaupten das Gegenteil des Verhaltens
(`Internal\ExtentExtensions.cs:126-137`): „Findet den Index des ersten Tokens, dessen Start
**größer oder gleich** der angegebenen Position ist" — tatsächlich liefert `~index - 1` das
**letzte** Element, dessen Start **kleiner oder gleich** der Position ist (bestätigt durch
`ExtentTests`: Position 5 bei Extents [0,10)/[20,30) → Element [0,10)). Der ratlose TODO-Kommentar
in `GetElementsInside` (`ExtentExtensions.cs:92-94` — „Wie kann das sein, dass
FindIndexAtOrAfterPosition ein ISymbol findet, das **vor** extent.Start liegt?") ist exakt die Folge
dieses Namens: kein Rätsel, sondern das reale Verhalten der Methode.

**Umsetzung:**

- Methode umbenennen (`FindIndexAtOrBeforePosition`), XML-Doku korrigieren (inkl. Rückgabe `-1`,
  wenn **alle** Starts hinter der Position liegen).
- Alle Aufrufer anpassen (klasseninterr: `FindElementAtPosition`, `GetElementsInside`,
  `GetElementsIncludeOverlapping`).
- Den TODO-Kommentar in `GetElementsInside` durch eine erklärende Doku ersetzen: der `continue` bei
  `token.Start < extent.Start` ist **notwendig**, weil die Suche bewusst das Element *vor* dem
  Extent-Start liefern kann (Punkt jetzt selbsterklärend durch den neuen Namen).
- Den kleineren offenen TODO „Sollte bei pos < 0 eine Ausnahme geworfen werden?" (`:130`)
  entscheiden: nein — negative Positionen liefern `-1`, das dokumentieren (Verhalten heute schon so,
  S4 fängt die öffentlichen Ränder ab).

**Unit-Tests:** `ExtentTests` um explizite Grenzfälle ergänzen: Position vor dem ersten Start
(→ `-1`), Position exakt auf einem Start, Position in einer Lücke zwischen Extents (→ Element
davor), Position hinter dem letzten Start (→ letzter Index). Bestehende Tests bleiben unverändert
grün (reines Rename + Doku).

**Betroffen:** `Nav.Language\Internal\ExtentExtensions.cs`, `Nav.Language.Tests\ExtentTests.cs`.

**Fertig, wenn:** Name/Doku/Verhalten deckungsgleich, TODO-Kommentare aufgelöst, beide TFMs grün.

---

## S6 — `SourceTextLine`-Konstruktor: Guard bereinigen

**Befund:** `if (line > lineEnd) throw …` (`SourceTextLine.cs:25-27`) vergleicht eine
Zeilen**nummer** mit einem Zeichen-**Offset** — semantisch bedeutungslos (vermutlich war einmal
`lineStart > lineEnd` gemeint, was `TextExtent.FromBounds` über `IsMissing` bzw. dessen eigene
Invariante bereits abdeckt). Der Check kann nie sinnvoll anschlagen und stiftet nur Verwirrung.

**Umsetzung:** Guard ersatzlos entfernen; die verbleibenden Guards (Extent nicht missing,
`line >= 0`, `lineEnd <= sourceText.Length`) decken alles Nötige ab. Kurz verifizieren, dass
`TextExtent.FromBounds` bei `start > end` tatsächlich wirft bzw. `IsMissing` liefert — sonst dort
nachschärfen statt hier.

**Unit-Tests:** Konstruktor ist `internal` — Tests über die öffentliche Fläche: `StringSourceText`
erzeugt für diverse Texte nur gültige Zeilen (von S1-Tests abgedeckt). Falls `TextExtent.FromBounds`
nachgeschärft wird: direkte `TextExtent`-Tests in `ExtentTests`.

**Betroffen:** `Nav.Language\Text\SourceTextLine.cs`, ggf. `Nav.Language\Text\TextExtent.cs`.

**Fertig, wenn:** kein bedeutungsloser Guard mehr, Invarianten weiter gesichert, beide TFMs grün.

---

## S7 — `SourceTextLineList.Enumerator.Current` härten

**Befund:** `Current` liefert außerhalb des gültigen Bereichs `default(SourceTextLine)`
(`SourceTextLineList.cs:38-47`) — dessen `SourceText`-Property ist dann `null` **trotz non-nullable
Annotation**; jeder Zugriff auf `Span`/`ToString()` wäre ein NRE beim Konsumenten statt einer klaren
Fehlermeldung an der Quelle. Roslyns Enumerator-Pattern wirft bei ungültigem `Current`
`InvalidOperationException`. Randnotiz: `Equals`/`GetHashCode` des Enumerators werfen
`NotSupportedException` — legal (Roslyn-Muster), kann bleiben.

**Umsetzung:** `Current` wirft `InvalidOperationException`, wenn `_index` außerhalb `[0, Count)`
liegt (vor erstem `MoveNext`, nach Ende). Vorher kurz prüfen, ob es Aufrufer gibt, die sich auf das
`default`-Verhalten verlassen (nicht zu erwarten — üblich ist `foreach`, das `Current` nur nach
erfolgreichem `MoveNext` liest).

**Unit-Tests:** neuer Testblock in `SourceTextTests` (oder eigene `SourceTextLineListTests`):
`foreach` über alle Zeilen unverändert (Überschneidung mit `TextLinesTest`); manuell:
`GetEnumerator()` → `Current` vor `MoveNext` wirft; nach letztem `MoveNext == false` wirft;
`Reset` (explizit via `IEnumerator`) setzt zurück.

**Betroffen:** `Nav.Language\Text\SourceTextLineList.cs`, Tests.

**Fertig, wenn:** ungültiges `Current` klar scheitert statt still `default` zu liefern, beide TFMs grün.

---

## S8 — Stil-Refactorings (verhaltensneutral)

**Befund (Sammelposten, alles kosmetisch):**

1. `Slice(int, int)` ist abstrakt (`SourceText.cs:36`), obwohl die Basisklasse es über das ebenfalls
   abstrakte `Span` selbst anbieten kann (`Span.Slice(startIndex, length)`) — eine abstrakte Methode
   weniger, `StringSourceText.Slice` (`StringSourceText.cs:33-35`) entfällt.
2. `StringSourceText.GetTextLine` (`StringSourceText.cs:37-51`): beide Zweige unterscheiden sich nur
   im `end`-Wert — Ternary (`line == lineStarts.Length - 1 ? Length : lineStarts[line + 1]`)
   halbiert die Methode.
3. `Text => _memory.ToString()` (`StringSourceText.cs:27`) funktioniert allokationsfrei nur dank der
   Full-Range-Fast-Paths — explizit machen: `string`-Feld behalten, `_memory` daraus ableiten,
   `Text` liefert das Feld direkt.
4. `Span {get;}` (`SourceText.cs:18`): Spacing an die übrigen Properties angleichen; Feld
   `_lastLineNumber` (`SourceText.cs:78`) an den Klassenanfang bzw. direkt über
   `GetTextLineAtPositionCore` mit Kommentar gruppieren.

**Unit-Tests:** keine neuen — S1-Netz plus Bestand müssen unverändert grün bleiben (byte-neutrale
Refactorings; `Is.SameAs`-Semantik von `Text` kann als Assert ergänzt werden:
`SourceText.From(s).Text` liefert dieselbe String-Instanz `s`).

**Betroffen:** `Nav.Language\Text\SourceText.cs`, `Nav.Language\Text\StringSourceText.cs`.

**Fertig, wenn:** identisches Verhalten, weniger Fläche, beide TFMs grün.

---

## S9 — Vollständige XML-Doku über die gesamte SourceText-Fläche

**Befund:** Die Doku-Abdeckung ist lückenhaft und uneinheitlich: `SourceText` dokumentiert nur
`GetTextLineAtPosition` (und das falsch, siehe S3) — alle abstrakten Member (`FileInfo`, `Text`,
`Span`, `Length`, `TextLines`, `Slice`, Indexer), die Factory `From`, `Empty`, `Substring`,
`GetLocation` und `ToString` sind undokumentiert. `StringSourceText` ist komplett undokumentiert
(intern, aber die nicht-triviale `GetTextLine`-Logik verdient Doku). `SourceTextLineList` hat weder
Klassen- noch Member-Doku (insbesondere das Enumerator-Verhalten nach S7). `SourceTextLine` ist
teilweise dokumentiert, `SourceTextLineExtensions` gut, `SourceTextExtensions` teilweise
(`ColumnsBetweenLocations`, `GetStartColumn`, `GetEndColumn` fehlen).

**Umsetzung:** Durchgängige deutsche XML-Doku (`<summary>`, bei Bedarf `<param>`, `<returns>`,
`<exception>`, `<example>` — Stil wie in `SourceTextLineExtensions`) über:

1. `SourceText.cs` — **alle** public Member. Verträge explizit machen: Positions-/Extent-Semantik
   (End exklusiv), `position == Length` erlaubt (EOF), welche `ArgumentOutOfRangeException` wann
   fliegt (nach S4 verlässlich), `Empty`-Singleton-Garantie (nach S2), Zeilen sind lückenlos und
   decken `[0, Length]` ab.
2. `StringSourceText.cs` — Klassen-Summary (String-backed Implementierung, lazy Zeilenindex,
   Thread-Sicherheit via `PublicationOnly`) + `GetTextLine`/`StringTextLineList`.
3. `SourceTextLineList.cs` — Klassen-Summary (Zeilen lückenlos, aufsteigend, mindestens eine Zeile),
   `Count`, Indexer, Enumerator-Verhalten (inkl. S7-Exception-Vertrag).
4. `SourceTextLine.cs` — fehlende Member nachziehen (`SourceText`, `Span`, `Slice`, `Location`,
   `GetLocation`, `Start`/`End`); den Kopier-Fehler „two `TextExtent` are different" beim
   `!=`-Operator (`SourceTextLine.cs:79`) korrigieren.
5. `SourceTextExtensions.cs` — die drei undokumentierten Spalten-Methoden.
6. Keine Plan-/Step-Verweise in der Doku (Projektregel) — die Doku beschreibt den Code, nicht das
   Review.

**Unit-Tests:** keine neuen (Doku-only). Aber: jede dokumentierte `<exception>`-Aussage muss durch
einen existierenden Test (S1/S4/S7) gedeckt sein — fehlt einer, dort nachziehen statt die Aussage
abzuschwächen. Build mit unveränderten Warnungen (kein `CS1591`-Rauschen einführen).

**Betroffen:** alle Dateien unter `Nav.Language\Text\` aus dem Review-Umfang sowie
`Nav.Language\Internal\ExtentExtensions.cs` (Rest-Doku nach S5).

**Fertig, wenn:** jede public/interne nicht-triviale Deklaration der Fläche dokumentiert ist, die
Verträge mit Tests belegt sind, beide TFMs grün.
