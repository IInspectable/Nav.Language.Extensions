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
/// <summary>
/// Snippet-Parser je Grammatikregel: jeder <c>Parse*</c>-Einstieg parst einen Quelltext-Ausschnitt,
/// der genau einer Regel entspricht (<see cref="NavParser.ParseRule"/>), und liefert den typisierten
/// Wurzelknoten. Die per-Regel-Einstiege sind die test-seitige Schnittstelle; Produktionscode parst
/// ganze Dateien über <see cref="ParseCodeGenerationUnit"/> bzw. <see cref="SyntaxTree.ParseText"/>.
/// Alle Einstiege teilen die Signatur: der zu parsende Text, ein optionaler Dateipfad (für
/// <see cref="Location"/>-Angaben) und ein <see cref="CancellationToken"/>.
/// </summary>
public static class Syntax {

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.DoClause"/> und liefert die <see cref="DoClauseSyntax"/>-Wurzel.</summary>
    public static DoClauseSyntax ParseDoClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (DoClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.DoClause, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.GoToEdge"/> und liefert die <see cref="GoToEdgeSyntax"/>-Wurzel.</summary>
    public static GoToEdgeSyntax ParseGoToEdge(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (GoToEdgeSyntax)NavParser.ParseRule(text, NavParser.Rule.GoToEdge, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ArrayType"/> und liefert die <see cref="ArrayTypeSyntax"/>-Wurzel.</summary>
    public static ArrayTypeSyntax ParseArrayType(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ArrayTypeSyntax)NavParser.ParseRule(text, NavParser.Rule.ArrayType, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ModalEdge"/> und liefert die <see cref="ModalEdgeSyntax"/>-Wurzel.</summary>
    public static ModalEdgeSyntax ParseModalEdge(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ModalEdgeSyntax)NavParser.ParseRule(text, NavParser.Rule.ModalEdge, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.Parameter"/> und liefert die <see cref="ParameterSyntax"/>-Wurzel.</summary>
    public static ParameterSyntax ParseParameter(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ParameterSyntax)NavParser.ParseRule(text, NavParser.Rule.Parameter, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.Identifier"/> und liefert die <see cref="IdentifierSyntax"/>-Wurzel.</summary>
    public static IdentifierSyntax ParseIdentifier(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IdentifierSyntax)NavParser.ParseRule(text, NavParser.Rule.Identifier, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.SimpleType"/> und liefert die <see cref="SimpleTypeSyntax"/>-Wurzel.</summary>
    public static SimpleTypeSyntax ParseSimpleType(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (SimpleTypeSyntax)NavParser.ParseRule(text, NavParser.Rule.SimpleType, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.GenericType"/> und liefert die <see cref="GenericTypeSyntax"/>-Wurzel.</summary>
    public static GenericTypeSyntax ParseGenericType(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (GenericTypeSyntax)NavParser.ParseRule(text, NavParser.Rule.GenericType, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.NonModalEdge"/> und liefert die <see cref="NonModalEdgeSyntax"/>-Wurzel.</summary>
    public static NonModalEdgeSyntax ParseNonModalEdge(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (NonModalEdgeSyntax)NavParser.ParseRule(text, NavParser.Rule.NonModalEdge, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.EndTargetNode"/> und liefert die <see cref="EndTargetNodeSyntax"/>-Wurzel.</summary>
    public static EndTargetNodeSyntax ParseEndTargetNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (EndTargetNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.EndTargetNode, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CancelTargetNode"/> und liefert die <see cref="CancelTargetNodeSyntax"/>-Wurzel.</summary>
    public static CancelTargetNodeSyntax ParseCancelTargetNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CancelTargetNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.CancelTargetNode, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ParameterList"/> und liefert die <see cref="ParameterListSyntax"/>-Wurzel.</summary>
    public static ParameterListSyntax ParseParameterList(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ParameterListSyntax)NavParser.ParseRule(text, NavParser.Rule.ParameterList, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.SignalTrigger"/> und liefert die <see cref="SignalTriggerSyntax"/>-Wurzel.</summary>
    public static SignalTriggerSyntax ParseSignalTrigger(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (SignalTriggerSyntax)NavParser.ParseRule(text, NavParser.Rule.SignalTrigger, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.StringLiteral"/> und liefert die <see cref="StringLiteralSyntax"/>-Wurzel.</summary>
    public static StringLiteralSyntax ParseStringLiteral(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (StringLiteralSyntax)NavParser.ParseRule(text, NavParser.Rule.StringLiteral, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.InitSourceNode"/> und liefert die <see cref="InitSourceNodeSyntax"/>-Wurzel.</summary>
    public static InitSourceNodeSyntax ParseInitSourceNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (InitSourceNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.InitSourceNode, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.TaskDefinition"/> und liefert die <see cref="TaskDefinitionSyntax"/>-Wurzel.</summary>
    public static TaskDefinitionSyntax ParseTaskDefinition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TaskDefinitionSyntax)NavParser.ParseRule(text, NavParser.Rule.TaskDefinition, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeDeclaration"/> und liefert die <see cref="CodeDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeDeclarationSyntax ParseCodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.TaskDeclaration"/> und liefert die <see cref="TaskDeclarationSyntax"/>-Wurzel.</summary>
    public static TaskDeclarationSyntax ParseTaskDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TaskDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.TaskDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.IncludeDirective"/> und liefert die <see cref="IncludeDirectiveSyntax"/>-Wurzel.</summary>
    public static IncludeDirectiveSyntax ParseIncludeDirective(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IncludeDirectiveSyntax)NavParser.ParseRule(text, NavParser.Rule.IncludeDirective, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.IfConditionClause"/> und liefert die <see cref="IfConditionClauseSyntax"/>-Wurzel.</summary>
    public static IfConditionClauseSyntax ParseIfConditionClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IfConditionClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.IfConditionClause, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ArrayRankSpecifier"/> und liefert die <see cref="ArrayRankSpecifierSyntax"/>-Wurzel.</summary>
    public static ArrayRankSpecifierSyntax ParseArrayRankSpecifier(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ArrayRankSpecifierSyntax)NavParser.ParseRule(text, NavParser.Rule.ArrayRankSpecifier, filePath, cancellationToken).Root;
    }

    /// <summary>Parst einen kompletten Nav-Quelltext (Whole-File-Parsing, siehe <see cref="SyntaxTree.ParseText"/>) und liefert die <see cref="CodeGenerationUnitSyntax"/>-Wurzel.</summary>
    public static CodeGenerationUnitSyntax ParseCodeGenerationUnit(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        // Produktions-Einstieg fürs Whole-File-Parsing: läuft über den handgeschriebenen NavParser
        // (SyntaxTree.ParseText).
        return (CodeGenerationUnitSyntax)SyntaxTree.ParseText(text, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.EndNodeDeclaration"/> und liefert die <see cref="EndNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static EndNodeDeclarationSyntax ParseEndNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (EndNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.EndNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.SpontaneousTrigger"/> und liefert die <see cref="SpontaneousTriggerSyntax"/>-Wurzel.</summary>
    public static SpontaneousTriggerSyntax ParseSpontaneousTrigger(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (SpontaneousTriggerSyntax)NavParser.ParseRule(text, NavParser.Rule.SpontaneousTrigger, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeBaseDeclaration"/> und liefert die <see cref="CodeBaseDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeBaseDeclarationSyntax ParseCodeBaseDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeBaseDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeBaseDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ElseConditionClause"/> und liefert die <see cref="ElseConditionClauseSyntax"/>-Wurzel.</summary>
    public static ElseConditionClauseSyntax ParseElseConditionClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ElseConditionClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.ElseConditionClause, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ExitNodeDeclaration"/> und liefert die <see cref="ExitNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static ExitNodeDeclarationSyntax ParseExitNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ExitNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.ExitNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.InitNodeDeclaration"/> und liefert die <see cref="InitNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static InitNodeDeclarationSyntax ParseInitNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (InitNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.InitNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.TaskNodeDeclaration"/> und liefert die <see cref="TaskNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static TaskNodeDeclarationSyntax ParseTaskNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TaskNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.TaskNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ViewNodeDeclaration"/> und liefert die <see cref="ViewNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static ViewNodeDeclarationSyntax ParseViewNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ViewNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.ViewNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeUsingDeclaration"/> und liefert die <see cref="CodeUsingDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeUsingDeclarationSyntax ParseCodeUsingDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeUsingDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeUsingDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.IdentifierSourceNode"/> und liefert die <see cref="IdentifierSourceNodeSyntax"/>-Wurzel.</summary>
    public static IdentifierSourceNodeSyntax ParseIdentifierSourceNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IdentifierSourceNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.IdentifierSourceNode, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.IdentifierTargetNode"/> und liefert die <see cref="IdentifierTargetNodeSyntax"/>-Wurzel.</summary>
    public static IdentifierTargetNodeSyntax ParseIdentifierTargetNode(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (IdentifierTargetNodeSyntax)NavParser.ParseRule(text, NavParser.Rule.IdentifierTargetNode, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.NodeDeclarationBlock"/> und liefert die <see cref="NodeDeclarationBlockSyntax"/>-Wurzel.</summary>
    public static NodeDeclarationBlockSyntax ParseNodeDeclarationBlock(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (NodeDeclarationBlockSyntax)NavParser.ParseRule(text, NavParser.Rule.NodeDeclarationBlock, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.TransitionDefinition"/> und liefert die <see cref="TransitionDefinitionSyntax"/>-Wurzel.</summary>
    public static TransitionDefinitionSyntax ParseTransitionDefinition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TransitionDefinitionSyntax)NavParser.ParseRule(text, NavParser.Rule.TransitionDefinition, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ChoiceNodeDeclaration"/> und liefert die <see cref="ChoiceNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static ChoiceNodeDeclarationSyntax ParseChoiceNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ChoiceNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.ChoiceNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeParamsDeclaration"/> und liefert die <see cref="CodeParamsDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeParamsDeclarationSyntax ParseCodeParamsDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeParamsDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeParamsDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeResultDeclaration"/> und liefert die <see cref="CodeResultDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeResultDeclarationSyntax ParseCodeResultDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeResultDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeResultDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ElseIfConditionClause"/> und liefert die <see cref="ElseIfConditionClauseSyntax"/>-Wurzel.</summary>
    public static ElseIfConditionClauseSyntax ParseElseIfConditionClause(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ElseIfConditionClauseSyntax)NavParser.ParseRule(text, NavParser.Rule.ElseIfConditionClause, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.DialogNodeDeclaration"/> und liefert die <see cref="DialogNodeDeclarationSyntax"/>-Wurzel.</summary>
    public static DialogNodeDeclarationSyntax ParseDialogNodeDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (DialogNodeDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.DialogNodeDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeNamespaceDeclaration"/> und liefert die <see cref="CodeNamespaceDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeNamespaceDeclarationSyntax ParseCodeNamespaceDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeNamespaceDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeNamespaceDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ExitTransitionDefinition"/> und liefert die <see cref="ExitTransitionDefinitionSyntax"/>-Wurzel.</summary>
    public static ExitTransitionDefinitionSyntax ParseExitTransitionDefinition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ExitTransitionDefinitionSyntax)NavParser.ParseRule(text, NavParser.Rule.ExitTransitionDefinition, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeGenerateToDeclaration"/> und liefert die <see cref="CodeGenerateToDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeGenerateToDeclarationSyntax ParseCodeGenerateToDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeGenerateToDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeGenerateToDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.TransitionDefinitionBlock"/> und liefert die <see cref="TransitionDefinitionBlockSyntax"/>-Wurzel.</summary>
    public static TransitionDefinitionBlockSyntax ParseTransitionDefinitionBlock(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (TransitionDefinitionBlockSyntax)NavParser.ParseRule(text, NavParser.Rule.TransitionDefinitionBlock, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeDoNotInjectDeclaration"/> und liefert die <see cref="CodeDoNotInjectDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeDoNotInjectDeclarationSyntax ParseCodeDoNotInjectDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeDoNotInjectDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeDoNotInjectDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeAbstractMethodDeclaration"/> und liefert die <see cref="CodeAbstractMethodDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeAbstractMethodDeclarationSyntax ParseCodeAbstractMethodDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeAbstractMethodDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeAbstractMethodDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.CodeNotImplementedDeclaration"/> und liefert die <see cref="CodeNotImplementedDeclarationSyntax"/>-Wurzel.</summary>
    public static CodeNotImplementedDeclarationSyntax ParseCodeNotImplementedDeclaration(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (CodeNotImplementedDeclarationSyntax)NavParser.ParseRule(text, NavParser.Rule.CodeNotImplementedDeclaration, filePath, cancellationToken).Root;
    }

    /// <summary>Parst ein Snippet der Regel <see cref="NavParser.Rule.ContinuationTransition"/> und liefert die <see cref="ContinuationTransitionSyntax"/>-Wurzel.</summary>
    public static ContinuationTransitionSyntax ParseContinuationTransition(string? text, string? filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
        return (ContinuationTransitionSyntax)NavParser.ParseRule(text, NavParser.Rule.ContinuationTransition, filePath, cancellationToken).Root;
    }

}