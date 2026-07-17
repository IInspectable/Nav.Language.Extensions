#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die Nav→C#-Anker eines Task-Exits (Exit-Transition bzw. dessen Task-Knoten-Quelle): der enthaltende
/// Task plus der Name der generierten Exit-Logic-Methode <c>After{Node}Logic</c>. Trägt die Navigation
/// von einer Exit-Verbindung in den generierten Code. <b>Versionsrichtig:</b> Exit-Präfix und
/// Logic-Suffix werden über <see cref="TaskCodeInfo.Facts"/> aus der Sprach-Version des Nav-Symbols
/// bezogen.
/// </summary>
public sealed class TaskExitCodeInfo {

    TaskExitCodeInfo(TaskCodeInfo containingTask, string? taskNodeName) {
        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        var nodeNamePascalcase = (taskNodeName ?? String.Empty).ToPascalcase();

        AfterLogicMethodName = $"{containingTask.Facts.ExitMethodPrefix}{nodeNamePascalcase}{containingTask.Facts.LogicMethodSuffix}";
    }

    /// <summary>Der den Exit umschließende Task als Namens-/Pfad-Anker (<see cref="TaskCodeInfo"/>).</summary>
    public TaskCodeInfo ContainingTask       { get; }
    /// <summary>
    /// Der Name der generierten Exit-Logic-Methode (<c>After{Node}Logic</c>, z.B. <c>AfterLoginLogic</c>).
    /// Ziel der Navigation vom Exit in den C#-Code; Präfix und Suffix stammen versionsrichtig aus
    /// <see cref="ICodeGenFacts.ExitMethodPrefix"/> und <see cref="ICodeGenFacts.LogicMethodSuffix"/>
    /// (via <see cref="ContainingTask"/>).
    /// </summary>
    public string       AfterLogicMethodName { get; }

    /// <summary>
    /// Factory: baut die <see cref="TaskExitCodeInfo"/> aus einer Exit-Connection-Point-Referenz; der
    /// Knoten-Name stammt aus der Quell-Referenz der Exit-Transition.
    /// </summary>
    public static TaskExitCodeInfo FromConnectionPointReference(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {

        if (exitConnectionPointReferenceSymbol == null) {
            throw new ArgumentNullException(nameof(exitConnectionPointReferenceSymbol));
        }

        var exitTransition = exitConnectionPointReferenceSymbol.ExitTransition;

        var containingTaskCodeInfo = TaskCodeInfo.FromTaskDefinition(exitTransition.ContainingTask);

        return new TaskExitCodeInfo(containingTaskCodeInfo, exitTransition.SourceReference?.Name);
    }

    /// <summary>
    /// Factory-Überladung für den Exit an einem Task-Knoten; verwendet den bereits gebauten
    /// <paramref name="containingTaskCodeInfo"/> wieder oder leitet ihn — wenn <c>null</c> — über
    /// <see cref="TaskCodeInfo.FromTaskDefinition"/> aus dem enthaltenden Task ab.
    /// </summary>
    internal static TaskExitCodeInfo FromTaskNode(ITaskNodeSymbol taskNode,
                                                  TaskCodeInfo? containingTaskCodeInfo) {

        if (taskNode == null) {
            throw new ArgumentNullException(nameof(taskNode));
        }

        containingTaskCodeInfo ??= TaskCodeInfo.FromTaskDefinition(taskNode.ContainingTask);

        return new TaskExitCodeInfo(containingTaskCodeInfo, taskNode.Name);
    }

}