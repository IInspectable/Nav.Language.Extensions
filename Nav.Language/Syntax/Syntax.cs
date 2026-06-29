
//==================================================
// HINWEIS: Diese Datei wurde ursprünglich aus Syntax.Generated.tt generiert; seit der Umstellung des
//			Whole-File-Parsings auf den handgeschriebenen NavParser wird sie von Hand gepflegt
//			(die T4-Vorlage wurde stillgelegt, die Datei aus dem Generated-Ordner herausgelöst).
//==================================================
namespace Pharmatechnik.Nav.Language {

    using System.Threading;

	public static class Syntax {
		
		public static DoClauseSyntax ParseDoClause(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (DoClauseSyntax)SyntaxTree.ParseText(text, parser => parser.doClause(), filePath, null, cancellationToken).Root;		
		}

		public static GoToEdgeSyntax ParseGoToEdge(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (GoToEdgeSyntax)SyntaxTree.ParseText(text, parser => parser.goToEdge(), filePath, null, cancellationToken).Root;		
		}

		public static ArrayTypeSyntax ParseArrayType(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ArrayTypeSyntax)SyntaxTree.ParseText(text, parser => parser.arrayType(), filePath, null, cancellationToken).Root;		
		}

		public static ModalEdgeSyntax ParseModalEdge(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ModalEdgeSyntax)SyntaxTree.ParseText(text, parser => parser.modalEdge(), filePath, null, cancellationToken).Root;		
		}

		public static ParameterSyntax ParseParameter(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ParameterSyntax)SyntaxTree.ParseText(text, parser => parser.parameter(), filePath, null, cancellationToken).Root;		
		}

		public static IdentifierSyntax ParseIdentifier(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (IdentifierSyntax)SyntaxTree.ParseText(text, parser => parser.identifier(), filePath, null, cancellationToken).Root;		
		}

		public static SimpleTypeSyntax ParseSimpleType(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (SimpleTypeSyntax)SyntaxTree.ParseText(text, parser => parser.simpleType(), filePath, null, cancellationToken).Root;		
		}

		public static GenericTypeSyntax ParseGenericType(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (GenericTypeSyntax)SyntaxTree.ParseText(text, parser => parser.genericType(), filePath, null, cancellationToken).Root;		
		}

		public static NonModalEdgeSyntax ParseNonModalEdge(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (NonModalEdgeSyntax)SyntaxTree.ParseText(text, parser => parser.nonModalEdge(), filePath, null, cancellationToken).Root;		
		}

		public static EndTargetNodeSyntax ParseEndTargetNode(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (EndTargetNodeSyntax)SyntaxTree.ParseText(text, parser => parser.endTargetNode(), filePath, null, cancellationToken).Root;		
		}

		public static ParameterListSyntax ParseParameterList(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ParameterListSyntax)SyntaxTree.ParseText(text, parser => parser.parameterList(), filePath, null, cancellationToken).Root;		
		}

		public static SignalTriggerSyntax ParseSignalTrigger(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (SignalTriggerSyntax)SyntaxTree.ParseText(text, parser => parser.signalTrigger(), filePath, null, cancellationToken).Root;		
		}

		public static StringLiteralSyntax ParseStringLiteral(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (StringLiteralSyntax)SyntaxTree.ParseText(text, parser => parser.stringLiteral(), filePath, null, cancellationToken).Root;		
		}

		public static InitSourceNodeSyntax ParseInitSourceNode(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (InitSourceNodeSyntax)SyntaxTree.ParseText(text, parser => parser.initSourceNode(), filePath, null, cancellationToken).Root;		
		}

		public static TaskDefinitionSyntax ParseTaskDefinition(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (TaskDefinitionSyntax)SyntaxTree.ParseText(text, parser => parser.taskDefinition(), filePath, null, cancellationToken).Root;		
		}

		public static CodeDeclarationSyntax ParseCodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static TaskDeclarationSyntax ParseTaskDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (TaskDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.taskDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static IncludeDirectiveSyntax ParseIncludeDirective(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (IncludeDirectiveSyntax)SyntaxTree.ParseText(text, parser => parser.includeDirective(), filePath, null, cancellationToken).Root;		
		}

		public static IfConditionClauseSyntax ParseIfConditionClause(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (IfConditionClauseSyntax)SyntaxTree.ParseText(text, parser => parser.ifConditionClause(), filePath, null, cancellationToken).Root;		
		}

		public static ArrayRankSpecifierSyntax ParseArrayRankSpecifier(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ArrayRankSpecifierSyntax)SyntaxTree.ParseText(text, parser => parser.arrayRankSpecifier(), filePath, null, cancellationToken).Root;		
		}

		public static CodeGenerationUnitSyntax ParseCodeGenerationUnit(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			// Produktions-Einstieg fürs Whole-File-Parsing: läuft über den handgeschriebenen NavParser
			// (SyntaxTree.ParseText). Die per-Regel-Einstiege unten parsen weiterhin über ANTLR.
			return (CodeGenerationUnitSyntax)SyntaxTree.ParseText(text, filePath, cancellationToken).Root;
		}

		public static EndNodeDeclarationSyntax ParseEndNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (EndNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.endNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static SpontaneousTriggerSyntax ParseSpontaneousTrigger(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (SpontaneousTriggerSyntax)SyntaxTree.ParseText(text, parser => parser.spontaneousTrigger(), filePath, null, cancellationToken).Root;		
		}

		public static CodeBaseDeclarationSyntax ParseCodeBaseDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeBaseDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeBaseDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static ElseConditionClauseSyntax ParseElseConditionClause(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ElseConditionClauseSyntax)SyntaxTree.ParseText(text, parser => parser.elseConditionClause(), filePath, null, cancellationToken).Root;		
		}

		public static ExitNodeDeclarationSyntax ParseExitNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ExitNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.exitNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static InitNodeDeclarationSyntax ParseInitNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (InitNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.initNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static TaskNodeDeclarationSyntax ParseTaskNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (TaskNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.taskNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static ViewNodeDeclarationSyntax ParseViewNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ViewNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.viewNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static CodeUsingDeclarationSyntax ParseCodeUsingDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeUsingDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeUsingDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static IdentifierSourceNodeSyntax ParseIdentifierSourceNode(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (IdentifierSourceNodeSyntax)SyntaxTree.ParseText(text, parser => parser.identifierSourceNode(), filePath, null, cancellationToken).Root;		
		}

		public static IdentifierTargetNodeSyntax ParseIdentifierTargetNode(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (IdentifierTargetNodeSyntax)SyntaxTree.ParseText(text, parser => parser.identifierTargetNode(), filePath, null, cancellationToken).Root;		
		}

		public static NodeDeclarationBlockSyntax ParseNodeDeclarationBlock(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (NodeDeclarationBlockSyntax)SyntaxTree.ParseText(text, parser => parser.nodeDeclarationBlock(), filePath, null, cancellationToken).Root;		
		}

		public static TransitionDefinitionSyntax ParseTransitionDefinition(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (TransitionDefinitionSyntax)SyntaxTree.ParseText(text, parser => parser.transitionDefinition(), filePath, null, cancellationToken).Root;		
		}

		public static ChoiceNodeDeclarationSyntax ParseChoiceNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ChoiceNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.choiceNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static CodeParamsDeclarationSyntax ParseCodeParamsDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeParamsDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeParamsDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static CodeResultDeclarationSyntax ParseCodeResultDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeResultDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeResultDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static ElseIfConditionClauseSyntax ParseElseIfConditionClause(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ElseIfConditionClauseSyntax)SyntaxTree.ParseText(text, parser => parser.elseIfConditionClause(), filePath, null, cancellationToken).Root;		
		}

		public static DialogNodeDeclarationSyntax ParseDialogNodeDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (DialogNodeDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.dialogNodeDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static CodeNamespaceDeclarationSyntax ParseCodeNamespaceDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeNamespaceDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeNamespaceDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static ExitTransitionDefinitionSyntax ParseExitTransitionDefinition(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (ExitTransitionDefinitionSyntax)SyntaxTree.ParseText(text, parser => parser.exitTransitionDefinition(), filePath, null, cancellationToken).Root;		
		}

		public static CodeGenerateToDeclarationSyntax ParseCodeGenerateToDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeGenerateToDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeGenerateToDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static TransitionDefinitionBlockSyntax ParseTransitionDefinitionBlock(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (TransitionDefinitionBlockSyntax)SyntaxTree.ParseText(text, parser => parser.transitionDefinitionBlock(), filePath, null, cancellationToken).Root;		
		}

		public static CodeDoNotInjectDeclarationSyntax ParseCodeDoNotInjectDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeDoNotInjectDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeDoNotInjectDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static CodeAbstractMethodDeclarationSyntax ParseCodeAbstractMethodDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeAbstractMethodDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeAbstractMethodDeclaration(), filePath, null, cancellationToken).Root;		
		}

		public static CodeNotImplementedDeclarationSyntax ParseCodeNotImplementedDeclaration(string text, string filePath = null, CancellationToken cancellationToken = default(CancellationToken)) {
			return (CodeNotImplementedDeclarationSyntax)SyntaxTree.ParseText(text, parser => parser.codeNotImplementedDeclaration(), filePath, null, cancellationToken).Root;		
		}

	}
}
