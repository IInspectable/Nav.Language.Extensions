# Nav.Language.CodeAnalysis — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: ABGESCHLOSSEN (2026-07-15, uncommittet).** Alle 3 Batches (B1–B3) fertig; **43× CS1591 → 0**
> unter den 18 Handdoku-Dateien, doku-only-Diff byte-exakt (G1), BOM/CRLF sauber (G3), normaler Build
> 0 Warnungen/0 Fehler (G4). Das **Glossar** (`doc/Glossar.md`) wurde um **§7 CodeAnalysis /
> Roslyn-Brücke** ergänzt (Annotation/Tag, AnnotationReader, LocationFinder, WfsReferenceFinder,
> WFS-/WFL-Klasse, nav-lose Klasse, Begin-Interface, Roslyn-Brücke) inkl. A–Z-Index. Commit macht der
> Nutzer (pro Batch + Glossar).
>
> **Kontext & Zielbild.** Nach dem vollständigen Abschluss der engine-weiten
> XML-Doku-Kampagne über `Nav.Language` (`doc/archive/nav-features-xmldoc-status.md`) ist die **Roslyn-Brücke
> `Nav.Language.CodeAnalysis`** das nächste Ziel: der engine-nächste Ring, der Nav-Symbole mit dem
> generierten C#-Code verknüpft (Nav ↔ Roslyn). Ziel: **alle handgeschriebenen Dateien** des Projekts
> durchgängig mit akkurater C#-XML-Dokumentation versehen — **ohne jede Code-Änderung** — und dabei
> auf das **Glossar** (`doc/Glossar.md`) stützen sowie es um die neuen Roslyn-Brücken-Begriffe
> **ergänzen**. Vorbild und Methodik: `doc/archive/nav-features-xmldoc-status.md` (Orchestrator + Subagent je
> Batch, Gates G1–G4).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-15)

`Nav.Language.CodeAnalysis` (net472, Assembly `Pharmatechnik.Nav.Language.CodeAnalysis`) ist die
bewusst VS-/Roslyn-gekoppelte Brücke zwischen der Nav-Sprachwelt und dem daraus generierten C#-Code.
Sie referenziert `Nav.Language`, `Nav.Utilities` und `Microsoft.CodeAnalysis`. Drei Kerne dominieren:

- **`Annotation/`** — der Nav↔C#-Anker. Die generierten WFS-/WFL-Klassen tragen Tags (Annotations),
  die auf die Nav-Herkunft zurückzeigen (`NavTaskAnnotation`, `NavInitAnnotation`, `NavExitAnnotation`,
  `NavTriggerAnnotation`, `NavChoiceAnnotation`, `NavChoiceCallAnnotation`, `NavInitCallAnnotation`,
  `NavMethodAnnotation`, `NavInvocationAnnotation`). `AnnotationReader` liest sie über das Roslyn-
  `SemanticModel` eines `Document` aus.
- **`FindSymbols/`** — `LocationFinder` löst ein Nav-Symbol (Task, Trigger, Choice, Init, Exit) in die
  zugehörige Roslyn-`Location` im generierten Code auf (und umgekehrt); Trägertypen `CallerLocation`,
  `AmbiguousLocation`, Fehlerfall `LocationNotFoundException`.
- **`FindReferences/`** — `WfsReferenceFinder` findet Referenzen eines Nav-Symbols im generierten
  C#-Code (Roslyn-`FindReferences`), inklusive der nav-losen Sonderklassen (`ClassInfo`/`NavlessClasses`).
- **`Common/`** — schmale Roslyn-Interop-Helfer (`LinePositionExtensions`, `TextSpanExtensions`).

- **Scope: voller Ordner** (wie in der Engine-Kampagne) — nicht nur die CS1591-messbare
  `public`-Surface. Die drei Kerne (`AnnotationReader`, `LocationFinder`, `WfsReferenceFinder`) sind
  überwiegend `internal`/`private` Maschinerie hinter einer schmalen `public`-Fassade; genau dort sitzt
  die zu erklärende Brücken-Logik, die CS1591 nicht misst.

- **Handdoku-Dateien (18):**
  - `Annotation/` (10): `AnnotationReader`, `NavTaskAnnotation`, `NavInitAnnotation`, `NavExitAnnotation`,
    `NavTriggerAnnotation`, `NavChoiceAnnotation`, `NavChoiceCallAnnotation`, `NavInitCallAnnotation`,
    `NavMethodAnnotation`, `NavInvocationAnnotation`.
  - `FindSymbols/` (4): `AmbiguousLocation`, `CallerLocation`, `LocationFinder`, `LocationNotFoundException`.
  - `FindReferences/` (2): `WfsReferenceFinder`, `WfsReferenceFinder.ClassInfo`.
  - `Common/` (2): `LinePositionExtensions`, `TextSpanExtensions`.
- **Nicht Gegenstand:**
  - `Properties/AssemblyInfo.cs` (reine Assembly-Attribute, keine Doku-Fläche).
  - `Annotation/generated/NavTaskAnnotationVisitor.Generated.cs` — **generierter Code** (T4 aus
    `NavTaskAnnotationVisitor.Generated.tt`). Wie `obj/`-`*.g.cs` in der Engine-Kampagne **kein
    Handdoku-Ziel**; ein Handdoku würde beim nächsten Generatorlauf überschrieben.

- **Messbare Ziellinie (Gate G2, Baseline verifiziert am 2026-07-15):**
  - **CS1591** (fehlende Doku an `public` Membern) unter den Handdoku-Dateien: **43** → Ziel **0**.
    Verteilung: `Annotation/` 26 (`NavTaskAnnotation` 5, `NavInitCallAnnotation`/`NavInvocationAnnotation`/
    `NavMethodAnnotation` je 3, übrige je 2), `FindSymbols/` 9 (`AmbiguousLocation` 3, `CallerLocation`/
    `LocationFinder`/`LocationNotFoundException` je 2), `Common/` 6 (je 3), `FindReferences/` 2 (je 1).
  - **CS1570–CS1584** (XML kaputt / `cref` unauflösbar / `<param>`-Mismatch): Baseline unter den
    Handdoku-Dateien **0** — nichts mitzureparieren.
- **CS1591 unterschätzt die reale Surface** (der `internal`/`private`-Kern zählt nicht — die großen
  Reader/Finder-Methoden). Ziellinie ist zweiteilig: **(a)** CS1591 unter den Handdoku-Dateien = 0
  **und** **(b)** der `internal`-Kern durchgängig sinnvoll dokumentiert — geprüft per Stichproben-Review
  je Batch.

- **Stil-Referenz:** `Nav.Language\CodeGen\Shared\CodeBuilder.cs` und die frisch fertige Engine-Doku
  (dichte, korrekte Bestands-Doku: deutsch mit echten Umlauten, `<see cref="…"/>` statt Klartext-
  Typnamen, Roslyn-Analogien wo tragfähig, `<param>`/`<returns>` an Methoden, knappe Ein-Zeilen-
  Summaries an trivialen Properties).
- **Bestands-Doku beachten:** `LocationFinder` (53 `///`), `CallerLocation` (11), `NavChoiceCallAnnotation`
  (6), `NavTaskAnnotation` (4) sind bereits teil-dokumentiert — Lücken füllen, Bestehendes nur bei
  sachlichem Fehler anfassen.

## 2. Glossar-Anschluss (stützen **und** ergänzen)

Die Doku benutzt konsequent die **Kanon-Begriffe** aus `doc/Glossar.md` (z.B. Location, Referenz,
Symbol, Task, Aufrufhierarchie, Sprungziel, Deklaration vs. Definition, Trigger, ConnectionPoint).
`Nav.Language.CodeAnalysis` bringt jedoch **neue Fachbegriffe** mit, die das Glossar noch nicht führt —
diese sind die „ggf. ergänzen"-Fläche:

- **Annotation** (die `Nav*Annotation`-Marker; im Bestand teils „Tag" genannt) — der Roslyn-seitige
  Rückverweis aus generiertem C# auf die Nav-Herkunft.
- **AnnotationReader / Annotation-Visitor** (`INavTaskAnnotationVisitor`).
- **Roslyn-Brücke**-Begriffe: `Document`, `SemanticModel`, `Solution` (Roslyn), `WFS`/`WFL`-Klasse,
  „nav-lose Klasse" (`NavlessClasses`).
- **LocationFinder / CallerLocation / AmbiguousLocation**.
- **WfsReferenceFinder**.

**Arbeitsmodus:** Jeder Batch-Subagent (a) verwendet die vorhandenen Glossar-Begriffe und (b) meldet im
Report **Kandidaten für neue Glossar-Einträge** (Begriff, Kurzdefinition, vorgeschlagene Kanon-
Schreibweise de/en). Der Orchestrator synthetisiert daraus **nach** den Batches eine Glossar-Ergänzung
(neuer Abschnitt bzw. Einträge in §1/§6) und liefert dafür eine eigene Commit-Message. Terminologie-
Konflikte (z.B. „Tag" vs. „Annotation") entscheidet der Orchestrator und markiert sie im Glossar.

## 3. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder korrigiert).
   Mechanisch verifiziert durch Gate G1 — der Diff ohne `///`-Zeilen muss byte-identisch zu HEAD sein,
   inkl. Einrückung, Zeilenenden, BOM. `//`-Kommentare, `#region`, `using`, `[NotNull]`-Attribute bleiben
   unberührt.
2. **Nur belegbare Aussagen.** Jede Doku-Aussage muss aus dem Code, seinen Verwendungen (Aufrufer in
   LSP/MCP/Extension, Tests) oder dem Semantikmodell ableitbar sein. Bei Unsicherheit: Member
   **unkommentiert lassen** und im Batch-Report als „offen" melden — eine Lücke ist besser als falsche
   Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2).
   **Scope je Sichtbarkeit:** Alle `public`-Typen und -Member sind **Pflicht** (via CS1591 messbar).
   `internal`/`protected`/`private` Member werden hier **breit mitdokumentiert** — der Brücken-Kern ist
   bewusst nicht `public`. Triviale Durchreicher-Properties und offensichtliche Felder brauchen keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF.** Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** — die Doku beschreibt den Code, nicht den
   Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, pro Batch, nach Review.
7. **Win-1252-Falle beachten** (CLAUDE.md): vor dem Bearbeiten einer Umlaut-Datei die Kodierung prüfen;
   bei Win-1252/`U+FFFD` erst `git checkout` + `nav fixenc`, dann Edits. (Audit 2026-07-15: alle 18
   Handdoku-Dateien sind sauber UTF-8 mit BOM, keine rohen Win-1252-Bytes.)

## 4. Verifikations-Gates (alle Pflicht)

**G1 — Doku-only-Diff** (Git Bash): Der Diff darf ausschließlich aus `///`-Zeilen bestehen. Byte-exakter
Vergleich — schlägt das Gate flächig an, ist meist ein Zeilenenden-/BOM-Schaden die Ursache; dann den
Schaden beheben, **nie** das Gate lockern.

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language.CodeAnalysis/**/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; **`--no-incremental`** ist Pflicht,
sonst verschluckt der inkrementelle Build die Warnungen). Der Doku-Build zieht die referenzierten
Projekte mit hoch (`Nav.Utilities`, Quellgeneratoren) — deren CS1591 sind **nicht** unser Scope; nur
Warnungen mit Pfad `\Nav.Language.CodeAnalysis\` und **ohne** `\obj\`/`\generated\` zählen:

```bash
dotnet build Nav.Language.CodeAnalysis/Nav.Language.CodeAnalysis.csproj -c Debug \
  -p:GenerateDocumentationFile=true --no-incremental 2>&1 > /tmp/docbuild.txt
grep -F '\Nav.Language.CodeAnalysis\' /tmp/docbuild.txt \
  | grep -Fv '\obj\' | grep -Fv '\generated\' \
  | grep -oE 'warning CS15[0-9][0-9]' | sort | uniq -c
```

Auswertung gegen die **Baseline vom 2026-07-15** (jede Warnung erscheint doppelt — net472 kompiliert in
zwei Durchläufen; die Unique-Zahl ist die Hälfte):

- **CS1591:** Baseline **43** → monoton sinkend, am Ende **0**.
- **CS1570–CS1584:** Baseline **0** → bleibt **0**.

**G3 — Kodierung/Zeilenenden** (Git Bash): BOM vorhanden, kein `U+FFFD`, CRLF intakt.

```bash
for f in $(git diff --name-only -- 'Nav.Language.CodeAnalysis/**/*.cs'); do
  head -c3 "$f" | od -An -tx1 | grep -q 'ef bb bf' || echo "BOM fehlt: $f"
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
done
```

**G4 — Build grün** (im Orchestrator): der G2-Aufruf genügt; am Kampagnen-Ende zusätzlich einmal
`nav build` + `nav test` als Schlussabsicherung.

## 5. Batch-Plan & Status

Ordner-diszipliniert (disjunkte Dateimengen → parallelisierbar), nach Aufwand balanciert (die drei
großen Kerne verteilen sich auf B1/B2/B3).

| Batch | Ordner (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **B1 — Annotation** (10) | `Annotation/AnnotationReader` (564 LOC) + 9 Marker (`NavTaskAnnotation`, `NavInitAnnotation`, `NavExitAnnotation`, `NavTriggerAnnotation`, `NavChoiceAnnotation`, `NavChoiceCallAnnotation`, `NavInitCallAnnotation`, `NavMethodAnnotation`, `NavInvocationAnnotation`) | 26 | **fertig** (2026-07-15) |
| **B2 — FindSymbols** (4) | `FindSymbols/LocationFinder` (782 LOC, 53 `///`) + `CallerLocation`, `AmbiguousLocation`, `LocationNotFoundException` | 9 | **fertig** (2026-07-15) |
| **B3 — FindReferences + Common** (4) | `FindReferences/WfsReferenceFinder` (508 LOC) + `WfsReferenceFinder.ClassInfo`; `Common/LinePositionExtensions`, `TextSpanExtensions` | 8 | **fertig** (2026-07-15) |

Die CS1591-Spalte summiert auf 43 (Baseline). B3 ist trotz niedriger CS1591-Zahl inhaltlich groß — es
ist der undokumentierte `internal`-Kern von `WfsReferenceFinder`, den CS1591 nicht misst.

## 6. Subagent-Auftrag (Vorlage)

> Du dokumentierst die Dateien eines Ordner-Batches unter `Nav.Language.CodeAnalysis\` mit C#-XML-Doku.
> **Dateien dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine Umformatierung,
>   keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare/`#region`/`[NotNull]` anfassen.
> - Lies zuerst `doc/archive/nav-codeanalysis-xmldoc-status.md`, Abschnitte 1–4 (Ziel, Glossar-Anschluss, Regeln,
>   Gates), und als Stil-Referenz eine bereits fertige Datei, z.B. `Nav.Language\CodeGen\Shared\CodeBuilder.cs`.
> - **Glossar `doc/Glossar.md` konsultieren** und dessen Kanon-Begriffe verwenden (Location, Referenz,
>   Symbol, Task, Aufrufhierarchie, Sprungziel, Trigger, ConnectionPoint, Deklaration vs. Definition …).
> - Vor der Formulierung je Typ die **Verwendung** ansehen: Wer ruft die Klasse (LSP-/MCP-/Extension-
>   Host, andere Ordner), welche Tests fixieren das Verhalten, welches Nav-Symbol / welcher Roslyn-Typ
>   fließt ein. Dokumentiere die Rolle des Typs in der Nav↔C#-Brücke.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `internal`/`protected`/`private` hier **breit
>   mitdokumentieren** — nur triviale Durchreicher/Felder auslassen.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Vor dem Bearbeiten je Datei die Kodierung prüfen (Win-1252-Falle, CLAUDE.md); nach den Edits
>   BOM/CRLF wiederherstellen, falls das Tooling LF hinterlassen hat.
> - Danach Gates G1–G3 aus dem Status-Dokument **nur für deine Batch-Dateien** ausführen und die Ausgabe
>   in den Report aufnehmen.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der „offen"
> gelassenen Member mit Grund, **Glossar-Kandidaten** (neue Begriffe mit Kurzdefinition + Vorschlag
> Kanon-Schreibweise), Gate-Ergebnisse G1–G3.

## 7. Commit-Konvention

Pro Batch ein Commit, Muster:

```
Nav-CodeAnalysis: XML-Doku für <Ordner> — nur ///-Zeilen, doku-only-Diff verifiziert
```

Die Glossar-Ergänzung ist ein eigener Commit:

```
Nav-Engine: Glossar um Roslyn-Brücken-Begriffe (CodeAnalysis) ergänzt
```

## 8. Fortschritts-Log

| Datum | Batch | Ergebnis |
|---|---|---|
| 2026-07-15 | — | Projektwahl `Nav.Language.CodeAnalysis` (Nutzer-Entscheid). Plan + Audit. Baseline (Gate G2) kalibriert: **43× CS1591** unter den 18 Handdoku-Dateien, **0× CS1570–84**. Ausgeklammert: `Properties/AssemblyInfo.cs` und der T4-generierte `Annotation/generated/NavTaskAnnotationVisitor.Generated.cs` (43 eigene CS1591 — generierter Code, kein Handdoku-Ziel; Nebenbefund: die `.tt` gibt keinen `<auto-generated>`-Header aus). Scope: voller Ordner inkl. `internal`-Kern. 3 Batches (B1–B3) an Subagenten vergeben. |
| 2026-07-15 | B1–B3 | Alle 3 Batches parallel per Subagent abgearbeitet. Neu dokumentiert v.a. der `internal`/`private`-Kern: B1 Annotation (~62 Member: `AnnotationReader`-Maschinerie inkl. `NavTag`-Hilfstyp + 9 Marker), B2 FindSymbols (30 Member: `LocationFinder`-Auflösung inkl. 6 öffentl. Überladungen + Location-Builder, `CallerLocation`/`AmbiguousLocation`/`LocationNotFoundException`), B3 FindReferences+Common (23 Member: `WfsReferenceFinder` inkl. `NavlessClasses`/`ClassInfo`, Roslyn-Interop-Helfer). Selbstverschuldete Trailing-WS-/CRLF-Reste je Batch byte-exakt zurückgesetzt; eingebrachte CS1574 (`SymbolFinder`-/`CodeInfo`-crefs) mitrepariert. |
| 2026-07-15 | Ende | Zentrale Verifikation: **G1 doku-only** über alle 18 Dateien byte-exakt OK; **G2** `--no-incremental`-Doku-Build → **0× CS1591** (Baseline 43→0), **0× CS1570–84**; **G3** BOM/CRLF sauber, kein `U+FFFD` (reiner CRLF, keine lone-LF); **G4** normaler Build 0 Warnungen/0 Fehler. **Glossar** um **§7 CodeAnalysis / Roslyn-Brücke** ergänzt (9 Einträge + A–Z-Index; Kanon-Entscheid „Tag = XML-Element, Annotation = C#-Objekt"). **Kampagne abgeschlossen** — Commit durch Nutzer. |
