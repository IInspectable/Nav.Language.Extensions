# Nav-Parser Recovery-Härtung — Status & Handoff

Ziel: den handgeschriebenen `NavParser` beim **Tippen** stabiler recovern lassen. Ausgangspunkt waren
sechs VS-Screenshots, in denen ein unvollständiges/vertipptes Keyword oder eine fehlende `]` **ganze,
für sich korrekte** Bereiche rot markierte.

## Diagnose (eine Wurzel, zwei Facetten)

Alle Fälle haben **eine** Wurzel: *ein Identifier steht dort, wo der Dispatch ein bestimmtes Keyword
erwartet* — beim Tippen durchläuft jedes Keyword eine Vorstufe, die als gültiger Identifier lext
(`t`/`ta`/`tas` vor `task`; `namespace` vor `namespaceprefix`; `usin` vor `using`). Keywords werden im
`NavLexer` **exakt** aus einer Dictionary-Tabelle gematcht (`Keywords`), Präfixe sind also Identifier.

- **Facette 1 — Dispatch ist keyword-exakt** (Bilder 1, 2, 4, 6): ein Beinah-Treffer heißt „gar nicht
  dieses Konstrukt", der äußere Panic-Mode (`Recover`/`SkipMalformedBrackets`) schluckt dann das ganze
  Konstrukt. `tas SimpleTask …` → gesamter Task-Body rot; `[namespace …]` → Namespace **und alle**
  Usings rot; `[usin …]` → alle folgenden Usings rot.
- **Facette 2 — Recovery-Anker ignorieren die Zeilenstruktur** (Bild 3): fehlendes `]` blutet in die
  Folgezeile, dort dann irreführend „unexpected input '['" statt „missing ']'".

## Entschiedene Design-Gabeln (Nutzer, per AskUserQuestion)

1. **Beide Hebel** umsetzen (Zeilen-Anker/Struktur **und** Keyword-Rescue).
2. **Keyword-Rescue = Roslyn-Modell**: den Identifier **nicht** zum Keyword umfärben, sondern ein
   **nullbreites Missing-Keyword** synthetisieren und das Störtoken (`tas`) als **SkippedTokensTrivia**
   überspringen. Das nutzt die vorhandene Maschinerie: `Eat` synthetisiert bei Mismatch bereits ein
   Missing-Token; ein nicht via `Tok(...)` angehängtes Token wird in `FinalizeTrivia`/`FoldSkippedRun`
   automatisch zu `SkippedTokensTrivia`.
3. **Disambiguierung nur über echtes Präfix** (kein Edit-Distanz/Tippfehler): der Präfix-Check dient
   ausschließlich dazu, zu entscheiden, **welches** Keyword gemeint war (welche Parse-Methode, welcher
   Diagnose-Text). Er gilt nur an Stellen, an denen ein Identifier strukturell **nie** gültig ist
   (nach `[` im Kopf/Code-Deklaration; Top-Level-Member).

## 4-Step-Plan

| Step | Inhalt | Status |
|---|---|---|
| 1 | **Zeilen-Anker** in `ClosesBracketRegion` | **FERTIG, committet `299eb806`** |
| 2 | **Verschränkte Kopf-Recovery** (usings Klammer-für-Klammer) | **FERTIG, committet `840cb1d7`** |
| 3 | **Keyword-Präfix-Rescue in Klammer-Wirten** (`EatKeywordOrSkip` + prefix-tolerantes `AtCodeDeclaration`) | **FERTIG, uncommittet** |
| 4 | **Keyword-Präfix-Rescue Top-Level-Member** (`tas`→`task`) | offen |

Workflow (CLAUDE.md): je Step Code-Review + Build/Test, dann **Commit-Message als Text** liefern —
**nie selbst committen**. Der Nutzer committet nach seinem Review.

---

## Step 1 — FERTIG (committet `299eb806`)

`NavParser.ClosesBracketRegion()` (in `Nav.Language/Syntax/NavParser.cs`) erhielt den Zusatz-Anker
`|| (OnNewLine() && At(SyntaxTokenType.OpenBracket))`: beginnt eine neue Zeile mit `[`, gehört das zu
einer neuen `[ … ]`-Deklaration → der laufenden Klammer fehlt nur das `]`. `EatCloseBracket` meldet
danach das treffende „missing ']'" am Zeilenende statt „unexpected input '['" in der Folgezeile.
Fehlalarmfrei, weil innerhalb einer Klammer auf einer Folgezeile nie legitim ein `[` steht (mehrzeilige
Inhalte wie `[code …]`-Literale sind konsumiert, bevor das `]` gesucht wird).

Korpus: `Nav.Language.Tests/Syntax/Tests/IncompleteUsingBracketBleed.nav` (+ 4 Golden) — pinnt **eine**
Diagnose `missing ']'` (3,19), beide `[using …]` als eigene Knoten.

Commit-Message:
```
Parser-Recovery: Zeilen-Anker für unvollständige [ … ]-Klammern

ClosesBracketRegion bricht die Klammer-Recovery zusätzlich ab, wenn eine
neue Zeile mit '[' beginnt — dieses '[' gehört dann zu einer neuen
[ … ]-Deklaration, der laufenden Klammer fehlt nur das ']'. Bisher lief
die Recovery über die Zeilengrenze in die (für sich korrekte) nächste
Deklaration und meldete dort irreführend "unexpected input '['"; jetzt
meldet EatCloseBracket das treffende "missing ']'" am Ende der laufenden
Zeile, und die Folgezeile parst sauber.

Innerhalb einer Klammer steht auf einer Folgezeile nie legitim ein '['
(mehrzeilige Inhalte wie [code …]-Literale sind konsumiert, bevor das ']'
gesucht wird) — der Anker ist damit fehlalarmfrei.

Neuer Golden-Korpus IncompleteUsingBracketBleed.nav pinnt den Fall.
Kein bestehender Golden-Output ändert sich.
```

## Step 2 — FERTIG (committet `840cb1d7`)

`ParseCodeGenerationUnit` parst die `[using …]` jetzt verschränkt mit der Klammer-Recovery (Muster von
`ParseCodeDeclarations`): sobald ein `namespaceprefix` den Kopf eröffnet, wird jede folgende Klammer
einzeln betrachtet — gültiges `[using …]` → Knoten, alles andere (`[usin …]`, leeres `[]`, Fremd-Keyword)
→ `ParseMalformedBracketDeclaration` (isoliert), danach werden die usings erneut angeboten.

**Grammatik-treu**: usings ohne vorangehendes `namespaceprefix` gehören **nicht** in den Kopf. Ein erster
(zu leniente) Versuch, sie namespace-unabhängig zu parsen, ließ den Formatter-Test
`NavFormattingErrorGoldenTests.StrayTokensBetweenMembersOnlyFreezeThatOneGap` kippen (fälschlich eine
Header-Leerzeile vor dem ersten Task). Konsequenz: **Bild 1** (`[namespace …]`, verunglücktes
`namespaceprefix`) ist erst mit **Step 3** gefixt — die Keyword-Rescue erkennt den Namespace, danach
greift die Step-2-Schleife für die usings. Step 2 fixt **Bild 2** (malformes `[usin]` **innerhalb** eines
etablierten Headers).

Korpus: `Nav.Language.Tests/Syntax/Tests/MalformedUsingBracketBleed.nav` (+ 4 Golden) — namespace + zwei
gültige usings als Knoten, dazwischen **eine** Diagnose `unexpected input '[usin …]'`.

Commit-Message:
```
Parser-Recovery: Kopf-usings verschränkt mit Klammer-Recovery

Der Using-Kopf der CodeGenerationUnit wird — sobald ein namespaceprefix
ihn eröffnet — wie die Code-Deklarationen der Wirte (ParseCodeDeclarations)
Klammer für Klammer geparst: eine hier nicht zuzuordnende Klammer (eine
malforme/unfertige using-Klammer wie `[usin …]`, ein leeres `[]`, ein
Fremd-Keyword) wird als eigene Fehlerproduktion isoliert übersprungen und
die usings danach erneut angeboten. Bisher brach das erste nicht-`[using]`
den ganzen Using-Kopf ab, sodass alle folgenden — für sich wohlgeformten —
usings in der Member-Schleife als Fehler aufliefen.

Grammatik-treu bleibt: usings ohne vorangehendes namespaceprefix gehören
nicht in den Kopf (bewusst, sonst formatiert der Formatter sie fälschlich
als Header). Ein verunglücktes namespaceprefix selbst (`[namespace …]`)
etabliert daher noch keinen Kopf — das behandelt die Keyword-Rescue.

Neuer Golden-Korpus MalformedUsingBracketBleed.nav pinnt den Fall.
Kein bestehender Golden-Output ändert sich.
```

---

## Step 3 — FERTIG (uncommittet): Keyword-Präfix-Rescue in Klammer-Wirten

**Erreicht**: `[namespace …]` → `namespaceprefix`, `[usin …]` → `using` (und analog `[bas …]`/`[resul …]`/…)
als echte **„missing 'keyword'"**-Diagnose + Störtoken als Skip-Trivia, statt „unexpected input". **Bild 1
gefixt** (danach greift die Step-2-Kopf-Schleife), **Bild 2** verbessert von „unexpected input" auf
„missing 'using'".

### Umgesetzte Bausteine

**A — `SyntaxFacts.GetKeywordText(SyntaxTokenType)`** (neu): Reverse-Map Token-Typ → kanonisches
Keyword-Literal (`NamespaceprefixKeyword`, `UsingKeyword`, …). Gegenstück zu `GetText` (nur Punctuation,
für Keywords `null`); zusammen liefern beide den Text jedes Token-Typs mit festem Literal. `Describe`
konsultiert jetzt beide (`GetText(type) ?? GetKeywordText(type)`), damit „missing 'namespaceprefix'"
statt „missing 'NamespaceprefixKeyword'" gemeldet wird. (Kein bestehender Golden hatte eine Keyword-Typ-
„missing"-Meldung — die Änderung ist an den Goldens verifiziert regressionsfrei.)

**B — `EatKeywordOrSkip(SyntaxTokenType keyword)`** (neu, neben `Eat`): steht das Keyword an → wie `Eat`;
sonst `ReportMissing(Describe(keyword))` (nullbreit, an der Einfügestelle) und das als Identifier gelexte
Störtoken überspringen (`_pos++` ohne `Tok()` ⇒ `SkippedTokensTrivia`). Der Skip **muss** geschehen,
sonst schluckt der folgende Name-Parse (`ParseIdentifierOrString`/`ParseCodeType`/…) das Störtoken als
Namen.

**C — Präfix-tolerantes `AtCodeDeclaration`**: zusätzlich zum exakten `PeekType(1) == keyword` ein
Präfix-Zweig — `PeekType(1)` ist Identifier **und** sein Text ist **echtes Präfix** des Keywords
(`IsKeywordPrefix`: nicht leer, kürzer, `StartsWith` Ordinal). Nach einem `[` an Code-Deklarations-
Position ist ein Identifier strukturell nie gültig → gefahrlos als gemeintes, unfertiges Keyword deutbar.
Neue Helfer `PeekRaw(int n)`/`PeekText(int n)` (analog `PeekType`) ziehen den Identifier-Text.

**Verkabelung**: **alle zehn** `ParseCode*Declaration` (namespace, using, notimplemented, donotinject,
abstractmethod, code, base, generateto, params, result) stellen ihr Leit-`Eat(<keyword>)` auf
`EatKeywordOrSkip(<keyword>)` um — sobald `AtCodeDeclaration` per Präfix matcht, **muss** die Deklaration
das Störtoken skippen. Die Step-2-Kopf-Schleife und die `ParseCodeDeclarations`-Delegaten der Wirte
(task/taskref/init/taskNode/choice) brauchten **keine** Änderung: der Rescue greift über `AtCodeDeclaration`
automatisch mit.

### Ambiguität / Fallstricke (umgesetzt)
- **`taskref`-Wirt**: `[n…]` ist Präfix von **namespaceprefix UND notimplemented** — die Delegat-Reihenfolge
  (`namespaceprefix` zuerst) entscheidet deterministisch. Beim Tippen harmlos: nur `[n` matcht
  namespaceprefix (schon `[no` ist keins mehr), self-heilt mit jedem weiteren Zeichen.
- **`notimplemented`** (verstecktes Keyword) nimmt am Rescue **bewusst mit** teil (generischer Zweig,
  einheitlich); während des Tippens unkritisch.
- Der Präfix-Zweig feuert nie auf das echte Keyword eines **anderen** Decls: dort steht ein Keyword-**Token**
  (kein Identifier), der Zweig verlangt Identifier. Nach `[` ist ein Identifier ohnehin immer ein Fehler.
- **Empty-Body-Trade** (akzeptiert): rein `[bas]` (ohne Inhalt) liefert jetzt zwei Diagnosen
  (`missing 'base'` + `missing 'identifier'` aus dem Typ-Parse) statt der einen „unexpected input '[bas]'".
  Der häufige Fall `[bas X:Y]` verbessert sich (eine treffende Diagnose); während des Tippens self-heilt es.

### Korpus (Step 3)
- **`MalformedNamespacePrefixBracket.nav`** (Bild 1): `[namespace Company.Product]` + valide usings →
  **eine** Diagnose `missing 'namespaceprefix'` (1,2), Namespace-Knoten entsteht, beide usings als Knoten,
  `namespace` als Skip-Trivia.
- **`MalformedBaseKeywordPrefix.nav`** (generischer Wirt-Beweis): `task A [bas Foo: Bar] { … }` →
  `missing 'base'` (1,9), `CodeBaseDeclarationSyntax` mit `Foo`/`Bar`, `bas` als Skip-Trivia.
- **`MalformedUsingBracketBleed.nav`** (Step-2-Golden aktualisiert): `.diag` von „unexpected input
  '[usin …]'" → **`missing 'using'`** (4,2); `.tree`/`.tokens`/`.trivia`: `[usin System.Linq]` parst jetzt
  als `CodeUsingDeclarationSyntax`, `usin` als Skip-Trivia. Beabsichtigte Verbesserung.

Verifiziert: net10 1715 grün, net472 1775 grün; `ParsesAndRoundTripsAllTypingPrefixes` (Tipp-Präfix-Stress)
über den ganzen Korpus grün. Kein anderer Golden ändert Inhalt (nur die dokumentierte `.trivia`-EOL-Churn).

---

## Step 4 — OFFEN: Keyword-Präfix-Rescue Top-Level-Member

**Ziel**: `tas SimpleTask [base …] { … }` → `task`-Definition mit **missing** `task`-Keyword, `tas` als
Skip-Trivia; der ganze Task-Body parst normal. Fixt **Bild 6**.

Stelle: die Member-Schleife in `ParseCodeGenerationUnit` (~`NavParser.cs:277`), Bedingung
`At(TaskrefKeyword) || At(TaskKeyword)`. Ergänzen: wenn `At(Identifier)` und der Identifier-Text **echtes
Präfix** von `task`/`taskref` ist **und die Form bestätigt** (Folge-Token ist Identifier — der Task-Name),
als Member behandeln. `ParseMemberDeclaration`/`ParseTaskDefinition`/`ParseTaskDeclaration` müssen ihr
Leit-Keyword über `EatKeywordOrSkip` konsumieren.

### Ambiguität / Form-Bestätigung
- `tas` ist Präfix von **task UND taskref**. Tie-Break: fester Vorrang **`task`** (häufiger; `taskref` erst
  ab eindeutigem Präfix `taskr…`). Dokumentieren.
- Form-Guard `At(Identifier) && PeekType(1) == Identifier` verhindert, dass ein loser Top-Level-Identifier
  (ohne folgenden Namen) fälschlich als Task-Kopf gilt — sonst normal in `Recover(_atMemberOrEof)`.
- Für `taskref` zusätzlich die bestehende Include-vs-Decl-Disambiguierung beachten (`ParseMemberDeclaration`:
  `taskref` + StringLiteral ⇒ include, sonst decl). Beim Rescue steht kein echtes `taskref`-Token; sinnvoll
  ist, den Rescue auf `task`-Definition zu beschränken (häufigster Fall) oder die Folge-Form auszuwerten.

### Korpus (Step 4)
- Neuer Fall: `tas SimpleTask [base X: Y] { init; exit End; init --> End; }` → `missing 'task'`, `tas` als
  Skip-Trivia, TaskDefinition mit vollständigem Body.

---

## Test-/Build-Workflow (Fallstricke — verifiziert)

- **`nav test` BAUT NICHT** (`Tools/Commands/Functions/Invoke-Test.ps1` ruft nur den NUnit-Runner gegen
  `bin\Debug\`). Vor jedem net472-Testlauf **`nav build`** (MSBuild.exe), sonst laufen die Tests gegen eine
  **stale** `Pharmatechnik.Nav.Language.dll` und zeigen falsch das alte Verhalten. (Genau diese Falle trat
  in Step 1 auf.) `Nav.Language` ist ein einzelnes netstandard2.0-Assembly — es gibt keine echte
  net472/net10-Divergenz; abweichende Ergebnisse = stale Copy.
- **net10 schnell**: `dotnet test Nav.Language.Tests\Nav.Language.Tests.csproj -f net10.0 [--filter …]`
  (baut selbst). Syntax-Filter: `--filter "FullyQualifiedName~Syntax"`.
- **Golden neu erzeugen**: den `[Explicit]`-Test `SyntaxGoldenTests.UpdateGolden` laufen lassen:
  `dotnet test … -f net10.0 --filter "Name=UpdateGolden"` (vorher `dotnet build Nav.Language`, damit die
  frische Engine wirkt). Schreibt `.tokens/.tree/.diag/.trivia` je Korpus-Datei neu.
- **Nach UpdateGolden**: `git checkout -- 'Nav.Language.Tests/Syntax/Tests/*.trivia'` — UpdateGolden
  schreibt LF, das Repo hält CRLF → **alle** `.trivia` erscheinen „geändert", sind aber reine
  Zeilenende-Churn. Danach mit `git diff --numstat -- '…/*.diag' '…/*.tokens' '…/*.tree' '…/*.trivia' |
  awk '$1!=0||$2!=0'` prüfen, dass **kein bestehender** Golden echten Inhaltsdiff hat (leer = nur
  Zeilenende).
- **Neue `.nav`-Korpusdateien** als **UTF-8 mit BOM** anlegen (z.B. `printf '\xEF\xBB\xBF…'`). Sie werden
  aus `TestDataDirectory.Resolve(@"Syntax\Tests")` (Quellordner) gelesen, kein Copy nötig.
- **Robustheits-Absicherung**: `SyntaxGoldenTests.ParsesAndRoundTripsAllTypingPrefixes` parst **jedes
  Tipp-Präfix** jeder Korpus-Datei und verlangt lückenlosen Round-Trip — der beste Wächter gegen
  Recovery-Regressionen. Jede Rescue muss round-trip-treu bleiben (Skip-Token → Trivia, nichts geht verloren).

## Schlüssel-Dateien / -Stellen

- `Nav.Language/Syntax/NavParser.cs` — Parser. Relevant: Header/Member-Schleife (`ParseCodeGenerationUnit`,
  ~`260`), `AtCodeDeclaration` (~`1987`), `Eat` (~`2050`), `EatCloseBracket` (~`2073`), `Recover` (~`2126`),
  `ReportMissing`/`ReportUnexpected` (~`2159`/`2219`), `ClosesBracketRegion` (~`2014`), `OnNewLine`
  (~`2193`), `PeekType` (~`2023`), `ParseMalformedBracketDeclaration` (~`1704`), `ParseCodeDeclarations`
  (~`1668`).
- `Nav.Language/Syntax/CodeBlockFacts.cs` — `VisibleDeclarationKeywords(host)` (erwartete Keyword-Strings
  je Wirt), `CodeBlockHost`.
- `Nav.Language/Syntax/SyntaxFacts.cs` — Keyword-Konstanten (kanonischer Text); `GetText` liefert für
  Keywords `null`.
- `Nav.Language/Syntax/NavLexer.cs` — `Keywords`-Dictionary (exakter Keyword-Match; Präfixe sind Identifier).
- `Nav.Language.Tests/Syntax/SyntaxGoldenTests.cs` — Golden-Harness (4 Stränge + Struktur-Invarianten +
  Tipp-Präfix-Stress); `UpdateGolden` `[Explicit]`.
- `Nav.Language.Tests/Syntax/Tests/*.nav(+.diag/.tokens/.tree/.trivia)` — Korpus.
