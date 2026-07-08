#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die Nav→C#-Anker einer Choice: der enthaltende Task plus der Name der abstrakten
/// Entscheidungsmethode <c>{Choice}Logic</c>. Trägt die GoTo-Navigation vom Choice-Knoten (und seinen
/// Referenzen) in den generierten Code. <b>Versionsrichtig:</b> der Logic-Suffix wird — wie bei
/// <see cref="TaskExitCodeInfo"/>/<see cref="SignalTriggerCodeInfo"/> — über
/// <see cref="TaskCodeInfo.Facts"/> aus der Sprach-Version des Nav-Symbols bezogen.
/// </summary>
public sealed class ChoiceCodeInfo {

    ChoiceCodeInfo(TaskCodeInfo containingTask, string? choiceNodeName) {
        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        var nodeNamePascalcase = (choiceNodeName ?? String.Empty).ToPascalcase();

        ChoiceLogicMethodName = $"{nodeNamePascalcase}{containingTask.Facts.LogicMethodSuffix}";
    }

    public TaskCodeInfo ContainingTask        { get; }
    public string       ChoiceLogicMethodName { get; }

    public static ChoiceCodeInfo FromChoiceNode(IChoiceNodeSymbol choiceNode) {

        if (choiceNode == null) {
            throw new ArgumentNullException(nameof(choiceNode));
        }

        var containingTaskCodeInfo = TaskCodeInfo.FromTaskDefinition(choiceNode.ContainingTask);

        return new ChoiceCodeInfo(containingTaskCodeInfo, choiceNode.Name);
    }

}
