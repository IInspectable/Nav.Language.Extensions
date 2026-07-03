#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

public class IntroduceChoiceCodeFix: RefactoringCodeFix {

    internal IntroduceChoiceCodeFix(INodeReferenceSymbol nodeReference, CodeFixContext context)
        : base(context) {
        NodeReference = nodeReference ?? throw new ArgumentNullException(nameof(nodeReference));
    }

    public INodeReferenceSymbol   NodeReference  { get; }
    public ITaskDefinitionSymbol? ContainingTask => NodeReference.Declaration?.ContainingTask;

    public override string        Name         => "Introduce Choice";
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => NodeReference.Location.Extent;
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    public string SuggestChoiceName() {
        string baseName   = $"Choice_{NodeReference.Name}";
        string choiceName = baseName;
        int    number     = 1;
        while (!String.IsNullOrEmpty(ValidateChoiceName(choiceName))) {
            choiceName = $"{baseName}{number++}";
        }

        return choiceName;
    }

    internal bool CanApplyFix() {

        return NodeReference.NodeReferenceType    == NodeReferenceType.Target &&
               NodeReference.Declaration          != null                     &&
               NodeReference.Edge.SourceReference != null                     &&
               NodeReference.Edge.EdgeMode        != null;
    }

    public string? ValidateChoiceName(string? choiceName) {
        return ContainingTask.ValidateNewNodeName(choiceName);
    }

    public IList<TextChange> GetTextChanges(string? choiceName) {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        choiceName = choiceName?.Trim() ?? String.Empty;

        var validationMessage = ValidateChoiceName(choiceName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(choiceName));
        }

        var edge       = NodeReference.Edge;
        var edgeMode   = edge.EdgeMode;
        // CanApplyFix (oben geprüft) garantiert NodeReference.Declaration != null.
        var nodeSymbol = NodeReference.Declaration!;

        var nodeDeclarationLine = SyntaxTree.SourceText.GetTextLineAtPosition(nodeSymbol.Start);
        var nodeTransitionLine  = SyntaxTree.SourceText.GetTextLineAtPosition(NodeReference.End);

        var choiceDeclaration = $"{GetIndentAsSpaces(nodeDeclarationLine)}{SyntaxFacts.ChoiceKeyword}{WhiteSpaceBetweenChoiceKeywordAndIdentifier(nodeSymbol)}{choiceName}{SyntaxFacts.Semicolon}";
        var choiceTransition  = $"{GetIndentAsSpaces(nodeTransitionLine)}{choiceName}{WhiteSpaceBetweenSourceAndEdgeMode(edge, choiceName)}{edge.EdgeMode?.Name}{WhiteSpaceBetweenEdgeModeAndTarget(edge)}{NodeReference.Name}{SyntaxFacts.Semicolon}";

        var textChanges = new List<TextChange>();
        // Die Choice Deklaration: choice NeueChoice;
        textChanges.AddRange(GetInsertChanges(nodeDeclarationLine.Extent.End, $"{choiceDeclaration}{Context.TextEditorSettings.NewLine}"));
        // Die Node Reference wird nun umgebogen auf die choice
        textChanges.AddRange(GetRenameSymbolChanges(NodeReference, choiceName));
        // Die Edge der choice ist immer '-->'
        textChanges.AddRange(GetRenameSymbolChanges(edgeMode, SyntaxFacts.GoToEdgeKeyword));
        // Die neue choice Transition 
        textChanges.AddRange(GetInsertChanges(nodeTransitionLine.Extent.End, $"{choiceTransition}{Context.TextEditorSettings.NewLine}"));

        return textChanges;
    }

    string WhiteSpaceBetweenChoiceKeywordAndIdentifier(INodeSymbol sampleNode) {

        var offset = ColumnsBetweenKeywordAndIdentifier(sampleNode, newKeyword: SyntaxFacts.ChoiceKeyword);
        return new String(' ', offset);
    }

}