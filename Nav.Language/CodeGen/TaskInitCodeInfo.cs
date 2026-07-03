#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public sealed class TaskInitCodeInfo {

    TaskInitCodeInfo(TaskCodeInfo containingTask, string? initName) {

        ContainingTask       = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        BeginMethodName      = $"{CodeGenFacts.BeginMethodPrefix}";
        BeginLogicMethodName = $"{CodeGenFacts.BeginMethodPrefix}{CodeGenFacts.LogicMethodSuffix}";
        InitName             = initName ?? String.Empty;
    }

    public TaskCodeInfo ContainingTask       { get; }
    public string       BeginLogicMethodName { get; }
    public string       BeginMethodName      { get; }
    public string       InitName             { get; }

    public static TaskInitCodeInfo FromInitNode(IInitNodeSymbol initNodeSymbol) {

        if (initNodeSymbol == null) {
            throw new ArgumentNullException(nameof(initNodeSymbol));
        }

        var taskCodeModel = TaskCodeInfo.FromTaskDefinition(initNodeSymbol.ContainingTask);

        return FromInitNode(taskCodeModel, initNodeSymbol);
    }

    internal static TaskInitCodeInfo FromInitNode(TaskCodeInfo taskCodeInfo, IInitNodeSymbol initNodeSymbol) {

        if (initNodeSymbol == null) {
            throw new ArgumentNullException(nameof(initNodeSymbol));
        }

        if (taskCodeInfo == null) {
            throw new ArgumentNullException(nameof(taskCodeInfo));
        }

        return new TaskInitCodeInfo(containingTask: taskCodeInfo, initName: initNodeSymbol.Name);
    }

}