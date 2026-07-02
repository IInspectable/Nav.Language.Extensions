#region Using Directives

using System.Linq;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// Die grammatische Situation an der Cursor-Position — abgeleitet aus dem (recovery-festen) Syntaxbaum statt
/// aus einem zeilenbegrenzten Text-Rückwärtsscan. Entscheidet, <em>welche</em> Kategorie von Vorschlägen an
/// dieser Stelle überhaupt sinnvoll ist.
/// </summary>
enum NavCompletionContextKind {

    /// <summary>
    /// Keine Vorschläge (Kommentar, Zeichenkette, C#-Inhalt eines Code-Blocks, Wert-Slot hinter <c>do</c>
    /// oder außerhalb des Texts).
    /// </summary>
    Suppress,

    /// <summary>
    /// Im Schlüsselwort-Slot einer Code-Deklaration direkt hinter <c>[</c> (z.B. <c>[using …]</c>,
    /// <c>[result …]</c>): die Code-Block-Schlüsselwörter. <em>Welche</em> zulässig sind, hängt vom Wirt des
    /// Blocks ab (<see cref="NavCompletionContext.Host"/>).
    /// </summary>
    CodeBlock,

    /// <summary>
    /// Direkt hinter dem <c>#</c> einer Präprozessor-Direktive (Schlüsselwort-Slot): das
    /// Direktiv-Schlüsselwort — derzeit ausschließlich <c>version</c>.
    /// </summary>
    DirectiveKeyword,

    /// <summary>
    /// Hinter <c>#version </c> im Werte-Slot: die gültigen Sprach-Versionsnummern
    /// (<see cref="NavLanguageVersion.SupportedVersions"/>).
    /// </summary>
    DirectiveVersionValue,

    /// <summary>Member-Ebene (außerhalb einer Task-Definition): nur <c>task</c> / <c>taskref</c>.</summary>
    MemberLevel,

    /// <summary>
    /// Satzanfang im Body einer <c>taskref</c>-Deklaration (<c>taskref Sub { … }</c>): die einzigen dort
    /// grammatisch zulässigen Bewohner sind Connection-Point-Deklarationen — <c>init</c> / <c>exit</c> /
    /// <c>end</c>. KEINE Member-Keywords (<c>task</c>/<c>taskref</c>) und keine Knoten-/Transitions-Konstrukte.
    /// </summary>
    ConnectionPointDeclaration,

    /// <summary>Hinter <c>task</c> innerhalb eines Task-Bodies (Task-Knoten): die deklarierten Tasks.</summary>
    TaskNodeName,

    /// <summary>
    /// Satzanfang im <b>Knoten-Deklarations-Block</b> des Task-Bodys (direkt hinter <c>{</c> oder dem <c>;</c>
    /// einer Knoten-Deklaration): die Knoten-Deklarations-Keywords sowie die vorhandenen quellfähigen Knoten
    /// (als Beginn der ersten Transition). Der Deklarations-Block ist hier noch offen — beides ist möglich.
    /// </summary>
    StatementStart,

    /// <summary>
    /// Satzanfang im <b>Transitions-Block</b> (hinter dem <c>;</c> einer (Exit-)Transition): der
    /// Deklarations-Block ist abgeschlossen, nur noch eine weitere Transition kann folgen — die quellfähigen
    /// Knoten plus das <c>init</c>-Schlüsselwort (Init-Transition). KEINE Knoten-Deklarations-Keywords.
    /// </summary>
    TransitionStart,

    /// <summary>Hinter einem Quellknoten: die (sichtbaren) Edge-Keywords.</summary>
    EdgeSlot,

    /// <summary>Hinter einer Edge: die Knoten (als Ziel) sowie das Ziel-Keyword <c>end</c>.</summary>
    TargetSlot,

    /// <summary>Hinter <c>Knoten:</c> einer Exit-Transition: die Exit-Connection-Points des Knotens.</summary>
    ExitConnectionPoint,

    /// <summary>Hinter einem vollständigen Ziel: die Folge-Klauseln <c>on</c> / <c>if</c> / <c>else</c> / <c>do</c>.</summary>
    AfterTarget,

    /// <summary>Innerhalb/hinter einem Trigger (<c>on …</c>): <c>if</c> / <c>else</c> / <c>do</c>.</summary>
    AfterTrigger,

    /// <summary>Innerhalb/hinter einer Bedingung (<c>if …</c>): <c>do</c>.</summary>
    AfterCondition,

    /// <summary>
    /// Nicht eindeutig klassifizierbar — der Aufrufer fällt auf das konservative Alt-Verhalten zurück
    /// (vorhandene Knoten + sichtbare Keywords), damit nie weniger als bisher angeboten wird.
    /// </summary>
    Fallback

}

/// <summary>
/// Der „Wirt" eines Code-Blocks (<c>[ … ]</c>) — er bestimmt, welche Code-Schlüsselwörter dort grammatisch
/// zulässig sind. Die zugehörigen Grammatikregeln stehen im <c>NavParser</c> (jeweils die optionalen
/// <c>code*</c>-Deklarationen des Wirts).
/// </summary>
enum CodeBlockHost {

    /// <summary>Datei-Ebene (Kopf der CodeGenerationUnit): <c>namespaceprefix</c>, <c>using</c>.</summary>
    CompilationUnit,

    /// <summary><c>taskref</c>-Deklaration: <c>namespaceprefix</c>, <c>result</c> (<c>notimplemented</c> ist versteckt).</summary>
    TaskRef,

    /// <summary><c>task</c>-Definitions-Kopf: <c>code</c>, <c>base</c>, <c>generateto</c>, <c>params</c>, <c>result</c>.</summary>
    TaskDefinition,

    /// <summary><c>init</c>-Knoten: <c>abstractmethod</c>, <c>params</c>.</summary>
    InitNode,

    /// <summary><c>task</c>-Knoten: <c>donotinject</c>, <c>abstractmethod</c>.</summary>
    TaskNode

}

/// <summary>
/// Bestimmt die <see cref="NavCompletionContextKind"/> an einer Cursor-Position. Trägt zusätzlich die
/// für die jeweilige Kategorie nötigen Bezugspunkte: die umschließende Task-Definition, — bei
/// <see cref="NavCompletionContextKind.ExitConnectionPoint"/> — den Namen des Exit-Knotens, und — bei
/// <see cref="NavCompletionContextKind.CodeBlock"/> — den <see cref="Host"/> des Code-Blocks.
/// </summary>
sealed class NavCompletionContext {

    NavCompletionContext(NavCompletionContextKind kind, ITaskDefinitionSymbol task, string exitNodeName, CodeBlockHost host) {
        Kind         = kind;
        Task         = task;
        ExitNodeName = exitNodeName;
        Host         = host;
    }

    public NavCompletionContextKind Kind { get; }

    [CanBeNull]
    public ITaskDefinitionSymbol Task { get; }

    [CanBeNull]
    public string ExitNodeName { get; }

    /// <summary>Der Wirt eines Code-Blocks — nur für <see cref="NavCompletionContextKind.CodeBlock"/> aussagekräftig.</summary>
    public CodeBlockHost Host { get; }

    static NavCompletionContext Of(NavCompletionContextKind kind, ITaskDefinitionSymbol task = null,
                                   string exitNodeName = null, CodeBlockHost host = CodeBlockHost.CompilationUnit)
        => new(kind, task, exitNodeName, host);

    [NotNull]
    public static NavCompletionContext Classify([NotNull] CodeGenerationUnit unit, int position) {

        var tree   = unit.Syntax.SyntaxTree;
        var source = tree.SourceText;

        if (position < 0 || position > source.Length) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Kommentare über die angehängte Trivia (Roslyn-Modell), nicht über einen Text-Scan.
        if (tree.IsPositionInComment(position)) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Präprozessor-Direktiven (`#…`) liegen als strukturierte Trivia NEBEN dem flachen Token-Strom; die
        // Token-Binärsuche unten erreicht sie nicht. Daher zuerst prüfen, ob der Cursor in einer Direktive steht.
        if (DirectiveContext(tree, position) is { } directiveKind) {
            return Of(directiveKind);
        }

        var line         = source.GetTextLineAtPosition(position);
        var lineText     = source.Substring(line.ExtentWithoutLineEndings);
        var linePosition = position - line.Start;

        // Keine Vorschläge in Zeichenketten ("…") — die taskref-Pfad-Vervollständigung läuft separat (vor uns).
        if (lineText.IsInQuotation(linePosition)) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Das signifikante Token LINKS der Position bzw. links des gerade getippten Wortes — der eigentliche
        // Kontext-Anker. Trägt über seinen Parent-Knoten die grammatische Rolle (Quelle/Ziel/Edge/Klausel).
        var contextToken = ContextToken(tree, position);

        // Code-Blöcke ([ … ]): direkt hinter `[` (Schlüsselwort-Slot) die Code-Block-Keywords, im C#-Inhalt
        // dahinter nichts. Der zeilenbegrenzte Scan sieht das öffnende `[` nur auf derselben Zeile (mehrzeilige
        // Blöcke bleiben offen, siehe doc/nav-completion-status.md, C4).
        if (lineText.IsInTextBlock(linePosition, SyntaxFacts.OpenBracket, SyntaxFacts.CloseBracket)) {
            return contextToken.Type == SyntaxTokenType.OpenBracket
                       ? Of(NavCompletionContextKind.CodeBlock, host: CodeBlockHostAt(tree, contextToken))
                       : Of(NavCompletionContextKind.Suppress);
        }

        // Kein tragendes Token (leere Datei, Position hinter dem letzten Member): Member-Ebene.
        if (contextToken.IsMissing || contextToken.Parent == null) {
            return Of(NavCompletionContextKind.MemberLevel);
        }

        // Eine schließende Klammer beendet den Task-Body — dahinter sind wir wieder auf Member-Ebene.
        if (contextToken.Type == SyntaxTokenType.CloseBrace) {
            return Of(NavCompletionContextKind.MemberLevel);
        }

        var task = EnclosingTask(unit, contextToken);

        // Hinter `task`: im Body referenziert das einen deklarierten Task (Task-Knoten); auf Member-Ebene
        // (Task-Definition) tippt der Nutzer dagegen einen neuen Namen → dort nichts anbieten.
        if (contextToken.Type == SyntaxTokenType.TaskKeyword) {
            return contextToken.Parent is TaskNodeDeclarationSyntax
                ? Of(NavCompletionContextKind.TaskNodeName, task)
                : Of(NavCompletionContextKind.Suppress);
        }

        // taskref-Body: dessen einzige Bewohner sind Connection-Point-Deklarationen (init/exit/end). Ein
        // taskref ist eine TaskDeclaration (kein ITaskDefinitionSymbol) und fiele daher unten auf die
        // Member-Ebene zurück — dort böte die Completion fälschlich task/taskref an. Stattdessen: am
        // Satzanfang im Body (direkt hinter `{` oder dem `;` eines Connection-Points) die Connection-Point-
        // Keywords; an jeder anderen Stelle im Body (der Connector-Name ist ein freier Bezeichner) nichts.
        if (EnclosingTaskDeclaration(contextToken) != null) {
            var atStatementStart = contextToken.Type == SyntaxTokenType.OpenBrace ||
                                    (contextToken.Type  == SyntaxTokenType.Semicolon &&
                                     contextToken.Parent is ConnectionPointNodeSyntax);

            return Of(atStatementStart
                          ? NavCompletionContextKind.ConnectionPointDeclaration
                          : NavCompletionContextKind.Suppress);
        }

        // Außerhalb jeder Task-Definition → Member-Ebene.
        if (task == null) {
            return Of(NavCompletionContextKind.MemberLevel);
        }

        // Hinter `Knoten:` einer Exit-Transition → deren Exit-Connection-Points.
        if (contextToken.Type == SyntaxTokenType.Colon &&
            contextToken.Parent is ExitTransitionDefinitionSyntax exitColon) {
            return Of(NavCompletionContextKind.ExitConnectionPoint, task, exitColon.SourceNode?.Name);
        }

        // Hinter einer Edge → Ziel-Position.
        if (contextToken.Parent is EdgeSyntax) {
            return Of(NavCompletionContextKind.TargetSlot, task);
        }

        // Satzanfang im Body. Der Task-Body ist grammatisch zweigeteilt und geordnet: erst der
        // Knoten-Deklarations-Block, dann der Transitions-Block (siehe NavParser.ParseTaskDefinition).
        // Direkt hinter `{` (Body-Öffnung) stehen wir am Anfang — der Deklarations-Block ist noch offen.
        if (contextToken.Type == SyntaxTokenType.OpenBrace) {
            return Of(NavCompletionContextKind.StatementStart, task);
        }

        // Hinter einem `;` entscheidet die Art der abgeschlossenen Anweisung über die Region: nach einer
        // Knoten-Deklaration ist der Deklarations-Block noch offen (Deklaration ODER erste Transition möglich →
        // StatementStart); nach einer (Exit-)Transition ist er abgeschlossen — es kann nur noch eine weitere
        // Transition folgen (TransitionStart), Deklarations-Keywords ergeben hier keinen Sinn mehr.
        if (contextToken.Type == SyntaxTokenType.Semicolon) {
            return contextToken.Parent is TransitionDefinitionSyntax or ExitTransitionDefinitionSyntax
                       ? Of(NavCompletionContextKind.TransitionStart, task)
                       : Of(NavCompletionContextKind.StatementStart, task);
        }

        // Rolle des Tokens über den INNERSTEN tragenden Knoten seiner Ancestor-Kette. Der Kontext-Anker steckt
        // je nach Konstrukt direkt in diesem Knoten (Quell-/Ziel-Token, Trigger-/Klausel-Keyword) ODER — bei
        // gefülltem Wert-Slot — eine Ebene tiefer im Identifier(OrString)-Wert (`on Signal`, `if Bedingung`,
        // `do Aufruf`). Nur den direkten Parent zu betrachten verfehlt den gefüllten Fall (Parent ist dann der
        // Wert, nicht die Klausel) und ließe ihn auf den pauschalen Fallback fallen.
        switch (ClassificationNode(contextToken.Parent)) {

            // Quellknoten (auch der Connector-Name `se` in `Sub:se`) → als Nächstes folgt die Edge.
            case SourceNodeSyntax:
            case ExitTransitionDefinitionSyntax when contextToken.Type == SyntaxTokenType.Identifier:
                return Of(NavCompletionContextKind.EdgeSlot, task);

            // Zielknoten → danach die Folge-Klauseln.
            case TargetNodeSyntax:
                return Of(NavCompletionContextKind.AfterTarget, task);

            // Trigger (`on …` / `spontaneous`) — auch mit gefülltem Signal → danach if/else/do.
            case TriggerSyntax:
                return Of(NavCompletionContextKind.AfterTrigger, task);

            // Bedingung (`if …` / `else …`) — auch mit gefülltem Wert → danach do.
            case ConditionClauseSyntax:
                return Of(NavCompletionContextKind.AfterCondition, task);

            // Hinter `do` steht der Wert-Slot: ein freier C#-Aufruf (identifierOrString), kein Nav-Konstrukt —
            // auch mit gefülltem Wert. Nichts anbieten, statt über den Fallback pauschal Knoten/Keywords zu streuen.
            case DoClauseSyntax:
                return Of(NavCompletionContextKind.Suppress);
        }

        return Of(NavCompletionContextKind.Fallback, task);
    }

    /// <summary>
    /// Der innerste Knoten der Ancestor-Kette (inkl. <paramref name="node"/> selbst), der eine für die
    /// Completion tragende grammatische Rolle hat — Quell-/Ziel-Knoten, Exit-Transition, Trigger, Bedingung
    /// oder <c>do</c>-Klausel. Anders als ein Blick nur auf den direkten Parent erfasst das auch den Fall, dass
    /// der Kontext-Anker im <em>Wert-Slot</em> einer Klausel steckt (<c>on Signal</c>, <c>if Bedingung</c>,
    /// <c>do Aufruf</c>): dessen direkter Parent ist der Identifier(OrString)-Wert, die tragende Rolle erst
    /// dessen Elternklausel. „Innerster zuerst" ist entscheidend — in einer Exit-Transition liegen Ziel/Trigger/
    /// Bedingung/do INNERHALB der <see cref="ExitTransitionDefinitionSyntax"/>; deren spezifischere Rolle muss
    /// gewinnen, die Exit-Transition selbst bleibt nur der Anker für ihren Connector-Namen.
    /// </summary>
    [CanBeNull]
    static SyntaxNode ClassificationNode([CanBeNull] SyntaxNode node) {

        if (node == null) {
            return null;
        }

        foreach (var ancestor in node.AncestorsAndSelf()) {
            switch (ancestor) {
                case SourceNodeSyntax:
                case TargetNodeSyntax:
                case TriggerSyntax:
                case ConditionClauseSyntax:
                case DoClauseSyntax:
                case ExitTransitionDefinitionSyntax:
                    return ancestor;
            }
        }

        return null;
    }

    /// <summary>
    /// Die umschließende Task-Definition (Symbol) zum gegebenen Token — oder <c>null</c> auf Member-Ebene.
    /// </summary>
    [CanBeNull]
    static ITaskDefinitionSymbol EnclosingTask(CodeGenerationUnit unit, SyntaxToken token) {

        var taskSyntax = token.Parent?
                              .AncestorsAndSelf()
                              .OfType<TaskDefinitionSyntax>()
                              .FirstOrDefault();

        if (taskSyntax == null) {
            return null;
        }

        return unit.TaskDefinitions.FirstOrDefault(t => t.Syntax.Extent == taskSyntax.Extent);
    }

    /// <summary>
    /// Die umschließende <c>taskref</c>-Deklaration (<see cref="TaskDeclarationSyntax"/>) zum gegebenen Token —
    /// oder <c>null</c>, wenn das Token nicht innerhalb einer solchen liegt. Anders als eine
    /// <see cref="TaskDefinitionSyntax"/> trägt sie kein <see cref="ITaskDefinitionSymbol"/>; für die Completion
    /// im taskref-Body genügen die statischen Connection-Point-Keywords, ein Symbol wird nicht benötigt.
    /// </summary>
    [CanBeNull]
    static TaskDeclarationSyntax EnclosingTaskDeclaration(SyntaxToken token) {
        return token.Parent?
                    .AncestorsAndSelf()
                    .OfType<TaskDeclarationSyntax>()
                    .FirstOrDefault();
    }

    /// <summary>
    /// Bestimmt den <see cref="CodeBlockHost"/> eines Code-Blocks anhand seines öffnenden <c>[</c>. Ein gerade
    /// getippter, leerer <c>[]</c> wird NICHT als Code-Deklaration geparst (kein Lookahead-Match auf <c>[</c> +
    /// Schlüsselwort) und hängt daher nicht an seinem Wirt — der verlässliche Anker ist stattdessen das
    /// signifikante Token <b>links</b> des <c>[</c>: das schließende <c>]</c> der vorigen Code-Deklaration bzw.
    /// der Name/das einleitende Schlüsselwort des Wirts. Über dessen Ancestor-Kette wird der <em>innerste</em>
    /// Wirt-Knoten bestimmt (Knoten-Deklarationen liegen in der Task-Definition geschachtelt); ohne tragenden
    /// Knoten (Datei-Kopf) ist es die Datei-Ebene.
    /// </summary>
    static CodeBlockHost CodeBlockHostAt(SyntaxTree tree, SyntaxToken openBracket) {

        var tokens = tree.Tokens;
        var index  = LastIndexStartingBefore(tokens, openBracket.Start);
        var anchor = index >= 0 ? tokens[index].Parent : null;

        if (anchor != null) {
            foreach (var node in anchor.AncestorsAndSelf()) {
                switch (node) {
                    case InitNodeDeclarationSyntax: return CodeBlockHost.InitNode;
                    case TaskNodeDeclarationSyntax: return CodeBlockHost.TaskNode;
                    case TaskDefinitionSyntax:      return CodeBlockHost.TaskDefinition;
                    case TaskDeclarationSyntax:     return CodeBlockHost.TaskRef;
                }
            }
        }

        return CodeBlockHost.CompilationUnit;
    }

    /// <summary>
    /// Klassifiziert eine Position innerhalb einer Präprozessor-Direktive (<c>#…</c>) — oder liefert
    /// <c>null</c>, wenn die Position in keiner Direktive liegt (dann greift die reguläre, Token-basierte
    /// Klassifikation). Direktiven sind strukturierte <see cref="SyntaxTokenType.DirectiveTrivia"/> und nicht
    /// Teil des flachen <see cref="SyntaxTree.Tokens"/>-Stroms; ihre lokalen Token liefert
    /// <see cref="DirectiveTriviaSyntax.ChildTokens"/>. Der Cursor sitzt entweder im Schlüsselwort-Slot direkt
    /// hinter dem <c>#</c> (<see cref="NavCompletionContextKind.DirectiveKeyword"/>) oder — bei einer erkannten
    /// <c>#version</c>-Direktive — im Werte-Slot dahinter (<see cref="NavCompletionContextKind.DirectiveVersionValue"/>).
    /// </summary>
    static NavCompletionContextKind? DirectiveContext(SyntaxTree tree, int position) {

        var directive = DirectiveAt(tree, position);
        if (directive == null) {
            return null;
        }

        var hash = directive.HashToken;

        // Vor bzw. genau am `#` beginnt die Direktive erst — noch keine Direktiv-Situation.
        if (hash.IsMissing || position <= hash.Start) {
            return null;
        }

        // Erkannte Versions-Direktive: bis einschließlich des Endes von `version` wird noch das Schlüsselwort
        // getippt (das Wort ist das Filter-Präfix); erst dahinter beginnt der Werte-Slot.
        if (directive is VersionDirectiveSyntax version && !version.VersionKeyword.IsMissing) {
            return position <= version.VersionKeyword.End
                       ? NavCompletionContextKind.DirectiveKeyword
                       : NavCompletionContextKind.DirectiveVersionValue;
        }

        // Noch kein erkanntes Schlüsselwort (`#`, `#v`, `#pragma`, …): das Schlüsselwort nur anbieten, solange
        // der Cursor im ersten Wort-Slot direkt hinter dem `#` sitzt (kein Wort, oder im/am Ende des Wortes) —
        // weiter hinten in der Zeile gibt es nichts anzubieten.
        var firstWord = directive.ChildTokens()
                                 .Where(t => t.Type is SyntaxTokenType.PreprocessorKeyword
                                                    or SyntaxTokenType.PragmaKeyword
                                                    or SyntaxTokenType.VersionKeyword)
                                 .DefaultIfEmpty(SyntaxToken.Missing)
                                 .First();

        if (firstWord.IsMissing || position <= firstWord.End) {
            return NavCompletionContextKind.DirectiveKeyword;
        }

        return NavCompletionContextKind.Suppress;
    }

    /// <summary>
    /// Die Direktive, deren Inhalts-Extent die <paramref name="position"/> abdeckt — <b>einschließlich</b> der
    /// Endposition, damit der gerade fertig getippte Fall (<c>#version </c> mit Caret am Trivia-Ende) noch
    /// erfasst wird. <see cref="SyntaxTree.FindTrivia"/> nutzt hingegen das Halbintervall <c>[Start, End)</c> und
    /// liefert dort <c>default</c>. Der Extent einer Direktive endet vor ihrem Zeilenende (siehe
    /// <see cref="NavDirectiveParser"/>), daher kollidiert die inklusive Endgrenze nicht mit der Folgezeile.
    /// </summary>
    [CanBeNull]
    static DirectiveTriviaSyntax DirectiveAt(SyntaxTree tree, int position) {
        foreach (var directive in tree.Directives()) {
            if (position >= directive.Start && position <= directive.End) {
                return directive;
            }
        }

        return null;
    }

    /// <summary>
    /// Das signifikante Token, das den Kontext links der Position bestimmt — über den flachen, nach
    /// <see cref="SyntaxToken.Start"/> sortierten Tokenstrom (NICHT <see cref="SyntaxToken.PreviousToken"/>,
    /// das nur innerhalb desselben Parent-Knotens navigiert). Tippt der Nutzer gerade ein Wort
    /// (Identifier/Keyword, in dessen Mitte oder an dessen Ende der Cursor klebt), ist der Kontext dessen
    /// <em>Vorgänger</em> (das Wort selbst ist nur das Filter-Präfix); steht zwischen Token und Cursor ein
    /// Whitespace, ist das Wort abgeschlossen und selbst der Kontext.
    /// </summary>
    static SyntaxToken ContextToken(SyntaxTree tree, int position) {

        var tokens = tree.Tokens;

        // Index des letzten Tokens, das echt links der Position beginnt (Start < position).
        var index = LastIndexStartingBefore(tokens, position);
        if (index < 0) {
            return SyntaxToken.Missing;
        }

        var token = tokens[index];

        // Klebt der Cursor in oder am Ende eines gerade getippten Wortes, ist dieses Wort das Präfix — der
        // eigentliche Kontext ist sein Vorgänger.
        if (IsWordToken(token) && position <= token.End) {
            return index > 0 ? tokens[index - 1] : SyntaxToken.Missing;
        }

        // Klebt der Cursor an einer gerade angefangenen Edge (`-`, `--`, `==`, `*` …), sind deren Zeichen nur
        // das Präfix des Edge-Keywords. Solche unvollständigen Edge-Zeichen bleiben als unbekannte Token übrig
        // und hängen — nicht an einem Task-Knoten, sondern an der Wurzel; sie tragen daher weder den Task- noch
        // den Edge-Kontext. Wie beim Wort-Präfix ist der eigentliche Kontext ihr Vorgänger, der Quellknoten →
        // EdgeSlot. (Die mehrzeichigen Edges werden zeichenweise als eigene Unknown-Token gelext, daher der
        // Rücklauf über den ganzen zusammenhängenden Lauf.)
        if (IsPartialEdgeToken(token) && position <= token.End) {
            while (index > 0 && IsPartialEdgeToken(tokens[index - 1])) {
                index--;
            }

            return index > 0 ? tokens[index - 1] : SyntaxToken.Missing;
        }

        return token;
    }

    // Ein Präfix einer noch unvollständigen Edge: ein unbekanntes Token, dessen Text ausschließlich aus
    // Edge-Zeichen besteht (`-`, `>`, `o`, `*`). Vollständige Edge-Keywords (`-->`, `o->`, …) tragen einen
    // eigenen Token-Typ und fallen bewusst heraus — sie bleiben ihr eigener Kontext (→ Ziel-Slot).
    static bool IsPartialEdgeToken(SyntaxToken token) {

        if (token.IsMissing || token.Type != SyntaxTokenType.Unknown) {
            return false;
        }

        var text = token.ToString();
        return text.Length > 0 && text.All(SyntaxFacts.IsEdgeCharacter);
    }

    // Index des letzten Tokens mit Start &lt; position (Binärsuche über die nach Start sortierte Liste), bzw. -1.
    static int LastIndexStartingBefore(SyntaxTokenList tokens, int position) {

        int lo = 0, hi = tokens.Count - 1, result = -1;

        while (lo <= hi) {
            int mid = lo + (hi - lo) / 2;
            if (tokens[mid].Start < position) {
                result = mid;
                lo     = mid + 1;
            } else {
                hi = mid - 1;
            }
        }

        return result;
    }

    // Ein „Wort" im Sinne der Präfix-Vervollständigung: nicht-leer und ausschließlich aus Identifier-Zeichen
    // (deckt Identifier wie Keywords ab; Edges `-->`/`o->` und Punctuation fallen heraus).
    static bool IsWordToken(SyntaxToken token) {

        if (token.IsMissing) {
            return false;
        }

        var text = token.ToString();
        return text.Length > 0 && text.All(SyntaxFacts.IsIdentifierCharacter);
    }

}
