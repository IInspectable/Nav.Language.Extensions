# Inside Parsing: Technical Deep Dive

Dieses Dokument erklärt den **Nav-Parser** („Kolibri") von oben nach unten. Als **Leitfaden** dient die
Fehlertoleranz — die eine Anforderung, die ein Editor-Parser nicht verhandeln kann: *jede* Eingabe, auch
eine halb getippte, muss einen vollständigen, brauchbaren Baum ergeben. An ihr entlang bauen wir das Bild
auf: vom Roh-Token-Strom des Lexers ([§2](#2-der-lexer-vom-zeichen-zum-rawtoken)) über die
Sichtbarkeits-Welten, die den Parser-Cursor einfach halten
([§3](#3-die-drei-sichtbarkeits-welten-des-token-stroms)), den Direktiven-Vorlauf
([§4](#4-der-direktiven-vorlauf-navdirectiveparser)) und den rekursiven Abstieg
([§5](#5-der-rekursive-abstieg-navparser)) hinunter zu den zwei Recovery-Grundmechanismen
([§6](#6-fehlertoleranz-i-missing-token-und-panic-mode)), ihren drei Verfeinerungen
([§7](#7-fehlertoleranz-ii-die-drei-verfeinerungen-gegen-das-ausbluten)) und der Trivia-Finalisierung, die
am Ende alles wieder lückenlos zusammensetzt
([§8](#8-die-trivia-finalisierung-finalizetrivia-und-buildtrivia)).

Alle Datei-Angaben beziehen sich auf `Nav.Language/Syntax/`.

---

## 1. Überblick — eine Engine-Idee: Full Fidelity durch Konstruktion

Der Parser ist ein handgeschriebener **Single-Pass Recursive-Descent-Parser**: ein Durchlauf über den
flachen Token-Strom des Lexers baut direkt den immutablen `SyntaxNode`-Baum — es gibt kein
Zwischen-Objektmodell (die frühere ANTLR-Pipeline brauchte zwei: mutables Parse-Tree plus Visitor, dazu
eine nachträgliche Trivia-Rekonstruktion). Die Pipeline hat fünf Stufen:

```
SourceText
    ↓
NavLexer.Lex             →  ImmutableArray<RawToken>   (flach, lückenlos: signifikante Token + Trivia gemischt)
    ↓
NavDirectiveParser       →  List<DirectiveRun>          (je #-Lauf ein strukturierter Direktiv-Knoten, vorab)
    ↓
NavParser (Parse*-Regeln) →  Knoten-Baum + konsumierte Token + Diagnosen
    ↓
FinalizeTrivia           →  ein geteiltes Trivia-Array, Leading/Trailing je Token (Roslyn-Regel)
    ↓
SyntaxTree               →  Root + Tokens + Diagnostics (immutable)
```

Der rote Faden durch das gesamte Design ist **Full Fidelity durch Konstruktion**: *Kein Zeichen der
Eingabe geht je verloren.* Jedes Zeichen landet in genau **einer** von drei Heimaten:

1. im **Extent eines konsumierten Tokens** (der Parser hat es einer Grammatikregel zugeordnet),
2. in einer **Trivia**, die als Leading/Trailing an einem konsumierten Token hängt (Whitespace,
   Zeilenende, Kommentar),
3. in einer **strukturierten Trivia** — eine Präprozessor-Direktivzeile als `DirectiveTrivia`, ein vom
   Parser übersprungener oder lexikalisch unbekannter Lauf als `SkippedTokensTrivia`; beide halten ihre
   Token lokal an einem eigenen Knoten.

Aneinandergehängt ergeben diese Ausschnitte wieder exakt den Eingabetext — der **Round-Trip** ist der
Prüfstein, der auf jeder Ebene wieder auftaucht (und in den Tests wörtlich geprüft wird,
[§11](#11-sicherheitsnetze-und-zahlen)). Die Eigenschaft ist nicht nachträglich geprüft, sondern
konstruktiv: der Lexer liefert eine lückenlose Kachelung ([§2.1](#21-rawtoken-und-die-lückenlose-kachelung)),
und keine der nachgelagerten Stufen wirft je ein Token weg — Recovery *verschiebt* Text nur von Heimat 1
nach Heimat 3.

Die zweite Zusage folgt direkt daraus: **jede Eingabe ergibt einen vollständigen Baum.** Syntaxfehler
landen als `Diagnostic` im Ergebnis, nie als Exception; fehlende Pflicht-Token werden nullbreit
synthetisiert, überzählige übersprungen ([§6](#6-fehlertoleranz-i-missing-token-und-panic-mode)). Merke
dir beide Zusagen als Prüfsteine — jede Design-Entscheidung der folgenden Kapitel zahlt auf mindestens
eine davon ein.

---

## 2. Der Lexer: vom Zeichen zum RawToken

### 2.1 RawToken und die lückenlose Kachelung

`NavLexer.Lex` liefert **alle** Token als flache `ImmutableArray<RawToken>` — signifikante Token und
Trivia gemischt, abgeschlossen durch genau ein nullbreites `EndOfFile`. Ein `RawToken` ist ein
Wert-Struct aus lexikalischem Typ und `TextExtent`:

```csharp
readonly record struct RawToken(SyntaxTokenType Type, TextExtent Extent) {
    public bool IsTrivia => SyntaxFacts.IsLexicalTrivia(Type);
}
```

Anders als ein `SyntaxToken` trägt es **keine** kontextabhängige `TextClassification` und keinen Parent —
beides vergibt erst der Parser ([§5.3](#53-klassifikation-dasselbe-lexem-viele-farben)). Zwei
Eigenschaften der Folge tragen alles Weitere:

- **Lückenlos:** die Extents kacheln den Quelltext ohne Loch und ohne Überlappung. Das ist die
  Konstruktions-Wurzel der Full Fidelity — und der Grund, warum die **Start-Position** eines Tokens
  später als Identitätsschlüssel taugt ([§8.1](#81-warum-nach-dem-parsen)).
- **Fortschritts-Garantie:** die Hauptschleife wirft eine `InvalidOperationException`, falls ein Scan die
  Position nicht vorrückt — der Lexer kann per Konstruktion nicht endlos Token allozieren. Der
  `Unknown`-Fallback (jedes nicht erkannte Zeichen wird als einzelnes `Unknown` konsumiert) stellt
  sicher, dass dieser Fall nie eintritt: es gibt keine Eingabe, für die kein Zweig zuständig ist.

### 2.2 Die Dispatch-Reihenfolge trägt Bedeutung

`ScanToken` erkennt genau ein Token; die Prüf-Reihenfolge ist kein Zufall:

```
Whitespace → NewLine → '//' → '/*' → Mehrzeichen-Kanten (-->, --^, ==>, o->, o-^)
    → '#' (nur am Zeilenanfang) → '"' → Einzelzeichen-Punctuation → Identifier/Keyword → Unknown
```

Zwei Stellen sind ordnungs-kritisch:

- **Mehrzeichen-Kanten vor dem Identifier-Scan** — sonst fräße der Identifier-Scanner das `o` aus
  `o->`/`o-^` und aus der Kante würde `oXyz`-Präfix plus Müll.
- **`#` nur als erstes Nicht-Whitespace-Zeichen seiner Zeile** (`AtLineStart`, Rückwärts-Scan bis zur
  Zeilengrenze — Einrückung ist erlaubt). Ein `#` mitten in der Zeile ist *keine* Direktive, sondern ein
  gewöhnliches `Unknown` (→ `Nav0000`, [§10](#10-das-diagnostik-inventar)) — dieselbe Regel wie in C#,
  und sie wird bewusst im **Lexer** erzwungen, damit der Direktiven-Sub-Parser
  ([§4](#4-der-direktiven-vorlauf-navdirectiveparser)) sie nie prüfen muss.

Zeilenenden kennen fünf Varianten (`\r\n` als Längstmatch, sonst einzeln `\r`, `\n`, NEL U+0085,
LS U+2028, PS U+2029); Whitespace ist die Unicode-`Zs`-Liste der alten Grammatik plus Tab/VT/FF —
Zeilenenden zählen dort *nicht* dazu, sie sind eigene `NewLine`-Token (die Trivia-Anhang-Regel in
[§8.2](#82-die-roslyn-anhang-regel) hängt an genau dieser Trennung).

### 2.3 Drei Zerfalls-Regeln für Unfertiges

Beim Tippen ist ständig etwas unabgeschlossen. Der Lexer behandelt das nach einer einheitlichen Idee —
**minimal zerfallen, nichts verschlucken**:

| Konstrukt | unfertig | Verhalten |
|---|---|---|
| `"…`-String ohne schließendes `"` | `"abc` + Zeilenende/EOF | nur das öffnende `"` wird `Unknown`, `abc` lext normal weiter |
| `/* …` ohne `*/` | bis EOF | nur das öffnende `/` wird `Unknown`, `* …` lext normal weiter |
| `// …`-Kommentar | endet an der Zeile | Kommentar stoppt **vor** dem Zeilenende; das Zeilenende wird ein eigenes `NewLine`-Token |

Die ersten beiden verhindern, dass ein einziges vergessenes Zeichen den Rest der Datei in ein
Riesen-Token saugt (und damit jede lokale Recovery unmöglich macht). Die dritte ist die Voraussetzung
für die Trailing-Trivia-Regel ([§8.2](#82-die-roslyn-anhang-regel)): nur wenn das Zeilenende ein eigenes
Token ist, lässt sich „Trailing = bis einschließlich des ersten Zeilenendes" überhaupt ausdrücken.

Eine bewusste Eigenheit erbt die Kommentar-Abspaltung von der alten Pipeline: bei CRLF wird nur das
**letzte** Newline-Zeichen abgespalten — das `\r` bleibt Teil des Kommentar-Tokens, das `NewLine`-Token
ist nur das `\n`. NEL/LS/PS beenden die Kommentar-Zeile zwar, bleiben aber vollständig im Kommentar
(kein `NewLine`-Token). Beides ist byte-genau in den Golden-Snapshots gepinnt
([§11](#11-sicherheitsnetze-und-zahlen)); wer `.trivia`-Dumps liest, sollte es kennen.

String-Literale haben ihre eigene Terminator-Nuance: `"`, CR, LF, LS und PS beenden das Literal — NEL
(U+0085) bleibt bewusst *darin* (`IsStringLiteralTerminator`), während es außerhalb sehr wohl ein
Zeilenende ist. Auch das ist Verhaltens-Parität zur alten Grammatik.

### 2.4 Der Directive-Mode (ScanPreprocessor)

Ab einem gültigen `#` wechselt der Lexer für den Rest der Zeile in einen Direktiv-Modus und liefert die
Zeile in **ganzen, typisierten Läufen**: das `HashToken`, das Direktiven-Schlüsselwort als eigener
Token-Typ (`VersionKeyword`/`PragmaKeyword` per `PreprocessorKeywords`-Tabelle, jedes andere Wort als
generisches `PreprocessorKeyword`), reine Ziffern-Läufe als `PreprocessorNumber`, alles Übrige
(Zwischenraum, Satzzeichen) als `PreprocessorText`. **Jedes** Zeilenende beendet die Direktive als
`PreprocessorNewLine` — Längstmatch `\r\n`, sonst ein Einzelzeichen; auch reine-LF-Dateien terminieren
also zeilengenau.

Der Gewinn dieser Vorab-Typisierung: der Direktiven-Sub-Parser ([§4](#4-der-direktiven-vorlauf-navdirectiveparser))
dispatcht über **Token-Arten** statt über Substring-Vergleiche, und die Einfärbung der Direktiv-Token
folgt allein aus ihrem Typ — es braucht keinerlei Sonderbehandlung im Hauptparser.

### 2.5 Keyword-Erkennung: exakt, mit Allokations-Guard

Ein Identifier-Lauf (Buchstaben inkl. Umlauten, Ziffern, `.`, `_` — Autorität
`SyntaxFacts.IsIdentifierCharacter`) wird per Dictionary-Lookup gegen die ~25 Wort-Schlüsselwörter
geprüft. **Exakt** — Präfixe sind Identifier; genau darauf baut später der Keyword-Präfix-Rescue auf
([§7.3](#73-keyword-präfix-rescue-eatkeywordorskip)). Der Lookup läuft nur, wenn der Lauf überhaupt ein
Keyword sein *kann*:

```csharp
var couldBeKeyword = true;
while (_pos < _length && SyntaxFacts.IsIdentifierCharacter(_text[_pos])) {
    var ch = _text[_pos];
    if (ch < 'a' || ch > 'z') {
        couldBeKeyword = false;
    }
    _pos++;
}
```

Alle Schlüsselwörter bestehen ausschließlich aus `a`–`z` und sind 2–15 Zeichen lang — sobald ein
Großbuchstabe, eine Ziffer, ein `.` auftaucht, entfällt der `Substring` fürs Dictionary. Bei nahezu allen
Identifiern echter Dateien (Typnamen, gepunktete Namespaces, camelCase) war genau dieser Substring die
häufigste vermeidbare Allokation des Lexers ([§11](#11-sicherheitsnetze-und-zahlen), Lektion 4).

---

## 3. Die drei Sichtbarkeits-Welten des Token-Stroms

Der rohe Strom mischt alles; der Parser will davon fast nichts sehen. Die Auflösung ist eine
Klassifikation in drei Welten:

| Welt | Token-Typen | wer sie verarbeitet | Heimat im Ergebnis |
|---|---|---|---|
| **signifikant** | Keywords, Identifier, Punctuation, Kanten, StringLiteral, `EndOfFile` | der Parser-Cursor | konsumiert → `SyntaxTree.Tokens`; nicht konsumiert → Skip-Trivia |
| **lexikalische Trivia** | Whitespace, NewLine, Kommentare | niemand (Cursor überspringt) | Leading/Trailing der Token ([§8](#8-die-trivia-finalisierung-finalizetrivia-und-buildtrivia)) |
| **versteckt-strukturiert** | Präprozessor-Token, `Unknown` | Direktiven-Vorlauf bzw. Skip-Faltung | strukturierte Trivia (`DirectiveTrivia` / `SkippedTokensTrivia`) |

Die Parser-Sicht darauf ist **eine** Komposition:

```csharp
static bool IsHidden(SyntaxTokenType type) {
    return SyntaxFacts.IsLexicalTrivia(type) ||
           type == SyntaxTokenType.Unknown   ||
           SyntaxFacts.IsPreprocessorToken(type);
}
```

Bewusst eine Komposition der drei Teilmengen statt einer eigenen Aufzählung — jede Menge hat genau eine
Pflege-Stelle in `SyntaxFacts`. Darauf sitzt die **Cursor-Invariante**: `_pos` zeigt *stets* auf ein
parser-sichtbares Token (signifikant oder `EndOfFile`). `SkipHidden()` stellt sie nach jedem Vorrücken
wieder her — Konsum, Recovery-Skip, egal wo. Die Folge ist enorm vereinfachend: **keine einzige
`Parse*`-Methode weiß, dass Trivia existiert.** `PeekType(n)`/`PeekRaw(n)`/`PeekText(n)` liefern das
n-te *sichtbare* Token; `At(type)`/`At0`/`AtEof` prüfen das aktuelle. Hinter dem Strom-Ende antwortet
alles konsistent mit `EndOfFile` — es gibt keinen „Index out of range"-Rand.

Dass `Unknown` in dieselbe Unsichtbarkeits-Klasse fällt wie Trivia, ist eine bewusste
Robustheits-Entscheidung: ein einzelnes Sonderzeichen mitten im Code (`init ¤ A;`) ist für die
*Grammatik* bedeutungslos und soll keine Recovery lostreten — es wird still übersprungen und in der
Finalisierung als Skip-Trivia gefaltet (dort entsteht auch seine `Nav0000`-Diagnose,
[§8.4](#84-falten-direktiven-und-skip-läufe)).

---

## 4. Der Direktiven-Vorlauf (NavDirectiveParser)

### 4.1 Warum ein Vorlauf

Präprozessor-Direktiven sind — nach Roslyn-Vorbild — **Trivia**: der Hauptparser sieht sie nie. Aber
*strukturierte* Trivia: eine Direktivzeile ist ein kleiner Syntaxteilbaum, erreichbar über
`trivia.GetStructure()`. Für Nav ist das Modell bewusst zustandslos vereinfacht (kein konditionaler
`#if`-Stack, kein Disabled-Text): jeder `#`-Lauf ist vom Lexer vollständig und selbst-abgegrenzt
typisiert ([§2.4](#24-der-directive-mode-scanpreprocessor)) und lässt sich daher **vorab** parsen — der
`NavParser`-Konstruktor ruft den Sub-Parser, *bevor* der eigentliche Cursor loszieht.

Der Sub-Parser ist bewusst eine eigene Klasse mit eigenem Mini-Cursor über genau einen Lauf
`[hashIndex, end)`: er kennt **nur** die generische Direktiv-Syntax (Keyword-Dispatch) und keinerlei
Insertion-/Recovery-Mechanik des Hauptparsers — ein Direktiv-Lauf ist stets vollständig, es gibt hier
kein Missing-Token. Die Grenzziehung hat einen zweiten Schnitt: **Platzierungs-Semantik** (ist die
Versions-Direktive *wirksam*?) gehört nicht hierher, sondern in einen nachgelagerten Schritt des
Hauptparsers ([§4.4](#44-platzierungs-semantik-resolvelanguageversion)).

### 4.2 Der Dispatch und die Nav300x-Familie

`ParseDirective` schaut auf das Token unmittelbar hinter dem `#` — eine Token-*Art*, kein Textvergleich:

- **`VersionKeyword`** → `ParseVersion` → immer ein `VersionDirectiveSyntax`. Das Argument ist genau
  **ein** `PreprocessorNumber`-Token, dem bis zum Zeilenende nur Zwischenraum folgen darf;
  `NavLanguageVersion.TryParse` validiert den Wert. Jede Abweichung meldet genau eine `Nav3002` —
  positions-präzise: fehlender Wert nullbreit **hinter dem `version`-Schlüsselwort** (Insertion-Punkt
  nach Roslyn-Konvention, [§6.1](#61-insertion-eat-und-das-missing-token)), ungültiger Wert über der
  Zahl, überzähliger Rest (`#version 1 2`) über dem Rest — wobei die gültige Zahl dann **gilt** und nur
  der Rest als `Skiped` ausgegraut wird (`skipFrom`-Mechanik in `PopulateLocalTokens`, das
  direktiv-lokale Pendant zu den Panic-Mode-Skips des Hauptparsers).
- **`PragmaKeyword`** → `ParsePragma`. Es gibt derzeit keine bekannten Pragmas: ein Subjekt hinter
  `pragma` ergibt eine wirkungslose `BadDirectiveTriviaSyntax` samt `Nav3001` („Unknown pragma '…'"),
  ein nacktes `#pragma` die generische `Nav3000`. Der Zweig ist der Erweiterungspunkt für spätere
  Pragma-Subjekte.
- **alles andere** (unbekanntes Wort, gar kein Wort) → `BadDirectiveTriviaSyntax` + `Nav3000`.

Alle Direktiv-Diagnosen spannen die **ganze Direktiv-Breite ohne Zeilenende** (`DirectiveExtent`) — so
markiert die Squiggle die Direktive statt nur das `#`, in VS (Extent-basiert) wie in LSP-Clients
(Zeilenpositions-basiert, daher `SourceText.GetLocation` statt der nullbreiten lexikalischen Location).

### 4.3 DirectiveRun — das Übergabe-Artefakt

Je Lauf entsteht ein `DirectiveRun` (`DirectiveRun.cs`): Roh-Index-Bereich `[RawStart, RawEnd)` (damit
`BuildTrivia` den Lauf später in einem Sprung überfalten kann), `ContentExtent` (Direktiv-Breite ohne
Zeilenende — die spätere Trivia-Breite), `NewLineExtent` (das terminierende Zeilenende, oder Missing am
Dateiende) und der Knoten. Die lokalen Token des Knotens (`PopulateLocalTokens`) tragen die aus ihrem
Token-Typ folgende Klassifikation; das terminierende `PreprocessorNewLine` gehört bewusst **nicht** zu
den lokalen Token — es wird in `BuildTrivia` als gewöhnliches `NewLine`-Trivia-Stück geführt, damit die
Zeilen-Logik der Anhang-Regel ([§8.2](#82-die-roslyn-anhang-regel)) es sieht.

### 4.4 Platzierungs-Semantik: ResolveLanguageVersion

Welche Versions-Direktive **wirksam** ist, entscheidet `NavParser.ResolveLanguageVersion` — und zwar
bewusst **nach** der Member-Schleife und **vor** dem Einfrieren der Diagnostics. Der Grund für dieses
Timing: die Regel „nur ganz oben wirksam" braucht `_firstSignificantStart` (den Start des ersten
signifikanten Tokens), und der steht erst nach dem Parsen endgültig fest.

Die Regel selbst: einer wirksamen Versions-Direktive darf ausschließlich Trivia vorausgehen — kein Code
und keine andere Direktive —, und nur die erste am Kopf zählt. Daraus die drei Verstoß-Fälle:

| Fall | Diagnose |
|---|---|
| `#version` hinter echtem Code (`ContentExtent.Start >= _firstSignificantStart`) | `Nav3003` (sticht eine etwaige Duplikat-Meldung) |
| am Kopf, aber eine wirksame ging schon voraus | `Nav3004` (Duplikat; die erste gewinnt) |
| am Kopf, aber irgendeine andere Direktive ging voraus (auch eine Bad-Direktive) | `Nav3003` |

Unwirksame Versions-Direktiven bleiben vollwertige `VersionDirectiveSyntax`-Knoten mit normal
eingefärbten Token — nur ihre *Wirkung* entfällt. Ohne Direktive gilt `NavLanguageVersion.Default`
(= Version 1), der Bestand parst also bit-identisch weiter.

---

## 5. Der rekursive Abstieg (NavParser)

### 5.1 Die Grammatik: winzig, LL(1), sechs Lookahead-Stellen

Die Nav-Grammatik hat ~25 Regeln, keine Linksrekursion, keine Präzedenz. Der Kern in Kurzform (jede
`Parse*`-Methode trägt ihre exakte Regel als EBNF in der XML-Doku — die Autorität; hier der Überblick):

```
codeGenerationUnit        ::= ( codeNamespaceDeclaration codeUsingDeclaration* )? memberDeclaration* EOF
memberDeclaration         ::= includeDirective | taskDeclaration | taskDefinition
includeDirective          ::= "taskref" StringLiteral ";"
taskDeclaration           ::= "taskref" Identifier codeNamespaceDeclaration? codeNotImplementedDeclaration?
                              codeResultDeclaration? "{" connectionPointNodeDeclaration* "}"
taskDefinition            ::= "task" Identifier codeDeclaration? codeBaseDeclaration?
                              codeGenerateToDeclaration? codeParamsDeclaration? codeResultDeclaration?
                              "{" nodeDeclarationBlock transitionDefinitionBlock "}"
nodeDeclarationBlock      ::= nodeDeclaration*                       (* init/exit/end/task/choice/dialog/view *)
transitionDefinitionBlock ::= ( transitionDefinition | exitTransitionDefinition )*
transitionDefinition      ::= sourceNode edge targetNode continuationTransition?
                              trigger? conditionClause? doClause? ";"
exitTransitionDefinition  ::= identifierSourceNode ":" Identifier edge targetNode
                              continuationTransition? conditionClause? doClause? ";"
continuationTransition    ::= continuationEdge targetNode            (* --^ / o-^, ab Sprachversion 2 *)
codeType                  ::= simpleType | genericType | arrayType
```

Mehr als ein Blick voraus ist nur an sechs Stellen nötig — alle trivial, keine braucht Backtracking:

| Stelle | Frage | Entscheidung |
|---|---|---|
| `memberDeclaration` | `taskref` — Include oder Deklaration? | LA(2): StringLiteral ⇒ Include, sonst TaskDeclaration |
| `transitionDefinitionBlock` | Transition oder Exit-Transition? | Identifier + `:` ⇒ Exit-Transition |
| `nodeDeclaration` | `init` — Knoten-Deklaration oder Transitions-Quelle? | `init` + Kante ⇒ Transition (`StartsNodeDeclaration`) |
| `codeType` | simple, generisch oder Array? | Identifier + `<` ⇒ generisch; Basistyp + `[` `]` ⇒ Array |
| `conditionClause` | `else` oder `else if`? | `else` + `if` ⇒ elseIf |
| `code*`-Deklarationen | welche `[keyword …]`-Klammer? | `[` + Keyword (LA(2), `AtCodeDeclaration`) |

Eine Baum-Eigenheit verdient den expliziten Hinweis: `ParseTransitionDefinitionBlock` erlaubt im
Quelltext gemischte Reihenfolge, **gruppiert im Baum aber getrennt** — erst alle Transitionen, dann alle
Exit-Transitionen, *nicht* in Quelltext-Reihenfolge (Verhaltens-Parität zum alten Modell; der
Block-Extent stimmt trotzdem, er ist die Hülle über beide Listen).

### 5.2 Anatomie einer Parse-Methode

Jede der ~25 `Parse*`-Methoden folgt demselben Vier-Schritt-Muster — hier die kürzeste vollständige:

```csharp
ExitNodeDeclarationSyntax ParseExitNodeDeclaration() {

    var keyword = Eat(SyntaxTokenType.ExitKeyword);          // 1. Token konsumieren (RawToken?)
    var name    = Eat(SyntaxTokenType.Identifier);
    var semi    = Eat(SyntaxTokenType.Semicolon);

    var node = new ExitNodeDeclarationSyntax(Span(keyword, name, semi));  // 2. Knoten mit Hüll-Extent

    Tok(node, keyword, TextClassification.Keyword);          // 3. Token klassifiziert anhängen
    Tok(node, name,    TextClassification.Identifier);
    Tok(node, semi,    TextClassification.Punctuation);

    return node;                                             // 4. an den Aufrufer (der ihn als Kind einbaut)
}
```

1. **Konsum** über `Eat` (Pflicht-Token; liefert bei Fehlstelle `null` = Missing,
   [§6.1](#61-insertion-eat-und-das-missing-token)), `TryEat` (optionale Wiederholungs-Token in
   Listen-Schleifen, ohne Diagnose) oder ein `At(...)`-geschütztes `Eat` (optionale Bestandteile — der
   Guard stellt sicher, dass `Eat`s Fehlerzweig nur für *wirklich fehlende Pflicht-Token* feuert).
   Kindregeln werden direkt als `Parse*`-Aufruf konsumiert.
2. **Extent** als Hülle über alle Bestandteile — `Span(…)` für feste Stelligkeiten, `ExtentBuilder` für
   Listen ([§5.4](#54-extents-die-hülle-über-die-bestandteile)).
3. **Klassifikation**: `Tok(node, raw, classification)` hängt das Token mit Parent-Zeiger und Farbe an
   den flachen Token-Strom; `null` (ein Missing) wird ignoriert — Missing-Token stehen nie im Strom.
4. Der **Aufrufer** baut den Knoten als Kind ein; die Knoten-Konstruktoren übernehmen ihre Kinder und
   verketten sie via `AddChildNode`.

### 5.3 Klassifikation: dasselbe Lexem, viele Farben

Die `TextClassification` ist **kontextabhängig** und wird an der Konsum-Stelle vergeben — derselbe
Identifier ist je nach Grammatik-Position etwas anderes:

| Klassifikation | vergeben für |
|---|---|
| `Keyword` | Struktur-Schlüsselwörter (`task`, `init`, …), alle Kanten (`-->`, `o->`, `==>`, `--^`, `o-^`), `spontaneous`/`spont` |
| `ControlKeyword` | die Fluss-Schlüsselwörter `on`, `if`, `else`, `do` |
| `TaskName` | der Name hinter `task`/`taskref` und der Task-Name eines `task`-Knotens |
| `Identifier` | Knoten-Namen (`init`/`exit`/`choice`), Alias, Quell-/Zielknoten, Trigger-/Bedingungs-Identifier, Exit-Konnektor |
| `GuiNode` | die Namen von `dialog`- und `view`-Knoten |
| `TypeName` | der Identifier eines `simpleType`/`genericType` |
| `ParameterName` | der Parametername in `parameter` |
| `StringLiteral` | Include-Pfad, `generateto`-Literal, String-Formen von `identifierOrString` |
| `Text` | die StringLiterale **in** `[code "…"]` (eingebetteter C#-Text, kein Nav-Literal) |
| `Punctuation` | Klammern, `;`, `:`, `,`, `?`, `<`/`>` |
| `Skiped` | die lokalen Token der Skip-Trivia sowie der direktiv-lokale Überschuss ([§4.2](#42-der-dispatch-und-die-nav300x-familie)) |

Diese Tabelle *ist* die Editor-Einfärbung: VS-Classifier und LSP-Semantic-Tokens lesen die
Klassifikation direkt vom Token — es gibt keinen zweiten Klassifikations-Pass.

### 5.4 Extents: die Hülle über die Bestandteile

Der Extent eines Knotens ist die **Hülle** (Minimum der Starts, Maximum der Enden) über seine
konsumierten Token und Kindknoten. Die Mechanik (`NavParser.Extents.cs`) ist auf den Happy Path
zugeschnitten: `Span(…)`-Überladungen fester Stelligkeit statt `params` (das pro Knoten ein Array
allozieren würde), ein `ExtentPart`-Struct mit impliziten Konvertierungen von `RawToken?` und
`SyntaxNode?` (vermeidet das Boxing eines Nullable-Structs), für höhere Stelligkeiten der
`ExtentBuilder` direkt.

Die tragende Konvention: **fehlende Bestandteile tragen nichts bei.** Ein `null`-RawToken (Missing) und
ein fehlender optionaler Kindknoten liefern `TextExtent.Missing`, das der Builder überspringt. Zwei
Konsequenzen:

- Ein nullbreites Missing-Token *verfälscht nie* den Extent seines Knotens — der Knoten ist exakt so
  breit wie sein real vorhandener Text.
- Ein Knoten **ganz ohne** reale Bestandteile (ein leerer `nodeDeclarationBlock`, eine komplett
  synthetisierte Deklaration) bekommt `TextExtent.Missing` — Konsumenten wie `SyntaxNode.ToString`
  behandeln das als leer statt zu rechnen.

Der Wurzel-Extent ist ein Sonderfall: er beginnt am **ersten signifikanten Token** (konsumiert *oder*
übersprungen — `_firstSignificantStart` wird an allen Konsum- und Skip-Stellen nachgeführt) und endet an
der EOF-Position. Direktiven liegen *davor* als Leading-Trivia — sie sind keine Kindknoten der Wurzel
und dehnen deren Extent nicht.

### 5.5 Baum-Bau in zwei Phasen

Der Baum entsteht **bottom-up im Aufbau-Modus** und wird dann **einmalig eingefroren**
(`SyntaxNode.FinalConstruct`): erst danach kennen die Knoten ihren `SyntaxTree` und `Parent`, und jeder
weitere Schreibversuch wirft. Die Schluss-Sequenz von `ParseCodeGenerationUnit`:

```csharp
FinalizeTrivia();                       // §8 — jetzt stehen die übersprungenen Token fest
AttachEndOfFile(root);                  // EOF als letztes Token, trägt die finale Datei-Trivia (Leading)

var syntaxTree = new SyntaxTree(sourceText, root, TakeSortedTokens(), _diagnostics.ToImmutable());

root.FinalConstruct(syntaxTree, null);  // Baum einfrieren (rekursiv: Parent + SyntaxTree nachtragen)
FinalizeStructuredTrivia(syntaxTree, root); // Direktiv-/Skip-Knoten anschließen (Parent = Wurzel, §9.5)
```

`TakeSortedTokens` sortiert die Token-Liste in-place und hängt sie **ohne Kopie** an
(`SyntaxTokenList.AttachSortedTokens`) — sortieren ist nötig, weil `Tok` in
Knoten-Konstruktions-Reihenfolge anhängt (Eltern-Token *nach* den Token ihrer Kindknoten), die flache
Liste aber Positions-Reihenfolge verspricht.

Zwei Rand-Fakten: das `CancellationToken` wird zwischen den Top-Level-Members geprüft (die einzige
Abbruchstelle — eine einzelne Member-Deklaration ist klein genug); und neben dem Whole-File-Einstieg
`NavParser.Parse` gibt es den test-seitigen per-Regel-Einstieg `ParseRule(text, Rule, …)` (hinter den
~44 `Syntax.ParseXxx`-Snippet-Einstiegen), der dieselbe Schluss-Sequenz auf dem Regel-Knoten als Wurzel
fährt — Produktionscode parst ausschließlich ganze Dateien.

---

## 6. Fehlertoleranz I: Missing-Token und Panic-Mode

Zwei Grundmechanismen decken zusammen jede Abweichung ab — nach der Leitfrage „Wie würde Roslyn das
tun?": **eine treffende Diagnose an der Divergenzstelle, Folgefehler unterdrücken.**

### 6.1 Insertion: Eat und das Missing-Token

Fehlt ein erwartetes Pflicht-Token, synthetisiert `Eat` ein Missing-Token — konkret: es liefert `null`,
meldet eine Diagnose und rückt **nicht** vor:

```csharp
RawToken? Eat(SyntaxTokenType type) {
    if (At0 != type) {
        ReportMissing(Describe(type));   // "missing ';'", "missing identifier", …
        return null;                     // Missing: trägt nichts zum Extent bei, steht nie im Strom
    }
    …
}
```

Drei Eigenschaften machen das Missing-Token gutartig:

- **Es existiert nicht als Objekt.** `null` fließt durch `Span` (Extent-Beitrag `Missing`,
  [§5.4](#54-extents-die-hülle-über-die-bestandteile)) und `Tok` (ignoriert) — der Baum entsteht
  vollständig, nur eben ohne dieses Token. `SyntaxTree.Tokens` enthält ausschließlich reale Token.
- **Die Diagnose hängt am Vorgänger.** `ReportMissing` verankert nullbreit am **Ende des zuletzt
  konsumierten signifikanten Tokens** (`PreviousSignificantEnd`) — direkt hinter dem zuvor Getippten,
  *vor* dessen Trailing-Trivia. Das ist die Roslyn-Konvention: ein fehlendes `;` erscheint am Ende der
  eigenen Zeile, nicht vor dem nächsten Knoten der Folgezeile. (Ohne Vorgänger — Fehlstelle am
  Dateianfang — nullbreit an der Cursor-Position.)
- **Die EOF-Kaskade ist gekappt.** Bricht die Eingabe vorzeitig ab, rollen sich die Regeln auf und
  synthetisieren eine Kette fehlender Pflicht-Token (Zielknoten → `;` → `}`), die alle nullbreit an
  derselben EOF-Position landen würden. `_reportedMissingAtEof` lässt nur die **erste** durch — die den
  unvollständigen Bau benennt; der Rest ist Mechanik.

Zwei Spezialformen desselben Musters: `TryEatSemicolonQuiet` konsumiert ein `;`, falls da, meldet aber
nie eines an — der „eine Diagnose pro Divergenzstelle"-Baustein der Verfeinerungen
([§7](#7-fehlertoleranz-ii-die-drei-verfeinerungen-gegen-das-ausbluten)); und die zwei
Fehlerproduktionen der Transition melden textlich statt token-basiert: fehlt die Kante → `missing edge`,
fehlt bei vorhandener Kante der Zielknoten → `missing target node` (die Bedingung `edge != null` am
Target-Check ist selbst schon Folgefehler-Unterdrückung: ohne Kante ist der fehlende Zielknoten
Mechanik).

### 6.2 Deletion: Recover und die gestaffelten Sync-Sets

Steht ein Token an, das an dieser Stelle nichts verloren hat, überspringt der Panic-Mode bis zu einem
Wiedereinstiegs- oder Anker-Token:

```csharp
void Recover(Func<bool> recovered) {
    if (recovered()) {
        return;
    }

    ReportUnexpected(…, CurrentText);    // genau EINE Diagnose, am ersten übersprungenen Token

    do {
        _firstSignificantStart ??= CurrentStart;
        _pos++;
        SkipHidden();
    } while (!recovered());
}
```

Drei Invarianten:

1. **Fortschritts-Garantie.** Trifft das Prädikat nicht schon zu Beginn zu, rückt der Aufruf um
   mindestens ein signifikantes Token vor (do-while). Zusammen mit „`Eat` rückt bei Miss nicht vor"
   ergibt das die Terminierungs-Garantie der Listen-Schleifen: jede Iteration, die nichts parst, läuft
   in ein `Recover`, das konsumiert — Endlosschleifen sind konstruktiv ausgeschlossen.
2. **Eine Diagnose pro Lauf**, verankert am ersten übersprungenen Token (`unexpected input '…'`). Die
   übersprungenen Token selbst werden hier nur überlesen — zu `SkippedTokensTrivia` gefaltet werden sie
   erst in der Finalisierung ([§8.4](#84-falten-direktiven-und-skip-läufe)); nichts geht verloren.
3. **FOLLOW gewinnt.** Die Prädikate sind **gestaffelt**: jede lokale Recovery hält an den Ankern der
   *äußeren* Regel an und überlässt ihr das Token, statt die äußere Struktur mitzureißen — die
   Anchor-Set-Technik des Roslyn-C#-Parsers, hier als handverdrahtete Prädikat-Komposition.

Die Staffelung konkret (alle Prädikate sind im Konstruktor einmalig als `Func<bool>` erzeugt — Recovery
ist damit auch auf dem Happy Path allokationsfrei):

| Prädikat | hält an bei | genutzt von |
|---|---|---|
| `BreaksBody` (der äußere Boden) | `}` · `task` · `taskref` · EOF | allen folgenden (per Komposition) |
| `_atMemberOrEof` | `task` · `taskref` · EOF | Top-Level-Member-Schleife (kein `}`: auf Top-Level gibt es keins) |
| `_atTaskDefinitionBodyOrAnchor` | `{` · Knoten-Start · Transitions-Start · äußere Anker | vor dem Body-`{` einer `task`-Definition |
| `_atTaskDeclarationBodyOrAnchor` | `{` · Connection-Point (`init`/`exit`/`end`) · äußere Anker | vor dem Body-`{` einer `taskref`-Deklaration |
| `_atConnectionPointOrAnchor` | Connection-Point · äußere Anker | Connection-Point-Schleife |
| `_atTransitionOrAnchor` | Transitions-Start (`init`/Identifier) · äußere Anker | Transitions-Schleife |
| `_closesBracketRegion` | `]` · `;` · `{` · Knoten-Start · äußere Anker · Zeilenstart-`[` | Klammer-Recovery ([§7.2](#72-eckige-klammern-als-geschlossene-region)) |

Das Muster in jeder Listen-Schleife ist dreiteilig: *passt* → parsen; *bricht* (`BreaksBody`) → Schleife
verlassen, das Token gehört der äußeren Regel; *sonst* → `Recover` bis zum nächsten lokalen oder äußeren
Anker.

### 6.3 Durchgerechnet: beide Mechanismen in einer Datei

```
task A { init; }
foo
task B { init }
```

**Zeile 2 — Deletion.** Die Member-Schleife sieht `foo`: kein `task`/`taskref`, kein
Member-Keyword-Präfix (dem Identifier folgt `task`, kein Identifier — der Form-Guard von
[§7.3](#73-keyword-präfix-rescue-eatkeywordorskip) greift nicht), kein `[`. Also
`Recover(_atMemberOrEof)`: **eine** Diagnose `unexpected input 'foo'` über `foo`, der Cursor hält am
`task` von Zeile 3 (FOLLOW gewinnt). `foo` wird später zu einer `SkippedTokensTrivia` gefaltet, die —
Roslyn-Regel, eigene Zeile — als Leading-Trivia am `task`-Keyword von `B` hängt.

**Zeile 3 — Insertion.** In `task B` parst der Body: `init` (Knoten-Deklaration, da keine Kante folgt),
dann `Eat(Semicolon)` — aber da steht `}`. `ReportMissing("';'")` verankert **nullbreit am Ende von
`init`**, der Cursor bleibt stehen. Die Knoten-Schleife sieht `}` → `BreaksBody` → bricht; das `}`
konsumiert die Task-Regel regulär. Zwei Diagnosen für zwei echte Probleme, null Folgefehler — und der
Baum enthält beide Tasks vollständig.

---

## 7. Fehlertoleranz II: die drei Verfeinerungen gegen das Ausbluten

Die Grundmechanismen sind korrekt, aber an drei Stellen zu grob: eine unvollständige Konstruktion saugt
dann Text der *Folgezeile* ein oder reißt für sich korrekte Nachbarn mit — die Fehler-Region „blutet
aus" (beim Tippen: halbe Datei rot). Alle drei Verfeinerungen folgen demselben Prinzip: **eine
strukturelle Zusatz-Information — Zeilengrenze, Klammer-Region, Keyword-Präfix — begrenzt den Schaden
auf die Divergenzstelle.**

### 7.1 Die Zeilengrenze als weicher Anker (Transitionen)

Eine unvollständige Transition (nur der Quellknoten getippt) würde den Quellknoten der **nächsten**
Zeile als Zielknoten einsaugen und bis zum nächsten `;` weiterparsen — mehrere für sich korrekte Zeilen
kaputt. Die Grammatik gibt die Rettung her: von den Folge-Bestandteilen einer Transition beginnt **nur
der Zielknoten** mit einem Identifier (Trigger/Bedingung/do sind Keywords) — ein Gate an genau dieser
Stelle genügt:

```csharp
bool TargetStartsNextTransition() {
    return OnNewLine() && (SyntaxFacts.IsEdgeKeyword(PeekType(1)) || PeekType(1) == SyntaxTokenType.Colon);
}
```

Beginnt der Zielknoten-Kandidat auf einer **neuen Zeile** (`OnNewLine`: zwischen letztem konsumiertem
Token und Cursor liegt ein `NewLine`-Trivia) und leitet das *darauffolgende* Token eine neue Transition
ein — eine Kante oder ein `:` (Exit-Transition) —, dann gehört der Kandidat zur nächsten Transition und
die laufende endet hier. Durchgerechnet:

```
A
B --> C;
```

`ParseTransitionDefinition`: Quelle `A`; keine Kante → **eine** Diagnose `missing edge` (nullbreit hinter
`A`). `TargetStartsNextTransition`: `B` steht auf neuer Zeile, dahinter `-->` → wahr → `continues =
false`: Zielknoten, Trigger, Bedingung, do werden gar nicht mehr versucht, und statt `Eat(Semicolon)`
läuft `TryEatSemicolonQuiet` — das mechanisch ebenfalls fehlende `;` bleibt stumm (analog zur
EOF-Kaskade). Die Transition besteht nur aus `A`; die Schleife parst `B --> C;` sauber als nächste.
Eine Diagnose, eine kaputte Zeile.

Dieselbe Stellschraube greift in der Exit-Transition eine Stufe früher: nach dem `:` erwartet der Parser
den Exit-Konnektor-Namen (Pflicht-Identifier). Beginnt der Kandidat auf neuer Zeile bereits eine neue
Transition, würde sonst der Quellknoten der Folgezeile als Name eingesaugt — stattdessen `missing
identifier` direkt hinter dem `:`, Abbruch, Rest stumm. Same-Line-Fälle bleiben unangetastet — echter
Nav-Code bricht eine Transition nie über Zeilen um (Korpus: 1104/1104 einzeilig), das Gate ist also
fehlalarmfrei.

### 7.2 Eckige Klammern als geschlossene Region

Das Pendant für die `[keyword …]`-Code-Deklarationen: ein unfertiges `[params …` (das `]` fehlt noch)
oder ein nicht zuordenbares `[` (leeres `[]`, `[foo]`) darf nicht in die folgenden Deklarationen
ausbluten — sonst bricht die Knoten-Deklaration ab und jede weitere Body-Zeile läuft als Kaskade auf.
Drei ineinandergreifende Bausteine:

**(a) `ClosesBracketRegion` — die Anker innerhalb einer Klammer.** Das schließende `]` selbst, die
harten äußeren Anker (`;`, Body-`{`, Knoten-Start, `BreaksBody`) — plus ein **Zeilen-Anker**: beginnt
eine *neue Zeile* mit `[`, gehört dieses `[` zu einer neuen Deklaration, der laufenden Klammer fehlt
also nur das `]`. Statt über die Zeilengrenze in die (für sich korrekte) nächste Deklaration zu laufen
und dort irreführend `unexpected input '['` zu melden, bricht die Recovery ab und `EatCloseBracket`
meldet das treffende `missing ']'` am Ende der laufenden Zeile. Fehlalarmfrei, weil innerhalb einer
Klammer auf einer Folgezeile nie legitim ein `[` steht (mehrzeilige Inhalte wie `[code …]`-Literale sind
konsumiert, bevor das `]` gesucht wird).

**(b) `EatCloseBracket` statt `Eat(CloseBracket)`.** Jede `ParseCode*Declaration` schließt mit
`Recover(_closesBracketRegion); Eat(CloseBracket)` — ein gezielter Panic-Mode resynchronisiert
überzähligen Klammerinhalt *vor* dem `]`, statt dass `Eat` das `]` still synthetisiert und die
Folgetoken downstream als Kaskade auflaufen.

**(c) `ParseMalformedBracketDeclaration` — die Fehlerproduktion.** Ein `[` an einer
Code-Deklarations-Position, das keiner bekannten Deklaration entspricht, wird als Ganzes verschluckt
(bis `]` bzw. hartem Anker) — **eine** Diagnose über die ganze Klammer. Die Diagnose ist
kontext-sensitiv: bei einem **leeren** `[]` gehört die Klammer erkennbar hierher, nur ihr Schlüsselwort
fehlt — gemeldet wird `expected 'abstractmethod' or 'params'` (die im Wirt gültigen Keywords aus
`CodeBlockFacts.VisibleDeclarationKeywords` — dieselbe Autorität, aus der auch die Completion ihre
Vorschläge zieht: Parser-Diagnose und IntelliSense können nicht auseinanderlaufen). Bei Klammern *mit*
ungültigem Inhalt bleibt das treffendere `unexpected input '…'`.

Verschränkt werden die Bausteine in `ParseCodeDeclarations`: die (in fester Grammatik-Reihenfolge
notierten) Deklarationen eines Wirts werden Klammer für Klammer angeboten — nach dem Überspringen einer
malformen Klammer werden die noch offenen Deklarationen **erneut** angeboten. So verschluckt ein
vorangestelltes `[]` keine nachfolgende gültige Deklaration (`task A [] [code …]` parst `[code …]`
weiterhin als Code-Deklaration), während die Reihenfolge-Grammatik gewahrt bleibt (Null-Wächter im
Delegaten: jede Deklaration parst höchstens einmal). War mindestens eine Klammer malform, unterdrückt
der Wirt das mechanisch fehlende `;` (`TryEatSemicolonQuiet` — wieder: eine Diagnose pro
Divergenzstelle). Dieselbe verschränkte Schleife treibt auch den `[namespaceprefix …] [using …]*`-Kopf
der Datei an.

### 7.3 Keyword-Präfix-Rescue (EatKeywordOrSkip)

Beim Tippen durchläuft jedes Schlüsselwort eine Vorstufe, die als gültiger **Identifier** lext (`tas`
vor `task`, `namespace` vor `namespaceprefix`, `usin` vor `using`) — der Lexer matcht Keywords exakt
([§2.5](#25-keyword-erkennung-exakt-mit-allokations-guard)). Ein keyword-exakter Dispatch deutete den
Beinah-Treffer als „gar nicht dieses Konstrukt", und der Panic-Mode schluckte das ganze Konstrukt:
`tas SimpleTask { … }` → gesamter Body rot.

Der Rescue deutet den Identifier als das gemeinte, noch unvollständige Keyword — aber nur unter zwei
strengen Bedingungen, die Fehldeutungen strukturell ausschließen:

1. **Nur an Stellen, an denen ein Identifier nie gültig ist.** Nach `[` an einer
   Code-Deklarations-Position steht grammatisch immer ein Keyword; auf Top-Level beginnt ein Member nur
   mit `task`/`taskref`. Ein Identifier dort ist *sicher* ein Fehler — die Frage ist nur, welcher.
2. **Nur bei echtem Präfix** (`IsKeywordPrefix`: nicht leer, *kürzer* als das Keyword, dessen Anfang —
   Ordinal, keine Edit-Distanz). Das vollständige Keyword ist kein Präfix seiner selbst (es lext ohnehin
   als Keyword-Token); das echte Keyword einer *anderen* Deklaration feuert den Zweig nie (es ist ein
   Keyword-Token, kein Identifier).

Der Konsum folgt dem Roslyn-Modell — **nicht umfärben, sondern Missing + Skip**:

```csharp
RawToken? EatKeywordOrSkip(SyntaxTokenType keyword) {
    if (At(keyword)) {
        return Eat(keyword);
    }

    ReportMissing(Describe(keyword));            // "missing 'using'" — nullbreit an der Einfügestelle

    if (At(SyntaxTokenType.Identifier)) {        // das Störtoken ('usin') überspringen: nicht via Tok
        _pos++;                                   // angehängt ⇒ FinalizeTrivia faltet es zu Skip-Trivia
        SkipHidden();
    }

    return null;
}
```

Der Skip ist zwingend — sonst konsumierte der folgende Name-Parse das Störtoken als Namen. Danach parst
der Rest der Deklaration normal weiter: `[usin System.Linq]` ergibt eine vollwertige
`CodeUsingDeclarationSyntax` mit einer Diagnose `missing 'using'` und `usin` als Skip-Trivia — statt
einer zerfetzten Klammer-Kaskade.

Zwei Dispatch-Zutaten komplettieren den Rescue: `AtCodeDeclaration(keyword)` akzeptiert hinter dem `[`
neben dem exakten Keyword auch einen Identifier mit echtem Präfix; `AtMemberKeywordPrefix` tut dasselbe
auf Top-Level, verlangt aber als **Form-Bestätigung** einen folgenden Identifier (den Task-Namen) — ein
loser Top-Level-Identifier ohne Namen bleibt dem Panic-Mode überlassen (so heilt auch `tas "file";`
korrekt erst als vollständiges `taskref`-Include). Ambiguitäten löst ein deterministischer Tie-Break:
`tas` ist Präfix von `task` *und* `taskref` — fester Vorrang `task` (der häufigere Fall), `taskref`
greift erst ab dem eindeutigen Präfix `taskr…`; im `taskref`-Wirt entscheidet bei `[n…]`
(`namespaceprefix` vs. `notimplemented`) die Delegat-Reihenfolge.

---

## 8. Die Trivia-Finalisierung (FinalizeTrivia und BuildTrivia)

### 8.1 Warum nach dem Parsen

Die Trivia-Zuordnung braucht eine Information, die erst am Ende existiert: **welche signifikanten Token
wurden konsumiert?** Direktiven sind vor dem Parsen bekannt (der Vorlauf, [§4](#4-der-direktiven-vorlauf-navdirectiveparser));
die übersprungenen Token aber sind die *Differenz* aus Roh-Strom und Konsum — und die steht erst fest,
wenn der letzte `Recover`-Lauf gelaufen ist. Deshalb sammelt der Parser seine Token zunächst **ohne**
Trivia (`Tok`), und `FinalizeTrivia` baut sie danach in einem Lauf:

```csharp
var consumedStarts = new HashSet<int>();       // Start-Position als Identität — Token überlappen nie (§2.1)
foreach (var token in _tokens) {
    consumedStarts.Add(token.Start);
}

_allTrivia        = BuildTrivia(consumedStarts, out _tokenTrivia, out var eofStart, out var eofLength);
_eofLeadingTrivia = new SyntaxTriviaList(_allTrivia, eofStart, eofLength);

for (var i = 0; i < _tokens.Count; i++) {      // Finalisierungs-Pass: jedes Token mit seiner Trivia neu setzen
    var token = _tokens[i];
    var (leading, trailing) = LookupTrivia(token.Start);
    _tokens[i] = SyntaxTokenFactory.CreateToken(token.Extent, token.Type, token.Classification, token.Parent,
                                                leading, trailing);
}
```

Der Finalisierungs-Pass ändert weder Baumstruktur noch Token-Identitäten — Parent, Extent, Typ,
Klassifikation bleiben; und die Gleichheits-Semantik von `SyntaxToken` klammert die Trivia **bewusst
aus** ([§9.2](#92-syntaxtoken)), genau damit dieses Neu-Setzen identitäts-neutral ist.

### 8.2 Die Roslyn-Anhang-Regel

Wem gehört die Trivia zwischen zwei Token? Die Regel (wörtlich Roslyns):

- **Trailing** eines Tokens: die anschließende Trivia **bis einschließlich des ersten Zeilenendes**.
  Folgt vor einem Zeilenende bereits das nächste Token, endet die Trailing-Trivia dort (alles Trailing,
  kein Leading).
- **Leading** eines Tokens: die restliche Trivia bis zu ihm — komplette Leer-/Kommentarzeilen samt der
  Einrückung seiner eigenen Zeile.
- Das nullbreite **`EndOfFile`** erhält die finale Datei-Trivia als Leading — die letzte Heimat, die den
  Round-Trip lückenlos schließt.

Durchgerechnet (CRLF-Datei):

```
init A;   // Start

exit B;
```

| Token | Leading | Trailing |
|---|---|---|
| `init` | — | `␣` (Whitespace vor `A` — kein Zeilenende dazwischen: alles Trailing) |
| `A` | — | — |
| `;` | — | `␣␣␣` + `// Start\r` + `\n` (bis einschließlich erstem Zeilenende — die CRLF-Kommentar-Eigenheit aus [§2.3](#23-drei-zerfalls-regeln-für-unfertiges)) |
| `exit` | `\r\n` (die Leerzeile) | `␣` |
| `B` | — | — |
| `;` | — | `\r\n` |
| `EOF` | — (nichts mehr da) | — |

Die Intuition hinter der Asymmetrie: ein Trailing-Kommentar gehört *zu seiner Zeile* (zum Token davor),
alles ab der nächsten Zeile — Leerzeilen, Kommentarblöcke, Einrückung — gehört *zum Kommenden*. Genau
diese Zuordnung konsumieren Formatter (`GapTrivia`), GoTo und Hover, ohne je selbst Zeilen zählen zu
müssen.

### 8.3 Ein Array, ein Dictionary

`BuildTrivia` materialisiert **alle** Trivia der Datei in genau *einem* geteilten
`ImmutableArray<SyntaxTrivia>` (Strom-Reihenfolge); ein Token merkt sich nur Index-Bereiche darin
(`TriviaRange`: Leading-Start/-Länge, Trailing-Start/-Länge), und `SyntaxTriviaList` ist eine
allokationsfreie Struct-*Sicht* auf einen solchen Bereich — mit der Oberfläche eines `ImmutableArray`
(Indexer, `foreach` über einen Struct-Enumerator), damit Aufrufstellen nichts merken.

Diese Form ist eine gemessene Lektion ([§11](#11-sicherheitsnetze-und-zahlen), Lektion 3): die erste
Fassung des Umbaus mit **drei** Positions-Dictionaries war *schlechter* als je-Token-Arrays (+16 MiB);
erst die Fassung mit **einem** Dictionary (`tokenTrivia`, Schlüssel = Token-Start, vorab auf
`_tokens.Count` dimensioniert — es bekommt exakt einen Eintrag je konsumiertem Token) brachte −44 MiB je
Korpus-Durchlauf. Der Lookup-Index ist der eigentliche Allokationsposten, nicht die Trivia selbst.

Der Algorithmus ist ein einziger Vorwärtslauf über `_raw` mit einem **pending-Lauf** (Trivia seit dem
letzten Trenner):

1. **Direktiv-Lauf erreicht** (`runByStart`, ohne Direktiven die geteilte leere Map): den ganzen Lauf
   als *ein* `DirectiveTrivia`-Stück (mit Struktur-Verweis) plus sein terminierendes Zeilenende als
   eigenes `NewLine` in den pending-Lauf legen; die Schleife springt hinter den Lauf.
2. **Lexikalische Trivia**: ins Array, weiter.
3. **Skip-Lauf erreicht** (`IsSkippedToken`: parser-sichtbar, aber nicht konsumiert — oder `Unknown`):
   `FoldSkippedRun` ([§8.4](#84-falten-direktiven-und-skip-läufe)), das Stück fließt in den pending-Lauf.
4. **Trenner erreicht** (ein konsumiertes signifikantes Token, das `EndOfFile` oder — theoretisch — ein
   verwaistes verstecktes Token): `SplitTrailingCount` spaltet vom pending-Lauf die Trailing-Trivia des
   *vorigen* signifikanten Tokens ab (bis einschließlich des ersten `NewLine`; ohne Zeilenende alles);
   der Rest wird Leading des Trenners. Ein signifikanter Trenner wird neuer Trailing-Anker
   (`lastSignificantKey`); das `EndOfFile` legt den Rest als EOF-Leading ab.

Der dritte Trenner-Arm (verstecktes Token als Trenner) ist unter der Lexer-Invariante praktisch tot —
Präprozessor-Token entstehen nur innerhalb eines `#`-Laufs, den der Vorlauf stets vollständig faltet —,
bleibt aber als korrekte Behandlung erhalten: er darf **kein** Trailing-Anker sein (kein `SyntaxToken`
würde diese Trivia je nachschlagen), trägt seine Leading-Trivia aber selbst, damit kein Text verloren
ginge, falls die Invariante je fällt.

### 8.4 Falten: Direktiven und Skip-Läufe

Beide strukturierten Trivia-Arten entstehen nach demselben Falt-Muster — je **maximalem Lauf** genau ein
Trivia-Stück, dessen Extent den ganzen Lauf deckt, mit einem Knoten, der die Token **lokal** hält:

- **Direktiven** wurden im Vorlauf erkannt; `BuildTrivia` setzt nur noch das Stück (Extent =
  `ContentExtent`) und überspringt die Roh-Token des Laufs.
- **Skip-Läufe** entdeckt `FoldSkippedRun` erst hier: der Lauf beginnt am ersten übersprungenen Token
  und dehnt sich über weitere Skips aus — **reine Trivia zwischen ihnen bricht den Lauf nicht** (sie
  fällt in dessen Extent); ein konsumiertes Token, ein Direktiv-Lauf oder das Dateiende beendet ihn. Der
  `SkippedTokensTriviaSyntax`-Knoten erhält die Skip-Token als lokale `SyntaxTokenList` (Klassifikation
  `Skiped` — daraus entsteht die rote Editor-Darstellung), und für jedes `Unknown` wird hier die
  `Nav0000`-Diagnose gemeldet (nullbreit an der Zeichen-Position, `LexicalLocation` — Parität zur alten
  Pipeline).

Ein Skip-Lauf ist — wie ein Direktiv-Lauf — **kein Trenner**: das Stück fließt in den umgebenden
Trivia-Lauf und hängt nach der Anhang-Regel am Nachbar-Token. Für `init [];` heißt das: die
`SkippedTokensTrivia` mit `[` `]` hängt (gleiche Zeile) als **Trailing** von `init`; in `tree.Tokens`
stehen die Klammern nicht.

### 8.5 EOF, AttachEndOfFile und der geschlossene Kreis

`AttachEndOfFile` hängt das nullbreite `EndOfFile` als letztes Token an die Wurzel, mit der finalen
Datei-Trivia als Leading. Damit ist die Buchführung geschlossen: **jedes** Roh-Token des Lexers ist
jetzt entweder konsumiertes Token im flachen Strom, Trivia an einem solchen (bzw. am EOF) — oder Teil
einer strukturierten Trivia. Der Round-Trip aus [§1](#1-überblick--eine-engine-idee-full-fidelity-durch-konstruktion)
ist konstruktiv erfüllt; die Tests prüfen ihn trotzdem wörtlich, für jede Korpus-Datei und jedes
Tipp-Präfix ([§11](#11-sicherheitsnetze-und-zahlen)).

---

## 9. Das Ergebnis: SyntaxTree, Token und Knoten

### 9.1 SyntaxTree

Das unveränderliche Ergebnis eines Parse-Laufs: `Root`, `Tokens`, `SourceText`, `Diagnostics`.
`Tokens` ist der flache Strom **nur der signifikanten** Token — Trivia hängt an den Token, Direktiv- und
Skip-Token liegen lokal an ihren Struktur-Knoten. Die Trivia-Oberfläche darüber:

- `DescendantTrivia()` — alle Trivia der Datei in Quelltext-Reihenfolge; weil jede Trivia an genau einem
  Token hängt, erscheint jede genau einmal.
- `Directives()` / `SkippedTokens()` — die strukturierten Trivia, über `HasStructure`/`GetStructure()`
  gefiltert; so erreichen Classifier, Versions-Logik und Diagnose-Konsumenten die Struktur-Knoten, ohne
  dass diese Kindknoten der Wurzel wären.
- `FindTrivia(position)` / `IsPositionInComment(position)` — Positions-Lookup mit derselben
  Halbintervall-Regel `[Start, End)` wie bei den Token (die Completion unterdrückt so ihr Auslösen in
  Kommentaren).

### 9.2 SyntaxToken

Ein Wert-Struct; Typ und Klassifikation teilen sich ein `int`-Feld (je ein Byte). Es kennt seinen
`Parent` (den konsumierenden Knoten — bei lokalen Token strukturierter Trivia deren Struktur-Knoten)
und trägt `LeadingTrivia`/`TrailingTrivia` als Sichten ([§8.3](#83-ein-array-ein-dictionary)).
`FullExtent` (≙ Roslyn `FullSpan`) reicht vom Anfang der Leading- bis zum Ende der Trailing-Trivia —
das trivia-freie `Extent` ist ≙ Roslyn `Span`.

Zwei bewusste Entscheidungen: Die **Navigation** (`NextToken()`/`PreviousToken()`) ist
**parent-lokal** — liegt der Nachbar außerhalb des Parent-Extents, kommt `Missing`; wer den globalen
Strom braucht, geht über `SyntaxTree.Tokens` (der bekannte Completion-Gotcha). Und die **Gleichheit**
klammert die Trivia aus (Typ/Klassifikation, Extent, Parent-Referenz) — Voraussetzung des
Finalisierungs-Passes ([§8.1](#81-warum-nach-dem-parsen)) und zugleich Schutz vor der reflektiven
Default-Struct-Gleichheit über die Sicht-Structs.

### 9.3 SyntaxTokenList und das Kachel-Argument

Die flache Liste ist nach Position sortiert; alle Lookups sind Binärsuchen. Zwei Positions-Lookups mit
verschiedenem Vertrag:

- `FindAtPosition` — exakt: das Token, dessen *eigener* Extent die Position deckt; in Trivia
  `SyntaxToken.Missing`.
- `FindOwningToken` — Roslyns `FindToken`: in Trivia das **tragende** Token. Die Korrektheit steht auf
  dem Kachel-Argument: der flache Strom ist trivia-frei, aber die **FullSpans** der Token (Leading +
  Extent + Trailing) kacheln den Text — eine Trivia-Position gehört daher entweder zur Trailing des
  unmittelbaren Vorgängers oder zur Leading des unmittelbaren Nachfolgers; beide liefert eine
  Binärsuche, danach entscheidet ein Blick in deren Trivia-Listen.

### 9.4 SyntaxNode

Knoten leben in zwei Phasen ([§5.5](#55-baum-bau-in-zwei-phasen)): Aufbau (Kinder anfügen) und — nach
`FinalConstruct` — eingefroren (nur noch lesend; `Parent`/`SyntaxTree` verfügbar, jeder Schreibversuch
wirft). `ChildTokens()` sind die dem Knoten *direkt* zugeordneten Token: per Extent-Bereichs-Query über
den flachen Strom, gefiltert auf `token.Parent == this`, einmalig materialisiert gecacht (der Baum ist
ab jetzt unveränderlich — und die Token-Property-Accessoren der Knoten fragen auf dem heißen Pfad sehr
oft). Die Trivia-Sicht eines Knotens (`GetLeadingTrivia`/`GetTrailingTrivia`, `GetFullExtent`) ist aus
seinem ersten bzw. letzten signifikanten Token **abgeleitet** — es gibt keine zweite
Trivia-Buchführung auf Knoten-Ebene.

### 9.5 Strukturierte Trivia: Knoten außerhalb des Baums

`StructuredTriviaSyntax` (Basis von `DirectiveTriviaSyntax` und `SkippedTokensTriviaSyntax`) ist ein
vollwertiger `SyntaxNode` mit einer Besonderheit: seine Token liegen in einer **eigenen, lokalen**
`SyntaxTokenList` (`SetLocalTokens`, `ChildTokens()`-Override) — nicht im flachen Strom. Die Knoten sind
**keine Kindknoten der Wurzel** (sie erscheinen in keiner `ChildNodes()`/`DescendantNodes()`-Sicht),
brauchen aber wie jeder Knoten `SyntaxTree` und `Parent`, damit ihre lokalen Token Position und
Quelltext auflösen — `FinalizeStructuredTrivia` ruft daher `FinalConstruct(tree, root)` für jeden
Direktiv- und Skip-Knoten separat. Erreichbar sind sie ausschließlich über ihre Trivia
(`SyntaxTrivia.GetStructure()`), also über `Directives()`/`SkippedTokens()`.

---

## 10. Das Diagnostik-Inventar

Alles, was der Parse-Lauf melden kann, auf einen Blick — mit der jeweils bewusst gewählten Verankerung:

| Diagnose | Auslöser | Verankerung | Quelle |
|---|---|---|---|
| `missing <was>` | Insertion: fehlendes Pflicht-Token; `missing edge` / `missing target node` / `missing continuation edge` als textliche Fehlerproduktionen | nullbreit am Ende des zuletzt konsumierten signifikanten Tokens (Roslyn-Konvention); EOF-Kaskade gekappt | `ReportMissing` ([§6.1](#61-insertion-eat-und-das-missing-token)) |
| `unexpected input '<text>'` | Deletion: Panic-Mode-Lauf; malforme Klammer mit Inhalt | erstes übersprungenes Token bzw. die ganze Klammer | `Recover` / `ParseMalformedBracketDeclaration` ([§6.2](#62-deletion-recover-und-die-gestaffelten-sync-sets), [§7.2](#72-eckige-klammern-als-geschlossene-region)) |
| `expected 'a', 'b' or 'c'` | leeres `[]` an Code-Deklarations-Position (Keywords je Wirt aus `CodeBlockFacts`) | die ganze Klammer | `ParseMalformedBracketDeclaration` ([§7.2](#72-eckige-klammern-als-geschlossene-region)) |
| `Nav0000` unexpected character | lexikalisch unbekanntes Zeichen (auch mid-line-`#`) | nullbreit an der Zeichen-Position (`LexicalLocation`) | `FoldSkippedRun` ([§8.4](#84-falten-direktiven-und-skip-läufe)) |
| `Nav3000` invalid preprocessor directive | unbekannte Direktive, nacktes `#pragma`, nacktes `#` | ganze Direktiv-Breite (ohne Zeilenende) | `NavDirectiveParser` ([§4.2](#42-der-dispatch-und-die-nav300x-familie)) |
| `Nav3001` unknown pragma | `#pragma <subjekt>` (derzeit jedes) | ganze Direktiv-Breite | `NavDirectiveParser` |
| `Nav3002` invalid version directive | `#version` ohne / mit ungültigem / mit überzähligem Wert | positions-präzise: Insertion-Punkt / Wert / Rest | `NavDirectiveParser` ([§4.2](#42-der-dispatch-und-die-nav300x-familie)) |
| `Nav3003` version directive must appear at top of file | `#version` hinter Code oder hinter einer anderen Direktive | `ContentExtent` des Laufs | `ResolveLanguageVersion` ([§4.4](#44-platzierungs-semantik-resolvelanguageversion)) |
| `Nav3004` duplicate version directive | zweite wirksam platzierte `#version` (die erste gewinnt) | `ContentExtent` des Laufs | `ResolveLanguageVersion` |

Die `missing`/`unexpected`/`expected`-Meldungen sind generische Syntax-Errors
(`DiagnosticDescriptors.NewSyntaxError`); die `Nav`-nummerierten haben feste Descriptoren. Kein Pfad
wirft je eine Exception in Richtung Aufrufer — die einzige Ausnahme ist das kooperative
`OperationCanceledException` des `CancellationToken`.

---

## 11. Sicherheitsnetze und Zahlen

Der Parser hat kein Differential-Netz mehr gegen ANTLR (das war das Cutover-Gerüst und ist mit dem
ANTLR-Ausbau gefallen); das dauerhafte Netz ist mehrschichtig:

- **Golden-Snapshots** (`SyntaxGoldenTests`, `Nav.Language.Tests/Syntax/Tests/`): je Korpus-Datei vier
  byte-genau gepinnte Stränge — `.tokens` (flacher Strom), `.tree` (Baumstruktur), `.diag` (Diagnosen
  mit Positionen), `.trivia` (Trivia-Zuordnung inkl. strukturierter Stücke). Regeneration über den
  `[Explicit]`-Test `UpdateGolden`.
- **Round-Trip-Tests**: Token + Trivia aneinandergehängt müssen exakt den Eingabetext ergeben — die
  wörtliche Prüfung des Leitmotivs aus [§1](#1-überblick--eine-engine-idee-full-fidelity-durch-konstruktion).
- **Tipp-Präfix-Stress** (`ParsesAndRoundTripsAllTypingPrefixes`): **jedes Präfix jeder Korpus-Datei**
  wird geparst und muss lückenlos round-trippen — der beste Wächter gegen Recovery-Regressionen, denn
  jede Rescue-Mechanik aus [§7](#7-fehlertoleranz-ii-die-drei-verfeinerungen-gegen-das-ausbluten) muss
  skip-treu bleiben (nichts geht verloren).
- **Korpus-Validierung** (1912 reale `.nav`, `D:\tfs\Main`): alle Dateien parsen ohne Exception,
  0 nicht-ignorierte Fehler — und der **erzeugte C#-Code ist byte-identisch** zum alten ANTLR-Generator
  (9229 Dateien, MD5-gleich). Der Handparser ist ein exakter Ersatz end-to-end.

Die Performance-Zahlen (ganzer Korpus 7,2 MiB, in-process, Minimum aus 10 Iterationen):

| Parser | Parse-Zeit | Durchsatz | Allokiert/Iteration |
|---|---|---|---|
| ANTLR (vor Cutover) | 682 ms | 10,5 MiB/s | 374 MiB |
| Kolibri | **340 ms** | **21,0 MiB/s** | **258 MiB** |

Vier gemessene Lektionen, die im Code sichtbar sind (und beim Weiterarbeiten gelten):

1. **Der Parser ist nicht der Flaschenhals des Batch-Generators** (< 2 % der End-to-End-Zeit) —
   Parser-Optimierungen zahlen auf die *Editor-Reparse-Latenz* ein (Reparse pro Tastendruck, weniger
   GC-Pausen), nicht auf `nav.exe`.
2. **Allokationen messen, nicht raten** — `GC.GetTotalAllocatedBytes()` ist deterministisch und deckt
   auf, was der Gen0-Zähler verschleiert; Parse-Zeit als Minimum aus N Iterationen.
3. **Geteilte Datenstruktur schlägt viele kleine — aber nur, wenn die Hilfsindizes nicht wachsen** (die
   Ein-Dictionary-Form von [§8.3](#83-ein-array-ein-dictionary); drei Dictionaries waren schlechter als
   der Ausgangszustand).
4. **Wenig-invasiver Span-Ersatz schlägt breite Span-Migration** — der `couldBeKeyword`-Guard
   ([§2.5](#25-keyword-erkennung-exakt-mit-allokations-guard)) entfernt fast alle Substring-Allokationen
   des Lexers, ganz ohne `ReadOnlySpan`-Umbau (den `netstandard2.0` beim Dictionary-Lookup ohnehin nicht
   hergäbe). In dieselbe Reihe gehören die vorab dimensionierten Builder (Lexer ~1 Token je 4 Zeichen,
   Parser ~½ des Roh-Stroms, `tokenTrivia` exakt `_tokens.Count`) und die im Konstruktor
   vorab erzeugten Recovery-Prädikate ([§6.2](#62-deletion-recover-und-die-gestaffelten-sync-sets)).

Damit schließt sich der Bogen: ein Durchlauf, der aus jedem Zeichen genau eine Heimat macht — konsumiert,
angehängt oder strukturiert gefaltet —, dessen Fehlertoleranz aus zwei kleinen Mechanismen und drei
strukturellen Begrenzern besteht, und dessen zentrale Zusagen nicht Konvention sind, sondern
Konstruktion: geprüft im Round-Trip, gepinnt in den Goldens, gestresst über jedes Tipp-Präfix des
Korpus.
