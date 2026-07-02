#region Using Directives

using System;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen; 

class ExitTransitionCodeModel: TransitionCodeModel {

    public ExitTransitionCodeModel(ImmutableList<Call> calls,
                                   ParameterCodeModel taskResult, bool generateAbstractMethod, string nodeName)
        : base(calls) {

        TaskResult             = taskResult ?? throw new ArgumentNullException(nameof(taskResult));
        GenerateAbstractMethod = generateAbstractMethod;
        NodeName               = nodeName ?? String.Empty;
    }

    public ParameterCodeModel TaskResult             { get; }
    public bool               GenerateAbstractMethod { get; }
    public string             NodeName               { get; }
    public string             NodeNamePascalcase     => NodeName.ToPascalcase();

    public static ExitTransitionCodeModel FromTaskNode(ITaskNodeSymbol taskNode, TaskCodeInfo taskCodeInfo) {

        if (taskNode == null) {
            throw new ArgumentNullException(nameof(taskNode));
        }

        var reachableCalls = taskNode.Outgoings.GetReachableCalls();
        var taskResult     = ParameterCodeModel.TaskResult(taskNode.Declaration);

        return new ExitTransitionCodeModel(
            calls                 : reachableCalls.ToImmutableList(),
            taskResult            : taskResult,
            generateAbstractMethod: taskNode.CodeGenerateAbstractMethod(),
            nodeName              : taskNode.Name);
    }

}