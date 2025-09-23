#region Using Directives

using System.Collections.Generic;

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

using Pharmatechnik.Nav.Language.Generated;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal; 

sealed class NavGrammarVisitor: NavGrammarBaseVisitor<SyntaxNode> {

    public NavGrammarVisitor(int expectedTokenCount) {
        Tokens = new List<SyntaxToken>(capacity: expectedTokenCount);
    }

    public List<SyntaxToken> Tokens { get; }

    #region CodeGenerationUnit

    public override SyntaxNode VisitCodeGenerationUnit([NotNull] NavGrammar.CodeGenerationUnitContext context) {

        var node = new CodeGenerationUnitSyntax(
            CreateExtent(context),
            codeNamespaceDeclaration:
            context.codeNamespaceDeclaration()
                   .Optional(VisitCodeNamespaceDeclaration)
                   .OfSyntaxType<CodeNamespaceDeclarationSyntax>(),
            codeUsingDeclarations:
            context.codeUsingDeclaration()
                   .ZeroOrMore(VisitCodeUsingDeclaration)
                   .OfSyntaxType<CodeUsingDeclarationSyntax>()
                   .ToReadOnlyList(expectedCapacity: context.codeUsingDeclaration().Length),
            memberDeclarations:
            context.memberDeclaration()
                   .ZeroOrMore(VisitMemberDeclaration)
                   .OfSyntaxType<MemberDeclarationSyntax>()
                   .ToReadOnlyList(expectedCapacity: context.memberDeclaration().Length)
        );

        CreateToken(node, context.Eof(), TextClassification.Whitespace);

        return node;
    }

    public override SyntaxNode VisitMemberDeclaration(NavGrammar.MemberDeclarationContext context) {
        if (context.includeDirective() != null) {
            return VisitIncludeDirective(context.includeDirective());
        }

        if (context.taskDeclaration() != null) {
            return VisitTaskDeclaration(context.taskDeclaration());
        }

        if (context.taskDefinition() != null) {
            return VisitTaskDefinition(context.taskDefinition());
        }

        return null;
    }

    #endregion

    #region CodeNamespaceDeclaration

    public override SyntaxNode VisitCodeNamespaceDeclaration([NotNull] NavGrammar.CodeNamespaceDeclarationContext context) {

        var node = new CodeNamespaceDeclarationSyntax(
            extent: CreateExtent(context),
            namespaceSyntax:
            context.identifierOrString()
                   .Optional(VisitIdentifierOrString)
                   .OfSyntaxType<IdentifierOrStringSyntax>()
        );

        CreateToken(node, context.OpenBracket(),            TextClassification.Punctuation);
        CreateToken(node, context.NamespaceprefixKeyword(), TextClassification.Keyword);
        CreateToken(node, context.CloseBracket(),           TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeUsingDeclaration

    public override SyntaxNode VisitCodeUsingDeclaration(NavGrammar.CodeUsingDeclarationContext context) {

        var node = new CodeUsingDeclarationSyntax(
            extent: CreateExtent(context),
            namespaceSyntax:
            context.identifierOrString()
                   .Optional(VisitIdentifierOrString)
                   .OfSyntaxType<IdentifierOrStringSyntax>()
        );

        CreateToken(node, context.OpenBracket(),  TextClassification.Punctuation);
        CreateToken(node, context.UsingKeyword(), TextClassification.Keyword);
        CreateToken(node, context.CloseBracket(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region IncludeDirective

    public override SyntaxNode VisitIncludeDirective(NavGrammar.IncludeDirectiveContext context) {

        var node = new IncludeDirectiveSyntax(
            CreateExtent(context));

        CreateToken(node, context.TaskrefKeyword(), TextClassification.Keyword);
        CreateToken(node, context.StringLiteral(),  TextClassification.StringLiteral);
        CreateToken(node, context.Semicolon(),      TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region TaskDeclaration

    public override SyntaxNode VisitTaskDeclaration(NavGrammar.TaskDeclarationContext context) {

        var node = new TaskDeclarationSyntax(
            CreateExtent(context),
            codeNamespaceDeclaration:
            context.codeNamespaceDeclaration()
                   .Optional(VisitCodeNamespaceDeclaration)
                   .OfSyntaxType<CodeNamespaceDeclarationSyntax>(),
            codeNotImplementedDeclaration:
            context.codeNotImplementedDeclaration()
                   .Optional(VisitCodeNotImplementedDeclaration)
                   .OfSyntaxType<CodeNotImplementedDeclarationSyntax>(),
            codeResultDeclaration:
            context.codeResultDeclaration()
                   .Optional(VisitCodeResultDeclaration)
                   .OfSyntaxType<CodeResultDeclarationSyntax>(),
            connectionPoints:
            context.connectionPointNodeDeclaration()
                   .ZeroOrMore(VisitConnectionPointNodeDeclaration)
                   .OfSyntaxType<ConnectionPointNodeSyntax>()
                   .ToReadOnlyList(expectedCapacity: context.connectionPointNodeDeclaration().Length)
        );

        CreateToken(node, context.TaskrefKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),     TextClassification.TaskName);
        CreateToken(node, context.OpenBrace(),      TextClassification.Punctuation);
        CreateToken(node, context.CloseBrace(),     TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitConnectionPointNodeDeclaration(NavGrammar.ConnectionPointNodeDeclarationContext context) {
        if (context.initNodeDeclaration() != null) {
            return VisitInitNodeDeclaration(context.initNodeDeclaration());
        }

        if (context.exitNodeDeclaration() != null) {
            return VisitExitNodeDeclaration(context.exitNodeDeclaration());
        }

        if (context.endNodeDeclaration() != null) {
            return VisitEndNodeDeclaration(context.endNodeDeclaration());
        }

        return null;
    }

    #endregion

    #region TaskDefinition

    public override SyntaxNode VisitTaskDefinition(NavGrammar.TaskDefinitionContext context) {

        var node = new TaskDefinitionSyntax(
            CreateExtent(context),
            codeDeclaration:
            context.codeDeclaration()
                   .Optional(VisitCodeDeclaration)
                   .OfSyntaxType<CodeDeclarationSyntax>(),
            codeBaseDeclaration:
            context.codeBaseDeclaration()
                   .Optional(VisitCodeBaseDeclaration)
                   .OfSyntaxType<CodeBaseDeclarationSyntax>(),
            codeGenerateToDeclaration:
            context.codeGenerateToDeclaration()
                   .Optional(VisitCodeGenerateToDeclaration)
                   .OfSyntaxType<CodeGenerateToDeclarationSyntax>(),
            codeParamsDeclaration:
            context.codeParamsDeclaration()
                   .Optional(VisitCodeParamsDeclaration)
                   .OfSyntaxType<CodeParamsDeclarationSyntax>(),
            codeResultDeclaration:
            context.codeResultDeclaration()
                   .Optional(VisitCodeResultDeclaration)
                   .OfSyntaxType<CodeResultDeclarationSyntax>(),
            nodeDeclarationBlock:
            context.nodeDeclarationBlock()
                   .Optional(VisitNodeDeclarationBlock)
                   .OfSyntaxType<NodeDeclarationBlockSyntax>(),
            transitionDefinitionBlock:
            context.transitionDefinitionBlock()
                   .Optional(VisitTransitionDefinitionBlock)
                   .OfSyntaxType<TransitionDefinitionBlockSyntax>()
        );

        CreateToken(node, context.TaskKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),  TextClassification.TaskName);
        CreateToken(node, context.OpenBrace(),   TextClassification.Punctuation);
        CreateToken(node, context.CloseBrace(),  TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region Node Declarations

    public override SyntaxNode VisitNodeDeclarationBlock(NavGrammar.NodeDeclarationBlockContext context) {

        var node = new NodeDeclarationBlockSyntax(
            CreateExtent(context),
            nodeDeclarations:
            context.nodeDeclaration()
                   .ZeroOrMore(VisitNodeDeclaration)
                   .OfSyntaxType<NodeDeclarationSyntax>()
                   .ToReadOnlyList(expectedCapacity: context.nodeDeclaration().Length)
        );

        return node;
    }

    public override SyntaxNode VisitNodeDeclaration(NavGrammar.NodeDeclarationContext context) {
        if (context.connectionPointNodeDeclaration() != null) {
            return VisitConnectionPointNodeDeclaration(context.connectionPointNodeDeclaration());
        }

        if (context.taskNodeDeclaration() != null) {
            return VisitTaskNodeDeclaration(context.taskNodeDeclaration());
        }

        if (context.choiceNodeDeclaration() != null) {
            return VisitChoiceNodeDeclaration(context.choiceNodeDeclaration());
        }

        if (context.dialogNodeDeclaration() != null) {
            return VisitDialogNodeDeclaration(context.dialogNodeDeclaration());
        }

        if (context.viewNodeDeclaration() != null) {
            return VisitViewNodeDeclaration(context.viewNodeDeclaration());
        }

        return null;
    }

    public override SyntaxNode VisitInitNodeDeclaration(NavGrammar.InitNodeDeclarationContext context) {

        var node = new InitNodeDeclarationSyntax(CreateExtent(context),
                                                 codeAbstractMethodDeclaration:
                                                 context.codeAbstractMethodDeclaration()
                                                        .Optional(VisitCodeAbstractMethodDeclaration)
                                                        .OfSyntaxType<CodeAbstractMethodDeclarationSyntax>(),
                                                 codeParamsDeclaration:
                                                 context.codeParamsDeclaration()
                                                        .Optional(VisitCodeParamsDeclaration)
                                                        .OfSyntaxType<CodeParamsDeclarationSyntax>(),
                                                 doClause:
                                                 context.doClause()
                                                        .Optional(VisitDoClause)
                                                        .OfSyntaxType<DoClauseSyntax>()
        );

        CreateToken(node, context.InitKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),  TextClassification.Identifier); // Name der Initfunktion
        CreateToken(node, context.Semicolon(),   TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitExitNodeDeclaration(NavGrammar.ExitNodeDeclarationContext context) {

        var node = new ExitNodeDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.ExitKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),  TextClassification.Identifier);
        CreateToken(node, context.Semicolon(),   TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitEndNodeDeclaration(NavGrammar.EndNodeDeclarationContext context) {

        var node = new EndNodeDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.EndKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Semicolon(),  TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitTaskNodeDeclaration(NavGrammar.TaskNodeDeclarationContext context) {

        var node = new TaskNodeDeclarationSyntax(CreateExtent(context),
                                                 codeDoNotInjectDeclaration:
                                                 context.codeDoNotInjectDeclaration()
                                                        .Optional(VisitCodeDoNotInjectDeclaration)
                                                        .OfSyntaxType<CodeDoNotInjectDeclarationSyntax>(),
                                                 codeAbstractMethodDeclaration:
                                                 context.codeAbstractMethodDeclaration()
                                                        .Optional(VisitCodeAbstractMethodDeclaration)
                                                        .OfSyntaxType<CodeAbstractMethodDeclarationSyntax>()
        );

        CreateToken(node, context.TaskKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(0), TextClassification.TaskName);
        CreateToken(node, context.Identifier(1), TextClassification.Identifier);
        CreateToken(node, context.Semicolon(),   TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitChoiceNodeDeclaration(NavGrammar.ChoiceNodeDeclarationContext context) {

        var node = new ChoiceNodeDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.ChoiceKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),    TextClassification.Identifier);
        CreateToken(node, context.Semicolon(),     TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitDialogNodeDeclaration(NavGrammar.DialogNodeDeclarationContext context) {

        var node = new DialogNodeDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.DialogKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),    TextClassification.GuiNode);
        CreateToken(node, context.Semicolon(),     TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitViewNodeDeclaration(NavGrammar.ViewNodeDeclarationContext context) {

        var node = new ViewNodeDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.ViewKeyword(), TextClassification.Keyword);
        CreateToken(node, context.Identifier(),  TextClassification.GuiNode);
        CreateToken(node, context.Semicolon(),   TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region TransitionDefinitionBlock

    public override SyntaxNode VisitTransitionDefinitionBlock(NavGrammar.TransitionDefinitionBlockContext context) {
        var node = new TransitionDefinitionBlockSyntax(
            extent:
            CreateExtent(context),
            transitionDefinitions:
            context.transitionDefinition()
                   .ZeroOrMore(VisitTransitionDefinition)
                   .OfSyntaxType<TransitionDefinitionSyntax>()
                   .ToReadOnlyList(expectedCapacity: context.transitionDefinition().Length),
            exitTransitionDefinitions:
            context.exitTransitionDefinition()
                   .ZeroOrMore(VisitExitTransitionDefinition)
                   .OfSyntaxType<ExitTransitionDefinitionSyntax>()
                   .ToReadOnlyList(expectedCapacity: context.exitTransitionDefinition().Length)
        );

        return node;
    }

    public override SyntaxNode VisitTransitionDefinition(NavGrammar.TransitionDefinitionContext context) {

        var node = new TransitionDefinitionSyntax(
            extent:
            CreateExtent(context),
            sourceNode:
            context.sourceNode()
                   .Optional(VisitSourceNode)
                   .OfSyntaxType<SourceNodeSyntax>(),
            edgeSyntax:
            context.edge()
                   .Optional(VisitEdge)
                   .OfSyntaxType<EdgeSyntax>(),
            targetNode:
            context.targetNode()
                   .Optional(VisitTargetNode)
                   .OfSyntaxType<TargetNodeSyntax>(),
            trigger:
            context.trigger()
                   .Optional(VisitTrigger)
                   .OfSyntaxType<TriggerSyntax>(),
            conditionClause:
            context.conditionClause()
                   .Optional(VisitConditionClause)
                   .OfSyntaxType<ConditionClauseSyntax>(),
            doClause:
            context.doClause()
                   .Optional(VisitDoClause)
                   .OfSyntaxType<DoClauseSyntax>()
        );

        CreateToken(node, context.Semicolon(), TextClassification.Punctuation);

        return node;
    }

    public override SyntaxNode VisitSourceNode(NavGrammar.SourceNodeContext context) {

        if (context.initSourceNode() != null) {
            return VisitInitSourceNode(context.initSourceNode());
        }

        if (context.identifierSourceNode() != null) {
            return VisitIdentifierSourceNode(context.identifierSourceNode());
        }

        return null;
    }

    public override SyntaxNode VisitInitSourceNode(NavGrammar.InitSourceNodeContext context) {
        var node = new InitSourceNodeSyntax(CreateExtent(context));
        CreateToken(node, context.InitKeyword(), TextClassification.Keyword);
        return node;
    }

    public override SyntaxNode VisitIdentifierSourceNode(NavGrammar.IdentifierSourceNodeContext context) {
        var node = new IdentifierSourceNodeSyntax(CreateExtent(context));
        CreateToken(node, context.Identifier(), TextClassification.Identifier);
        return node;
    }

    public override SyntaxNode VisitTargetNode(NavGrammar.TargetNodeContext context) {

        if (context.endTargetNode() != null) {
            return VisitEndTargetNode(context.endTargetNode());
        }

        if (context.identifierTargetNode() != null) {
            return VisitIdentifierTargetNode(context.identifierTargetNode());
        }

        return null;
    }

    public override SyntaxNode VisitEndTargetNode(NavGrammar.EndTargetNodeContext context) {
        var node = new EndTargetNodeSyntax(extent: CreateExtent(context));
        CreateToken(node, context.EndKeyword(), TextClassification.Keyword);
        return node;
    }

    public override SyntaxNode VisitIdentifierTargetNode(NavGrammar.IdentifierTargetNodeContext context) {
        var node = new IdentifierTargetNodeSyntax(
            extent:
            CreateExtent(context)
        );

        CreateToken(node, context.Identifier(), TextClassification.Identifier);

        return node;
    }

    public override SyntaxNode VisitTrigger(NavGrammar.TriggerContext context) {
        if (context.signalTrigger() != null) {
            return VisitSignalTrigger(context.signalTrigger());
        }

        if (context.spontaneousTrigger() != null) {
            return VisitSpontaneousTrigger(context.spontaneousTrigger());
        }

        return null;
    }

    public override SyntaxNode VisitSpontaneousTrigger(NavGrammar.SpontaneousTriggerContext context) {
        var node = new SpontaneousTriggerSyntax(
            extent:
            CreateExtent(context)
        );

        CreateToken(node, context.SpontKeyword(),       TextClassification.Keyword);
        CreateToken(node, context.SpontaneousKeyword(), TextClassification.Keyword);

        return node;
    }

    public override SyntaxNode VisitSignalTrigger(NavGrammar.SignalTriggerContext context) {

        var node = new SignalTriggerSyntax(
            extent:
            CreateExtent(context),
            identifier:
            context.identifier()
                   .Optional(VisitIdentifier)
                   .OfSyntaxType<IdentifierSyntax>()
        );

        CreateToken(node, context.OnKeyword(), TextClassification.ControlKeyword);

        return node;
    }

    public override SyntaxNode VisitConditionClause([NotNull] NavGrammar.ConditionClauseContext context) {

        if (context.ifConditionClause() != null) {
            return VisitIfConditionClause(context.ifConditionClause());
        }

        if (context.elseIfConditionClause() != null) {
            return VisitElseIfConditionClause(context.elseIfConditionClause());
        }

        if (context.elseConditionClause() != null) {
            return VisitElseConditionClause(context.elseConditionClause());
        }

        return null;
    }

    public override SyntaxNode VisitIfConditionClause(NavGrammar.IfConditionClauseContext context) {
        var node = new IfConditionClauseSyntax(
            extent: CreateExtent(context),
            identifierOrString:
            context.identifierOrString()
                   .Optional(VisitIdentifierOrString)
                   .OfSyntaxType<IdentifierOrStringSyntax>());

        CreateToken(node, context.IfKeyword(), TextClassification.ControlKeyword);

        return node;
    }

    public override SyntaxNode VisitElseConditionClause(NavGrammar.ElseConditionClauseContext context) {
        var node = new ElseConditionClauseSyntax(extent: CreateExtent(context));

        CreateToken(node, context.ElseKeyword(), TextClassification.ControlKeyword);

        return node;
    }

    public override SyntaxNode VisitElseIfConditionClause(NavGrammar.ElseIfConditionClauseContext context) {
        var node = new ElseIfConditionClauseSyntax(
            extent: CreateExtent(context),
            elseCondition:
            context.elseConditionClause()
                   .Optional(VisitElseConditionClause)
                   .OfSyntaxType<ElseConditionClauseSyntax>(),
            ifCondition:
            context.ifConditionClause()
                   .Optional(VisitIfConditionClause)
                   .OfSyntaxType<IfConditionClauseSyntax>());

        return node;
    }

    public override SyntaxNode VisitDoClause(NavGrammar.DoClauseContext context) {

        var node = new DoClauseSyntax(
            extent:
            CreateExtent(context),
            identifierOrString:
            context.identifierOrString()
                   .Optional(VisitIdentifierOrString)
                   .OfSyntaxType<IdentifierOrStringSyntax>()
        );

        CreateToken(node, context.DoKeyword(), TextClassification.ControlKeyword);

        return node;
    }

    public override SyntaxNode VisitExitTransitionDefinition(NavGrammar.ExitTransitionDefinitionContext context) {

        var node = new ExitTransitionDefinitionSyntax(
            extent:
            CreateExtent(context),
            sourceNode:
            context.identifierSourceNode()
                   .Optional(VisitIdentifierSourceNode)
                   .OfSyntaxType<IdentifierSourceNodeSyntax>(),
            edge:
            context.edge()
                   .Optional(VisitEdge)
                   .OfSyntaxType<EdgeSyntax>(),
            targetNode:
            context.targetNode()
                   .Optional(VisitTargetNode)
                   .OfSyntaxType<TargetNodeSyntax>(),
            conditionClause:
            context.conditionClause()
                   .Optional(VisitConditionClause)
                   .OfSyntaxType<ConditionClauseSyntax>(),
            doClause:
            context.doClause()
                   .Optional(VisitDoClause)
                   .OfSyntaxType<DoClauseSyntax>()
        );

        CreateToken(node, context.Colon(),      TextClassification.Punctuation);
        CreateToken(node, context.Identifier(), TextClassification.Identifier); // ExitNode:Exit
        CreateToken(node, context.Semicolon(),  TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region Edge

    public override SyntaxNode VisitEdge(NavGrammar.EdgeContext context) {

        if (context.nonModalEdge() != null) {
            return VisitNonModalEdge(context.nonModalEdge());
        }

        if (context.goToEdge() != null) {
            return VisitGoToEdge(context.goToEdge());
        }

        if (context.modalEdge() != null) {
            return VisitModalEdge(context.modalEdge());
        }

        return null;
    }

    #region Overrides of NavGrammarBaseVisitor<SyntaxNode>

    public override SyntaxNode VisitModalEdge(NavGrammar.ModalEdgeContext context) {

        var node = new ModalEdgeSyntax(CreateExtent(context));

        CreateToken(node, context.ModalEdgeKeyword(), TextClassification.Keyword);

        return node;
    }

    public override SyntaxNode VisitGoToEdge(NavGrammar.GoToEdgeContext context) {

        var node = new GoToEdgeSyntax(CreateExtent(context));

        CreateToken(node, context.GoToEdgeKeyword(), TextClassification.Keyword);

        return node;
    }

    public override SyntaxNode VisitNonModalEdge(NavGrammar.NonModalEdgeContext context) {

        var node = new NonModalEdgeSyntax(CreateExtent(context));

        CreateToken(node, context.NonModalEdgeKeyword(), TextClassification.Keyword);

        return node;
    }

    #endregion

    #endregion

    #region CodeNotImplementedDeclaration

    public override SyntaxNode VisitCodeNotImplementedDeclaration(NavGrammar.CodeNotImplementedDeclarationContext context) {

        var node = new CodeNotImplementedDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.OpenBracket(),           TextClassification.Punctuation);
        CreateToken(node, context.NotimplementedKeyword(), TextClassification.Keyword);
        CreateToken(node, context.CloseBracket(),          TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeDoNotInjectDeclaration

    public override SyntaxNode VisitCodeDoNotInjectDeclaration(NavGrammar.CodeDoNotInjectDeclarationContext context) {

        var node = new CodeDoNotInjectDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.OpenBracket(),        TextClassification.Punctuation);
        CreateToken(node, context.DonotinjectKeyword(), TextClassification.Keyword);
        CreateToken(node, context.CloseBracket(),       TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeAbstractMethodDeclaration

    public override SyntaxNode VisitCodeAbstractMethodDeclaration(NavGrammar.CodeAbstractMethodDeclarationContext context) {

        var node = new CodeAbstractMethodDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.OpenBracket(),           TextClassification.Punctuation);
        CreateToken(node, context.AbstractmethodKeyword(), TextClassification.Keyword);
        CreateToken(node, context.CloseBracket(),          TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeDeclaration

    public override SyntaxNode VisitCodeDeclaration(NavGrammar.CodeDeclarationContext context) {

        var node = new CodeDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.OpenBracket(), TextClassification.Punctuation);
        CreateToken(node, context.CodeKeyword(), TextClassification.Keyword);
        CreateTokens(node, context.StringLiteral(), TextClassification.Text);
        CreateToken(node, context.CloseBracket(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeBaseDeclaration

    public override SyntaxNode VisitCodeBaseDeclaration(NavGrammar.CodeBaseDeclarationContext context) {

        var node = new CodeBaseDeclarationSyntax(CreateExtent(context),
                                                 context.codeType()
                                                        .ZeroOrMore(VisitCodeType)
                                                        .OfSyntaxType<CodeTypeSyntax>()
                                                        .ToReadOnlyList(expectedCapacity: context.codeType().Length)
        );

        CreateToken(node, context.OpenBracket(),  TextClassification.Punctuation);
        CreateToken(node, context.BaseKeyword(),  TextClassification.Keyword);
        CreateToken(node, context.Colon(),        TextClassification.Punctuation);
        CreateToken(node, context.Comma(),        TextClassification.Punctuation);
        CreateToken(node, context.CloseBracket(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeGenerateToDeclaration

    public override SyntaxNode VisitCodeGenerateToDeclaration(NavGrammar.CodeGenerateToDeclarationContext context) {
        var node = new CodeGenerateToDeclarationSyntax(CreateExtent(context));

        CreateToken(node, context.OpenBracket(),       TextClassification.Punctuation);
        CreateToken(node, context.GeneratetoKeyword(), TextClassification.Keyword);
        CreateToken(node, context.StringLiteral(),     TextClassification.StringLiteral);
        CreateToken(node, context.CloseBracket(),      TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeParamsDeclaration

    public override SyntaxNode VisitCodeParamsDeclaration(NavGrammar.CodeParamsDeclarationContext context) {

        var node = new CodeParamsDeclarationSyntax(CreateExtent(context),
                                                   parameterList:
                                                   context.parameterList()
                                                          .Optional(VisitParameterList)
                                                          .OfSyntaxType<ParameterListSyntax>()
        );

        CreateToken(node, context.OpenBracket(),   TextClassification.Punctuation);
        CreateToken(node, context.ParamsKeyword(), TextClassification.Keyword);

        CreateToken(node, context.CloseBracket(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region ParameterList

    public override SyntaxNode VisitParameterList(NavGrammar.ParameterListContext context) {

        var node = new ParameterListSyntax(CreateExtent(context),
                                           parameters:
                                           context.parameter()
                                                  .ZeroOrMore(VisitParameter)
                                                  .OfSyntaxType<ParameterSyntax>()
                                                  .ToReadOnlyList(expectedCapacity: context.parameter().Length)
        );

        CreateTokens(node, context.Comma(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region Parameter

    public override SyntaxNode VisitParameter([NotNull] NavGrammar.ParameterContext context) {
        var node = new ParameterSyntax(
            CreateExtent(context),
            type:
            context.codeType()
                   .Optional(VisitCodeType)
                   .OfSyntaxType<CodeTypeSyntax>()
        );

        CreateToken(node, context.Identifier(), TextClassification.ParameterName);

        return node;
    }

    #endregion

    #region CodeResultDeclaration

    public override SyntaxNode VisitCodeResultDeclaration(NavGrammar.CodeResultDeclarationContext context) {

        var node = new CodeResultDeclarationSyntax(CreateExtent(context),
                                                   result: context.parameter()
                                                                  .Optional(VisitParameter)
                                                                  .OfSyntaxType<ParameterSyntax>());

        CreateToken(node, context.OpenBracket(),   TextClassification.Punctuation);
        CreateToken(node, context.ResultKeyword(), TextClassification.Keyword);
        CreateToken(node, context.CloseBracket(),  TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region CodeType

    public override SyntaxNode VisitCodeType(NavGrammar.CodeTypeContext context) {

        if (context.simpleType() != null) {
            return VisitSimpleType(context.simpleType());
        }

        if (context.genericType() != null) {
            return VisitGenericType(context.genericType());
        }

        if (context.arrayType() != null) {
            return VisitArrayType(context.arrayType());
        }

        return null;
    }

    public override SyntaxNode VisitSimpleType(NavGrammar.SimpleTypeContext context) {

        var node = new SimpleTypeSyntax(CreateExtent(context));

        CreateToken(node, context.Identifier(), TextClassification.TypeName);
        if (context.Questionmark() != null) {
            CreateToken(node, context.Questionmark(), TextClassification.Punctuation);
        }

        return node;
    }

    public override SyntaxNode VisitArrayType(NavGrammar.ArrayTypeContext context) {

        CodeTypeSyntax type = null;
        if (context.simpleType() != null) {
            type = (CodeTypeSyntax) VisitSimpleType(context.simpleType());
        }

        if (context.genericType() != null) {
            type = (CodeTypeSyntax) VisitGenericType(context.genericType());
        }

        var node = new ArrayTypeSyntax(CreateExtent(context), type,
                                       context.arrayRankSpecifier()
                                              .ZeroOrMore(VisitArrayRankSpecifier)
                                              .OfSyntaxType<ArrayRankSpecifierSyntax>()
                                              .ToReadOnlyList(expectedCapacity: context.arrayRankSpecifier().Length));

        return node;
    }

    #region Overrides of NavGrammarBaseVisitor<SyntaxNode>

    public override SyntaxNode VisitArrayRankSpecifier(NavGrammar.ArrayRankSpecifierContext context) {
        var node = new ArrayRankSpecifierSyntax(CreateExtent(context));

        CreateToken(node, context.OpenBracket(),  TextClassification.Punctuation);
        CreateToken(node, context.CloseBracket(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    public override SyntaxNode VisitGenericType(NavGrammar.GenericTypeContext context) {

        var node = new GenericTypeSyntax(CreateExtent(context),
                                         genericArguments:
                                         context.codeType()
                                                .ZeroOrMore(VisitCodeType)
                                                .OfSyntaxType<CodeTypeSyntax>()
                                                .ToReadOnlyList(expectedCapacity: context.codeType().Length)
        );

        CreateToken(node, context.Identifier(), TextClassification.TypeName);
        CreateToken(node, context.LessThan(),   TextClassification.Punctuation);
        CreateTokens(node, context.Comma(), TextClassification.Punctuation);
        CreateToken(node, context.GreaterThan(), TextClassification.Punctuation);

        return node;
    }

    #endregion

    #region IdentifierOrString
   

    public override SyntaxNode VisitIdentifierOrString(NavGrammar.IdentifierOrStringContext context) {
        if (context.identifier() != null) {
            return VisitIdentifier(context.identifier());
        }

        if (context.stringLiteral() != null) {
            return VisitStringLiteral(context.stringLiteral());
        }

        return null;
    }

    public override SyntaxNode VisitIdentifier(NavGrammar.IdentifierContext context) {
        var node = new IdentifierSyntax(CreateExtent(context));
        CreateToken(node, context.Identifier(), TextClassification.Identifier);
        return node;
    }

    public override SyntaxNode VisitStringLiteral(NavGrammar.StringLiteralContext context) {
        var node = new StringLiteralSyntax(CreateExtent(context));
        CreateToken(node, context.StringLiteral(), TextClassification.StringLiteral);
        return node;
    }

    #endregion

    #region Helper

    TextExtent CreateExtent(ParserRuleContext context) {
        return TextExtentFactory.CreateExtent(context);
    }

    void CreateTokens(SyntaxNode parent, IReadOnlyList<ITerminalNode> nodes, TextClassification classification) {
        foreach (var node in nodes) {
            CreateToken(parent, node, classification);
        }
    }

    void CreateToken(SyntaxNode parent, ITerminalNode node, TextClassification classification) {
        if (node == null) {
            return;
        }

        var token = SyntaxTokenFactory.CreateToken(node, classification, parent);
        if (!token.IsMissing) {
            Tokens.Add(token);
        }
    }

    #endregion

}