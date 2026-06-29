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
/// Grammatikregel. Fehlertolerante Recovery (Missing-/Skipped-Token, Sync-Sets, Diagnostics-Parität) ist
/// bewusst noch nicht enthalten und folgt in einem eigenen Schritt; dieser Parser bildet den wohlgeformten
/// Fall ab.
/// </summary>
sealed class NavParser {

    readonly SourceText                          _sourceText;
    readonly ImmutableArray<RawToken>            _raw;
    readonly List<SyntaxToken>                   _tokens;
    readonly ImmutableArray<Diagnostic>.Builder  _diagnostics;
    readonly int                                 _eofPos;

    int  _pos;                     // Index in _raw; Invariante: zeigt stets auf ein parser-sichtbares Token (signifikant oder EOF).
    int? _firstSignificantStart;   // Start des ersten konsumierten signifikanten Tokens (für den Wurzel-Extent).

    NavParser(SourceText sourceText) {
        _sourceText  = sourceText;
        _raw         = NavLexer.Lex(sourceText.Text);
        _tokens      = new List<SyntaxToken>(_raw.Length);
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        _eofPos      = _raw[_raw.Length - 1].Start; // Das abschließende EndOfFile ist nullbreit am Textende.

        _pos = 0;
        SkipHidden();
    }

    public static SyntaxTree Parse(string text, string filePath = null, CancellationToken cancellationToken = default) {

        var sourceText = SourceText.From(text ?? String.Empty, filePath);

        return new NavParser(sourceText).ParseCodeGenerationUnit(cancellationToken);
    }

    #region CodeGenerationUnit

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

        while (At(SyntaxTokenType.TaskrefKeyword) || At(SyntaxTokenType.TaskKeyword)) {
            cancellationToken.ThrowIfCancellationRequested();
            members.Add(ParseMemberDeclaration());
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

    TaskDeclarationSyntax ParseTaskDeclaration() {

        var keyword = Eat(SyntaxTokenType.TaskrefKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        var codeNamespace  = AtCodeDeclaration(SyntaxTokenType.NamespaceprefixKeyword) ? ParseCodeNamespaceDeclaration()       : null;
        var notImplemented = AtCodeDeclaration(SyntaxTokenType.NotimplementedKeyword)  ? ParseCodeNotImplementedDeclaration()  : null;
        var result         = AtCodeDeclaration(SyntaxTokenType.ResultKeyword)          ? ParseCodeResultDeclaration()          : null;

        var open = Eat(SyntaxTokenType.OpenBrace);

        var connectionPoints = new List<ConnectionPointNodeSyntax>();
        while (At(SyntaxTokenType.InitKeyword) || At(SyntaxTokenType.ExitKeyword) || At(SyntaxTokenType.EndKeyword)) {
            connectionPoints.Add(ParseConnectionPointNodeDeclaration());
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

    ConnectionPointNodeSyntax ParseConnectionPointNodeDeclaration() {
        return At(SyntaxTokenType.InitKeyword) ? ParseInitNodeDeclaration()
             : At(SyntaxTokenType.ExitKeyword) ? ParseExitNodeDeclaration()
             :                                   ParseEndNodeDeclaration();
    }

    #endregion

    #region TaskDefinition

    TaskDefinitionSyntax ParseTaskDefinition() {

        var keyword = Eat(SyntaxTokenType.TaskKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);

        var code       = AtCodeDeclaration(SyntaxTokenType.CodeKeyword)       ? ParseCodeDeclaration()           : null;
        var codeBase   = AtCodeDeclaration(SyntaxTokenType.BaseKeyword)       ? ParseCodeBaseDeclaration()       : null;
        var generateTo = AtCodeDeclaration(SyntaxTokenType.GeneratetoKeyword) ? ParseCodeGenerateToDeclaration() : null;
        var codeParams = AtCodeDeclaration(SyntaxTokenType.ParamsKeyword)     ? ParseCodeParamsDeclaration()     : null;
        var result     = AtCodeDeclaration(SyntaxTokenType.ResultKeyword)     ? ParseCodeResultDeclaration()     : null;

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

    InitNodeDeclarationSyntax ParseInitNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.InitKeyword);
        var name    = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : (RawToken?) null;

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

    EndNodeDeclarationSyntax ParseEndNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.EndKeyword);
        var semi    = Eat(SyntaxTokenType.Semicolon);

        var node = new EndNodeDeclarationSyntax(Span(keyword, semi));

        Tok(node, keyword, TextClassification.Keyword);
        Tok(node, semi,    TextClassification.Punctuation);

        return node;
    }

    TaskNodeDeclarationSyntax ParseTaskNodeDeclaration() {

        var keyword = Eat(SyntaxTokenType.TaskKeyword);
        var name    = Eat(SyntaxTokenType.Identifier);
        var alias   = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : (RawToken?) null;

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

    TransitionDefinitionBlockSyntax ParseTransitionDefinitionBlock() {

        // Quelltext-Reihenfolge darf transitionDefinition und exitTransitionDefinition mischen; im Baum
        // werden sie jedoch — wie im bisherigen Modell — getrennt gruppiert (erst alle Transitionen, dann
        // alle Exit-Transitionen).
        var transitions     = new List<TransitionDefinitionSyntax>();
        var exitTransitions = new List<ExitTransitionDefinitionSyntax>();
        var span            = new ExtentBuilder();

        while (StartsTransition()) {
            if (At(SyntaxTokenType.Identifier) && PeekType(1) == SyntaxTokenType.Colon) {
                var exit = ParseExitTransitionDefinition();
                exitTransitions.Add(exit);
                span.Add(exit);
            } else {
                var transition = ParseTransitionDefinition();
                transitions.Add(transition);
                span.Add(transition);
            }
        }

        return new TransitionDefinitionBlockSyntax(span.ToExtent(), transitions, exitTransitions);
    }

    bool StartsTransition() {
        return At(SyntaxTokenType.InitKeyword) || At(SyntaxTokenType.Identifier);
    }

    TransitionDefinitionSyntax ParseTransitionDefinition() {

        var source    = ParseSourceNode();
        var edge      = StartsEdge()       ? ParseEdge()            : null;
        var target    = StartsTargetNode() ? ParseTargetNode()      : null;
        var trigger   = StartsTrigger()    ? ParseTrigger()         : null;
        var condition = StartsCondition()  ? ParseConditionClause() : null;
        var doClause  = At(SyntaxTokenType.DoKeyword) ? ParseDoClause() : null;

        var semi = Eat(SyntaxTokenType.Semicolon);

        var node = new TransitionDefinitionSyntax(Span(source, edge, target, trigger, condition, doClause, semi),
                                                  source, edge, target, trigger, condition, doClause);

        Tok(node, semi, TextClassification.Punctuation);

        return node;
    }

    ExitTransitionDefinitionSyntax ParseExitTransitionDefinition() {

        var source    = ParseIdentifierSourceNode();
        var colon     = Eat(SyntaxTokenType.Colon);
        var name      = Eat(SyntaxTokenType.Identifier);
        var edge      = StartsEdge()       ? ParseEdge()            : null;
        var target    = StartsTargetNode() ? ParseTargetNode()      : null;
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

    SourceNodeSyntax ParseSourceNode() {
        return At(SyntaxTokenType.InitKeyword) ? ParseInitSourceNode() : ParseIdentifierSourceNode();
    }

    InitSourceNodeSyntax ParseInitSourceNode() {
        var keyword = Eat(SyntaxTokenType.InitKeyword);
        var node    = new InitSourceNodeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    IdentifierSourceNodeSyntax ParseIdentifierSourceNode() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierSourceNodeSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

    TargetNodeSyntax ParseTargetNode() {
        return At(SyntaxTokenType.EndKeyword) ? ParseEndTargetNode() : ParseIdentifierTargetNode();
    }

    EndTargetNodeSyntax ParseEndTargetNode() {
        var keyword = Eat(SyntaxTokenType.EndKeyword);
        var node    = new EndTargetNodeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

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

    EdgeSyntax ParseEdge() {
        switch (At0) {
            case SyntaxTokenType.GoToEdgeKeyword:    return ParseGoToEdge();
            case SyntaxTokenType.ModalEdgeKeyword:   return ParseModalEdge();
            default:                                 return ParseNonModalEdge();
        }
    }

    GoToEdgeSyntax ParseGoToEdge() {
        var keyword = Eat(SyntaxTokenType.GoToEdgeKeyword);
        var node    = new GoToEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

    ModalEdgeSyntax ParseModalEdge() {
        var keyword = Eat(SyntaxTokenType.ModalEdgeKeyword);
        var node    = new ModalEdgeSyntax(Span(keyword));
        Tok(node, keyword, TextClassification.Keyword);
        return node;
    }

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

    TriggerSyntax ParseTrigger() {
        return At(SyntaxTokenType.OnKeyword) ? ParseSignalTrigger() : ParseSpontaneousTrigger();
    }

    SignalTriggerSyntax ParseSignalTrigger() {

        var keyword    = Eat(SyntaxTokenType.OnKeyword);
        var identifier = At(SyntaxTokenType.Identifier) ? ParseIdentifier() : null;

        var node = new SignalTriggerSyntax(Span(keyword, identifier), identifier);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    SpontaneousTriggerSyntax ParseSpontaneousTrigger() {

        var spont       = At(SyntaxTokenType.SpontKeyword)       ? Eat(SyntaxTokenType.SpontKeyword)       : (RawToken?) null;
        var spontaneous = At(SyntaxTokenType.SpontaneousKeyword) ? Eat(SyntaxTokenType.SpontaneousKeyword) : (RawToken?) null;

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

    ConditionClauseSyntax ParseConditionClause() {
        if (At(SyntaxTokenType.IfKeyword)) {
            return ParseIfConditionClause();
        }

        return PeekType(1) == SyntaxTokenType.IfKeyword ? ParseElseIfConditionClause() : ParseElseConditionClause();
    }

    IfConditionClauseSyntax ParseIfConditionClause() {

        var keyword            = Eat(SyntaxTokenType.IfKeyword);
        var identifierOrString = ParseIdentifierOrString();

        var node = new IfConditionClauseSyntax(Span(keyword, identifierOrString), identifierOrString);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    ElseConditionClauseSyntax ParseElseConditionClause() {

        var keyword = Eat(SyntaxTokenType.ElseKeyword);

        var node = new ElseConditionClauseSyntax(Span(keyword));

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    ElseIfConditionClauseSyntax ParseElseIfConditionClause() {

        var elseCondition = ParseElseConditionClause();
        var ifCondition   = ParseIfConditionClause();

        return new ElseIfConditionClauseSyntax(Span(elseCondition, ifCondition), elseCondition, ifCondition);
    }

    DoClauseSyntax ParseDoClause() {

        var keyword            = Eat(SyntaxTokenType.DoKeyword);
        var identifierOrString = ParseIdentifierOrString();

        var node = new DoClauseSyntax(Span(keyword, identifierOrString), identifierOrString);

        Tok(node, keyword, TextClassification.ControlKeyword);

        return node;
    }

    #endregion

    #region Code Declarations

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

    CodeDeclarationSyntax ParseCodeDeclaration() {

        var open    = Eat(SyntaxTokenType.OpenBracket);
        var keyword = Eat(SyntaxTokenType.CodeKeyword);

        var literals = new List<RawToken>();
        while (At(SyntaxTokenType.StringLiteral)) {
            literals.Add(Eat(SyntaxTokenType.StringLiteral).Value);
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

    ParameterListSyntax ParseParameterList() {

        var parameters = new List<ParameterSyntax> { ParseParameter() };
        var commas     = new List<RawToken>();

        while (At(SyntaxTokenType.Comma)) {
            commas.Add(Eat(SyntaxTokenType.Comma).Value);
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

    ParameterSyntax ParseParameter() {

        var type = ParseCodeType();
        var name = At(SyntaxTokenType.Identifier) ? Eat(SyntaxTokenType.Identifier) : (RawToken?) null;

        var node = new ParameterSyntax(Span(type, name), type);

        Tok(node, name, TextClassification.ParameterName);

        return node;
    }

    #endregion

    #region CodeType

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

    SimpleTypeSyntax ParseSimpleType() {

        var identifier   = Eat(SyntaxTokenType.Identifier);
        var questionmark = At(SyntaxTokenType.Questionmark) ? Eat(SyntaxTokenType.Questionmark) : (RawToken?) null;

        var node = new SimpleTypeSyntax(Span(identifier, questionmark));

        Tok(node, identifier,   TextClassification.TypeName);
        Tok(node, questionmark, TextClassification.Punctuation);

        return node;
    }

    GenericTypeSyntax ParseGenericType() {

        var identifier = Eat(SyntaxTokenType.Identifier);
        var lessThan   = Eat(SyntaxTokenType.LessThan);

        var arguments = new List<CodeTypeSyntax> { ParseCodeType() };
        var commas    = new List<RawToken>();

        while (At(SyntaxTokenType.Comma)) {
            commas.Add(Eat(SyntaxTokenType.Comma).Value);
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

    IdentifierOrStringSyntax ParseIdentifierOrString() {
        if (At(SyntaxTokenType.Identifier)) {
            return ParseIdentifier();
        }

        if (At(SyntaxTokenType.StringLiteral)) {
            return ParseStringLiteral();
        }

        return null;
    }

    IdentifierSyntax ParseIdentifier() {
        var identifier = Eat(SyntaxTokenType.Identifier);
        var node       = new IdentifierSyntax(Span(identifier));
        Tok(node, identifier, TextClassification.Identifier);
        return node;
    }

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

    bool AtCodeDeclaration(SyntaxTokenType keyword) {
        return At(SyntaxTokenType.OpenBracket) && PeekType(1) == keyword;
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
    /// sichtbare Token vor. Sonst <c>null</c> (Recovery folgt in einem eigenen Schritt).
    /// </summary>
    RawToken? Eat(SyntaxTokenType type) {
        if (At0 != type) {
            return null;
        }

        var token = _raw[_pos];
        _firstSignificantStart ??= token.Start;

        _pos++;
        SkipHidden();

        return token;
    }

    void SkipHidden() {
        while (_pos < _raw.Length && IsHidden(_raw[_pos].Type)) {
            _pos++;
        }
    }

    /// <summary>
    /// Hängt — nach dem Aufbau des Wurzelknotens — alle nicht vom Parser konsumierten Token an die Wurzel:
    /// Trivia (Whitespace/Zeilenende/Kommentare), unbekannte Zeichen, Präprozessor-Token und das
    /// abschließende <see cref="SyntaxTokenType.EndOfFile"/>. Reproduziert die Token-Zuordnung der
    /// bisherigen Pipeline. Dabei werden auch die rein lexikalischen Diagnosen gemeldet (unerwartetes
    /// Zeichen, nicht unterstützte Präprozessor-Direktive) — derselbe Mechanismus wie im bisherigen
    /// <c>PostprocessTokens</c>.
    /// </summary>
    void AttachNonSignificantTokens(SyntaxNode root) {
        foreach (var raw in _raw) {
            if (!TryClassifyNonSignificant(raw.Type, out var classification)) {
                continue;
            }

            _tokens.Add(SyntaxTokenFactory.CreateToken(raw.Extent, raw.Type, classification, root));

            ReportLexicalDiagnostics(raw);
        }
    }

    /// <summary>
    /// Meldet die nur vom Lexer ableitbaren Diagnosen für ein nicht-signifikantes Token: ein
    /// <see cref="SyntaxTokenType.Unknown"/> als unerwartetes Zeichen (<c>Nav0000</c>); ein
    /// <see cref="SyntaxTokenType.HashToken"/> bzw. <see cref="SyntaxTokenType.PreprocessorKeyword"/>
    /// als nicht unterstützte Präprozessor-Direktive (<c>Nav3000</c>), beim <c>#</c> zusätzlich
    /// <c>Nav3001</c>, falls davor in der Zeile nicht nur Whitespace steht. Reihenfolge und Location
    /// (nullbreite Start-Position des Tokens) entsprechen dem bisherigen <c>PostprocessTokens</c>.
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
            case SyntaxTokenType.PreprocessorKeyword:
                _diagnostics.Add(new Diagnostic(LexicalLocation(raw.Extent),
                                                DiagnosticDescriptors.Syntax.Nav3000InvalidPreprocessorDirective));
                break;
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

        var token = SyntaxTokenFactory.CreateToken(raw.Value.Extent, raw.Value.Type, classification, parent);
        if (!token.IsMissing) {
            _tokens.Add(token);
        }
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
