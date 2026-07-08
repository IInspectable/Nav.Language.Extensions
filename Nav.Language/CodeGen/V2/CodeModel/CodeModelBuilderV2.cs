#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Baut die V2-Transitionsmodelle (<see cref="TransitionCallContextCodeModel"/>) aus dem Semantic
/// Model — das V2-Pendant zu den <c>Get*Transitions</c>-Fabriken des V1-<c>CodeModelBuilder</c>.
/// Reachability, Init-/Exit-/Trigger-Auswahl und deren Reihenfolge sind bewusst deckungsgleich mit
/// V1; nur die Gestalt der erzeugten Modelle (CallContext statt <c>switch</c>) unterscheidet sich.
/// </summary>
static class CodeModelBuilderV2 {

    public static IEnumerable<TransitionCallContextCodeModel> GetInitTransitions(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {
        return taskDefinition.NodeDeclarations
                             .OfType<IInitNodeSymbol>()
                             .Select(initNode => TransitionCallContextCodeModel.FromInit(initNode, taskResult));
    }

    public static IEnumerable<TransitionCallContextCodeModel> GetExitTransitions(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {
        return taskDefinition.NodeDeclarations
                             .OfType<ITaskNodeSymbol>()
                             .Where(taskNode => taskNode.IsReachable())
                             .Where(taskNode => !taskNode.CodeNotImplemented())
                             .Distinct()
                             .Select(taskNode => TransitionCallContextCodeModel.FromExit(taskNode, taskResult));
    }

    public static IEnumerable<TransitionCallContextCodeModel> GetTriggerTransitions(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {
        return taskDefinition.TriggerTransitions
                             .SelectMany(triggerTransition => TransitionCallContextCodeModel.FromTrigger(triggerTransition, taskResult))
                             .OrderBy(t => t.MachineryName.Length)
                             .ThenBy(t => t.MachineryName);
    }

}
