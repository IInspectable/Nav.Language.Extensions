#region Using Directives

using System;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class InitTransitionCodeModel: TransitionCodeModel {

    InitTransitionCodeModel(ImmutableList<ParameterCodeModel> parameter, ImmutableList<Call> reachableCalls,
                            bool generateAbstractMethod, string? nodeName)
        : base(reachableCalls) {

        Parameter              = parameter ?? throw new ArgumentNullException(nameof(parameter));
        GenerateAbstractMethod = generateAbstractMethod;
        NodeName               = nodeName ?? String.Empty;
    }

    public bool   GenerateAbstractMethod { get; }
    public string NodeName               { get; }

    public ImmutableList<ParameterCodeModel> Parameter { get; }

    internal static InitTransitionCodeModel FromInitTransition(IInitNodeSymbol initNode, TaskCodeInfo taskCodeInfo) {
        if (initNode == null) {
            throw new ArgumentNullException(nameof(initNode));
        }

        if (taskCodeInfo == null) {
            throw new ArgumentNullException(nameof(taskCodeInfo));
        }

        var parameter = ParameterCodeModel.FromParameterSyntaxes(initNode.Syntax.CodeParamsDeclaration?.ParameterList);

        return new InitTransitionCodeModel(
            parameter             : parameter.ToImmutableList(),
            reachableCalls        : initNode.Outgoings.GetReachableCalls().ToImmutableList(),
            generateAbstractMethod: initNode.CodeGenerateAbstractMethod(),
            nodeName              : initNode.Name);
    }

}