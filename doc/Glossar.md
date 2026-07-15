# Glossar — Nav.Language

Verbindliches Begriffs-Inventar für die Doku- und Kommentar-Prosa (`///` **und** `//`) im
Engine-Projekt **`Nav.Language`**. Zweck: künftig einheitliche Terminologie, kein Nebeneinander
mehrerer Wörter für dasselbe Konzept.

Dieses Dokument legt nur die **Soll-Begriffe** fest — es schreibt selbst keine Kommentare um. Wo der
Ist-Zustand noch abweicht, verweist der [Abweichungs-Anhang](#anhang-a--abweichungs-fundstellen) auf
die konkreten Fundstellen als Arbeitsliste für einen späteren Rewrite.

## Leitregel

1. **Fachbegriff zuerst.** Kanon ist der im Deutschen geläufigste Fachbegriff des Konzepts — mal
   deutsch (*Knoten*, *Kante*), mal englisch (*Host*, *Token*, *Trivia*, *Parser*). Ausschlaggebend
   ist die Geläufigkeit, nicht die Sprache.
2. **Code-Symbol als Tie-Breaker.** Existiert ein Bezeichner im Code (Typ/Property/Enum), der das
   Konzept benennt, richtet sich die Prosa danach — aber nur, wenn (1) beide Formen zulässt oder der
   Ist-Zustand ohnehin schwankt.
3. **Konsistenz sticht immer.** Genau ein Begriff pro Konzept. Wo der Bestand bereits einen stabilen
   Begriff nutzt, wird dieser kanonisiert — auch wenn eine andere Form denkbar wäre.

**Hausstil.** Die Prosa ist deutsch, die Code-Symbole sind englisch. Ein englisches Symbol macht die
deutsche Prosa **nicht** falsch: `INodeSymbol` heißt im Fließtext *Knoten*, nicht *Node*. Das Symbol
entscheidet nur dort, wo die Prosa selbst uneins ist.

## Legende

- **Kanon** — der festgelegte Begriff.
- **Symbol** — der zugehörige Code-Bezeichner (Autorität nach Regel 2).
- **verwirft** — Varianten, die nicht mehr verwendet werden.
- ⚠ **abweichend** — im Bestand steht noch eine verworfene Variante → [Anhang A](#anhang-a--abweichungs-fundstellen).
- 🔸 **strittig** — echte Richtungs-Entscheidung; meine Empfehlung steht dabei, Gegenlesen erbeten.

## Index (A–Z)

[Abhängigkeit](#abhängigkeit) ·
[Alignment / Ausrichtung](#alignment--ausrichtung) ·
[Anweisung → Statement](#statement--anweisung) ·
[Aufrufhierarchie](#aufrufhierarchie) ·
[Aufruffläche](#aufruffläche) ·
[Bedingung / Guard](#bedingung--guard) ·
[CallContext](#callcontext) ·
[CodeBuilder](#codebuilder) ·
[CodeInfo](#codeinfo) ·
[CodeModel](#codemodel) ·
[ConnectionPoint / Verbindungspunkt](#connectionpoint--verbindungspunkt) ·
[Continuation / Fortsetzung](#continuation--fortsetzung) ·
[Deckel → Obergrenze](#obergrenze) ·
[Deklaration vs. Definition](#deklaration-vs-definition) ·
[Diagnose / Diagnostics](#diagnose--diagnostics) ·
[Disk / Platte](#disk--platte) ·
[Einrückung / Einzug](#einrückung--einzug) ·
[Emitter](#emitter) ·
[Extent / Span](#extent--span) ·
[Fabrik](#fabrik) ·
[Facts / Fakten](#facts--fakten) ·
[Gap / Lücke](#gap--lücke) ·
[hand-gelegt](#hand-gelegt) ·
[Host (Code-Block)](#host-code-block) ·
[Host / Schale](#host--schale-server) ·
[Include / Einbindung](#include--einbindung) ·
[Invarianten](#invarianten) ·
[Kante](#kante) ·
[Kantenmodus](#kantenmodus) ·
[Knoten](#knoten) ·
[Location](#location) ·
[Maschinerie](#maschinerie) ·
[Obergrenze](#obergrenze) ·
[Overlay](#overlay) ·
[OverwritePolicy](#overwritepolicy) ·
[PathProvider](#pathprovider) ·
[Pipeline](#pipeline) ·
[Pfeil / Arrow](#pfeil--arrow) ·
[Referenz](#referenz) ·
[Regel / Rule](#regel--rule) ·
[Rücksprung](#rücksprung) ·
[Schlüsselwort](#schlüsselwort) ·
[Schweregrad](#schweregrad) ·
[Senke / Sink](#senke--sink) ·
[Skipped (Skiped)](#skipped) ·
[Solution](#solution) ·
[Spalte / Column](#spalte--column) ·
[Spec / Artefakt](#spec--artefakt) ·
[Sprungziel](#sprungziel) ·
[Stempel / Stamp](#stempel--stamp) ·
[String / Zeichenkette](#string--zeichenkette) ·
[Suppression / Unterdrückung](#suppression--unterdrückung) ·
[Symbol](#symbol) ·
[Tail](#tail) ·
[Task](#task) ·
[TO (Transfer-Objekt)](#to--transfer-objekt) ·
[Transition](#transition) ·
[Trigger](#trigger) ·
[Trivia](#trivia) ·
[Durchlauf (Pass)](#durchlauf--pass) ·
[Weiche / Versions-Dispatch](#weiche--versions-dispatch) ·
[Wert-Slot](#wert-slot)

---

## §1 Übergreifend

### Symbol
Ein benanntes Element des semantischen Modells (Knoten, Task, Trigger, …), abgeleitet von `ISymbol`.
Durchgängig „Symbol" — konsistent, kein Konfliktbegriff.
**Symbol:** `ISymbol`, `Symbol`

### Location
Die **Location** — Datei plus zeichen- und zeilenbasierter Bereich eines Elements. Kanon ist der
Symbolname; im Fließtext wird die `Location` als solche benannt, nicht umschrieben.
**Kanon:** Location · **Symbol:** `Location`, `ISymbol.Location`
**verwirft:** Verortung, Quelltextposition, „Position" (als Eigenname der Klasse) ⚠ 🔸
🔸 *Empfehlung Location statt der drei deutschen Umschreibungen: der Typ ist im Fließtext sofort
wiedererkennbar, und „Fundstelle" bleibt so für die Referenz-Trefferstelle frei (s. u.). Gegenargument:
„Verortung" liest sich flüssiger — dann aber überall einheitlich.*

### Referenz
Ein **Verweis auf ein Symbol** — die Verwendungsstelle eines Knotens/Tasks, im Gegensatz zu seiner
Deklaration. Die gefundene Trefferzeile einer Referenzsuche heißt **Fundstelle**.
**Kanon:** Referenz (Verwendung eines Symbols); Fundstelle (Ergebniszeile der Suche)
**Symbol:** `INodeReferenceSymbol`, `ReferenceItem`, `.References`
**verwirft:** Verweis (als drittes Synonym neben Referenz/Fundstelle) ⚠
*„Referenz" und „Fundstelle" bezeichnen bewusst Verschiedenes; nur das dritte Wort „Verweis" fällt weg.*

### Diagnose / Diagnostics
Eine gemeldete Feststellung (Fehler/Warnung/Hinweis). Kanon im Singular/für den Katalog ist die
deutsche **Diagnose**; **Diagnostics** bleibt nur, wo es der Name des LSP-/Pull-Mechanismus ist
(Push-/Pull-Diagnostics, Cross-File-Diagnostics).
**Kanon:** Diagnose (die Feststellung); Diagnostics (der Protokoll-/Feature-Name)
**Symbol:** `Diagnostic`, `DiagnosticDescriptor`, `DiagnosticsComputer`
**verwirft:** Meldung, Meldungsvorlage (als Synonym für Diagnose) ⚠ 🔸
🔸 *Empfehlung: „Meldung" auf „Diagnose" vereinheitlichen; die de/en-Spaltung Diagnose (Katalog) vs.
Diagnostics (Workspace-Feature) ist sinnvoll und bleibt.*

### Schweregrad
Die Einstufung einer Diagnose (Error/Warning/…). Sauber und einheitlich übersetzt — Musterfall.
**Kanon:** Schweregrad · **Symbol:** `DiagnosticSeverity`

### Task
Die zentrale Workflow-Einheit der Nav-Sprache. Bewusst **nie** „Aufgabe" — vorbildlich konsistent.
**Kanon:** Task · **Symbol:** `ITaskDefinitionSymbol`, `ITaskNodeSymbol`

### Deklaration vs. Definition
Echte fachliche Unterscheidung, **keine** Schwankung: die *Deklaration* kündigt an (`taskref`), die
*Definition* führt aus. Beide Wörter bewusst getrennt halten.
**Symbol:** `ITaskDeclarationSymbol` vs. `ITaskDefinitionSymbol`

---

## §2 Syntax & Lexer

### Host (Code-Block)
Der **Host** eines Code-Blocks `[ … ]` — der Deklarationsknoten (bzw. die Datei-Wurzel), an dem die
eckige Klammer hängt und der bestimmt, welche Code-Schlüsselwörter dort zulässig sind.
**Kanon:** Host · **Symbol:** `NavCompletionContext.Host`, `CodeBlockFacts.HostKindOf`, „host-neutral"
**verwirft:** Wirt, Wirt-Knoten, wirt-abhängig ⚠
*Der auffälligste Fall: die Prosa sagt „Wirt", jedes Symbol sagt „Host" — teils im selben Satz. „Host"
ist im Deutschen etablierter Fachbegriff und deckt sich mit dem Symbol. Beiläufig: die Schreibweise
**„host-neutral"** (klein) vereinheitlichen.*

### Knoten
Ein **Knoten** des Workflow-Graphen (`init`, `view`, `dialog`, `choice`, `task`, `exit`, `end`).
Standard-Eindeutschung von *Node*, durchgängig verwendet — bleibt deutsch (das englische Symbol
`INodeSymbol` ändert daran nichts, s. Hausstil).
**Kanon:** Knoten · **Symbol:** `INodeSymbol`, `NodeDeclarationSyntax`
**verwirft:** Ecke, Ecken (graphentheoretisch *vertex*) ⚠

### Kante
Eine **Kante** — die gerichtete Verbindung zweier Knoten auf **Syntaxebene** (der Pfeil `-->`, `o->`,
`==>`). Pendant zu *Knoten*; das englische Symbol `EdgeSyntax`/`IEdge` bleibt englisch, die Prosa
sagt „Kante".
**Kanon:** Kante · **Symbol:** `EdgeSyntax`, `IEdge` · 🔸
🔸 *Empfehlung Kante (deutsch), parallel zu Knoten und in der Prosa bereits dominant. Gegenargument:
in Komposita steht oft „Edge" (`Edge-Operator`, `Edge-Keyword`); wer die Symbolnähe höher gewichtet,
könnte „Edge" wählen. Entscheidung wirkt sich auf* Kantenmodus/Kanten-Operator *aus.*
> **Kante** (Syntax, der Pfeil) vs. **Transition** (SemanticModel, Quelle→Ziel + Modus + Trigger) sind
> **verschiedene Ebenen** desselben Sachverhalts, kein Synonympaar.

### Kantenmodus
Der **Kantenmodus** — die vom Kanten-Operator kodierte Aufrufsemantik (Modal / NonModal / Goto). Der
**Kanten-Operator** ist das Token selbst (`-->`), der **Kantenmodus** seine Bedeutung; beide sind
distinkt und bleiben. „Aufruf-Art" ist ein drittes Wort für den Modus und fällt weg.
**Kanon:** Kantenmodus (die Semantik); Kanten-Operator (das Token)
**Symbol:** `EdgeMode`, `IEdgeModeSymbol`
**verwirft:** Aufruf-Art ⚠

### Schlüsselwort
Ein **Schlüsselwort** der Nav-Grammatik. Deutsche Prosa dominiert; „Keyword" nur, wo es Teil eines
etablierten Kompositums am Symbol ist (`NavKeywords`, `…Keyword`).
**Kanon:** Schlüsselwort · **Symbol:** `SyntaxTokenType.*Keyword` · 🔸
🔸 *Empfehlung Schlüsselwort in der Prosa; die Mischung „Code-Block-Keywords" ↔ „Schlüsselwort-Slot"
vereinheitlichen. Wer Symbolnähe bevorzugt, könnte durchgängig „Keyword" wählen.*

### String / Zeichenkette
Zwei Konzepte sauber trennen: das **Nav-String-Literal** (`"…"` als Token/Syntaxknoten) heißt
**String-Literal**; ein beliebiger `System.String`-Wert in technischer Prosa darf **Zeichenkette**
heißen (Standarddeutsch, unkritisch).
**Kanon:** String-Literal (Nav-Token); Zeichenkette (generischer String-Wert)
**Symbol:** `StringLiteralSyntax`, `IdentifierOrStringSyntax`
*Nicht das Nav-Literal als „Zeichenkette-Literal" bezeichnen — dort „String-Literal".*

### Trigger
Der **Trigger** einer Transition (`on Signal`) — der Auslöser des Übergangs. Kanon ist „Trigger"
(Symbol + dominante Prosa). Achtung: **doppelt belegt** — die *Trigger-Characters* der Completion
(Zeichen, die Vorschläge auslösen) sind ein anderes Konzept; dort **„Trigger-Zeichen"** verwenden,
nicht bloß „Auslöser".
**Kanon:** Trigger (Nav-Auslöser); Trigger-Zeichen (Completion)
**Symbol:** `ITriggerSymbol`, `TriggerSyntax`, `NavCompletionService.TriggerCharacters`
**verwirft:** Auslöser (als Synonym für den Nav-Trigger) ⚠

### Bedingung / Guard
Die **Bedingung** einer Transition (`if …`). „Guard" nur als erläuternde Klammer-Glosse.
**Kanon:** Bedingung · **Symbol:** `ConditionClauseSyntax`

### Tail
Der **Tail** einer Knoten-Deklaration — der Rest hinter Schlüsselwort/Name (vor der `do`-Klausel).
**Kanon:** Tail · **Symbol:** `NavCompletionContextKind.InitNodeTail`
**verwirft:** Schwanz ⚠ 🔸
🔸 *Empfehlung: „Tail" (deckt sich mit dem Symbol); „Schwanz" ist im Fachkontext befremdlich. Wer
Deutsch bevorzugt, nähme „Ende/Rest" — aber nicht „Schwanz".*

### Wert-Slot
Die Position hinter `do` bzw. `#version`, an der ein freier Wert/C#-Aufruf steht.
**Kanon:** Wert-Slot · **verwirft:** Werte-Slot (reine Schreibvariante) ⚠

### Trivia
Nicht-signifikanter Text zwischen Token (Whitespace, Kommentare, übersprungene Token). Englisches
Lehnwort, konsistent — bleibt.
**Kanon:** Trivia · **Symbol:** `SyntaxTrivia`, `GapTrivia`

### Skipped
Übersprungene Token (Panic-Mode / ungültige Direktiven), als Fehler-Trivia geführt.
**Kanon (Prosa):** übersprungene Token / Skip-Trivia
⚠ **Symbol-Tippfehler:** das öffentliche Symbol heißt `Skiped` (ein `p`) — u. a.
`TextClassification.Skiped`, `FileGeneratorAction.Skiped`, `FilesSkiped`. Die Umbenennung nach
`Skipped` ist ein **eigener** (API-brechender) Schritt, kein Glossar-Thema; bis dahin Symbolnamen
unverändert zitieren, in freier Prosa aber „übersprungen" schreiben.

### Bewohner
Keine Metapher für die im Body zulässigen Deklarationen. Statt „die einzigen Bewohner sind
Connection-Point-Deklarationen" schlicht: „die einzigen dort zulässigen Deklarationen".
**verwirft:** Bewohner, bewohnen ⚠

---

## §3 SemanticModel & Analyzer

### Transition
Eine **Transition** — Quelle → Ziel samt Kantenmodus und optionalem Trigger, auf **Modell**-Ebene.
Dominante Prosa und Symbol sind englisch; „Übergang" ist die Nebenform.
**Kanon:** Transition · **Symbol:** `ITransition`, `Transition`, `TriggerTransition` · 🔸
🔸 *Empfehlung Transition (Symbol + dominante Prosa). „Übergang" ist gültiges Deutsch (UML), aber es
proliferiert zu „Übergangsgraph"/„Übergangsart"; darum vereinheitlichen.*
**verwirft:** Übergang, Übergangsgraph, Übergangsart ⚠

### ConnectionPoint / Verbindungspunkt
Ein **Verbindungspunkt** — `init` / `exit` / `end` einer Task. Kanon deutsch (dominant), englisch nur
in Symbol-nahen Komposita. Der spezielle Exit-Verbindungspunkt bleibt „Verbindungspunkt", nicht
„Ausgang".
**Kanon:** Verbindungspunkt · **Symbol:** `IConnectionPointSymbol`, `ExitConnectionPointReferenceSymbol`
**verwirft:** Ausgang (für den Exit-Verbindungspunkt); Connection-Point (roh in deutscher Prosa) ⚠ 🔸
🔸 *Die Ordner spalten sich: `FindReferences`/`CodeFixes` schreiben „Verbindungspunkt",
`References`/`CallHierarchy` „Connection-Point". Empfehlung: deutsch, ordnerübergreifend.*

### Continuation / Fortsetzung
Der **Continuation**-Mechanismus (`--^` / `o-^`, ab Version 2) — Weiterreichen an eine Folge-Task.
Kanon „Continuation" (Symbole `ContinuationTransition`, `ShowContinuation…`). „Fortsetzung" wird
zusätzlich für konkrete Folgeaufrufe (Begin-/Exit-Rücksprung) benutzt; diese Grenze verschwimmt.
**Kanon:** Continuation · **Symbol:** `ContinuationTransition`, `ContinuationEdgeSyntax` · 🔸
🔸 *Empfehlung: „Continuation" fürs Feature/den Typ; „Fortsetzung" nur als generische Beschreibung
eines Folgeaufrufs, nicht als zweiter Name desselben Konzepts. Bei Bedarf ganz auf „Continuation"
vereinheitlichen.*
**verwirft:** Fortsetzung (als Synonym für den Continuation-Typ) ⚠

### Call
Ein aufgelöster **Aufruf** einer Transition — dedupliziert nach Ziel und Kantenmodus.
**Kanon:** Call (Symbol/Typ); Aufruf (freie Prosa) — beide zulässig, da eng am Symbol
**Symbol:** `Call`
*„Aufruf-Art" hier nicht verwenden — das ist der* Kantenmodus *(s. §2).*

### Erreichbarkeit
Ob ein Knoten vom `init` aus erreichbar ist. Sauber und einheitlich übersetzt — Musterfall.
**Kanon:** Erreichbarkeit, erreichbar/unerreichbar · **Symbol:** `IsReachable`

---

## §4 CodeGen & Generator

### CodeBuilder
Der einrückungs- und spaltenbewusste **Builder** für die C#-Codegenerierung.
**Kanon:** CodeBuilder / Builder · **Symbol:** `CodeBuilder`
**verwirft:** Textbauer ⚠
*Einmalige, befremdliche Eindeutschung von „Builder"; überall sonst heißt es schlicht „Builder".*

### CodeModel
Das **CodeModel** — das C#-nahe Zwischenmodell zwischen Semantic Model und Emitter.
**Kanon:** CodeModel · **Symbol:** `CodeModel`, `*CodeModel`
**verwirft:** Codemodell (deutsche Schreibung, nur in V2-Dateien) ⚠

### CodeInfo
Die **CodeInfo** — die Nav→C#-Namens- und Pfadschicht (welcher generierte Typ/Member zu welchem
Nav-Symbol gehört).
**Kanon:** CodeInfo · **Symbol:** `TaskCodeInfo`, `ChoiceCodeInfo`, …
*Wechselnde Bilder („Anker", „Namensalgebra", „Schicht") sind anschaulich, aber im Zweifel den
neutralen Begriff „CodeInfo" bzw. „Namens-/Pfadschicht" nutzen; „Anker" kollidiert mit dem
Ausrichtungs-Anker des CodeBuilder.*

### CallContext
Der **CallContext** — der pro Quelle generierte, benannte Aufruf-Kontext des V2-Codegens.
**Kanon:** CallContext · **Symbol:** `CallContextCodeModel`, `*CallContext`
**verwirft:** Schreibvarianten „Call-Context" / bloßes „Context" für denselben Typ ⚠
*Siehe auch* [Aufruffläche](#aufruffläche) *— die generierte Methodenfläche, ein verwandtes, aber
eigenes Konzept.*

### Aufruffläche
Die **Aufruffläche** — die im erzeugten Code sichtbare, flache Menge benannter Aufruf-Methoden einer
Quelle (das, was der CallContext trägt). Konsistent verwendete deutsche Prägung; bleibt.
**Kanon:** Aufruffläche 🔸
🔸 *Empfehlung behalten (durchgängig genutzt, beschreibt die generierte Oberfläche treffend, ist vom
C#-Typ `CallContext` klar abgegrenzt). Wer keine Prägungen mag, ersetzt sie durch „benannte
Aufruf-Methoden".*

### Maschinerie
Die **Maschinerie** — der generierte `{Task}WFSBase`-Rumpf (Begin-Wrapper, Unwrap-Methoden). Distinktiv
und durchgängig verwendet; bleibt.
**Kanon:** Maschinerie 🔸
🔸 *Empfehlung behalten (konsistent, eingeführt, unverwechselbar). Neutrale Alternative wäre
„Basisklassen-Rumpf" — nur bei Wunsch nach Entmetaphorisierung.*

### Facts / Fakten
Die **Facts** — versionierbare Codegen-Fakten (Methodennamen, Präfixe), abgegrenzt von den
[Invarianten](#invarianten).
**Kanon:** Facts (am Symbol); Codegen-Fakten (deutsche Prosa, Plural)
**Symbol:** `ICodeGenFacts`, `NavCodeGenFacts`
**verwirft:** „ein Fakt" (eingedeutschter Singular) ⚠ 🔸
🔸 *Empfehlung: Singular vermeiden; „ein versionierbarer Fact"/„…Facts-Eintrag" statt „ein Fakt".*

### Invarianten
Die versionsunabhängigen Konstanten des Codegens — Gegenstück zu den Facts.
**Kanon:** Invarianten · **Symbol:** `CodeGenInvariants`

### Weiche / Versions-Dispatch
Zwei Konzepte trennen: der **Versions-Dispatch** wählt anhand `#version` den passenden Codegenerator;
die im **erzeugten** Code stehende `switch(body)`-Verzweigung ist eine **switch-Verzweigung**. Nicht
beides „Weiche" nennen.
**Kanon:** Versions-Dispatch / Versions-Weiche (die Auswahl); switch-Verzweigung (der generierte switch)
**Symbol:** `VersionDispatchingCodeGenerator`
**verwirft:** „Weiche" für die generierte switch-Verzweigung; „Dispatcher" (englisch) für den Dispatch ⚠

### TO (Transfer-Objekt)
Die generierten **TO**-Klassen (`{View}TO`) — Transfer-Objekte je referenziertem View-Knoten.
**Kanon:** Transfer-Objekt (TO) · **Symbol:** `TOEmitter`, `TOCodeModel`
**verwirft:** Task-Objekt (falsche Auflösung des Kürzels) ⚠
*Inhaltlicher Fehler, keine Stilfrage: das Kürzel steht für „Transfer", nicht „Task".*

### Rücksprung
Der **Exit-Rücksprung** — die `After{Node}`-Folgemethode beim Verlassen einer Sub-Task.
**Kanon:** Rücksprung / Exit-Rücksprung · **Symbol:** `TaskExitCodeInfo`, `After{Node}`
*„(Exit-)Fortsetzung" hier vermeiden, um es von* [Continuation](#continuation--fortsetzung) *zu trennen.*

### Spec / Artefakt
Die **Spec** (`CodeGenerationSpec`) ist das Codegen-Ergebnisobjekt; das **Artefakt** ist die
resultierende Datei. Verwandte, aber nicht identische Begriffe — nicht synonym gebrauchen.
**Kanon:** Spec (das Objekt); Artefakt (die Datei) · **Symbol:** `CodeGenerationSpec`

### OverwritePolicy
Die Regel, ob eine Datei überschrieben werden darf.
**Kanon:** OverwritePolicy / Schreib-Policy · **Symbol:** `OverwritePolicy`, `FileGenerator`

### PathProvider
**Kanon:** PathProvider / Pfad-Provider (beide zulässig) · **Symbol:** `IPathProvider`
*Nur Bindestrich-/Sprachvariation, unkritisch — im selben Text konsistent halten.*

### Fabrik
Konsequente Eindeutschung von *Factory* — bleibt.
**Kanon:** Fabrik · **Symbol:** `*Factory`

### Emitter
Englisch, konsistent — bleibt.
**Kanon:** Emitter · **Symbol:** `*Emitter`, `IWfsEmitter`

### Pipeline
Englisch, konsistent — bleibt.
**Kanon:** Pipeline · **Symbol:** `NavCodeGeneratorPipeline`

---

## §5 Formatting & Text

### Gap / Lücke
Der **Zwischenraum** zwischen zwei Token (das, was der Formatter neu setzt). Prosa durchgängig
„Lücke", jedes Symbol „Gap".
**Kanon:** Lücke · **Symbol:** `GapContext`, `GapRules`, `GapRenderer`, `GapTrivia`, `IGapRule` · 🔸
🔸 *Empfehlung Lücke (in der Prosa ausnahmslos konsistent, analog zu Knoten/Kante). Alternative „Gap"
nur, wenn maximale Symbolnähe gewünscht ist — beträfe viele Komposita.*

### Alignment / Ausrichtung
Die spaltenweise **Ausrichtung** (Pfeile, Trigger, …) im Vorpass.
**Kanon:** Ausrichtung · **Symbol:** `AlignmentMap`, `AlignmentMapBuilder`

### Spalte / Column
**Kanon:** Spalte · **Symbol:** `ColumnId`, `AlignmentColumnPolicy`

### Pfeil / Arrow
**Kanon:** Pfeil · **Symbol:** `ColumnId.Arrow`, `AddArrowColumns`

### Einrückung / Einzug
Die **Einrückung** einer Zeile. Zwei deutsche Wörter im Umlauf — auf eines festlegen.
**Kanon:** Einrückung · **Symbol:** `IndentStyle`, `IndentSize`, `ComputeIndentDepth` · 🔸
🔸 *Empfehlung Einrückung (geläufiger im Code-/Typografie-Deutsch). „Einzug" konsequent ersetzen —
oder umgekehrt, aber nicht beides nebeneinander.*
**verwirft:** Einzug (als zweite Form) ⚠

### Statement / Anweisung
Eine **Anweisung** — die Grammatik-Zeile (Transition, Deklaration). Achtung **doppelt belegt**: der
Wert hinter `do` ist **kein** „Statement" in diesem Sinn — dort „Wert-Slot"/„do-Aufruf" sagen.
**Kanon:** Anweisung · **Symbol:** `StatementFacts`, `EnumerateStatements`
*Für das Statement-Konzept konsequent „Anweisung", nicht „Satz-/Satzanfang".*

### Suppression / Unterdrückung
Die **Unterdrückung** von Formatierung (verbatim gehaltene Regionen).
**Kanon:** Unterdrückung · **Symbol:** `FormatterSuppression` · 🔸
🔸 *Empfehlung deutsch „Unterdrückung" in der Prosa; das rohe „Suppression" nur als Symbolzitat. In
`StatementFacts` stehen beide Formen nebeneinander.*

### Regel / Rule
**Kanon:** Regel · **Symbol:** `IGapRule`, `GapRules`, `RulePriority`

### hand-gelegt
Eine **hand-gelegte** Anweisung — vom Autor bewusst mehrzeilig umbrochen; der Formatter hält ihr
Inneres verbatim. Durchgängige Lehnübersetzung von `IsHandLaid`; bleibt.
**Kanon:** hand-gelegt · **Symbol:** `AlignmentMapBuilder.IsHandLaid` 🔸
🔸 *Empfehlung behalten (konsistent, klar am Symbol). Wer die Lehnübersetzung meiden will: „manuell
umbrochen" — dann durchgängig.*

### Obergrenze
Das **Leerzeilen-Limit** (`MaxBlankLines`), auf das aufeinanderfolgende Leerzeilen gekappt werden.
**Kanon:** Obergrenze / Leerzeilen-Limit · **Symbol:** `MaxBlankLines`
**verwirft:** Deckel („auf den Deckel kappen") ⚠ 🔸
🔸 *Empfehlung: „Deckel" (Alltagswort) durch „Obergrenze"/„Limit" ersetzen. Da durchgängig verwendet,
ist es ein leichter, lohnender Sweep.*

### Durchlauf / Pass
Ein **Durchlauf** des Formatters. Drei Wörter im Umlauf: „Durchlauf", „Lauf", „Vorpass".
**Kanon:** Durchlauf (allgemein); Vor-Durchlauf (für einen vorgeschalteten Pass)
**verwirft:** „Lauf" als drittes Synonym; „Vorpass" (→ „Vor-Durchlauf") ⚠ 🔸
🔸 *Empfehlung „Durchlauf"; „Vorpass" ist eine Roslyn-Anleihe — wenn die Kürze geschätzt wird, kann
„Vorpass" als Fachbegriff bleiben, dann aber „Lauf/Durchlauf" darum herum vereinheitlichen.*

### Extent / Span
Der **Extent** — ein zeichenbasierter Bereich (Start + Länge). Für den einen Begriff kursieren ≥ 5
Wörter; auf Symbolnähe festlegen.
**Kanon:** Extent (der Typ `TextExtent`/`IExtent`); Span (nur für `.Span`); „Bereich/Ausschnitt" als
gelegentliche freie Umschreibung
**Symbol:** `TextExtent`, `IExtent`, `.Extent`, `.Span`
**verwirft:** Fenster, Fensterausschnitt ⚠
*„Fenster(ausschnitt)" für einen Text-Extent ist der befremdlichste Ausreißer; „Bereich (Extent)" als
Klammer-Glosse ist ok, aber im Zweifel den Typnamen nennen.*

### SourceText / Quelltext
**Kanon:** Quelltext (Prosa); SourceText (der Typ) · **Symbol:** `SourceText`, `StringSourceText`

### DisplayParts
Die klassifizierten **Anzeigeteile** eines Tooltip-/Ergebnistexts.
**Kanon:** Anzeigeteil (deutsch) bzw. DisplayPart (am Symbol) — im Text konsistent
**Symbol:** `ClassifiedText`, `DisplayPartsBuilder`
*Nicht „Stück" und „Teil" mischen.*

### Whitespace
**Kanon:** Whitespace (am Symbol) bzw. Leerraum — konsistent; nicht zusätzlich „Zwischenraum"/
„Leerzeichen" für dasselbe. · **Symbol:** `TextClassification.Whitespace`

---

## §6 Workspace, Hosts & Features

### Disk / Platte
Der Persistenz-Zustand einer Datei „auf Platte" gegenüber dem Overlay (offener Editor-Puffer).
**Kanon:** Disk (in Komposita: Disk-Stempel, Disk-Cache, Disk-Provider) · **Symbol:** `DiskStamp` · 🔸
🔸 *Empfehlung: die Substantiv-Komposita an `DiskStamp` ausrichten („Disk-…"), die idiomatische
Wendung „von/auf Platte" darf als Prosa bleiben. Wer ganz deutsch will, nimmt durchgängig „Platte" —
dann aber weg vom Symbolwort „Disk".*
**verwirft:** Platten-Stempel, Platten-Cache, Platten-Provider (uneinheitlich zu `DiskStamp`) ⚠

### Host / Schale (Server)
Der **Host** eines Servers (LSP-/MCP-Server als Wirtsprozess der geteilten Engine-Schicht).
**Kanon:** Host (Server-Host, LSP-/MCP-Host); Host-Schicht (die geteilte „eine Engine"-Ebene)
**verwirft:** Schale (Server-Schale, MCP-Schale, aufrufende Schale) ⚠
*„Schale" ist eine befremdliche Eindeutschung von shell/host — deckt sich mit dem
[Host-Befund aus §2](#host-code-block): „Host" ist der etablierte Begriff.*

### Include / Einbindung
Der **Include**-Mechanismus (eine `.nav`-Datei bindet eine andere ein).
**Kanon:** Include, inkludieren · **Symbol:** `IIncludeSymbol`, `IncludeDependencyGraph` · 🔸
🔸 *Empfehlung: am Symbol/Nav-Konzept „Include" ausrichten. „Einbindung/einbindende Datei" ist gutes
Deutsch, aber „Inkludierer" ist eine Prägung — im Zweifel „einbindende Datei" statt „Inkludierer".*
**verwirft:** Inkludierer ⚠

### Dependency / Abhängigkeit
Durchgängig deutsch — Musterfall.
**Kanon:** Abhängigkeit · **Symbol:** `Dependency`, `DependencyItem`

### Stempel / Stamp
Ein **Stempel** — ein Versionsmerkmal zur Cache-Validierung.
**Kanon:** Stempel · **Symbol:** `VersionStamp`, `DiskStamp`
*Konsistente Eindeutschung von „stamp"; „Datei-Stempel" und „Disk-Stempel" meinen denselben
`DiskStamp` — konsistent benennen (s. [Disk](#disk--platte)).*

### Solution
Englisch, konsistent (nie „Projektmappe") — bleibt.
**Kanon:** Solution · **Symbol:** `NavSolution`

### Overlay
Englisch, konsistent — bleibt.
**Kanon:** Overlay · **Symbol:** `OverlaySyntaxProvider`

### Aufrufhierarchie
Die **Aufrufhierarchie** (Call Hierarchy) auf Task-Ebene.
**Kanon:** Aufrufhierarchie · **Symbol:** `NavCallHierarchyService`
**verwirft:** Aufrufliste ⚠
*Schon in derselben Klassen-Doku wechseln „Aufrufliste" und „Aufrufhierarchie".*

### Sprungziel
Das **Sprungziel** einer Go-To-Navigation.
**Kanon:** Sprungziel · **Symbol:** `GoToTargetResolver`
**verwirft:** Navigationsziel (als zweite Form) ⚠ 🔸
🔸 *Empfehlung „Sprungziel" (dominant); „Navigationsziel" angleichen.*

### Senke / Sink
Der **Ergebnis-Sink** einer Referenzsuche bzw. die **Ausgabe-Senke** eines Loggers — beides legitime
Datenfluss-Metaphern (CS „sink"); bleiben.
**Kanon:** Senke · **Symbol:** `IFindReferencesContext` (FindReferences), `ILogger` (Ausgabe) 🔸
🔸 *Empfehlung behalten. Roslyn nennt den FindReferences-Sink „Context"; wer Roslyn-Nähe will,
schriebe „Context" — die Metapher „Senke" ist aber verständlich und konsistent.*

---

## Anhang A — Abweichungs-Fundstellen

Arbeitsliste für den späteren Rewrite: Stellen, an denen der Ist-Zustand noch eine **verworfene**
Variante nutzt. Nach Kanon-Begriff. (Symbol-Zitate wie `TextClassification.Skiped` sind hier **nicht**
gelistet — Symbolnamen bleiben, bis sie separat umbenannt werden.)

### Host — verwirft „Wirt" (⚠ ~52 Stellen)
- `Completion/NavCompletionService.cs`: 133, 342, 343, 346, 497, 498
- `Completion/NavCompletionContext.cs`: 28, 174, 178, 246, 497, 499, 500, 511, 519, 525, 527, 555
- `Syntax/CodeBlockFacts.cs`: 12, 14, 50, 51, 55, 67, 83, 91, 98, 99, 101
- `Syntax/SyntaxFacts.cs`: 187, 188, 210, 212, 213, 214, 217, 231, 241, 242, 260, 261
- `Syntax/NavParser.cs`: 285, 312, 1691, 1695, 1709
- `Syntax/CodeAbstractMethodDeclarationSyntax.cs`: 15 · `Syntax/CodeSyntax.cs`: 13 ·
  `Syntax/CodeResultDeclarationSyntax.cs`: 9 · `Syntax/CodeParamsDeclarationSyntax.cs`: 9 ·
  `Syntax/CodeNotImplementedDeclarationSyntax.cs`: 14 · `Syntax/CodeNamespaceDeclarationSyntax.cs`: 9
- `Formatting/GapRules.cs`: 416
- Schreibweise „host-neutral" vereinheitlichen: `Syntax/SyntaxFacts.cs`: 188, 231

### Knoten — verwirft „Ecke" (⚠)
- `SemanticModel/INodeSymbol.cs`: 8 · `SemanticModel/ITaskDefinitionSymbol.cs`: 60

### Tail — verwirft „Schwanz" (⚠)
- `Completion/NavCompletionService.cs`: 179
- `Completion/NavCompletionContext.cs`: 105, 369, 482

### Bewohner — Metapher entfernen (⚠)
- `Completion/NavCompletionContext.cs`: 50, 279

### Kantenmodus — verwirft „Aufruf-Art" (⚠)
- `SemanticModel/Call.cs`: 12, 62 · `SemanticModel/EdgeModeSymbol.cs`: 18 ·
  `SemanticModel/IEdgeModeSymbol.cs`: 6, 11 · `Syntax/EdgeSyntax.cs`: 9, 29, 42

### Transition — verwirft „Übergangsgraph/Übergangsart" (⚠)
- `SemanticModel/Call.cs`: 11 · `SemanticModel/IEdge.cs`: 6 ·
  `SemanticAnalyzer/Nav0122DifferentViewsInContinuationNotSupported.cs`: 35

### CodeBuilder — verwirft „Textbauer" (⚠)
- `CodeGen/Shared/CodeBuilder.cs`: 12

### CodeModel — verwirft „Codemodell" (⚠)
- `CodeGen/V2/CodeModel/WfsCodeModelV2.cs`: 13, 55 ·
  `CodeGen/V2/CodeModel/WfsBaseCodeModelV2.cs`: 13, 78 ·
  `SemanticModel/CodeGenerationUnitExtensions.cs`: 32 · `SemanticModel/EdgeExtensions.cs`: 14

### TO — verwirft „Task-Objekt" (⚠)
- `CodeGen/GenerationOptions.cs`: 40

### Facts — verwirft eingedeutschten Singular „Fakt" (⚠)
- `CodeGen/V1/Emitters/IBeginWfsEmitter.cs`: 13, 67 · `CodeGen/VersionDispatchingCodeGenerator.cs`: 13

### Obergrenze — verwirft „Deckel" (⚠)
- `Formatting/NavFormattingOptions.cs`: 16, 90, 93, 97, 99, 104, 107, 111
- `Formatting/GapRenderer.cs`: 146, 147, 150, 196, 248, 310, 311, 315, 329
- `Formatting/GapRules.cs`: 234, 235 · `Formatting/GapLayout.cs`: 62

### Extent — verwirft „Fenster/Fensterausschnitt" (⚠)
- `Text/ClassifiedTextExtensions.cs`: 33

### Location — verwirft „Verortung/Quelltextposition" (⚠)
- `Common/Location.cs`: 13, 28, 38, 51, 62, 75, 81, 96, 101, 111, 116, 117, 122, 142, 188, 193
- `FindReferences/DefinitionItem.cs`: 35 · `FindReferences/ReferenceItem.cs`: 25, 90, 113
- `Dependencies/DependencyItem.cs`: 24 (Quelltextposition)

### Disk — verwirft „Platten-…" (⚠)
- `Workspace/OverlaySyntaxProvider.cs`: 16, 18, 20, 33, 39, 44, 50, 59, 101, 108, 110, 129
- `Workspace/NavWorkspaceCore.cs`: 22, 97, 107, 108

### Host (Server) — verwirft „Schale" (⚠)
- `Workspace/NavWorkspaceCore.cs`: 20, 21, 137 · `Workspace/OverlaySyntaxProvider.cs`: 23 ·
  `Workspace/DiagnosticsComputer.cs`: 16

### Aufrufhierarchie — verwirft „Aufrufliste" (⚠)
- `CallHierarchy/NavCallHierarchyService.cs`: 14

### Sprungziel — verwirft „Navigationsziel" (⚠)
- `Common/Location.cs`: 17

### Wert-Slot — verwirft „Werte-Slot" (⚠)
- `Completion/NavCompletionContext.cs`: 40, 583, 600

> Nicht einzeln aufgelistet, weil breit gestreut (per `grep` im Rewrite präzise zu ziehen):
> **Auslöser→Trigger**, **Übergang→Transition** (Fließtext), **Verweis→Referenz**,
> **Meldung→Diagnose**, **Suppression→Unterdrückung**, **Einzug→Einrückung**,
> **Fortsetzung→Continuation**, **Vorpass/Lauf→Durchlauf**.

## Anhang B — Tippfehler-Hygiene (kein Terminologie-Thema)

Beim Sichten mit aufgefallen — reine Rechtschreibung, hier nur festgehalten:

- **`Skiped`** statt `Skipped` — im **öffentlichen API** zementiert (`TextClassification.Skiped`,
  `FileGeneratorAction.Skiped`, `FilesSkiped`) sowie in zahllosen Kommentaren. Korrektur = eigener,
  API-brechender Schritt.
- **„quitierung"** statt „Quotierung": `Text/StringExtensions.cs`: 257
- **„Eingentlich"** statt „Eigentlich": `Text/DisplayPartsBuilder.cs`: 63
- **„Liefer"** statt „Liefert": `Text/StringExtensions.cs`: 105, 115
