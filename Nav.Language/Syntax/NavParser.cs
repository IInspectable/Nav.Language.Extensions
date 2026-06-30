#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Handgeschriebener Recursive-Descent-Parser für die Nav-Sprache. Läuft über den flachen Token-Strom
/// des <see cref="NavLexer"/> und baut in einem Durchlauf direkt den immutablen <see cref="SyntaxNode"/>-Baum,
/// vergibt die kontextabhängige <see cref="TextClassification"/> je signifikantem Token und hängt es an
/// seinen Knoten. Trivia (Whitespace, Zeilenenden, Kommentare), unbekannte Zeichen, Präprozessor-Token und
/// das <see cref="SyntaxTokenType.EndOfFile"/> sieht der Parser nicht; sie werden in einem abschließenden
/// Durchlauf an die Wurzel gehängt — dasselbe beobachtbare Verhalten wie die bisherige ANTLR-Pipeline.
/// <para/>
/// Die Struktur folgt 1:1 der bisherigen Grammatik/dem Visitor: jede <c>Parse*</c>-Methode entspricht einer
/// Grammatikregel.
/// <para/>
/// Der Parser ist fehlertolerant. Zwei Mechanismen sorgen dafür, dass jede Eingabe — auch eine
/// unvollständige — einen vollständigen Baum ergibt und <b>kein Zeichen verloren geht</b>:
/// <list type="number">
///   <item><description><b>Insertion</b> — fehlt ein erwartetes Token, synthetisiert <see cref="Eat"/>
///   ein nullbreites Missing-Token (es landet nicht im Token-Strom), meldet eine Diagnose und rückt
///   <i>nicht</i> vor.</description></item>
///   <item><description><b>Deletion</b> — steht an einer Stelle ein unerwartetes Token, überspringt es
///   der Panic-Mode (<see cref="Recover"/>) bis zu einem Wiedereinstiegs- oder äußeren Anker-Token, mit
///   garantiertem Fortschritt je Schleifendurchlauf. Übersprungene signifikante Token werden — wie die
///   Trivia — abschließend als <see cref="TextClassification.Skiped"/>-Token an die Wurzel gehängt.</description></item>
/// </list>
/// Die Recovery liefert bewusst knappe, treffende Diagnosen; abgesichert ist sie über einen
/// Golden-Satz je Korpus-Datei (Token, Baum und Diagnostics werden gepinnt).
/// <para/>
/// Jede <c>Parse*</c>-Methode trägt die zugehörige Grammatikregel als EBNF-Fragment. Lesehilfe zur
/// Notation:
/// <list type="bullet">
///   <item><description><c>"…"</c> — ein literales Schlüsselwort, ein Operator oder ein
///   Punctuation-Zeichen (z.B. <c>"task"</c>, <c>"--&gt;"</c>, <c>";"</c>).</description></item>
///   <item><description><c>Identifier</c>, <c>StringLiteral</c>, <c>EOF</c> — kategorische Terminale
///   (kein fester Text), groß geschrieben und ohne Anführungszeichen.</description></item>
///   <item><description>Nichtterminale in <c>camelCase</c> verweisen auf die gleichnamige
///   <c>Parse*</c>-Methode.</description></item>
///   <item><description><c>?</c> optional, <c>*</c> null bis n, <c>+</c> ein bis n, <c>|</c> Alternative,
///   <c>( … )</c> Gruppierung, <c>(* … *)</c> erläuternder Kommentar.</description></item>
/// </list>
/// Die EBNF beschreibt die <i>akzeptierte Sprache</i>; bewusste Implementierungs-Eigenheiten (Baum-
/// Gruppierung, Lookahead-Disambiguierung, fehlertolerante Bestandteile) stehen als Prosa-Hinweis im
/// jeweiligen <c>&lt;remarks&gt;</c>.
/// </summary>
sealed class NavParser {

    readonly SourceText                          _sourceText;
    readonly ImmutableArray<RawToken>            _raw;
    readonly List<SyntaxToken>                   _tokens;
    readonly ImmutableArray<Diagnostic>.Builder  _diagnostics;
    readonly int                                 _eofPos;

    // Vorab aus _raw berechnete Token-Trivia (echtes Roslyn-Modell): je signifikantem RawToken — Schlüssel
    // ist seine eindeutige Start-Position — die Leading/Trailing-Trivia, plus separat die Leading-Trivia des
    // abschließenden EndOfFile (die finale Datei-Trivia). Wird in Tok() bzw. beim EOF-Anhängen nachgeschlagen.
    readonly Dictionary<int, TokenTrivia> _tokenTrivia;
    readonly ImmutableArray<SyntaxTrivia> _eofLeadingTrivia;

    int  _pos;                     // Index in _raw; Invariante: zeigt stets auf ein parser-sichtbares Token (signifikant oder EOF).
    int? _firstSignificantStart;   // Start des ersten konsumierten signifikanten Tokens (für den Wurzel-Extent).
    bool _reportedMissingAtEof;    // Ein "missing …" am Dateiende wurde bereits gemeldet — weitere am EOF werden unterdrückt.

    NavParser(SourceText sourceText) {
        _sourceText  = sourceText;
        _raw         = NavLexer.Lex(sourceText.Text);
        _tokens      = new List<SyntaxToken>(_raw.Length);
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        _eofPos      = _raw[_raw.Length - 1].Start; // Das abschließende EndOfFile ist nullbreit am Textende.

        _tokenTrivia = BuildTokenTrivia(_raw, out _eofLeadingTrivia);

        _pos = 0;
        SkipHidden();
    }

    public static SyntaxTree Parse(string text, string filePath = null, CancellationToken cancellationToken = default) {

        var sourceText = SourceText.From(text ?? String.Empty, filePath);

        return new NavParser(sourceText).ParseCodeGenerationUnit(cancellationToken);
    }

    #region Per-Regel-Einstiege (test-only)

    /// <summary>
    /// Grammatikregeln, die einzeln (statt als ganze Datei) geparst werden können — die test-seitige
    /// Snippet-Schnittstelle hinter <see cref="Syntax"/>. Jeder Wert verweist auf die gleichnamige private
    /// <c>Parse*</c>-Methode (Ausnahme: <see cref="ArrayType"/> nutzt <see cref="ParseCodeType"/>, das die
    /// Array-Regel mit abdeckt).
    /// </summary>
    internal enum Rule {
        DoClause,                     GoToEdge,                  ArrayType,
        ModalEdge,                    Parameter,                 Identifier,
        SimpleType,                   GenericType,               NonModalEdge,
        EndTargetNode,                ParameterList,             SignalTrigger,
        StringLiteral,                InitSourceNode,            TaskDefinition,
        CodeDeclaration,              TaskDeclaration,           IncludeDirective,
        IfConditionClause,            ArrayRankSpecifier,        EndNodeDeclaration,
        SpontaneousTrigger,           CodeBaseDeclaration,       ElseConditionClause,
        ExitNodeDeclaration,          InitNodeDeclaration,       TaskNodeDeclaration,
        ViewNodeDeclaration,          CodeUsingDeclaration,      IdentifierSourceNode,
        IdentifierTargetNode,         NodeDeclarationBlock,      TransitionDefinition,
        ChoiceNodeDeclaration,        CodeParamsDeclaration,     CodeResultDeclaration,
        ElseIfConditionClause,        DialogNodeDeclaration,     CodeNamespaceDeclaration,
        ExitTransitionDefinition,     CodeGenerateToDeclaration, TransitionDefinitionBlock,
        CodeDoNotInjectDeclaration,   CodeAbstractMethodDeclaration,
        CodeNotImplementedDeclaration
    }

    /// <summary>
    /// Test-seitiger Einstieg je Grammatikregel (siehe <see cref="Syntax"/>): parst ein Snippet, das genau
    /// einer Regel entspricht, und liefert dessen Knoten als Wurzel. Setzt — wie <see cref="Parse"/> — den
    /// Cursor auf, ruft die zur Regel gehörende private <c>Parse*</c>-Methode und hängt den Rest (Trivia,
    /// im Panic-Mode übersprungene Token, das abschließende <see cref="SyntaxTokenType.EndOfFile"/>) an die
    /// so entstandene Wurzel. Produktionscode parst ausschließlich ganze Dateien über <see cref="Parse"/>.
    /// </summary>
    internal static SyntaxTree ParseRule(string text, Rule rule, string filePath = null, CancellationToken cancellationToken = default) {

        var sourceText = SourceText.From(text ?? String.Empty, filePath);
        var parser     = new NavParser(sourceText);

        cancellationToken.ThrowIfCancellationRequested();

        var root = parser.ParseRuleRoot(rule);

        // Trivia/Unknown/Präprozessor/EndOfFile sowie übersprungene Token hängen — wie beim Whole-File-
        // Parsing — an der Wurzel (hier dem Regel-Knoten).
        parser.AttachNonSignificantTokens(root);

        var syntaxTree = new SyntaxTree(sourceText : sourceText,
                                        root       : root,
                                        tokens     : new SyntaxTokenList(parser._tokens),
                                        diagnostics: parser._diagnostics.ToImmutable());

        root.FinalConstruct(syntaxTree, null);

        return syntaxTree;
    }

    SyntaxNode ParseRuleRoot(Rule rule) {
        switch (rule) {
            case Rule.DoClause:                     return ParseDoClause();
            case Rule.GoToEdge:                     return ParseGoToEdge();
            case Rule.ArrayType:                    return ParseCodeType();
            case Rule.ModalEdge:                    return ParseModalEdge();
            case Rule.Parameter:                    return ParseParameter();
            case Rule.Identifier:                   return ParseIdentifier();
            case Rule.SimpleType:                   return ParseSimpleType();
            case Rule.GenericType:                  return ParseGenericType();
            case Rule.NonModalEdge:                 return ParseNonModalEdge();
            case Rule.EndTargetNode:                return ParseEndTargetNode();
            case Rule.ParameterList:                return ParseParameterList();
            case Rule.SignalTrigger:                return ParseSignalTrigger();
            case Rule.StringLiteral:                return ParseStringLiteral();
            case Rule.InitSourceNode:               return ParseInitSourceNode();
            case Rule.TaskDefinition:               return ParseTaskDefinition();
            case Rule.CodeDeclaration:              return ParseCodeDeclaration();
            case Rule.TaskDeclaration:              return ParseTaskDeclaration();
            case Rule.IncludeDirective:             return ParseIncludeDirective();
            case Rule.IfConditionClause:            return ParseIfConditionClause();
            case Rule.ArrayRankSpecifier:           return ParseArrayRankSpecifier();
            case Rule.EndNodeDeclaration:           return ParseEndNodeDeclaration();
            case Rule.SpontaneousTrigger:           return ParseSpontaneousTrigger();
            case Rule.CodeBaseDeclaration:          return ParseCodeBaseDeclaration();
            case Rule.ElseConditionClause:          return ParseElseConditionClause();
            case Rule.ExitNodeDeclaration:          return ParseExitNodeDeclaration();
            case Rule.InitNodeDeclaration:          return ParseInitNodeDeclaration();
            case Rule.TaskNodeDeclaration:          return ParseTaskNodeDeclaration();
            case Rule.ViewNodeDeclaration:          return ParseViewNodeDeclaration();
            case Rule.CodeUsingDeclaration:         return ParseCodeUsingDeclaration();
            case Rule.IdentifierSourceNode:         return ParseIdentifierSourceNode();
            case Rule.IdentifierTargetNode:         return ParseIdentifierTargetNode();
            case Rule.NodeDeclarationBlock:         return ParseNodeDeclarationBlock();
            case Rule.TransitionDefinition:         return ParseTransitionDefinition();
            case Rule.ChoiceNodeDeclaration:        return ParseChoiceNodeDeclaration();
            case Rule.CodeParamsDeclaration:        return ParseCodeParamsDeclaration();
            case Rule.CodeResultDeclaration:        return ParseCodeResultDeclaration();
            case Rule.ElseIfConditionClause:        return ParseElseIfConditionClause();
            case Rule.DialogNodeDeclaration:        return ParseDialogNodeDeclaration();
            case Rule.CodeNamespaceDeclaration:     return ParseCodeNamespaceDeclaration();
            case Rule.ExitTransitionDefinition:     return ParseExitTransitionDefinition();
            case Rule.CodeGenerateToDeclaration:    return ParseCodeGenerateToDeclaration();
            case Rule.TransitionDefinitionBlock:    return ParseTransitionDefinitionBlock();
            case Rule.CodeDoNotInjectDeclaration:   return ParseCodeDoNotInjectDeclaration();
            case Rule.CodeAbstractMethodDeclaration:  return ParseCodeAbstractMethodDeclaration();
            case Rule.CodeNotImplementedDeclaration: return ParseCodeNotImplementedDeclaration();
            default: throw new ArgumentOutOfRangeException(nameof(rule), rule, null);
        }
    }

    #endregion

    #region CodeGenerationUnit

    /// <summary>Grammatik-Einstiegsregel <c>codeGenerationUnit</c> → <see cref="CodeGenerationUnitSyntax"/> (Wurzel).</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeGenerationUnit ::= ( codeNamespaceDeclaration codeUsingDeclaration* )?
    ///                        memberDeclaration*
    ///                        EOF
    /// ]]></code>
    /// Der optionale Namespace-/Using-Kopf wird über Zwei-Token-Lookahead erkannt (<c>[</c> + <c>namespaceprefix</c>
    /// bzw. <c>using</c>). Auf Top-Level gibt es keinen äußeren Anker: alles, was kein Member beginnt, wird bis
    /// zum nächsten Member oder zum Dateiende übersprungen (Panic-Mode).
    /// </remarks>
    SyntaxTree ParseCodeGenerationUnit(CancellationToken cancellationToken) {

        CodeNamespaceDeclarationSyntax    codeNamespace = null;
        var codeUsings = new List<CodeUsingDeclarationSyntax>();
        var members    = new List<MemberDeclarationSyntax>();

        if (At(SyntaxTokenType.OpenBracket) && PeekType(1) == SyntaxTokenType.NamespaceprefixKeyword) {
            codeNamespace = ParseCodeNamespaceDeclaration();

            while (At(SyntaxTokenType.OpenBracket) && PeekType(1) == SyntaxTokenType.UsingKeyword) {
                codeUsings.Add(ParseCodeUsingDeclaration());
            }
        }

        while (!AtEof) {
            cancellationToken.ThrowIfCancellationRequested();

            if (At(SyntaxTokenType.TaskrefKeyword) || At(SyntaxTokenType.TaskKeyword)) {
                members.Add(ParseMemberDeclaration());
                continue;
            }

            // Auf Top-Level gibt es keinen äußeren Anker: alles, was kein Member beginnt, wird bis zum
            // nächsten Member oder zum Dateiende übersprungen.
            Recover(() => At(SyntaxTokenType.TaskrefKeyword) || At(SyntaxTokenType.TaskKeyword) || AtEof);
        }

        var rootStart  = _firstSignificantStart ?? _eofPos;
        var rootExtent = TextExtent.FromBounds(rootStart, _eofPos);

        var root = new CodeGenerationUnitSyntax(rootExtent, codeNamespace, codeUsings, members);

        // Trivia/Unknown/Präprozessor/EndOfFile hängen — wie in der bisherigen Pipeline — an der Wurzel.
        AttachNonSignificantTokens(root);

        var syntaxTree = new SyntaxTree(sourceText : _sourceText,
                                        root       : root,
                                        tokens     : new SyntaxTokenList(_tokens),
                                        diagnostics: _diagnostics.ToImmutable());

        root.FinalConstruct(syntaxTree, null);

        return syntaxTree;
    }

    /// <summary>Grammatikregel <c>memberDeclaration</c> → <see cref="MemberDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// memberDeclaration ::= includeDirective
    ///                     | taskDeclaration
    ///                     | taskDefinition
    /// ]]></code>
    /// Disambiguierung per Lookahead: <c>taskref</c> + StringLiteral ⇒ <c>includeDirective</c>; <c>taskref</c> +
    /// Identifier ⇒ <c>taskDeclaration</c>; <c>task</c> ⇒ <c>taskDefinition</c>.
    /// </remarks>
    MemberDeclarationSyntax ParseMemberDeclaration() {
        if (At(SyntaxTokenType.TaskrefKeyword)) {
            return PeekType(1) == SyntaxTokenType.StringLiteral
                ? ParseIncludeDirective()
                : ParseTaskDeclaration();
        }

        return ParseTaskDefinition();
    }

    #endregion

    #region Code Namespace / Using

    /// <summary>Grammatikregel <c>codeNamespaceDeclaration</c> → <see cref="CodeNamespaceDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeNamespaceDeclaration ::= "[" "namespaceprefix" identifierOrString "]"
    /// ]]></code>
    /// </remarks>
    CodeNamespaceDeclarationSyntax ParseCodeNamespaceDeclaration() {

        var open      = Eat(SyntaxTokenType.OpenBracket);
        var keyword   = Eat(SyntaxTokenType.NamespaceprefixKeyword);
        var nsSyntax  = ParseIdentifierOrString();
        var close     = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeNamespaceDeclarationSyntax(Span(open, keyword, nsSyntax, close), nsSyntax);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeUsingDeclaration</c> → <see cref="CodeUsingDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeUsingDeclaration ::= "[" "using" identifierOrString "]"
    /// ]]></code>
    /// </remarks>
    CodeUsingDeclarationSyntax ParseCodeUsingDeclaration() {

        var open     = Eat(SyntaxTokenType.OpenBracket);
        var keyword  = Eat(SyntaxTokenType.UsingKeyword);
        var nsSyntax = ParseIdentifierOrString();
        var close    = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeUsingDeclarationSyntax(Span(open, keyword, nsSyntax, close), nsSyntax);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region IncludeDirective

    /// <summary>Grammatikregel <c>includeDirective</c> → <see cref="IncludeDirectiveSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// includeDirective ::= "taskref" StringLiteral ";"
    /// ]]></code>
    /// </remarks>
    IncludeDirectiveSyntax ParseIncludeDirective() {

        var keyword = Eat(SyntaxTokenType.TaskrefKeyword);
        var literal = Eat(SyntaxTokenType.StringLiteral);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new IncludeDirectiveSyntax(Span(keyword, literal, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, literal, TextClassification.StringLiteral);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region TaskDeclaration

    /// <summary>Grammatikregel <c>taskDeclaration</c> → <see cref="TaskDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// taskDeclaration ::= "taskref" Identifier
    ///                     codeNamespaceDeclaration?
    ///                     codeNotImplementedDeclaration?
    ///                     codeResultDeclaration?
    ///                     "{" connectionPointNodeDeclaration* "}"
    /// ]]></code>
    /// Die optionalen <c>code*</c>-Deklarationen werden über Zwei-Token-Lookahead erkannt (<c>[</c> +
    /// Schlüsselwort). Vor dem Body-<c>{</c> überspringt ein gezielter Panic-Mode ungültige Token bis zum
    /// <c>{</c>, zum Beginn eines Connection-Points oder zu einem äußeren Anker.
    /// </remarks>
    TaskDeclarationSyntax ParseTaskDeclaration() {

        var keyword = Eat(SyntaxTokenType.TaskrefKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        var codeNamespace  = AtCodeDeclaration(SyntaxTokenType.NamespaceprefixKeyword) ? ParseCodeNamespaceDeclaration()       : null;
        var notImplemented = AtCodeDeclaration(SyntaxTokenType.NotimplementedKeyword)  ? ParseCodeNotImplementedDeclaration()  : null;
        var result         = AtCodeDeclaration(SyntaxTokenType.ResultKeyword)          ? ParseCodeResultDeclaration()          : null;

        Recover(() => At(SyntaxTokenType.OpenBrace) || StartsConnectionPoint() || BreaksBody());
        var open = Eat(SyntaxTokenType.OpenBrace);

        var connectionPoints = new List<ConnectionPointNodeSyntax>();
        while (true) {
            if (StartsConnectionPoint()) {
                connectionPoints.Add(ParseConnectionPointNodeDeclaration());
                continue;
            }

            if (BreaksBody()) {
                break;
            }

            Recover(() => StartsConnectionPoint() || BreaksBody());
        }

        var close = Eat(SyntaxTokenType.CloseBrace);

        var span = new ExtentBuilder();
        span.Add(keyword); span.Add(name); span.Add(codeNamespace); span.Add(notImplemented); span.Add(result);
        span.Add(open); span.AddRange(connectionPoints); span.Add(close);

        var node = new TaskDeclarationSyntax(span.ToExtent(), codeNamespace, notImplemented, result, connectionPoints);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.TaskName);
        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>connectionPointNodeDeclaration</c> → <see cref="ConnectionPointNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// connectionPointNodeDeclaration ::= initNodeDeclaration
    ///                                  | exitNodeDeclaration
    ///                                  | endNodeDeclaration
    /// ]]></code>
    /// Disambiguierung per Start-Schlüsselwort (<c>init</c>/<c>exit</c>/<c>end</c>).
    /// </remarks>
    ConnectionPointNodeSyntax ParseConnectionPointNodeDeclaration() {
        return At(SyntaxTokenType.InitKeyword) ? ParseInitNodeDeclaration()
             : At(SyntaxTokenType.ExitKeyword) ? ParseExitNodeDeclaration()
             :                                   ParseEndNodeDeclaration();
    }

    #endregion

    #region TaskDefinition

    /// <summary>Grammatikregel <c>taskDefinition</c> → <see cref="TaskDefinitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// taskDefinition ::= "task" Identifier
    ///                    codeDeclaration?
    ///                    codeBaseDeclaration?
    ///                    codeGenerateToDeclaration?
    ///                    codeParamsDeclaration?
    ///                    codeResultDeclaration?
    ///                    "{" nodeDeclarationBlock transitionDefinitionBlock "}"
    /// ]]></code>
    /// Die optionalen <c>code*</c>-Deklarationen werden über Zwei-Token-Lookahead erkannt (<c>[</c> +
    /// Schlüsselwort). Vor dem Body-<c>{</c> überspringt ein gezielter Panic-Mode ungültige Token bis zum
    /// <c>{</c>, zum Beginn einer Knoten-/Transitions-Deklaration oder zu einem äußeren Anker.
    /// </remarks>
    TaskDefinitionSyntax ParseTaskDefinition() {

        var keyword = Eat(SyntaxTokenType.TaskKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        var code       = AtCodeDeclaration(SyntaxTokenType.CodeKeyword)       ? ParseCodeDeclaration()           : null;
        var codeBase   = AtCodeDeclaration(SyntaxTokenType.BaseKeyword)       ? ParseCodeBaseDeclaration()       : null;
        var generateTo = AtCodeDeclaration(SyntaxTokenType.GeneratetoKeyword) ? ParseCodeGenerateToDeclaration() : null;
        var codeParams = AtCodeDeclaration(SyntaxTokenType.ParamsKeyword)     ? ParseCodeParamsDeclaration()     : null;
        var result     = AtCodeDeclaration(SyntaxTokenType.ResultKeyword)     ? ParseCodeResultDeclaration()     : null;

        Recover(() => At(SyntaxTokenType.OpenBrace) || StartsNodeDeclaration() || StartsTransition() || BreaksBody());
        var open = Eat(SyntaxTokenType.OpenBrace);

        var nodeBlock       = ParseNodeDeclarationBlock();
        var transitionBlock = ParseTransitionDefinitionBlock();

        var close = Eat(SyntaxTokenType.CloseBrace);

        var span = new ExtentBuilder();
        span.Add(keyword); span.Add(name); span.Add(code); span.Add(codeBase); span.Add(generateTo);
        span.Add(codeParams); span.Add(result); span.Add(open); span.Add(nodeBlock); span.Add(transitionBlock); span.Add(close);

        var node = new TaskDefinitionSyntax(span.ToExtent(), code, codeBase, generateTo, codeParams, result, nodeBlock, transitionBlock);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.TaskName);
        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region Node Declarations

    /// <summary>Grammatikregel <c>nodeDeclarationBlock</c> → <see cref="NodeDeclarationBlockSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// nodeDeclarationBlock ::= nodeDeclaration*
    /// ]]></code>
    /// Ein leerer Block (kein einziger Knoten) erhält den Extent <see cref="TextExtent.Missing"/>.
    /// </remarks>
    NodeDeclarationBlockSyntax ParseNodeDeclarationBlock() {

        var nodes = new List<NodeDeclarationSyntax>();
        var span  = new ExtentBuilder();

        while (StartsNodeDeclaration()) {
            var node = ParseNodeDeclaration();
            nodes.Add(node);
            span.Add(node);
        }

        return new NodeDeclarationBlockSyntax(span.ToExtent(), nodes);
    }

    bool StartsNodeDeclaration() {
        switch (At0) {
            case SyntaxTokenType.ExitKeyword:
            case SyntaxTokenType.EndKeyword:
            case SyntaxTokenType.TaskKeyword:
            case SyntaxTokenType.ChoiceKeyword:
            case SyntaxTokenType.DialogKeyword:
            case SyntaxTokenType.ViewKeyword:
                return true;
            case SyntaxTokenType.InitKeyword:
                // 'init' gefolgt von einer Kante ist eine Transition (initSourceNode), keine Knoten-Deklaration.
                return !IsEdge(PeekType(1));
            default:
                return false;
        }
    }

    /// <summary>Grammatikregel <c>nodeDeclaration</c> → <see cref="NodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// nodeDeclaration ::= connectionPointNodeDeclaration   (* init | exit | end *)
    ///                   | taskNodeDeclaration
    ///                   | choiceNodeDeclaration
    ///                   | dialogNodeDeclaration
    ///                   | viewNodeDeclaration
    /// ]]></code>
    /// Disambiguierung per Start-Schlüsselwort. Sonderfall <c>init</c>: nur dann eine Knoten-Deklaration, wenn
    /// <b>keine</b> Kante folgt — <c>init --&gt; …</c> ist eine Transition (<c>initSourceNode</c>), siehe
    /// <see cref="StartsNodeDeclaration"/>.
    /// </remarks>
    NodeDeclarationSyntax ParseNodeDeclaration() {
        switch (At0) {
            case SyntaxTokenType.InitKeyword:   return ParseInitNodeDeclaration();
            case SyntaxTokenType.ExitKeyword:   return ParseExitNodeDeclaration();
            case SyntaxTokenType.EndKeyword:    return ParseEndNodeDeclaration();
            case SyntaxTokenType.TaskKeyword:   return ParseTaskNodeDeclaration();
            case SyntaxTokenType.ChoiceKeyword: return ParseChoiceNodeDeclaration();
            case SyntaxTokenType.DialogKeyword: return ParseDialogNodeDeclaration();
            default:                            return ParseViewNodeDeclaration();
        }
    }

    /// <summary>Grammatikregel <c>initNodeDeclaration</c> → <see cref="InitNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// initNodeDeclaration ::= "init" Identifier?
    ///                         codeAbstractMethodDeclaration?
    ///                         codeParamsDeclaration?
    ///                         doClause?
    ///                         ";"
    /// ]]></code>
    /// </remarks>
    InitNodeDeclarationSyntax ParseInitNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.InitKeyword);
        var name    = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : null;

        var abstractMethod = AtCodeDeclaration(SyntaxTokenType.AbstractmethodKeyword) ? ParseCodeAbstractMethodDeclaration() : null;
        var codeParams     = AtCodeDeclaration(SyntaxTokenType.ParamsKeyword)         ? ParseCodeParamsDeclaration()         : null;
        var doClause       = At(SyntaxTokenType.DoKeyword)                            ? ParseDoClause()                      : null;

        var semi = Eat(SyntaxTokenType.Semicolon);

        var node = new InitNodeDeclarationSyntax(Span(keyword, name, abstractMethod, codeParams, doClause, semi),
                                                 abstractMethod, codeParams, doClause);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>exitNodeDeclaration</c> → <see cref="ExitNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// exitNodeDeclaration ::= "exit" Identifier ";"
    /// ]]></code>
    /// </remarks>
    ExitNodeDeclarationSyntax ParseExitNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.ExitKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new ExitNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>endNodeDeclaration</c> → <see cref="EndNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// endNodeDeclaration ::= "end" ";"
    /// ]]></code>
    /// </remarks>
    EndNodeDeclarationSyntax ParseEndNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.EndKeyword);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new EndNodeDeclarationSyntax(Span(keyword, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>taskNodeDeclaration</c> → <see cref="TaskNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// taskNodeDeclaration ::= "task" Identifier Identifier?
    ///                         codeDoNotInjectDeclaration?
    ///                         codeAbstractMethodDeclaration?
    ///                         ";"
    /// ]]></code>
    /// Das zweite (optionale) <c>Identifier</c> ist der Alias des Task-Knotens.
    /// </remarks>
    TaskNodeDeclarationSyntax ParseTaskNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.TaskKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var alias   = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : null;

        var doNotInject    = AtCodeDeclaration(SyntaxTokenType.DonotinjectKeyword)    ? ParseCodeDoNotInjectDeclaration()    : null;
        var abstractMethod = AtCodeDeclaration(SyntaxTokenType.AbstractmethodKeyword) ? ParseCodeAbstractMethodDeclaration() : null;

        var semi = Eat(SyntaxTokenType.Semicolon);

        var node = new TaskNodeDeclarationSyntax(Span(keyword, name, alias, doNotInject, abstractMethod, semi),
                                                 doNotInject, abstractMethod);

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.TaskName);
        Tok(node, alias,   TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>choiceNodeDeclaration</c> → <see cref="ChoiceNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// choiceNodeDeclaration ::= "choice" Identifier ";"
    /// ]]></code>
    /// </remarks>
    ChoiceNodeDeclarationSyntax ParseChoiceNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.ChoiceKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new ChoiceNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.Identifier);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>dialogNodeDeclaration</c> → <see cref="DialogNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// dialogNodeDeclaration ::= "dialog" Identifier ";"
    /// ]]></code>
    /// </remarks>
    DialogNodeDeclarationSyntax ParseDialogNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.DialogKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new DialogNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.GuiNode);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>viewNodeDeclaration</c> → <see cref="ViewNodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// viewNodeDeclaration ::= "view" Identifier ";"
    /// ]]></code>
    /// </remarks>
    ViewNodeDeclarationSyntax ParseViewNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.ViewKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new ViewNodeDeclarationSyntax(Span(keyword, name, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, name,    TextClassification.GuiNode);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region TransitionDefinitionBlock

    /// <summary>Grammatikregel <c>transitionDefinitionBlock</c> → <see cref="TransitionDefinitionBlockSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// transitionDefinitionBlock ::= ( transitionDefinition | exitTransitionDefinition )*
    /// ]]></code>
    /// Im Quelltext dürfen sich beide Alternativen mischen; im Baum werden sie jedoch getrennt gruppiert —
    /// erst alle <c>transitionDefinition</c>, dann alle <c>exitTransitionDefinition</c> (<b>nicht</b> in
    /// Quelltext-Reihenfolge). Disambiguierung per Lookahead: <c>Identifier</c> + <c>:</c> ⇒
    /// <c>exitTransitionDefinition</c>, sonst <c>transitionDefinition</c>.
    /// </remarks>
    TransitionDefinitionBlockSyntax ParseTransitionDefinitionBlock() {

        // Quelltext-Reihenfolge darf transitionDefinition und exitTransitionDefinition mischen; im Baum
        // werden sie jedoch — wie im bisherigen Modell — getrennt gruppiert (erst alle Transitionen, dann
        // alle Exit-Transitionen).
        var transitions     = new List<TransitionDefinitionSyntax>();
        var exitTransitions = new List<ExitTransitionDefinitionSyntax>();
        var span            = new ExtentBuilder();

        while (true) {
            if (StartsTransition()) {
                if (At(SyntaxTokenType.Identifier) && PeekType(1) == SyntaxTokenType.Colon) {
                    var exit = ParseExitTransitionDefinition();
                    exitTransitions.Add(exit);
                    span.Add(exit);
                } else {
                    var transition = ParseTransitionDefinition();
                    transitions.Add(transition);
                    span.Add(transition);
                }

                continue;
            }

            if (BreaksBody()) {
                break;
            }

            Recover(() => StartsTransition() || BreaksBody());
        }

        return new TransitionDefinitionBlockSyntax(span.ToExtent(), transitions, exitTransitions);
    }

    bool StartsTransition() {
        return At(SyntaxTokenType.InitKeyword) || At(SyntaxTokenType.Identifier);
    }

    /// <summary>Grammatikregel <c>transitionDefinition</c> → <see cref="TransitionDefinitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// transitionDefinition ::= sourceNode edge targetNode trigger? conditionClause? doClause? ";"
    /// ]]></code>
    /// <c>edge</c> und <c>targetNode</c> werden fehlertolerant geparst: fehlt die Kante, wird
    /// <c>missing edge</c> gemeldet; fehlt — bei vorhandener Kante — das Zielknoten, <c>missing target node</c>
    /// (die beiden Fehlerproduktionen der ursprünglichen Grammatik).
    /// </remarks>
    TransitionDefinitionSyntax ParseTransitionDefinition() {

        var source = ParseSourceNode();

        var edge = StartsEdge() ? ParseEdge() : null;
        if (edge == null) {
            ReportMissing("edge");
        }

        var target = StartsTargetNode() ? ParseTargetNode() : null;
        if (target == null && edge != null) {
            ReportMissing("target node");
        }

        var trigger   = StartsTrigger()    ? ParseTrigger()         : null;
        var condition = StartsCondition()  ? ParseConditionClause() : null;
        var doClause  = At(SyntaxTokenType.DoKeyword) ? ParseDoClause() : null;

        var semi = Eat(SyntaxTokenType.Semicolon);

        var node = new TransitionDefinitionSyntax(Span(source, edge, target, trigger, condition, doClause, semi),
                                                  source, edge, target, trigger, condition, doClause);

        Tok(node, semi, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>exitTransitionDefinition</c> → <see cref="ExitTransitionDefinitionSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// exitTransitionDefinition ::= identifierSourceNode ":" Identifier edge targetNode
    ///                              conditionClause? doClause? ";"
    /// ]]></code>
    /// Wie bei <see cref="ParseTransitionDefinition"/> werden <c>edge</c>/<c>targetNode</c> fehlertolerant
    /// behandelt (<c>missing edge</c> / <c>missing target node</c>).
    /// </remarks>
    ExitTransitionDefinitionSyntax ParseExitTransitionDefinition() {

        var source    = ParseIdentifierSourceNode();
        var colon     = Eat(SyntaxTokenType.Colon);
        var name      = Eat(SyntaxTokenType.Identifier);

        var edge = StartsEdge() ? ParseEdge() : null;
        if (edge == null) {
            ReportMissing("edge");
        }

        var target = StartsTargetNode() ? ParseTargetNode() : null;
        if (target == null && edge != null) {
            ReportMissing("target node");
        }

        var condition = StartsCondition()  ? ParseConditionClause() : null;
        var doClause  = At(SyntaxTokenType.DoKeyword) ? ParseDoClause() : null;

        var semi = Eat(SyntaxTokenType.Semicolon);

        var span = new ExtentBuilder();
        span.Add(source); span.Add(colon); span.Add(name); span.Add(edge); span.Add(target);
        span.Add(condition); span.Add(doClause); span.Add(semi);

        var node = new ExitTransitionDefinitionSyntax(span.ToExtent(), source, edge, target, condition, doClause);

        Tok(node, colon, TextClassification.Punctuation);
        Tok(node, name,  TextClassification.Identifier);
        Tok(node, semi,  TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region SourceNode / TargetNode / Edge

    /// <summary>Grammatikregel <c>sourceNode</c> → <see cref="SourceNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// sourceNode ::= initSourceNode         (* "init" *)
    ///              | identifierSourceNode   (* Identifier *)
    /// ]]></code>
    /// </remarks>
    SourceNodeSyntax ParseSourceNode() {
        return At(SyntaxTokenType.InitKeyword) ? ParseInitSourceNode() : ParseIdentifierSourceNode();
    }

    /// <summary>Grammatikregel <c>initSourceNode</c> → <see cref="InitSourceNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// initSourceNode ::= "init"
    /// ]]></code>
    /// </remarks>
    InitSourceNodeSyntax ParseInitSourceNode() {
        var keyword = Eat(SyntaxTokenType.InitKeyword);
        var node    = new InitSourceNodeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>identifierSourceNode</c> → <see cref="IdentifierSourceNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifierSourceNode ::= Identifier
    /// ]]></code>
    /// </remarks>
    IdentifierSourceNodeSyntax ParseIdentifierSourceNode() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierSourceNodeSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    /// <summary>Grammatikregel <c>targetNode</c> → <see cref="TargetNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// targetNode ::= endTargetNode          (* "end" *)
    ///              | identifierTargetNode   (* Identifier *)
    /// ]]></code>
    /// </remarks>
    TargetNodeSyntax ParseTargetNode() {
        return At(SyntaxTokenType.EndKeyword) ? ParseEndTargetNode() : ParseIdentifierTargetNode();
    }

    /// <summary>Grammatikregel <c>endTargetNode</c> → <see cref="EndTargetNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// endTargetNode ::= "end"
    /// ]]></code>
    /// </remarks>
    EndTargetNodeSyntax ParseEndTargetNode() {
        var keyword = Eat(SyntaxTokenType.EndKeyword);
        var node    = new EndTargetNodeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>identifierTargetNode</c> → <see cref="IdentifierTargetNodeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifierTargetNode ::= Identifier
    /// ]]></code>
    /// </remarks>
    IdentifierTargetNodeSyntax ParseIdentifierTargetNode() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierTargetNodeSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    bool StartsEdge() {
        return IsEdge(At0);
    }

    bool StartsTargetNode() {
        return At(SyntaxTokenType.EndKeyword) || At(SyntaxTokenType.Identifier);
    }

    /// <summary>Grammatikregel <c>edge</c> → <see cref="EdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// edge ::= goToEdge       (* "-->" *)
    ///        | modalEdge      (* "o->" *)
    ///        | nonModalEdge   (* "==>" *)
    /// ]]></code>
    /// </remarks>
    EdgeSyntax ParseEdge() {
        switch (At0) {
            case SyntaxTokenType.GoToEdgeKeyword:    return ParseGoToEdge();
            case SyntaxTokenType.ModalEdgeKeyword:   return ParseModalEdge();
            default:                                 return ParseNonModalEdge();
        }
    }

    /// <summary>Grammatikregel <c>goToEdge</c> → <see cref="GoToEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// goToEdge ::= "-->"
    /// ]]></code>
    /// </remarks>
    GoToEdgeSyntax ParseGoToEdge() {
        var keyword = Eat(SyntaxTokenType.GoToEdgeKeyword);
        var node    = new GoToEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>modalEdge</c> → <see cref="ModalEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// modalEdge ::= "o->"
    /// ]]></code>
    /// </remarks>
    ModalEdgeSyntax ParseModalEdge() {
        var keyword = Eat(SyntaxTokenType.ModalEdgeKeyword);
        var node    = new ModalEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    /// <summary>Grammatikregel <c>nonModalEdge</c> → <see cref="NonModalEdgeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// nonModalEdge ::= "==>"
    /// ]]></code>
    /// </remarks>
    NonModalEdgeSyntax ParseNonModalEdge() {
        var keyword = Eat(SyntaxTokenType.NonModalEdgeKeyword);
        var node    = new NonModalEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    #endregion

    #region Trigger

    bool StartsTrigger() {
        return At(SyntaxTokenType.OnKeyword) || At(SyntaxTokenType.SpontaneousKeyword) || At(SyntaxTokenType.SpontKeyword);
    }

    /// <summary>Grammatikregel <c>trigger</c> → <see cref="TriggerSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// trigger ::= signalTrigger        (* "on" … *)
    ///           | spontaneousTrigger   (* "spontaneous" | "spont" *)
    /// ]]></code>
    /// </remarks>
    TriggerSyntax ParseTrigger() {
        return At(SyntaxTokenType.OnKeyword) ? ParseSignalTrigger() : ParseSpontaneousTrigger();
    }

    /// <summary>Grammatikregel <c>signalTrigger</c> → <see cref="SignalTriggerSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// signalTrigger ::= "on" identifier
    /// ]]></code>
    /// Der <c>identifier</c> wird fehlertolerant behandelt: fehlt er, entsteht der Knoten dennoch (ohne ihn).
    /// </remarks>
    SignalTriggerSyntax ParseSignalTrigger() {

        var keyword    = Eat(SyntaxTokenType.OnKeyword);
        var identifier = At(SyntaxTokenType.Identifier) ? ParseIdentifier() : null;

        var node = new SignalTriggerSyntax(Span(keyword, identifier), identifier);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    /// <summary>Grammatikregel <c>spontaneousTrigger</c> → <see cref="SpontaneousTriggerSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// spontaneousTrigger ::= "spontaneous" | "spont"
    /// ]]></code>
    /// </remarks>
    SpontaneousTriggerSyntax ParseSpontaneousTrigger() {

        var spont       = At(SyntaxTokenType.SpontKeyword)       ? Eat(SyntaxTokenType.SpontKeyword)       : null;
        var spontaneous = At(SyntaxTokenType.SpontaneousKeyword) ? Eat(SyntaxTokenType.SpontaneousKeyword) : null;

        var node = new SpontaneousTriggerSyntax(Span(spont, spontaneous));

        Tok(node, spont,       TextClassification.Keyword);
        Tok(node, spontaneous, TextClassification.Keyword);

        return node;
    }

    #endregion

    #region ConditionClause / DoClause

    bool StartsCondition() {
        return At(SyntaxTokenType.IfKeyword) || At(SyntaxTokenType.ElseKeyword);
    }

    /// <summary>Grammatikregel <c>conditionClause</c> → <see cref="ConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// conditionClause ::= ifConditionClause       (* "if" … *)
    ///                   | elseIfConditionClause   (* "else" "if" … *)
    ///                   | elseConditionClause     (* "else" *)
    /// ]]></code>
    /// Disambiguierung per Lookahead: <c>if</c> ⇒ <c>ifConditionClause</c>; <c>else</c> + <c>if</c> ⇒
    /// <c>elseIfConditionClause</c>; sonst <c>elseConditionClause</c>.
    /// </remarks>
    ConditionClauseSyntax ParseConditionClause() {
        if (At(SyntaxTokenType.IfKeyword)) {
            return ParseIfConditionClause();
        }

        return PeekType(1) == SyntaxTokenType.IfKeyword ? ParseElseIfConditionClause() : ParseElseConditionClause();
    }

    /// <summary>Grammatikregel <c>ifConditionClause</c> → <see cref="IfConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// ifConditionClause ::= "if" identifierOrString
    /// ]]></code>
    /// </remarks>
    IfConditionClauseSyntax ParseIfConditionClause() {

        var keyword            = Eat(SyntaxTokenType.IfKeyword);
        var identifierOrString = ParseIdentifierOrString();

        var node = new IfConditionClauseSyntax(Span(keyword, identifierOrString), identifierOrString);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    /// <summary>Grammatikregel <c>elseConditionClause</c> → <see cref="ElseConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// elseConditionClause ::= "else"
    /// ]]></code>
    /// </remarks>
    ElseConditionClauseSyntax ParseElseConditionClause() {

        var keyword = Eat(SyntaxTokenType.ElseKeyword);

        var node = new ElseConditionClauseSyntax(Span(keyword));

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    /// <summary>Grammatikregel <c>elseIfConditionClause</c> → <see cref="ElseIfConditionClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// elseIfConditionClause ::= elseConditionClause ifConditionClause   (* "else" "if" identifierOrString *)
    /// ]]></code>
    /// </remarks>
    ElseIfConditionClauseSyntax ParseElseIfConditionClause() {

        var elseCondition = ParseElseConditionClause();
        var ifCondition   = ParseIfConditionClause();

        return new ElseIfConditionClauseSyntax(Span(elseCondition, ifCondition), elseCondition, ifCondition);
    }

    /// <summary>Grammatikregel <c>doClause</c> → <see cref="DoClauseSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// doClause ::= "do" identifierOrString
    /// ]]></code>
    /// </remarks>
    DoClauseSyntax ParseDoClause() {

        var keyword            = Eat(SyntaxTokenType.DoKeyword);
        var identifierOrString = ParseIdentifierOrString();

        var node = new DoClauseSyntax(Span(keyword, identifierOrString), identifierOrString);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    #endregion

    #region Code Declarations

    /// <summary>Grammatikregel <c>codeNotImplementedDeclaration</c> → <see cref="CodeNotImplementedDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeNotImplementedDeclaration ::= "[" "notimplemented" "]"
    /// ]]></code>
    /// </remarks>
    CodeNotImplementedDeclarationSyntax ParseCodeNotImplementedDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.NotimplementedKeyword);
        var close   = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeNotImplementedDeclarationSyntax(Span(open, keyword, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeDoNotInjectDeclaration</c> → <see cref="CodeDoNotInjectDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeDoNotInjectDeclaration ::= "[" "donotinject" "]"
    /// ]]></code>
    /// </remarks>
    CodeDoNotInjectDeclarationSyntax ParseCodeDoNotInjectDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.DonotinjectKeyword);
        var close   = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeDoNotInjectDeclarationSyntax(Span(open, keyword, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeAbstractMethodDeclaration</c> → <see cref="CodeAbstractMethodDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeAbstractMethodDeclaration ::= "[" "abstractmethod" "]"
    /// ]]></code>
    /// </remarks>
    CodeAbstractMethodDeclarationSyntax ParseCodeAbstractMethodDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.AbstractmethodKeyword);
        var close   = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeAbstractMethodDeclarationSyntax(Span(open, keyword, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeDeclaration</c> → <see cref="CodeDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeDeclaration ::= "[" "code" StringLiteral* "]"
    /// ]]></code>
    /// </remarks>
    CodeDeclarationSyntax ParseCodeDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.CodeKeyword);

        var literals = new List<RawToken>();
        while (TryEat(SyntaxTokenType.StringLiteral, out var literal)) {
            literals.Add(literal);
        }

        var close = Eat(SyntaxTokenType.CloseBracket);

        var span = new ExtentBuilder();
        span.Add(open); span.Add(keyword);
        foreach (var literal in literals) {
            span.Add(literal);
        }

        span.Add(close);

        var node = new CodeDeclarationSyntax(span.ToExtent());

        Tok(node, open, TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        foreach (var literal in literals) {
            Tok(node, literal, TextClassification.Text);
        }

        Tok(node, close, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeBaseDeclaration</c> → <see cref="CodeBaseDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeBaseDeclaration ::= "[" "base" codeType ( ":" codeType ( "," codeType )? )? "]"
    /// ]]></code>
    /// </remarks>
    CodeBaseDeclarationSyntax ParseCodeBaseDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.BaseKeyword);

        var baseTypes = new List<CodeTypeSyntax> { ParseCodeType() };

        RawToken? colon = null;
        RawToken? comma = null;
        if (At(SyntaxTokenType.Colon)) {
            colon = Eat(SyntaxTokenType.Colon);
            baseTypes.Add(ParseCodeType());

            if (At(SyntaxTokenType.Comma)) {
                comma = Eat(SyntaxTokenType.Comma);
                baseTypes.Add(ParseCodeType());
            }
        }

        var close = Eat(SyntaxTokenType.CloseBracket);

        var span = new ExtentBuilder();
        span.Add(open); span.Add(keyword); span.AddRange(baseTypes); span.Add(colon); span.Add(comma); span.Add(close);

        var node = new CodeBaseDeclarationSyntax(span.ToExtent(), baseTypes);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, colon,   TextClassification.Punctuation);
        Tok(node, comma,   TextClassification.Punctuation);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeGenerateToDeclaration</c> → <see cref="CodeGenerateToDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeGenerateToDeclaration ::= "[" "generateto" StringLiteral "]"
    /// ]]></code>
    /// </remarks>
    CodeGenerateToDeclarationSyntax ParseCodeGenerateToDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.GeneratetoKeyword);
        var literal = Eat(SyntaxTokenType.StringLiteral);
        var close   = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeGenerateToDeclarationSyntax(Span(open, keyword, literal, close));

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, literal, TextClassification.StringLiteral);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeParamsDeclaration</c> → <see cref="CodeParamsDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeParamsDeclaration ::= "[" "params" parameterList? "]"
    /// ]]></code>
    /// </remarks>
    CodeParamsDeclarationSyntax ParseCodeParamsDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.ParamsKeyword);

        var parameterList = At(SyntaxTokenType.Identifier) ? ParseParameterList() : null;

        var close = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeParamsDeclarationSyntax(Span(open, keyword, parameterList, close), parameterList);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>codeResultDeclaration</c> → <see cref="CodeResultDeclarationSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeResultDeclaration ::= "[" "result" parameter "]"
    /// ]]></code>
    /// </remarks>
    CodeResultDeclarationSyntax ParseCodeResultDeclaration() {

        var open      = Eat(SyntaxTokenType.OpenBracket);
        var keyword   = Eat(SyntaxTokenType.ResultKeyword);
        var parameter = ParseParameter();
        var close     = Eat(SyntaxTokenType.CloseBracket);

        var node = new CodeResultDeclarationSyntax(Span(open, keyword, parameter, close), parameter);

        Tok(node, open,    TextClassification.Punctuation);
        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, close,   TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region ParameterList / Parameter

    /// <summary>Grammatikregel <c>parameterList</c> → <see cref="ParameterListSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// parameterList ::= parameter ( "," parameter )*
    /// ]]></code>
    /// </remarks>
    ParameterListSyntax ParseParameterList() {

        var parameters = new List<ParameterSyntax> { ParseParameter() };
        var commas     = new List<RawToken>();

        while (TryEat(SyntaxTokenType.Comma, out var comma)) {
            commas.Add(comma);
            parameters.Add(ParseParameter());
        }

        var span = new ExtentBuilder();
        span.AddRange(parameters);
        foreach (var comma in commas) {
            span.Add(comma);
        }

        var node = new ParameterListSyntax(span.ToExtent(), parameters);

        foreach (var comma in commas) {
            Tok(node, comma, TextClassification.Punctuation);
        }

        return node;
    }

    /// <summary>Grammatikregel <c>parameter</c> → <see cref="ParameterSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// parameter ::= codeType Identifier?
    /// ]]></code>
    /// Das optionale <c>Identifier</c> ist der Parametername.
    /// </remarks>
    ParameterSyntax ParseParameter() {

        var type = ParseCodeType();
        var name = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : null;

        var node = new ParameterSyntax(Span(type, name), type);

        Tok(node, name, TextClassification.ParameterName);

        return node;
    }

    #endregion

    #region CodeType

    /// <summary>Grammatikregel <c>codeType</c> → <see cref="CodeTypeSyntax"/> (deckt auch <c>arrayType</c> ab).</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// codeType   ::= simpleType
    ///              | genericType
    ///              | arrayType
    /// arrayType  ::= ( simpleType | genericType ) arrayRankSpecifier+
    /// ]]></code>
    /// Die Regel <c>arrayType</c> ist hier eingefaltet: zuerst wird der Basistyp geparst (<c>Identifier</c> +
    /// <c>&lt;</c> ⇒ <c>genericType</c>, sonst <c>simpleType</c>); folgt ein <c>[</c> <c>]</c>, entsteht ein
    /// <see cref="ArrayTypeSyntax"/> mit einem oder mehreren <c>arrayRankSpecifier</c>.
    /// </remarks>
    CodeTypeSyntax ParseCodeType() {

        CodeTypeSyntax baseType = At(SyntaxTokenType.Identifier) && PeekType(1) == SyntaxTokenType.LessThan
            ? ParseGenericType()
            : ParseSimpleType();

        if (At(SyntaxTokenType.OpenBracket) && PeekType(1) == SyntaxTokenType.CloseBracket) {
            var rankSpecifiers = new List<ArrayRankSpecifierSyntax>();
            var span           = new ExtentBuilder();
            span.Add(baseType);

            while (At(SyntaxTokenType.OpenBracket) && PeekType(1) == SyntaxTokenType.CloseBracket) {
                var rank = ParseArrayRankSpecifier();
                rankSpecifiers.Add(rank);
                span.Add(rank);
            }

            return new ArrayTypeSyntax(span.ToExtent(), baseType, rankSpecifiers);
        }

        return baseType;
    }

    /// <summary>Grammatikregel <c>simpleType</c> → <see cref="SimpleTypeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// simpleType ::= Identifier "?"?
    /// ]]></code>
    /// Das optionale <c>?</c> markiert einen Nullable-Typ.
    /// </remarks>
    SimpleTypeSyntax ParseSimpleType() {

        var identifier   = Eat(SyntaxTokenType.Identifier);
        var questionmark = At(SyntaxTokenType.Questionmark) ? Eat(SyntaxTokenType.Questionmark) : null;

        var node = new SimpleTypeSyntax(Span(identifier, questionmark));

        Tok(node, identifier,   TextClassification.TypeName);
        Tok(node, questionmark, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>genericType</c> → <see cref="GenericTypeSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// genericType ::= Identifier "<" codeType ( "," codeType )* ">"
    /// ]]></code>
    /// </remarks>
    GenericTypeSyntax ParseGenericType() {

        var identifier = Eat(SyntaxTokenType.Identifier);
        var lessThan   = Eat(SyntaxTokenType.LessThan);

        var arguments = new List<CodeTypeSyntax> { ParseCodeType() };
        var commas    = new List<RawToken>();

        while (TryEat(SyntaxTokenType.Comma, out var comma)) {
            commas.Add(comma);
            arguments.Add(ParseCodeType());
        }

        var greaterThan = Eat(SyntaxTokenType.GreaterThan);

        var span = new ExtentBuilder();
        span.Add(identifier); span.Add(lessThan); span.AddRange(arguments);
        foreach (var comma in commas) {
            span.Add(comma);
        }

        span.Add(greaterThan);

        var node = new GenericTypeSyntax(span.ToExtent(), arguments);

        Tok(node, identifier, TextClassification.TypeName);
        Tok(node, lessThan,   TextClassification.Punctuation);
        foreach (var comma in commas) {
            Tok(node, comma, TextClassification.Punctuation);
        }

        Tok(node, greaterThan, TextClassification.Punctuation);

        return node;
    }

    /// <summary>Grammatikregel <c>arrayRankSpecifier</c> → <see cref="ArrayRankSpecifierSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// arrayRankSpecifier ::= "[" "]"
    /// ]]></code>
    /// </remarks>
    ArrayRankSpecifierSyntax ParseArrayRankSpecifier() {

        var open  = Eat(SyntaxTokenType.OpenBracket);
        var close = Eat(SyntaxTokenType.CloseBracket);

        var node = new ArrayRankSpecifierSyntax(Span(open, close));

        Tok(node, open,  TextClassification.Punctuation);
        Tok(node, close, TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region IdentifierOrString

    /// <summary>Grammatikregel <c>identifierOrString</c> → <see cref="IdentifierOrStringSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifierOrString ::= identifier      (* Identifier *)
    ///                      | stringLiteral   (* StringLiteral *)
    /// ]]></code>
    /// Steht weder ein <c>Identifier</c> noch ein <c>StringLiteral</c> an, liefert die Methode <c>null</c>
    /// (der aufrufende Knoten entsteht dann ohne diesen Bestandteil).
    /// </remarks>
    IdentifierOrStringSyntax ParseIdentifierOrString() {
        if (At(SyntaxTokenType.Identifier)) {
            return ParseIdentifier();
        }

        if (At(SyntaxTokenType.StringLiteral)) {
            return ParseStringLiteral();
        }

        return null;
    }

    /// <summary>Grammatikregel <c>identifier</c> → <see cref="IdentifierSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// identifier ::= Identifier
    /// ]]></code>
    /// </remarks>
    IdentifierSyntax ParseIdentifier() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    /// <summary>Grammatikregel <c>stringLiteral</c> → <see cref="StringLiteralSyntax"/>.</summary>
    /// <remarks>
    /// <code><![CDATA[
    /// stringLiteral ::= StringLiteral
    /// ]]></code>
    /// </remarks>
    StringLiteralSyntax ParseStringLiteral() {
        var literal = Eat(SyntaxTokenType.StringLiteral);
        var node    = new StringLiteralSyntax(Span(literal));
        Tok(node, literal, TextClassification.StringLiteral);
        return node;
    }

    #endregion

    #region Token-Strom: Cursor, Konsum, Trivia-Anhang

    SyntaxTokenType At0 => _pos < _raw.Length ? _raw[_pos].Type : SyntaxTokenType.EndOfFile;

    bool At(SyntaxTokenType type) => At0 == type;

    bool AtEof => At0 == SyntaxTokenType.EndOfFile;

    bool AtCodeDeclaration(SyntaxTokenType keyword) {
        return At(SyntaxTokenType.OpenBracket) && PeekType(1) == keyword;
    }

    bool StartsConnectionPoint() {
        return At(SyntaxTokenType.InitKeyword) || At(SyntaxTokenType.ExitKeyword) || At(SyntaxTokenType.EndKeyword);
    }

    /// <summary>
    /// Äußere Anker eines Task-Körpers: das schließende <c>}</c> sowie der Beginn eines neuen Top-Level-
    /// Members (<c>task</c>/<c>taskref</c>) und das Dateiende. An diesen Token bricht lokale Recovery ab und
    /// überlässt das Token der äußeren Regel (gestaffelte Sync-Sets), statt die äußere Struktur mitzureißen.
    /// </summary>
    bool BreaksBody() {
        return At(SyntaxTokenType.CloseBrace)    ||
               At(SyntaxTokenType.TaskKeyword)   ||
               At(SyntaxTokenType.TaskrefKeyword) ||
               AtEof;
    }

    /// <summary>Typ des n-ten parser-sichtbaren Tokens ab der aktuellen Position (Trivia übersprungen).</summary>
    SyntaxTokenType PeekType(int n) {
        var index = _pos;
        var seen  = 0;
        while (index < _raw.Length) {
            if (!IsHidden(_raw[index].Type)) {
                if (seen == n) {
                    return _raw[index].Type;
                }

                seen++;
            }

            index++;
        }

        return SyntaxTokenType.EndOfFile;
    }

    /// <summary>
    /// Konsumiert das aktuelle Token, wenn es <paramref name="type"/> entspricht, und rückt auf das nächste
    /// sichtbare Token vor. Andernfalls (Insertion-Recovery) wird ein nullbreites Missing-Token
    /// synthetisiert: es kommt <i>nicht</i> in den Token-Strom (<c>null</c>-Rückgabe trägt nichts zum
    /// Extent bei), es wird eine Diagnose an der Einfügestelle gemeldet, und der Cursor rückt
    /// <b>nicht</b> vor. Alle optionalen Aufrufstellen sind durch ein vorheriges <see cref="At"/>
    /// abgesichert und erreichen den Fehlerzweig daher nie — so meldet nur ein wirklich fehlendes
    /// Pflicht-Token.
    /// </summary>
    RawToken? Eat(SyntaxTokenType type) {
        if (At0 != type) {
            ReportMissing(Describe(type));
            return null;
        }

        var token = _raw[_pos];
        _firstSignificantStart ??= token.Start;

        _pos++;
        SkipHidden();

        return token;
    }

    /// <summary>
    /// Konsumiert das aktuelle Token, wenn es <paramref name="type"/> entspricht, gibt es über
    /// <paramref name="token"/> zurück und rückt vor (Ergebnis <c>true</c>). Andernfalls bleibt der Cursor
    /// stehen und das Ergebnis ist <c>false</c> — <b>ohne</b> Diagnose. Das ist die nicht-nullbare,
    /// konsumierende Form von <see cref="At(SyntaxTokenType)"/> für Listen-Schleifen (z.B. komma-getrennte
    /// Aufzählungen): es gibt hier kein Pflicht-Token, daher auch kein Missing-Token wie bei <see cref="Eat"/>.
    /// </summary>
    bool TryEat(SyntaxTokenType type, out RawToken token) {
        if (At0 != type) {
            token = default;
            return false;
        }

        token = _raw[_pos];
        _firstSignificantStart ??= token.Start;

        _pos++;
        SkipHidden();

        return true;
    }

    void SkipHidden() {
        while (_pos < _raw.Length && IsHidden(_raw[_pos].Type)) {
            _pos++;
        }
    }

    /// <summary>
    /// Panic-Mode (Deletion-Recovery): überspringt das aktuelle und alle folgenden signifikanten Token, bis
    /// <paramref name="recovered"/> zutrifft — also ein Wiedereinstiegs- oder äußeres Anker-Token (inkl.
    /// Dateiende) erreicht ist. Die übersprungenen Token werden hier nur überlesen; an die Wurzel gehängt
    /// werden sie — als <see cref="TextClassification.Skiped"/> — erst in <see cref="AttachNonSignificantTokens"/>
    /// (nichts geht verloren, der Round-Trip bleibt vollständig). Pro Lauf wird genau eine Diagnose an der
    /// ersten übersprungenen Stelle gemeldet. Fortschritts-Garantie: trifft <paramref name="recovered"/> nicht
    /// schon zu Beginn zu, rückt der Aufruf um mindestens ein signifikantes Token vor — Listen-Schleifen können
    /// also nicht endlos drehen.
    /// </summary>
    void Recover(Func<bool> recovered) {
        if (recovered()) {
            return;
        }

        ReportUnexpected(TextExtent.FromBounds(CurrentStart, CurrentEnd), CurrentText);

        do {
            _firstSignificantStart ??= CurrentStart;
            _pos++;
            SkipHidden();
        } while (!recovered());
    }

    int CurrentStart => _pos < _raw.Length ? _raw[_pos].Extent.Start : _eofPos;
    int CurrentEnd   => _pos < _raw.Length ? _raw[_pos].Extent.End   : _eofPos;

    string CurrentText => _sourceText.Substring(TextExtent.FromBounds(CurrentStart, CurrentEnd));

    /// <summary>
    /// Meldet ein fehlendes Pflicht-Token (Insertion) nullbreit am <b>Ende des zuletzt konsumierten
    /// signifikanten Tokens</b> — also direkt hinter dem zuvor Getippten, vor dessen Trailing-Trivia.
    /// Das entspricht der Roslyn-Konvention (»X erwartet« hängt am vorigen Token, nicht am Anfang des
    /// folgenden): ein fehlendes <c>;</c> erscheint am Ende der vorigen Zeile statt vor dem nächsten
    /// Knoten. Gibt es kein vorheriges signifikantes Token (Fehlstelle am Dateianfang), wird nullbreit
    /// an der aktuellen Cursor-Position gemeldet.
    /// <para/>
    /// Am Dateiende wird die <b>Kaskade</b> nach der ersten Meldung abgebrochen: bricht die Eingabe
    /// vorzeitig ab, synthetisiert der Parser beim Aufrollen der Regeln eine Reihe fehlender Pflicht-Token
    /// (z.B. Zielknoten → <c>;</c> → <c>}</c>), die alle nullbreit an derselben EOF-Position landen würden.
    /// Gemeldet wird nur die <b>erste</b> (die den unvollständigen Bau benennt); die mechanischen
    /// Folgefehler werden unterdrückt.
    /// </summary>
    void ReportMissing(string what) {
        if (AtEof) {
            if (_reportedMissingAtEof) {
                return;
            }

            _reportedMissingAtEof = true;
        }

        var position = PreviousSignificantEnd() ?? CurrentStart;
        var at       = TextExtent.FromBounds(position, position);
        _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(at),
                                        DiagnosticDescriptors.NewSyntaxError($"missing {what}")));
    }

    /// <summary>
    /// Liefert das Extent-Ende des letzten signifikanten (nicht versteckten) Tokens vor dem Cursor —
    /// die Stelle unmittelbar hinter dem zuvor konsumierten Token, noch vor dessen Trailing-Trivia.
    /// <c>null</c>, wenn vor dem Cursor kein signifikantes Token liegt.
    /// </summary>
    int? PreviousSignificantEnd() {
        for (var i = _pos - 1; i >= 0; i--) {
            if (!IsHidden(_raw[i].Type)) {
                return _raw[i].Extent.End;
            }
        }

        return null;
    }

    /// <summary>Meldet ein unerwartetes (übersprungenes) Token (Deletion) über dessen Extent.</summary>
    void ReportUnexpected(TextExtent extent, string text) {
        _diagnostics.Add(new Diagnostic(_sourceText.GetLocation(extent),
                                        DiagnosticDescriptors.NewSyntaxError($"unexpected input '{text}'")));
    }

    /// <summary>Lesbare Bezeichnung eines erwarteten Tokens für „missing …"-Diagnosen.</summary>
    static string Describe(SyntaxTokenType type) {
        switch (type) {
            case SyntaxTokenType.Semicolon:     return "';'";
            case SyntaxTokenType.OpenBrace:     return "'{'";
            case SyntaxTokenType.CloseBrace:    return "'}'";
            case SyntaxTokenType.OpenBracket:   return "'['";
            case SyntaxTokenType.CloseBracket:  return "']'";
            case SyntaxTokenType.Colon:         return "':'";
            case SyntaxTokenType.Comma:         return "','";
            case SyntaxTokenType.LessThan:      return "'<'";
            case SyntaxTokenType.GreaterThan:   return "'>'";
            case SyntaxTokenType.Questionmark:  return "'?'";
            case SyntaxTokenType.Identifier:    return "identifier";
            case SyntaxTokenType.StringLiteral: return "string literal";
            default:                            return $"'{type}'";
        }
    }

    /// <summary>
    /// Hängt — nach dem Aufbau des Wurzelknotens — alle nicht vom Parser konsumierten, aber im flachen
    /// Strom verbleibenden Token an die Wurzel: unbekannte Zeichen, Präprozessor-Token, vom Panic-Mode
    /// übersprungene signifikante Token und das abschließende <see cref="SyntaxTokenType.EndOfFile"/>.
    /// Trivia (Whitespace/Zeilenende/Kommentare) wird hier <b>nicht</b> mehr in den Strom aufgenommen —
    /// sie liegt ausschließlich als Leading/Trailing an den Token (siehe <see cref="BuildTokenTrivia"/>).
    /// Dabei werden auch die rein lexikalischen Diagnosen gemeldet (unerwartetes Zeichen, nicht
    /// unterstützte Präprozessor-Direktive).
    /// </summary>
    void AttachNonSignificantTokens(SyntaxNode root) {

        // Vom Parser bereits an Knoten gehängte (signifikante) Token — anhand ihrer eindeutigen
        // Start-Position. Token überlappen nie, daher ist die Start-Position ein sicherer Identitätsschlüssel.
        var consumedStarts = new HashSet<int>();
        foreach (var token in _tokens) {
            consumedStarts.Add(token.Start);
        }

        foreach (var raw in _raw) {
            // Trivia (Whitespace/Zeilenende/Kommentar) hängt ausschließlich als Leading/Trailing an
            // den signifikanten bzw. Trenner-Token (siehe BuildTokenTrivia) — sie wird nicht mehr als
            // eigenes Token in den flachen Strom aufgenommen.
            if (SyntaxFacts.IsTrivia(raw.Type)) {
                continue;
            }

            if (TryClassifyNonSignificant(raw.Type, out var classification)) {
                // Das abschließende EndOfFile trägt die finale Datei-Trivia als seine Leading-Trivia; ein Trenner
                // (unbekanntes Zeichen / Präprozessor-Token) trägt die ihm vorausgehende, sonst heimatlose Trivia
                // als Leading (siehe BuildTokenTrivia).
                var leading = raw.Type == SyntaxTokenType.EndOfFile ? _eofLeadingTrivia : LookupTrivia(raw.Extent.Start).Leading;
                _tokens.Add(SyntaxTokenFactory.CreateToken(raw.Extent, raw.Type, classification, root,
                                                           leading, ImmutableArray<SyntaxTrivia>.Empty));

                ReportLexicalDiagnostics(raw);
            } else if (!consumedStarts.Contains(raw.Extent.Start)) {
                // Signifikantes Token, das der Parser nicht konsumiert hat (Panic-Mode-Skip): wie die Trivia
                // als Skiped-Token an die Wurzel — so deckt der Token-Strom lückenlos den ganzen Text ab.
                var trivia = LookupTrivia(raw.Extent.Start);
                _tokens.Add(SyntaxTokenFactory.CreateToken(raw.Extent, raw.Type, TextClassification.Skiped, root,
                                                           trivia.Leading, trivia.Trailing));
            }
        }
    }

    /// <summary>
    /// Meldet die nur vom Lexer ableitbaren Diagnosen für ein nicht-signifikantes Token: ein
    /// <see cref="SyntaxTokenType.Unknown"/> als unerwartetes Zeichen (<c>Nav0000</c>); ein
    /// <see cref="SyntaxTokenType.HashToken"/> als nicht unterstützte Präprozessor-Direktive
    /// (<c>Nav3000</c>), beim <c>#</c> zusätzlich <c>Nav3001</c>, falls davor in der Zeile nicht nur
    /// Whitespace steht. Die Diagnose hängt einmalig am einleitenden <c>#</c> — das anschließende
    /// <see cref="SyntaxTokenType.PreprocessorKeyword"/> löst keine eigene Meldung aus, damit eine
    /// Direktive nur eine einzige <c>Nav3000</c> erzeugt. Location ist die nullbreite Start-Position
    /// des Tokens.
    /// </summary>
    void ReportLexicalDiagnostics(RawToken raw) {
        switch (raw.Type) {
            case SyntaxTokenType.Unknown:
                _diagnostics.Add(new Diagnostic(LexicalLocation(raw.Extent),
                                                DiagnosticDescriptors.Syntax.Nav0000UnexpectedCharacter,
                                                _sourceText.Substring(raw.Extent)));
                break;
            case SyntaxTokenType.HashToken: {
                if (!_sourceText.SliceFromLineStartToPosition(raw.Start).IsWhiteSpace()) {
                    _diagnostics.Add(new Diagnostic(LexicalLocation(raw.Extent),
                                                    DiagnosticDescriptors.Syntax.Nav3001PreprocessorDirectiveMustAppearOnFirstNonWhitespacePosition));
                }

                _diagnostics.Add(new Diagnostic(LexicalLocation(raw.Extent),
                                                DiagnosticDescriptors.Syntax.Nav3000InvalidPreprocessorDirective));
                break;
            }
        }
    }

    /// <summary>
    /// Baut die <see cref="Location"/> für eine lexikalische Diagnose. Anders als
    /// <see cref="SourceText.GetLocation"/> (Zeilen<i>bereich</i>) trägt sie — wie die bisherige
    /// ANTLR-Pipeline für diese Diagnosen — Start- und End-Zeilenposition <b>identisch</b> an der
    /// Startposition des Tokens; im Test-Formatter erscheint sie damit nullbreit.
    /// </summary>
    Location LexicalLocation(TextExtent extent) {
        var line         = _sourceText.GetTextLineAtPosition(extent.Start);
        var linePosition = new LinePosition(line.Line, extent.Start - line.Start);
        return new Location(extent, linePosition, _sourceText.FileInfo?.FullName);
    }

    static bool TryClassifyNonSignificant(SyntaxTokenType type, out TextClassification classification) {
        switch (type) {
            case SyntaxTokenType.Whitespace:
            case SyntaxTokenType.NewLine:
            case SyntaxTokenType.EndOfFile:
                classification = TextClassification.Whitespace;
                return true;
            case SyntaxTokenType.SingleLineComment:
            case SyntaxTokenType.MultiLineComment:
                classification = TextClassification.Comment;
                return true;
            case SyntaxTokenType.Unknown:
                classification = TextClassification.Skiped;
                return true;
            case SyntaxTokenType.HashToken:
            case SyntaxTokenType.PreprocessorKeyword:
                classification = TextClassification.PreprocessorKeyword;
                return true;
            case SyntaxTokenType.PreprocessorText:
            case SyntaxTokenType.PreprocessorNewLine:
                classification = TextClassification.PreprocessorText;
                return true;
            default:
                classification = TextClassification.Unknown;
                return false;
        }
    }

    void Tok(SyntaxNode parent, RawToken? raw, TextClassification classification) {
        if (raw == null) {
            return;
        }

        var trivia = LookupTrivia(raw.Value.Start);
        var token  = SyntaxTokenFactory.CreateToken(raw.Value.Extent, raw.Value.Type, classification, parent,
                                                    trivia.Leading, trivia.Trailing);
        if (!token.IsMissing) {
            _tokens.Add(token);
        }
    }

    /// <summary>Die vorab berechnete Trivia eines signifikanten Tokens (per Start-Position), sonst leer.</summary>
    TokenTrivia LookupTrivia(int start) {
        return _tokenTrivia.TryGetValue(start, out var trivia) ? trivia : TokenTrivia.Empty;
    }

    /// <summary>Leading/Trailing-Trivia eines Tokens — das vorab aus <c>_raw</c> berechnete Roslyn-Bündel.</summary>
    readonly struct TokenTrivia {

        public TokenTrivia(ImmutableArray<SyntaxTrivia> leading, ImmutableArray<SyntaxTrivia> trailing) {
            Leading  = leading;
            Trailing = trailing;
        }

        public ImmutableArray<SyntaxTrivia> Leading  { get; }
        public ImmutableArray<SyntaxTrivia> Trailing { get; }

        public static readonly TokenTrivia Empty = new(ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
    }

    /// <summary>
    /// Ordnet die rein lexikalische Trivia (Whitespace, Zeilenenden, Kommentare) nach der Roslyn-Regel den
    /// signifikanten Token zu — vorab, da der Lexer-Strom <paramref name="raw"/> bereits vollständig vorliegt:
    /// <list type="bullet">
    ///   <item><description><b>Trailing</b> eines Tokens: die anschließende Trivia bis <b>einschließlich</b>
    ///   des ersten Zeilenendes; folgt vor einem Zeilenende bereits das nächste (nicht-Trivia-)Token, endet
    ///   die Trailing-Trivia dort.</description></item>
    ///   <item><description><b>Leading</b> eines Tokens: die restliche Trivia bis zu ihm — also die kompletten
    ///   nachfolgenden (Leer-/Kommentar-)Zeilen samt der Einrückung seiner eigenen Zeile.</description></item>
    ///   <item><description>Das abschließende <see cref="SyntaxTokenType.EndOfFile"/> erhält die finale
    ///   Datei-Trivia als seine Leading-Trivia (Round-Trip bleibt lückenlos).</description></item>
    /// </list>
    /// Nicht-Trivia-, aber dennoch nicht-signifikante Token (unbekannte Zeichen, Präprozessor-Token) wirken —
    /// wie signifikante Token — als Trenner einer Trivia-Folge, erhalten selbst aber keine angehängte Trivia.
    /// </summary>
    static Dictionary<int, TokenTrivia> BuildTokenTrivia(ImmutableArray<RawToken> raw, out ImmutableArray<SyntaxTrivia> eofLeadingTrivia) {

        var result  = new Dictionary<int, TokenTrivia>();
        var leading = new Dictionary<int, ImmutableArray<SyntaxTrivia>>();

        var pending             = new List<SyntaxTrivia>(); // Trivia seit dem letzten Trenner (Kandidat für Trailing/Leading).
        var lastSignificantKey  = -1;                       // Start des letzten Tokens, das noch Trailing-Trivia aufnimmt; -1 = keins.
        eofLeadingTrivia = ImmutableArray<SyntaxTrivia>.Empty;

        foreach (var token in raw) {

            if (token.IsTrivia) {
                pending.Add(new SyntaxTrivia(token.Type, token.Extent));
                continue;
            }

            // Trenner erreicht: zuerst die Trailing-Trivia des vorigen signifikanten Tokens vom Anfang von
            // pending abspalten (bis einschließlich des ersten Zeilenendes; sonst — kein Zeilenende — alles).
            if (lastSignificantKey >= 0) {
                var trailing = SplitTrailing(pending);
                result[lastSignificantKey] = new TokenTrivia(
                    leading.TryGetValue(lastSignificantKey, out var lead) ? lead : ImmutableArray<SyntaxTrivia>.Empty,
                    trailing);
            }

            // Der Rest von pending ist die Leading-Trivia des aktuellen Trenners (sofern er welche aufnimmt).
            var remaining = pending.ToImmutableArray();
            pending.Clear();

            if (token.Type == SyntaxTokenType.EndOfFile) {
                eofLeadingTrivia   = remaining;
                lastSignificantKey = -1;
            } else if (IsHidden(token.Type)) {
                // Trenner (unbekanntes Zeichen / Präprozessor-Token): nimmt selbst keine Trailing-Trivia auf,
                // trägt die restliche Trivia aber als Leading. Sonst ginge der Text zwischen dem vorigen Token
                // und dem Trenner verloren, sobald die Trivia nicht mehr separat im flachen Strom geführt wird.
                if (!remaining.IsEmpty) {
                    leading[token.Start] = remaining;
                }

                lastSignificantKey = -1;
            } else {
                leading[token.Start] = remaining;
                lastSignificantKey   = token.Start;
            }
        }

        // Signifikante Token, die keine Trailing-Trivia bekommen haben (z.B. letztes Token vor EOF ohne
        // nachfolgende Trivia), tragen dennoch ihre Leading-Trivia.
        foreach (var entry in leading) {
            if (!result.ContainsKey(entry.Key)) {
                result[entry.Key] = new TokenTrivia(entry.Value, ImmutableArray<SyntaxTrivia>.Empty);
            }
        }

        return result;
    }

    /// <summary>
    /// Spaltet vom Anfang von <paramref name="pending"/> die Trailing-Trivia ab: alle Elemente bis
    /// <b>einschließlich</b> des ersten Zeilenendes. Enthält <paramref name="pending"/> kein Zeilenende
    /// (der nächste Trenner steht in derselben Zeile), wird alles als Trailing-Trivia genommen. Die
    /// abgespaltenen Elemente werden aus <paramref name="pending"/> entfernt.
    /// </summary>
    static ImmutableArray<SyntaxTrivia> SplitTrailing(List<SyntaxTrivia> pending) {

        var count       = 0;
        var sawNewLine  = false;
        for (var i = 0; i < pending.Count; i++) {
            count++;
            if (pending[i].Type == SyntaxTokenType.NewLine) {
                sawNewLine = true;
                break;
            }
        }

        if (!sawNewLine) {
            count = pending.Count;
        }

        var builder = ImmutableArray.CreateBuilder<SyntaxTrivia>(count);
        for (var i = 0; i < count; i++) {
            builder.Add(pending[i]);
        }

        pending.RemoveRange(0, count);
        return builder.MoveToImmutable();
    }

    static bool IsEdge(SyntaxTokenType type) {
        return type is SyntaxTokenType.GoToEdgeKeyword or SyntaxTokenType.ModalEdgeKeyword or SyntaxTokenType.NonModalEdgeKeyword;
    }

    static bool IsHidden(SyntaxTokenType type) {
        switch (type) {
            case SyntaxTokenType.Whitespace:
            case SyntaxTokenType.NewLine:
            case SyntaxTokenType.SingleLineComment:
            case SyntaxTokenType.MultiLineComment:
            case SyntaxTokenType.Unknown:
            case SyntaxTokenType.HashToken:
            case SyntaxTokenType.PreprocessorKeyword:
            case SyntaxTokenType.PreprocessorText:
            case SyntaxTokenType.PreprocessorNewLine:
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Extent-Hilfen

    TextExtent Span(params object[] parts) {
        var builder = new ExtentBuilder();
        foreach (var part in parts) {
            switch (part) {
                case null:
                    break;
                case RawToken raw:
                    builder.Add(raw);
                    break;
                case SyntaxNode node:
                    builder.Add(node);
                    break;
            }
        }

        return builder.ToExtent();
    }

    /// <summary>
    /// Sammelt den umschließenden <see cref="TextExtent"/> über konsumierte Token und Kindknoten. Fehlende
    /// (optionale, nicht vorhandene) Bestandteile tragen nichts bei; bleibt alles leer, ist der Extent
    /// <see cref="TextExtent.Missing"/> (z.B. ein leerer Knoten-/Transitionsblock).
    /// </summary>
    struct ExtentBuilder {

        int _start;
        int _end;
        bool _any;

        public void Add(RawToken? raw) {
            if (raw == null) {
                return;
            }

            Add(raw.Value.Extent);
        }

        public void Add(SyntaxNode node) {
            if (node != null) {
                Add(node.Extent);
            }
        }

        public void AddRange<T>(IEnumerable<T> nodes) where T : SyntaxNode {
            foreach (var node in nodes) {
                Add(node);
            }
        }

        void Add(TextExtent extent) {
            if (extent.IsMissing) {
                return;
            }

            if (!_any) {
                _start = extent.Start;
                _end   = extent.End;
                _any   = true;
                return;
            }

            if (extent.Start < _start) {
                _start = extent.Start;
            }

            if (extent.End > _end) {
                _end = extent.End;
            }
        }

        public TextExtent ToExtent() {
            return _any ? TextExtent.FromBounds(_start, _end) : TextExtent.Missing;
        }
    }

    #endregion

}
