//==================================================
// HINWEIS: Diese Datei wurde ursprünglich aus Syntax.Generated.tt generiert; seit der Umstellung des
//			Whole-File-Parsings auf den handgeschriebenen NavParser wird sie von Hand gepflegt
//			(die T4-Vorlage wurde stillgelegt, die Datei aus dem Generated-Ordner herausgelöst).
//==================================================
namespace Pharmatechnik.Nav.Language;

using System.Threading;

// Snippet-Parser je Grammatikregel. Produktionscode parst ganze Dateien über ParseCodeGenerationUnit;
// die per-Regel-Einstiege sind die test-seitige Schnittstelle und laufen — wie das Whole-File-Parsing —
// über den handgeschriebenen NavParser (per-Regel über NavParser.ParseRule).
public static class Syntax {

    public static DoClauseSyntax ParseDoClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (DoClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.DoClause, filePath, cancellationToken).Root;
    }

    public static GoToEdgeSyntax ParseGoToEdge(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (GoToEdgeSyntax)NavParser.ParseRule(text, NavParser.Rule.GoToEdge, filePath, cancellationToken).Root;
    }

    public static ArrayTypeSyntax ParseArrayType(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ArrayTypeSyntax)NavParser.ParseRule(text, NavParser.Rule.ArrayType, filePath, cancellationToken).Root;
    }

    public static ModalEdgeSyntax ParseModalEdge(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ModalEdgeSyntax)NavParser.ParseRule(text, NavParser.Rule.ModalEdge, filePath, cancellationToken).Root;
    }

    public static ParameterSyntax ParseParameter(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ParameterSyntax)NavParser.ParseRule(text, NavParser.Rule.Parameter, filePath, cancellationToken).Root;
    }

    public static IdentifierSyntax ParseIdentifier(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IdentifierSyntax)NavParser.ParseRule(text, NavParser.Rule.Identifier, filePath, cancellationToken).Root;
    }

    public static SimpleTypeSyntax ParseSimpleType(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (SimpleTypeSyntax)NavParser.ParseRule(text, NavParser.Rule.SimpleType, filePath, cancellationToken).Root;
    }

    public static GenericTypeSyntax ParseGenericType(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (GenericTypeSyntax)NavParser.ParseRule(text, NavParser.Rule.GenericType, filePath, cancellationToken).Root;
    }

    public static NonModalEdgeSyntax ParseNonModalEdge(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (NonModalEdgeSyntax)NavParser.ParseRule(text, NavParser.Rule.NonModalEdge, filePath, cancellationToken).Root;
    }

    public static EndTargetNodeSyntax ParseEndTargetNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (EndTargetNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.EndTargetNode, filePath, cancellationToken).Root;
    }

    public static ParameterListSyntax ParseParameterList(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ParameterListSyntax)NavParser.ParseRule(text, NavParser.Rule.ParameterList, filePath, cancellationToken).Root;
    }

    public static SignalTriggerSyntax ParseSignalTrigger(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (SignalTriggerSyntax)NavParser.ParseRule(text, NavParser.Rule.SignalTrigger, filePath, cancellationToken).Root;
    }

    public static StringLiteralSyntax ParseStringLiteral(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (StringLiteralSyntax)NavParser.ParseRule(text, NavParser.Rule.StringLiteral, filePath, cancellationToken).Root;
    }

    public static InitSourceNodeSyntax ParseInitSourceNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (InitSourceNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.InitSourceNode, filePath, cancellationToken).Root;
    }

    public static TaskDefinitionSyntax ParseTaskDefinition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TaskDefinitionSyntax)NavParser.ParseRule(text, NavParser.Rule.TaskDefinition, filePath, cancellationToken).Root;
    }

    public static CodeDeclarationSyntax ParseCodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeDeclaration, filePath, cancellationToken).Root;
    }

    public static TaskDeclarationSyntax ParseTaskDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TaskDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.TaskDeclaration, filePath, cancellationToken).Root;
    }

    public static IncludeDirectiveSyntax ParseIncludeDirective(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IncludeDirectiveSyntax)NavParser.ParseRule(text, NavParser.Rule.IncludeDirective, filePath, cancellationToken).Root;
    }

    public static IfConditionClauseSyntax ParseIfConditionClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IfConditionClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.IfConditionClause, filePath, cancellationToken).Root;
    }

    public static ArrayRankSpecifierSyntax ParseArrayRankSpecifier(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ArrayRankSpecifierSyntax)NavParser.ParseRule(text, NavParser.Rule.ArrayRankSpecifier, filePath, cancellationToken).Root;
    }

    public static CodeGenerationUnitSyntax ParseCodeGenerationUnit(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        // Produktions-Einstieg fürs Whole-File-Parsing: läuft über den handgeschriebenen NavParser
        // (SyntaxTree.ParseText).
        return (CodeGenerationUnitSyntax)SyntaxTree.ParseText(text, filePath, cancellationToken).Root;
    }

    public static EndNodeDeclarationSyntax ParseEndNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (EndNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.EndNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static SpontaneousTriggerSyntax ParseSpontaneousTrigger(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (SpontaneousTriggerSyntax)NavParser.ParseRule(text, NavParser.Rule.SpontaneousTrigger, filePath, cancellationToken).Root;
    }

    public static CodeBaseDeclarationSyntax ParseCodeBaseDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeBaseDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeBaseDeclaration, filePath, cancellationToken).Root;
    }

    public static ElseConditionClauseSyntax ParseElseConditionClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ElseConditionClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.ElseConditionClause, filePath, cancellationToken).Root;
    }

    public static ExitNodeDeclarationSyntax ParseExitNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ExitNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.ExitNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static InitNodeDeclarationSyntax ParseInitNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (InitNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.InitNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static TaskNodeDeclarationSyntax ParseTaskNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TaskNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.TaskNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static ViewNodeDeclarationSyntax ParseViewNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ViewNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.ViewNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static CodeUsingDeclarationSyntax ParseCodeUsingDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeUsingDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeUsingDeclaration, filePath, cancellationToken).Root;
    }

    public static IdentifierSourceNodeSyntax ParseIdentifierSourceNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IdentifierSourceNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.IdentifierSourceNode, filePath, cancellationToken).Root;
    }

    public static IdentifierTargetNodeSyntax ParseIdentifierTargetNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IdentifierTargetNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.IdentifierTargetNode, filePath, cancellationToken).Root;
    }

    public static NodeDeclarationBlockSyntax ParseNodeDeclarationBlock(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (NodeDeclarationBlockSyntax)NavParser.ParseRule(text, NavParser.Rule.NodeDeclarationBlock, filePath, cancellationToken).Root;
    }

    public static TransitionDefinitionSyntax ParseTransitionDefinition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TransitionDefinitionSyntax)NavParser.ParseRule(text, NavParser.Rule.TransitionDefinition, filePath, cancellationToken).Root;
    }

    public static ChoiceNodeDeclarationSyntax ParseChoiceNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ChoiceNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.ChoiceNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static CodeParamsDeclarationSyntax ParseCodeParamsDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeParamsDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeParamsDeclaration, filePath, cancellationToken).Root;
    }

    public static CodeResultDeclarationSyntax ParseCodeResultDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeResultDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeResultDeclaration, filePath, cancellationToken).Root;
    }

    public static ElseIfConditionClauseSyntax ParseElseIfConditionClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ElseIfConditionClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.ElseIfConditionClause, filePath, cancellationToken).Root;
    }

    public static DialogNodeDeclarationSyntax ParseDialogNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (DialogNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.DialogNodeDeclaration, filePath, cancellationToken).Root;
    }

    public static CodeNamespaceDeclarationSyntax ParseCodeNamespaceDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeNamespaceDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeNamespaceDeclaration, filePath, cancellationToken).Root;
    }

    public static ExitTransitionDefinitionSyntax ParseExitTransitionDefinition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ExitTransitionDefinitionSyntax)NavParser.ParseRule(text, NavParser.Rule.ExitTransitionDefinition, filePath, cancellationToken).Root;
    }

    public static CodeGenerateToDeclarationSyntax ParseCodeGenerateToDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeGenerateToDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeGenerateToDeclaration, filePath, cancellationToken).Root;
    }

    public static TransitionDefinitionBlockSyntax ParseTransitionDefinitionBlock(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TransitionDefinitionBlockSyntax)NavParser.ParseRule(text, NavParser.Rule.TransitionDefinitionBlock, filePath, cancellationToken).Root;
    }

    public static CodeDoNotInjectDeclarationSyntax ParseCodeDoNotInjectDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeDoNotInjectDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeDoNotInjectDeclaration, filePath, cancellationToken).Root;
    }

    public static CodeAbstractMethodDeclarationSyntax ParseCodeAbstractMethodDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeAbstractMethodDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeAbstractMethodDeclaration, filePath, cancellationToken).Root;
    }

    public static CodeNotImplementedDeclarationSyntax ParseCodeNotImplementedDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeNotImplementedDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeNotImplementedDeclaration, filePath, cancellationToken).Root;
    }

    public static ContinuationTransitionSyntax ParseContinuationTransition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ContinuationTransitionSyntax)NavParser.ParseRule(text, NavParser.Rule.ContinuationTransition, filePath, cancellationToken).Root;
    }

}