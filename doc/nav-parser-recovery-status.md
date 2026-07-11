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
| 3 | **Keyword-Präfix-Rescue in Klammer-Wirten** (`EatKeywordOrSkip` + prefix-tolerantes `AtCodeDeclaration`) | offen |
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

## Step 3 — OFFEN: Keyword-Präfix-Rescue in Klammer-Wirten

**Ziel**: `[namespace …]` → `namespaceprefix`, `[usin …]` → `using` (und analog `[bas]`/`[resul]`/…) als
echte **„missing 'keyword'"**-Diagnose + Störtoken als Skip-Trivia, statt „unexpected input". Fixt
**Bild 1** (danach greift die Step-2-Kopf-Schleife) und verbessert **Bild 2** von „unexpected input" auf
„missing 'using'".

### Baustein A — Helfer `EatKeywordOrSkip`

Neben `Eat` (in der Region „Token-Strom: Cursor, Konsum, Trivia-Anhang", ~`NavParser.cs:2050`):

```csharp
// current ist ein verunglücktes Keyword (vom Dispatch per Präfix bereits so eingestuft):
// überspringen (→ wird SkippedTokensTrivia), Keyword als missing synthetisieren. Eine Diagnose.
RawToken? EatKeywordOrSkip(SyntaxTokenType keyword) {
    if (At(keyword)) {
        return Eat(keyword);
    }
    ReportMissing(Describe(keyword));       // "missing 'namespaceprefix'" — vor dem Skip verankert
    if (At(SyntaxTokenType.Identifier)) {
        _firstSignificantStart ??= CurrentStart;
        _pos++; SkipHidden();                // 'namespace' NICHT via Tok() ⇒ SkippedTokensTrivia
    }
    return null;                             // missing keyword
}
```
- **Wichtig**: Der Skip (`_pos++`) muss passieren, sonst konsumiert der folgende `Eat(Identifier)`/
  `ParseIdentifierOrString` das Störtoken als Namen und die Kaskade kehrt zurück.
- `_firstSignificantStart ??= …` wie in `Recover`/`ParseMalformedBracketDeclaration` (Root-Extent/
  Versions-Direktiv-Platzierung).

### Baustein B — Präfix-tolerantes Erkennungs-Prädikat

`AtCodeDeclaration` (~`NavParser.cs:1987`) ist heute `At(OpenBracket) && PeekType(1) == keyword`.
Erweitern um einen Präfix-Zweig: wenn `PeekType(1)` ein **Identifier** ist, dessen Text ein **echtes
Präfix** (`keywordText.StartsWith(identText, Ordinal) && 0 < identText.Length < keywordText.Length`) des
erwarteten Keywords ist. Dazu:
- Ein `PeekRaw(int n)` analog `PeekType` (liefert das n-te sichtbare `RawToken`, um den Identifier-Text
  über `_sourceText.Substring(raw.Extent)` zu ziehen). `PeekType` ist die Vorlage (`NavParser.cs:2023`).
- Keyword-Text: die Konstanten in `SyntaxFacts` (`NamespaceprefixKeyword="namespaceprefix"`,
  `UsingKeyword="using"`, …). **Achtung**: `SyntaxFacts.GetText(type)` liefert für Keywords `null` —
  nicht darüber gehen. Am einfachsten eine kleine Map `SyntaxTokenType → kanonischer Text` (oder direkt
  die Keyword-Konstante an der Aufrufstelle mitgeben).

`ParseCodeNamespaceDeclaration`/`ParseCodeUsingDeclaration` (und die übrigen `ParseCode*Declaration`)
das `Eat(<keyword>)` auf `EatKeywordOrSkip(<keyword>)` umstellen.

### Verkabelung
- Die Step-2-Kopf-Schleife braucht **keine** Code-Änderung: sobald `AtCodeDeclaration(NamespaceprefixKeyword)`
  auch `[namespace …]` matcht, läuft `ParseCodeNamespaceDeclaration` (skippt `namespace`, missing
  keyword), etabliert den Namespace, und die vorhandene `while`-Using-Schleife greift → **Bild 1 gefixt**.
- Die Wirte `task`/`taskref`/`init`/`taskNode`/`choice` nutzen `AtCodeDeclaration` in ihren
  `ParseCodeDeclarations`-Delegaten → Rescue greift dort automatisch mit (`[bas]`→base, `[resul]`→result …).

### Ambiguität / Fallstricke
- **`taskref`-Wirt**: `[n…]` ist Präfix von **namespaceprefix UND notimplemented**. Die Delegat-Reihenfolge
  entscheidet deterministisch (erst geprüftes gewinnt). Dokumentieren; für Recovery unkritisch.
- **`notimplemented`** ist ein **verstecktes** Keyword (`IsHiddenKeyword`) — es ist im Lexer trotzdem ein
  echtes Keyword; Rescue nur für die **sichtbaren** anbieten oder bewusst mitnehmen (entscheiden).
- Prüfen, dass der Präfix-Zweig **nicht** feuert, wenn `PeekType(1)` bereits das echte Keyword eines
  **anderen** Decls ist (dort ist es ein Keyword-Token, kein Identifier → Zweig verlangt Identifier → ok).

### Korpus (Step 3)
- Neuer Fall für **Bild 1**: `[namespace Company.Product]` + valide usings → erwartet
  `missing 'namespaceprefix'` (statt „unexpected input"), Namespace-Knoten entsteht, usings als Knoten,
  `namespace` als Skip-Trivia. (Beim Umbau darauf achten: die **Step-2**-`.diag` von
  `MalformedUsingBracketBleed.nav` ändert sich von „unexpected input '[usin …]'" auf „missing 'using'" —
  Golden neu erzeugen und der Änderung zustimmen; das ist die beabsichtigte Verbesserung.)

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
