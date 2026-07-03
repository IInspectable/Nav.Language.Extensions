#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix; 

public sealed class AddMissingExitTransitionCodeFix: ErrorCodeFix {

    internal AddMissingExitTransitionCodeFix(INodeReferenceSymbol targetNodeRef, IConnectionPointSymbol connectionPoint, CodeFixContext context)
        : base(context) {

        ConnectionPoint = connectionPoint                              ?? throw new ArgumentNullException(nameof(connectionPoint));
        TargetNodeRef   = targetNodeRef                                ?? throw new ArgumentNullException(nameof(targetNodeRef));
        TaskNode        = targetNodeRef.Declaration as ITaskNodeSymbol ?? throw new ArgumentException(nameof(targetNodeRef));

        if (TaskNode.Declaration != ConnectionPoint.TaskDeclaration) {
            throw new ArgumentException();
        }
    }

    public ITaskNodeSymbol        TaskNode        { get; }
    public IConnectionPointSymbol ConnectionPoint { get; }
    public INodeReferenceSymbol   TargetNodeRef   { get; }

    public ITaskDefinitionSymbol ContainingTask => TaskNode.ContainingTask;

    public override string        Name         => "Add Missing Edge";
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => TargetNodeRef.Location.Extent;
    public override CodeFixPrio   Prio         => CodeFixPrio.High;
        

    internal bool CanApplyFix() {

        var templateEdge = GetTemplateEdge();

        // 1. Wir brauchen eine vollständige Kante als "Formatvorlage"
        if (templateEdge.SourceReference == null || templateEdge.EdgeMode == null || templateEdge.TargetReference == null) {
            return false;
        }

        // 2. Es darf noch keine ExitTransition mit dem Verbindungspunkt geben
        return TaskNode.Outgoings
                       .Where(trans => trans.ExitConnectionPointReference != null)
                        // Das vorherige Where garantiert ExitConnectionPointReference != null.
                       .All(o => o.ExitConnectionPointReference!.Declaration != ConnectionPoint);
    }

    public IList<TextChange> GetTextChanges() {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        var textChanges = new List<TextChange>();

        var sourceName   = $"{TaskNode.Name}{SyntaxFacts.Colon}{ConnectionPoint.Name}";
        var edgeKeyword  = SyntaxFacts.GoToEdgeKeyword;
        var targetName   = GetApplicableTargetName();
        var templateEdge = GetTemplateEdge();
        // Die neue Exit Transition
        var exitTransition = ComposeEdge(templateEdge, sourceName, edgeKeyword, targetName);

        // CanApplyFix (oben geprüft) garantiert templateEdge.SourceReference != null.
        var transitionLine = SyntaxTree.SourceText.GetTextLineAtPosition(templateEdge.SourceReference!.Start);
        textChanges.AddRange(GetInsertChanges(transitionLine.Extent.End, $"{exitTransition}{Context.TextEditorSettings.NewLine}"));

        return textChanges;
    }

    public TextExtent TryGetSelectionAfterChanges(CodeGenerationUnit? codegenerationUnit) {

        var taskDef    = codegenerationUnit?.TryFindTaskDefinition(TargetNodeRef.Declaration?.ContainingTask.Name);
        var taskNode   = taskDef.TryFindNode<ITaskNodeSymbol>(TaskNode.Name);
        var exitEdge   = taskNode?.Outgoings.FirstOrDefault(e => e.ExitConnectionPointReference?.Name == ConnectionPoint.Name);
        var targetNode = exitEdge?.TargetReference;

        return targetNode?.Location.Extent ?? TextExtent.Missing;
    }

    IEdge GetTemplateEdge() {
        return TargetNodeRef.Edge;
    }

    string GetApplicableTargetName() {
        var guiNode = TaskNode.ContainingTask.NodeDeclarations.OfType<IGuiNodeSymbol>().FirstOrDefault();
        return guiNode?.Name ?? "TO_BE_FILLED";
    }

}