# Nav.Language.ExtensionShared — XML-Doku-Kampagne (Status & Umsetzungsplan)

> **Status: IN ARBEIT (gestartet 2026-07-15).** Nach dem Abschluss der XML-Doku-Kampagnen über die
> Engine `Nav.Language` (`doc/archive/nav-features-xmldoc-status.md`) und die Roslyn-Brücke
> `Nav.Language.CodeAnalysis` (`doc/archive/nav-codeanalysis-xmldoc-status.md`) ist die **Visual-Studio-Extension
> `Nav.Language.ExtensionShared`** das nächste — und mit **254 Dateien** größte — Ziel. Zielbild: **alle
> handgeschriebenen Dateien** des geteilten Extension-Projekts durchgängig mit akkurater C#-XML-Doku
> versehen — **ohne jede Code-Änderung** — und dabei die Kanon-Begriffe des **Glossars**
> (`doc/Glossar.md`) verwenden. Vorbild und Methodik: die beiden abgeschlossenen Kampagnen (Orchestrator
> + Subagent je Batch, Gates G1–G4), **angepasst** an die Besonderheiten eines `.shproj`/Legacy-WPF-Hosts
> (siehe §3).

## 1. Ziel & Ausgangslage (Audit vom 2026-07-15)

`Nav.Language.ExtensionShared` (`.shproj`, Assembly-Ziel `net472`) ist die Editor-Integration der Nav
Language in Visual Studio: Classification, Tagging, IntelliSense, Outlining, Diagnostics, GoTo/Find
References, Command-Handling, Optionen. Sie ist bewusst **VS-/Roslyn-/`Microsoft.VisualStudio.Text`-lastig**
und fügt der geteilten Engine `Nav.Language` **nur Protokoll/UI** hinzu, keine Sprachlogik.

- **Scope: voller Ordner** (wie in beiden Vorgänger-Kampagnen) — nicht nur die CS1591-messbare
  `public`-Surface. Viele Editor-Features haben eine schmale MEF-`[Export]`-Fassade (Provider) über
  einem `internal`/`private` Maschinenraum (Tagger, Adorner, Command-Target, Presenter); genau dort
  sitzt die zu erklärende Logik, die CS1591 nicht misst.

- **254 handgeschriebene `.cs`-Dateien** in 28 Unterordnern + 7 Root-Dateien. **Doku-Dichte
  Ausgangslage:** nur 33/254 Dateien tragen überhaupt eine `///`-Zeile — die Kampagne ist überwiegend
  Greenfield.

- **Nicht Gegenstand:**
  - `Properties/AssemblyInfo.cs` (reine Assembly-Attribute — existiert hier ohnehin nicht separat).
  - Etwaiger generierter Code (`obj/`, `*.g.cs`) — kein Handdoku-Ziel.
  - Die zugehörige `.xaml`-Markup-Datei je `*.xaml.cs`; dokumentiert wird nur die **Code-Behind**-Klasse.

- **Messbare Ziellinie (Gate G2, Baseline verifiziert am 2026-07-15):**
  - **CS1591** (fehlende Doku an `public` Membern) über das Extension-Compile: **244** → Ziel **0**.
    Verteilung nach Top-Ordner/Root-Datei:

    | Ort | CS1591 |
    |---|---:|
    | `Images/` (fast alles `ImageMonikers.cs`) | 94 |
    | `GoToLocation/` | 30 |
    | Root `NavLanguagePackage.cs` | 30 |
    | `Underlining/` | 22 |
    | `LanguageService/` | 22 |
    | `Diagnostics/` | 14 |
    | `Options/` | 12 |
    | `Notification/` | 6 |
    | Root `NavLanguagePackage.Guids.cs` | 6 |
    | `QuickInfo/` | 4 |
    | `ParserService/` | 2 |
    | Root `NavLanguagePackage.AdvancedOptions.cs` | 2 |

    Die **übrigen 17 Ordner** (Common, CodeFixes, Commands, Completion, Classification, BraceMatching,
    …) sind CS1591-**frei** — ihre `public`-Fassade ist bereits (oder trivial) dokumentiert, ihr
    `internal`-Kern jedoch nicht. Sie sind trotzdem voll Gegenstand der Kampagne (Scope = voller Ordner).

  - **CS1574** (unauflösbarer `cref`): Baseline **2**, beide in Root `NavLanguagePackage.cs` (Zeilen
    78/81): `cref="ThreadHelper.JoinableTaskFactory"` löst nicht auf. → Vom Root-Batch **mitreparieren**
    (z.B. Member-`cref` korrigieren oder auf `<c>…</c>` umstellen). Sonstige CS1570–CS1584: **0**.

- **`ImageMonikers.cs`-Sonderfall (94× CS1591):** eine `static partial class` mit ~94 trivialen
  `public static ImageMoniker X => KnownMonikers.Y;`-Eigenschaften (semantischer Name → VS-Icon). Hier
  genügen **knappe Ein-Zeilen-Summaries** („Icon für …"); die Menge ist groß, der Inhalt mechanisch.

- **Stil-Referenz:** `Nav.Language\CodeGen\Shared\CodeBuilder.cs` sowie die frisch fertige Engine-/
  CodeAnalysis-Doku (dicht, korrekt: deutsch mit echten Umlauten, `<see cref="…"/>` statt Klartext-
  Typnamen, `<param>`/`<returns>` an Methoden, knappe Summaries an trivialen Membern).

## 2. Glossar-Anschluss (stützen **und** ggf. ergänzen)

Die Doku benutzt konsequent die **Kanon-Begriffe** aus `doc/Glossar.md` (Location, Referenz, Symbol,
Task, Trigger, Aufrufhierarchie, Sprungziel, Deklaration vs. Definition, ConnectionPoint, Host,
Roslyn-Brücke …). Die Extension bringt **VS-Editor-Fachbegriffe** mit, die das Glossar noch nicht führt
und die die „ggf. ergänzen"-Fläche bilden:

- **MEF-`[Export]`/Provider vs. Dienst** (`ITaggerProvider`, `IViewTaggerProvider`,
  `IAsyncCompletionSourceProvider`, …) — das VS-Erweiterungs-Kompositionsmodell.
- **Tagger / Tag / Adorner / Classifier** (`ITagger<T>`, `ITag`, `IntraTextAdornment`, `IClassifier`).
- **TextBuffer / TextSnapshot / TextView / SnapshotSpan** (`Microsoft.VisualStudio.Text`).
- **Command-Handler / Command-Target / OleCommandTarget** (die `Commands/`-Infrastruktur).
- **CommandHandlerService, Presenter (Find-References), Margin, NavigationBar, QuickInfo/Hover**.

**Arbeitsmodus:** Jeder Batch-Subagent (a) verwendet die vorhandenen Glossar-Begriffe und (b) meldet im
Report **Kandidaten für neue Glossar-Einträge** (Begriff, Kurzdefinition, Vorschlag Kanon-Schreibweise
de/en). Der Orchestrator synthetisiert daraus **nach** den Wellen eine Glossar-Ergänzung (eigener
Abschnitt „§8 VS-Extension / Editor-Integration") und liefert dafür eine eigene Commit-Message.

## 3. Besonderheit dieses Projekts — Build-/Gate-Story (WICHTIG, weicht von den Vorgängern ab)

`Nav.Language.ExtensionShared` ist ein **Shared Project (`.shproj`)** — es baut **nicht** für sich. Seine
Quelldateien werden erst im **Legacy-non-SDK-WPF-Projekt `Nav.Language.Extension2026`** (net472, VSIX)
mitkompiliert. Daraus folgt:

- **`dotnet build` baut das nicht** (VSSDK/VSIX-Toolchain) — es braucht **Full-Framework `MSBuild.exe`**.
- **`GenerateDocumentationFile=true` greift hier NICHT** (das ist eine SDK-Konvenienz; das Legacy-Projekt
  ignoriert es). Für CS1591 muss **`DocumentationFile` explizit** gesetzt werden.
- Der Doku-Build (Gate G2) läuft daher **zentral im Orchestrator**, **nicht** je Subagent. Rezept:

  ```bash
  MSB="/c/Program Files/Microsoft Visual Studio/18/Professional/MSBuild/Current/Bin/MSBuild.exe"
  SP=<scratchpad>
  "$MSB" Nav.Language.Extension2026/Nav.Language.Extension2026.csproj \
    -t:Rebuild -p:Configuration=Debug -p:BuildProjectReferences=false \
    -p:DocumentationFile="$SP/ext.xml" -v:m -nologo > "$SP/docbuild.txt" 2>&1
  grep -oE 'warning CS15[0-9][0-9]' "$SP/docbuild.txt" | sort | uniq -c
  ```

  `-p:BuildProjectReferences=false` isoliert auf das Extension-Compile (die referenzierten DLLs müssen
  einmal voll gebaut vorliegen — der erste `nav build`/Rebuild sorgt dafür). Anders als bei net472-SDK
  gibt es **nur einen** Compile-Durchlauf → die CS-Zahlen sind bereits die Unique-Zahl (kein Halbieren).

- **Konsequenz für Subagenten:** Subagenten **bauen nicht**. Sie führen nur **G1 (Doku-only-Diff)** und
  **G3 (Encoding)** über **ihre** Dateien aus und melden das Ergebnis. **G2 (Compiler als XML-Prüfer)**
  und **G4 (Gesamt-Build/Test)** macht der Orchestrator zentral nach jeder Welle.

- Encoding-Audit 2026-07-15: **alle 254 Dateien sind sauber UTF-8 mit BOM**, kein `U+FFFD`, kein rohes
  Win-1252-Byte. Die Win-1252-Falle (CLAUDE.md) ist hier also nicht akut — trotzdem je Datei G3 nach den
  Edits fahren (Tooling kann LF/BOM verlieren).

## 4. Eiserne Regeln (nicht verhandelbar)

1. **Kein Code wird verändert.** Erlaubt sind ausschließlich `///`-Zeilen (neu oder korrigiert).
   Mechanisch verifiziert durch Gate G1 — der Diff ohne `///`-Zeilen muss byte-identisch zu HEAD sein,
   inkl. Einrückung, Zeilenenden, BOM. `//`-Kommentare, `#region`, `using`, `[Export]`-Attribute bleiben
   unberührt.
2. **Nur belegbare Aussagen.** Jede Aussage muss aus dem Code, seinen Verwendungen (andere Extension-
   Ordner, Engine-Kerne, VS-SDK-Verträge, Tests) oder dem Semantikmodell ableitbar sein. Bei
   Unsicherheit: Member **unkommentiert lassen** und im Report als „offen" melden — eine Lücke ist besser
   als falsche Doku.
3. **Typ-/Member-Verweise als `cref`**, nicht als Klartext — der Compiler prüft sie (Gate G2). `public`
   ist Pflicht (via CS1591 messbar); `internal`/`protected`/`private` hier **breit mitdokumentieren** —
   der Feature-Kern ist bewusst nicht `public`. Triviale Durchreicher und offensichtliche Felder brauchen
   keine Doku.
4. **Deutsch, echte Umlaute (ä ö ü ß), UTF-8 mit BOM, CRLF.** Nach jedem Edit-Lauf Gate G3.
5. **Keine Verweise auf Plan-Steps/Batches in der Code-Doku** (CLAUDE.md) — die Doku beschreibt den Code,
   nicht den Weg dorthin.
6. **Nichts committen** — Commit macht ausschließlich der Nutzer, pro Batch, nach Review.
7. **Win-1252-Falle im Blick behalten** (CLAUDE.md), auch wenn der Bestand sauber ist: vor dem Bearbeiten
   einer Umlaut-Datei ggf. Kodierung prüfen; nach Edits BOM/CRLF sicherstellen.

## 5. Verifikations-Gates (alle Pflicht)

**G1 — Doku-only-Diff** (Git Bash; **je Subagent für seine Dateien**, zentral über alle):

```bash
fail=0
for f in $(git diff --name-only -- 'Nav.Language.ExtensionShared/**/*.cs'); do
  if ! diff -q <(git show "HEAD:$f" | grep -v '^[[:space:]]*///') \
              <(grep -v '^[[:space:]]*///' "$f") >/dev/null; then
    echo "CODE-ÄNDERUNG (nicht nur ///): $f"; fail=1
  fi
done
[ $fail -eq 0 ] && echo "OK: Diff ist doku-only"
```

**G2 — XML wohlgeformt + `cref` auflösbar** (Compiler als Prüfer; **zentral im Orchestrator**, Rezept in
§3). Auswertung gegen die **Baseline vom 2026-07-15**:
- **CS1591:** Baseline **244** → monoton sinkend, am Ende **0**.
- **CS1574:** Baseline **2** (NavLanguagePackage.cs) → **0**.
- **CS1570–CS1584 (übrige):** Baseline **0** → bleibt **0**.

**G3 — Kodierung/Zeilenenden** (Git Bash; **je Subagent** + zentral): BOM vorhanden (kein Doppel-BOM),
kein `U+FFFD`, **reines CRLF (kein bare-LF)**. **Achtung:** der doku-only-Vergleich aus G1 ist
CR-**un**empfindlich (er verglich CRLF-Arbeitskopien fehlerfrei gegen den LF-Blob); eine
Zeilenenden-Regression fällt daher **nur** über den bare-LF-Zähler auf, nicht über G1. `.gitattributes`
schreibt `* text=auto eol=crlf` vor — die Arbeitskopie muss CRLF sein.

```bash
for f in $(git diff --name-only -- 'Nav.Language.ExtensionShared/**/*.cs'); do
  b=$(head -c6 "$f" | od -An -tx1 | tr -d ' \n')
  case "$b" in efbbbfefbbbf*) echo "DOPPEL-BOM: $f";; efbbbf*) :;; *) echo "BOM fehlt: $f";; esac
  grep -q $'\xef\xbf\xbd' "$f" && echo "U+FFFD (zerstörter Umlaut): $f"
  bare=$(perl -0777 -ne 'my $lf=()=/\n/g; my $crlf=()=/\r\n/g; print $lf-$crlf;' "$f")
  [ "$bare" != "0" ] && echo "bare-LF ($bare): $f — auf CRLF normalisieren"
done
```

Eine bare-LF-Datei byte-sicher (BOM-erhaltend) auf CRLF normalisieren:
`perl -e 'for(@ARGV){local $/;open my $i,"<:raw",$_;my $c=<$i>;close $i;$c=~s/\r\n/\n/g;$c=~s/\n/\r\n/g;open my $o,">:raw",$_;print $o $c}' <datei…>`

**G4 — Build grün** (im Orchestrator): der G2-Aufruf genügt je Welle; am **Kampagnen-Ende** zusätzlich
einmal `nav build` + `nav test` als Schlussabsicherung (VSIX kompiliert vollständig, Tests grün).

## 6. Wellen- & Batch-Plan (15 Batches / 3 Wellen)

Ordner-diszipliniert (disjunkte Dateimengen → parallelisierbar). Große Ordner sind an ihrer natürlichen
Naht (`Infrastructure/`, bzw. hälftig) auf zwei Batches derselben Welle verteilt — die Dateimengen
bleiben disjunkt, daher gefahrlos parallel.

### Welle 1 — Editor-Oberfläche / Tagging / Navigation (5 Batches, 103 Dateien)

| Batch | Ordner (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **W1-B1** | `Classification/` (8) + `BraceMatching/` (4) + `BraceCompletion/` (1) + `Underlining/` (3) | 22 | **fertig** (2026-07-15) |
| **W1-B2** | `Outlining/` (9) + `Margin/` (3) + `Diagnostics/` (11) | 14 | **fertig** (2026-07-15) |
| **W1-B3** | `HighlightReferences/` (7) + `GoTo/` (8) | 0 | **fertig** (2026-07-15) |
| **W1-B4** | `GoToLocation/` (26, inkl. `Provider/`) | 30 | **fertig** (2026-07-15) |
| **W1-B5** | `CSharp/` (8) + `CallHierarchy/` (3) + `NavigationBar/` (4) + `FindReferences/` (8) | 0 | **fertig** (2026-07-15) |

### Welle 2 — Commands / CodeFixes / IntelliSense (5 Batches, 75 Dateien)

| Batch | Ordner (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **W2-B6** | `Commands/` Top-Level (15, ohne `Infrastructure/`) | 0 | **fertig** (2026-07-15) |
| **W2-B7** | `Commands/Infrastructure/` (11) | 0 | **fertig** (2026-07-15) |
| **W2-B8** | `CodeFixes/` Top-Level (18, ohne `Infrastructure/`) | 0 | **fertig** (2026-07-15) |
| **W2-B9** | `CodeFixes/Infrastructure/` (12) | 0 | **fertig** (2026-07-15) |
| **W2-B10** | `Completion/` (10) + `QuickInfo/` (9) | 4 | **fertig** (2026-07-15) |

### Welle 3 — Common / Services / Host / Root (5 Batches, 76 Dateien)

| Batch | Ordner (Dateien) | CS1591 | Status |
|---|---|---:|---|
| **W3-B11** | `Common/` Teil A (16) | 0 | **fertig** (2026-07-15) |
| **W3-B12** | `Common/` Teil B (16) | 0 | **fertig** (2026-07-15) |
| **W3-B13** | `Utilities/` (12) + `LanguageService/` (6) | 22 | **fertig** (2026-07-15) |
| **W3-B14** | `ParserService/` (3) + `SemanticModelService/` (3) + `Options/` (3) + `Notification/` (2) + `UI/` (2) + `DropHandler/` (3) | 20 | **fertig** (2026-07-15) |
| **W3-B15** | `Images/` (3) + Root (7: `NavLanguagePackage*`, `NavSolutionProvider*`, `NavSolutionSnapshot`, `NavLanguageContentDefinitions`) — inkl. **CS1574-Fix** | 132 | **fertig** (2026-07-15) |

CS1591-Summe der Spalte = 244 (Baseline). Batches mit 0 sind trotzdem voll Gegenstand — dort ist der
undokumentierte `internal`-Kern das Ziel, den CS1591 nicht misst.

## 7. Subagent-Auftrag (Vorlage)

> Du dokumentierst die Dateien eines Batches unter `Nav.Language.ExtensionShared\` mit C#-XML-Doku.
> **Dateien dieses Batches:** `<Liste>`.
>
> **Regeln (bindend):**
> - **Kein Code ändern** — ausschließlich `///`-Zeilen hinzufügen/korrigieren. Keine Umformatierung,
>   keine `using`-Änderung, kein Umsortieren, keine `//`-Kommentare/`#region`/`[Export]`-Attribute anfassen.
> - Lies zuerst `doc/archive/nav-extension-xmldoc-status.md`, Abschnitte 1–5 (Ziel, Glossar, Build-/Gate-Story,
>   Regeln, Gates), und als Stil-Referenz `Nav.Language\CodeGen\Shared\CodeBuilder.cs`.
> - **Du baust NICHT** (Legacy-VSIX-Toolchain, siehe §3) — der Orchestrator übernimmt Gate G2/G4 zentral.
> - **Glossar `doc/Glossar.md` konsultieren** und dessen Kanon-Begriffe verwenden.
> - Vor der Formulierung je Typ die **Verwendung** ansehen: Wer exportiert/ruft die Klasse (MEF-Provider,
>   Command-Target, anderer Extension-Ordner, Engine-Kern), welchen VS-SDK-Vertrag erfüllt sie
>   (`ITagger<T>`, `IOleCommandTarget`, `IAsyncCompletionSource`, …), welches Nav-Symbol/Semantikmodell
>   fließt ein. Dokumentiere die Rolle des Typs in der Editor-Integration.
> - Deutsch, echte Umlaute, `<see cref="…"/>` für alle Typ-/Member-Verweise.
> - **Doku-Block-Platzierung (Praxis-Falle CS1587):** Der `/// <summary>`-Block muss **über** einem
>   etwaigen Attributblock (`[Export]`, `[ContentType]`, `[Name]`, …) stehen — **niemals** zwischen dem
>   letzten Attribut und dem `class`/Member-Keyword (sonst „XML-Kommentar auf keinem gültigen
>   Sprachelement", CS1587). Reihenfolge: `///`-Block → Attribute → Deklaration.
> - **Trailing-Whitespace-Falle (Praxis-Falle G1):** Das Edit-Tooling strippt gern nachlaufende
>   Leerzeichen bestehender Code-/Leerzeilen (viele Dateien haben `namespace …; ` mit Endleerzeichen).
>   Das ist eine Code-Änderung → G1 schlägt an. Nach den Edits die betroffenen non-`///`-Zeilen
>   **byte-genau** an HEAD zurücksetzen (z.B. perl/python-Byte-Patch), Gate nie lockern.
> - **`cref` auf VS-SDK-Member absichern:** Member wie `IWpfTextView.Properties`/`.Closed` sind auf
>   Basistypen (`ITextView`, `IPropertyOwner`) deklariert — ein `cref="IWpfTextView.Closed"` löst **nicht**
>   auf (CS1574). Dann auf den deklarierenden Typ zeigen (`ITextView.Closed`) oder den Member weglassen.
> - **Überladene Methoden im `cref` immer mit Signatur** (`GetKeywordDescription(SyntaxToken)`), sonst
>   CS0419 (mehrdeutiger Verweis).
> - **`<param>` ist alles-oder-nichts:** dokumentierst du an einer Methode *einen* Parameter, brauchen
>   **alle** ein `<param>` (sonst CS1573). Lieber gar keine `<param>` als nur manche.
> - **Öffentliche Ctors zählen für CS1591** — auch der parameterlose WPF-`*.xaml.cs`-Ctor braucht ein
>   Ein-Zeilen-Summary; „triviale Durchreicher auslassen" gilt nur für **nicht**-`public` Member.
> - **Sichtbarkeits-Scope:** `public` ist Pflicht; `internal`/`protected`/`private` breit
>   mitdokumentieren — nur triviale Durchreicher/Felder auslassen. Bei `*.xaml.cs` nur die
>   Code-Behind-Klasse dokumentieren.
> - Nur belegbare Aussagen; Unsicheres unkommentiert lassen und im Report als „offen" listen.
> - Nach den Edits **G1 (Doku-only-Diff)** und **G3 (BOM/CRLF/kein U+FFFD)** aus §5 **nur für deine
>   Batch-Dateien** ausführen und die Ausgabe in den Report aufnehmen. BOM/CRLF wiederherstellen, falls
>   das Tooling LF hinterlassen hat.
>
> **Report (deine Rückgabe):** je Datei 1 Zeile (dokumentierte Member-Anzahl), Liste der „offen"
> gelassenen Member mit Grund, **Glossar-Kandidaten**, Gate-Ergebnisse G1/G3.

## 8. Commit-Konvention

Pro Batch ein Commit, Muster:

```
Nav-Extension: XML-Doku für <Ordner> — nur ///-Zeilen, doku-only-Diff verifiziert
```

Die Glossar-Ergänzung ist ein eigener Commit:

```
Nav-Engine: Glossar um VS-Extension-/Editor-Begriffe ergänzt
```

## 9. Fortschritts-Log

| Datum | Welle/Batch | Ergebnis |
|---|---|---|
| 2026-07-15 | — | Projektwahl `Nav.Language.ExtensionShared` (Nutzer-Auftrag: mehrwellige Kampagne, mehrere Subagents). Audit: 254 Dateien / 28 Ordner + Root; Encoding sauber (alle UTF-8-BOM). Baseline (Gate G2, isolierter Doku-Build mit explizitem `DocumentationFile`, da Legacy-non-SDK): **244× CS1591 + 2× CS1574** (NavLanguagePackage.cs). Build-/Gate-Story angepasst (G2/G4 zentral im Orchestrator, §3). 15 Batches / 3 Wellen geplant. Status-Doc + `.slnx`-Einhängung. |
| 2026-07-15 | W3-B11…B15 | Welle 3 (Common A/B, Utilities/LanguageService, Dienste, Images/Root) parallel per Subagent, 76 Dateien. Alle 5 meldeten G1 + G3 (inkl. **bare-LF=0**) grün — die Zeilenenden-Lehre aus Welle 2 hat gegriffen. Zentral **G1 doku-only** + **G3 (BOM/CRLF/bare-LF/FFFD)** über **alle 244 geänderten Dateien** byte-exakt OK. Orchestrator-Nachbesserung (vom zentralen G2 gefunden): **5× CS1574** (auf Basistyp deklarierte/aus Nachbar-Namespace stammende Member: `IWpfTextView.ViewportLeft`→`ITextView`, `NavLanguagePreferences.ShowNavigationBar` [von VSSDK-Basis geerbt] → Typ-cref + `<c>`, `NavLanguageService`/`ApplyKind.Apply` qualifiziert/`<c>`) + **1× CS0419** (`ImageMoniker` mehrdeutig zwischen `…Imaging` und `…Imaging.Interop` → voll qualifiziert). **Der CS1574-Root-Fix** (`ThreadHelper.JoinableTaskFactory`) kam aus B15. **Finaler Gate G2: 0 ExtensionShared-CS-Warnungen** (CS1591 244→0, CS1574 2→0, CS1570–84/CS0419 alle 0). |
| 2026-07-15 | Ende | **Kampagne inhaltlich abgeschlossen.** Zentrale Schlussverifikation: **G2** 0× CS15xx/CS0419 in ExtensionShared; **G4** `nav build` (volle Solution inkl. VSIX) + `nav test`. 244/254 Dateien mit XML-Doku (10 waren bei HEAD bereits voll dokumentiert). Glossar-Ergänzung (§8 VS-Extension/Editor) aus den Batch-Kandidaten synthetisiert (eigener Commit). Commit-Messages je Batch geliefert — **committen tut der Nutzer**. |
| 2026-07-15 | W2-B6…B10 | Welle 2 (Commands/CodeFixes/IntelliSense) parallel per Subagent abgearbeitet, 75 Dateien. Zentral **G1 doku-only** OK. **G3 deckte eine Zeilenenden-Regression auf:** W2-B9 (CodeFixes/Infrastructure) hatte alle 12 Dateien beim Byte-Reconcile auf **bare-LF** umgeschrieben (von G1 unbemerkt, da CR-blind) → zentral auf CRLF normalisiert (BOM erhalten). Orchestrator-Nachbesserung (vom zentralen G2 gefunden): **2× QuickInfo-`*.xaml.cs`-Ctor** (public → CS1591) mit Summary versehen; **4× CS1573** (`GetCommandState<T>` unvollständige `<param>`) ergänzt; **1× CS1574** (`ExportCommandHandlerAttribute`, cref `MetadataAttribute` → `<c>`); **1× CS0419** (`SyntaxFacts.GetKeywordDescription` mehrdeutig → Signatur-cref). **Gate G2: CS1591 178 → 174** (−4 QuickInfo), CS1587/CS1573/CS0419 = 0, CS1574 nur noch die 2 Root-Baseline-Stellen. Neue Lehren ins Doc (§3/§5/§7): bare-LF-Gate, CS0419/CS1573/public-Ctor-Regeln. Commit-Messages je Batch geliefert. |
| 2026-07-15 | W1-B1…B5 | Welle 1 (Editor/Tagging/Navigation) parallel per Subagent abgearbeitet, 101 Dateien geändert (2 CallHierarchy-Dateien schon bei HEAD voll doku'd). Neu dokumentiert überwiegend der `internal`/`private`-Kern (Tagger/Adorner/Provider/Presenter/Location-Provider). **Zentral G1 (doku-only) + G3 (BOM/CRLF) über alle 101 Dateien byte-exakt OK.** Orchestrator-Nachbesserung an 6 Stellen (vom zentralen G2 gefunden, von den Subagenten nicht baubar): **4× CS1587** (Klassen-`<summary>` stand zwischen `[Export]`-Attributen und `class` statt darüber — GoTo/HighlightReferences-Provider) korrigiert (Doku-Block über den Attributblock verschoben, G1 bleibt gültig); **2× neuer CS1574** (`cref="IWpfTextView.Properties"` in DiagnosticService, `cref="IWpfTextView.Closed"` in NavMargin → auf `IWpfTextView` bzw. `ITextView.Closed` umgestellt). **Gate G2 danach: CS1591 244 → 178** (−22 Underlining, −14 Diagnostics, −30 GoToLocation), **CS1587 0**, **CS1574 nur noch die 2 Root-Baseline-Stellen** (Wave 3). Commit-Messages je Batch geliefert. |
