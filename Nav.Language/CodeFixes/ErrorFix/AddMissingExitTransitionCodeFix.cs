#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix; 

/// <summary>
/// Ergänzt eine fehlende Exit-Transition für einen offenen Exit-Verbindungspunkt eines eingebetteten
/// Task-Knotens (<c>Nav0025</c> — <c>No outgoing edge declared for exit '{0}'</c>). Zu einem Exit-Punkt
/// <c>e</c> des Task-Knotens <c>T</c> wird eine neue Zeile <c>T:e --&gt; Ziel;</c> in den Transitionsblock
/// eingefügt, formatiert nach dem Vorbild einer bereits vorhandenen Kante (<see cref="GetTemplateEdge"/>).
/// Als Ziel wird — falls vorhanden — der erste GUI-Knoten des Tasks gewählt, sonst der Platzhalter
/// <c>TO_BE_FILLED</c>.
/// </summary>
/// <remarks>
/// Anwendbar nur, wenn eine vollständige Kante als Formatvorlage existiert und der Exit-Punkt noch nicht
/// verbunden ist (siehe <see cref="CanApplyFix"/>). Der Fix mutiert nichts selbst, sondern liefert das
/// Edit-Set als <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>-Liste.
/// </remarks>
public sealed class AddMissingExitTransitionCodeFix: ErrorCodeFix {

    /// <summary>
    /// Erzeugt den Fix für den offenen Exit-Punkt <paramref name="connectionPoint"/> des über
    /// <paramref name="targetNodeRef"/> referenzierten Task-Knotens.
    /// </summary>
    /// <param name="targetNodeRef">Die Knoten-Referenz auf den eingebetteten Task-Knoten; ihre
    /// <see cref="INodeReferenceSymbol.Declaration"/> muss ein <see cref="ITaskNodeSymbol"/> sein.</param>
    /// <param name="connectionPoint">Der noch unverbundene Exit-Verbindungspunkt, der eine Transition erhält.</param>
    /// <param name="context">Der Fix-Kontext (Quelltext, Editor-Einstellungen, betroffener Bereich).</param>
    /// <exception cref="ArgumentException">Der Ziel-Knoten ist kein Task-Knoten oder der
    /// <paramref name="connectionPoint"/> gehört nicht zu dessen Task-Deklaration.</exception>
    internal AddMissingExitTransitionCodeFix(INodeReferenceSymbol targetNodeRef, IConnectionPointSymbol connectionPoint, CodeFixContext context)
        : base(context) {

        ConnectionPoint = connectionPoint                              ?? throw new ArgumentNullException(nameof(connectionPoint));
        TargetNodeRef   = targetNodeRef                                ?? throw new ArgumentNullException(nameof(targetNodeRef));
        TaskNode        = targetNodeRef.Declaration as ITaskNodeSymbol ?? throw new ArgumentException(nameof(targetNodeRef));

        if (TaskNode.Declaration != ConnectionPoint.TaskDeclaration) {
            throw new ArgumentException();
        }
    }

    /// <summary>Der eingebettete Task-Knoten, dessen Exit-Punkt eine Transition erhält.</summary>
    public ITaskNodeSymbol        TaskNode        { get; }
    /// <summary>Der offene Exit-Verbindungspunkt, für den die neue Exit-Transition ergänzt wird.</summary>
    public IConnectionPointSymbol ConnectionPoint { get; }
    /// <summary>Die Knoten-Referenz, die den <see cref="TaskNode"/> im Transitionsblock adressiert.</summary>
    public INodeReferenceSymbol   TargetNodeRef   { get; }

    /// <summary>Die Task-Definition, in deren Transitionsblock die Transition eingefügt wird.</summary>
    public ITaskDefinitionSymbol ContainingTask => TaskNode.ContainingTask;

    /// <inheritdoc/>
    public override string        Name         => "Add Missing Edge";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => TargetNodeRef.Location.Extent;
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.High;
        

    /// <summary>
    /// Ob der Fix anwendbar ist: Es muss eine vollständige Kante (Quelle, Kanten-Modus, Ziel) als
    /// Formatvorlage geben, und für <see cref="ConnectionPoint"/> darf noch keine Exit-Transition existieren.
    /// </summary>
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

    /// <summary>
    /// Erzeugt das Edit-Set: eine einzelne Einfüge-Änderung, die hinter der Zeile der Vorlage-Kanten-Quelle
    /// die neue Exit-Transition <c>TaskNode:ConnectionPoint --&gt; Ziel;</c> (samt Zeilenende) einfügt. Die
    /// Kante wird nach dem Vorbild der Vorlage-Kante formatiert (<see cref="CodeFix.ComposeEdge"/>).
    /// </summary>
    /// <returns>Die einzufügenden <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s.</returns>
    /// <exception cref="InvalidOperationException">Der Fix ist nicht anwendbar (<see cref="CanApplyFix"/>).</exception>
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

    /// <summary>
    /// Ermittelt im neu berechneten Semantik-Modell (nach Anwenden des Fixes) die Ziel-Referenz der eben
    /// ergänzten Exit-Transition — gedacht, um den Cursor/die Auswahl dorthin zu setzen (etwa auf den
    /// <c>TO_BE_FILLED</c>-Platzhalter).
    /// </summary>
    /// <param name="codegenerationUnit">Das Semantik-Modell nach Anwenden der Änderungen, oder <c>null</c>.</param>
    /// <returns>Der Extent der neuen Ziel-Referenz, oder <see cref="TextExtent.Missing"/>, wenn sie nicht
    /// gefunden wird.</returns>
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
