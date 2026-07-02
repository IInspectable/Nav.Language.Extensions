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

    /// <summary>Keine Vorschläge (Kommentar, Zeichenkette, C#-Inhalt eines Code-Blocks oder außerhalb des Texts).</summary>
    Suppress,

    /// <summary>
    /// Im Schlüsselwort-Slot einer Code-Deklaration direkt hinter <c>[</c> (z.B. <c>[using …]</c>,
    /// <c>[result …]</c>): die Code-Block-Schlüsselwörter (<see cref="SyntaxFacts.CodeKeywords"/>).
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

    /// <summary>Hinter <c>task</c> innerhalb eines Task-Bodies (Task-Knoten): die deklarierten Tasks.</summary>
    TaskNodeName,

    /// <summary>Satzanfang im Task-Body: Knoten-Deklarations-Keywords und die vorhandenen Knoten (als Quelle).</summary>
    StatementStart,

    /// <summary>Hinter einem Quellknoten: die (sichtbaren) Edge-Keywords.</summary>
    EdgeSlot,

    /// <summary>Hinter einer Edge: die Knoten (als Ziel) sowie das Ziel-Keyword <c>end</c>.</summary>
    TargetSlot,

    /// <summary>Hinter <c>Knoten:</c> einer Exit-Transition: die Exit-Connection-Points des Knotens.</summary>
    ExitConnectionPoint,

    /// <summary>Hinter einem vollständigen Ziel: die Folge-Klauseln <c>on</c> / <c>if</c> / <c>do</c>.</summary>
    AfterTarget,

    /// <summary>Innerhalb/hinter einem Trigger (<c>on …</c>): <c>if</c> / <c>do</c>.</summary>
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
/// Bestimmt die <see cref="NavCompletionContextKind"/> an einer Cursor-Position. Trägt zusätzlich die
/// für die jeweilige Kategorie nötigen Bezugspunkte: die umschließende Task-Definition und — bei
/// <see cref="NavCompletionContextKind.ExitConnectionPoint"/> — den Namen des Exit-Knotens.
/// </summary>
sealed class NavCompletionContext {

    NavCompletionContext(NavCompletionContextKind kind, ITaskDefinitionSymbol task, string exitNodeName) {
        Kind         = kind;
        Task         = task;
        ExitNodeName = exitNodeName;
    }

    public NavCompletionContextKind Kind { get; }

    [CanBeNull]
    public ITaskDefinitionSymbol Task { get; }

    [CanBeNull]
    public string ExitNodeName { get; }

    static NavCompletionContext Of(NavCompletionContextKind kind, ITaskDefinitionSymbol task = null, string exitNodeName = null)
        => new(kind, task, exitNodeName);

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
                       ? Of(NavCompletionContextKind.CodeBlock)
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

        // Satzanfang im Body: direkt hinter `{` (Body-Öffnung) oder `;` (Ende der vorigen Deklaration).
        if (contextToken.Type == SyntaxTokenType.OpenBrace ||
            contextToken.Type == SyntaxTokenType.Semicolon) {
            return Of(NavCompletionContextKind.StatementStart, task);
        }

        // Rolle des Tokens über seinen Parent-Knoten.
        switch (contextToken.Parent) {

            // Quellknoten (auch der Connector-Name `se` in `Sub:se`) → als Nächstes folgt die Edge.
            case SourceNodeSyntax:
            case ExitTransitionDefinitionSyntax when contextToken.Type == SyntaxTokenType.Identifier:
                return Of(NavCompletionContextKind.EdgeSlot, task);

            // Zielknoten → danach die Folge-Klauseln.
            case TargetNodeSyntax:
                return Of(NavCompletionContextKind.AfterTarget, task);

            // Trigger (`on …`) → danach if/do.
            case TriggerSyntax:
                return Of(NavCompletionContextKind.AfterTrigger, task);

            // Bedingung (`if …`) → danach do.
            case ConditionClauseSyntax:
                return Of(NavCompletionContextKind.AfterCondition, task);
        }

        return Of(NavCompletionContextKind.Fallback, task);
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
