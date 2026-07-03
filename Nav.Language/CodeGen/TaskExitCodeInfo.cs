#nullable enable

#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public sealed class TaskExitCodeInfo {

    TaskExitCodeInfo(TaskCodeInfo containingTask, string? taskNodeName) {
        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        var nodeNamePascalcase = (taskNodeName ?? String.Empty).ToPascalcase();

        AfterMethodName      = $"{CodeGenFacts.ExitMethodPrefix}{nodeNamePascalcase}";
        AfterLogicMethodName = $"{CodeGenFacts.ExitMethodPrefix}{nodeNamePascalcase}{CodeGenFacts.LogicMethodSuffix}";
    }

    public TaskCodeInfo ContainingTask       { get; }
    public string       AfterMethodName      { get; }
    public string       AfterLogicMethodName { get; }

    public static TaskExitCodeInfo FromConnectionPointReference(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {

        if (exitConnectionPointReferenceSymbol == null) {
            throw new ArgumentNullException(nameof(exitConnectionPointReferenceSymbol));
        }

        var exitTransition = exitConnectionPointReferenceSymbol.ExitTransition;

        var containingTaskCodeInfo = TaskCodeInfo.FromTaskDefinition(exitTransition.ContainingTask);

        return new TaskExitCodeInfo(containingTaskCodeInfo, exitTransition.SourceReference?.Name);
    }

    internal static TaskExitCodeInfo FromTaskNode(ITaskNodeSymbol taskNode,
                                                  TaskCodeInfo? containingTaskCodeInfo) {

        if (taskNode == null) {
            throw new ArgumentNullException(nameof(taskNode));
        }

        containingTaskCodeInfo ??= TaskCodeInfo.FromTaskDefinition(taskNode.ContainingTask);

        return new TaskExitCodeInfo(containingTaskCodeInfo, taskNode.Name);
    }

}