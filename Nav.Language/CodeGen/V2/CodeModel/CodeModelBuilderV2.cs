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

    /// <summary>
    /// Baut je <see cref="IInitNodeSymbol"/> des Tasks eine Init-Transition (in Deklarationsreihenfolge) —
    /// das V2-Pendant zu <c>CodeModelBuilder.GetInitTransitions</c>.
    /// </summary>
    public static IEnumerable<TransitionCallContextCodeModel> GetInitTransitions(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {
        return taskDefinition.NodeDeclarations
                             .OfType<IInitNodeSymbol>()
                             .Select(initNode => TransitionCallContextCodeModel.FromInit(initNode, taskResult));
    }

    /// <summary>
    /// Baut je <b>erreichbarem</b>, nicht-<c>[notimplemented]</c> <see cref="ITaskNodeSymbol"/> eine
    /// Exit-Transition (<c>After{Node}</c>-Rücksprung), entdoppelt — deckungsgleich mit der V1-Quellenauswahl.
    /// </summary>
    public static IEnumerable<TransitionCallContextCodeModel> GetExitTransitions(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {
        return taskDefinition.NodeDeclarations
                             .OfType<ITaskNodeSymbol>()
                             .Where(taskNode => taskNode.IsReachable())
                             .Where(taskNode => !taskNode.CodeNotImplemented())
                             .Distinct()
                             .Select(taskNode => TransitionCallContextCodeModel.FromExit(taskNode, taskResult));
    }

    /// <summary>
    /// Baut je Signal-Trigger der <see cref="ITaskDefinitionSymbol"/> eine Trigger-Transition, sortiert nach
    /// Namenslänge und dann alphabetisch (wie V1).
    /// </summary>
    public static IEnumerable<TransitionCallContextCodeModel> GetTriggerTransitions(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {
        return taskDefinition.TriggerTransitions
                             .SelectMany(triggerTransition => TransitionCallContextCodeModel.FromTrigger(triggerTransition, taskResult))
                             .OrderBy(t => t.MachineryName.Length)
                             .ThenBy(t => t.MachineryName);
    }

    /// <summary>
    /// Baut je <b>erreichbarer</b> Choice ihren V2-Baustein (Context + <c>{Choice}Logic</c>, §3.5) — in
    /// Deklarationsreihenfolge. Erreichbar heißt: von einer Quelle (Init/Trigger/Exit) direkt oder über eine
    /// Choice-Kette angesteuert; unerreichbare Choices erzeugen — wie bei V1 — keinen Code. Der Command-Typ je
    /// Choice folgt der Init-Erreichbarkeit (§3.8/④): <c>IINIT_TASK</c>, sobald sie von <b>irgendeinem</b>
    /// Init aus (transitiv über Choices) erreicht wird, sonst <c>INavCommand</c>.
    /// </summary>
    public static IEnumerable<ChoiceCallContextCodeModel> GetChoices(ITaskDefinitionSymbol taskDefinition, ParameterCodeModel taskResult) {

        var initReachable   = CollectReachableChoices(InitRootEdges(taskDefinition));
        var sourceReachable = CollectReachableChoices(SourceRootEdges(taskDefinition));

        return taskDefinition.NodeDeclarations
                             .OfType<IChoiceNodeSymbol>()
                             .Where(sourceReachable.Contains)
                             .Select(choiceNode => ChoiceCallContextCodeModel.FromChoice(choiceNode, taskResult, initReachable.Contains(choiceNode)));
    }

    // Die Ausgangs-Kanten aller Init-Knoten (für die Init-Erreichbarkeit der Choices, §3.8/④).
    static IEnumerable<IEdge> InitRootEdges(ITaskDefinitionSymbol taskDefinition) {
        return taskDefinition.NodeDeclarations
                             .OfType<IInitNodeSymbol>()
                             .SelectMany(initNode => initNode.Outgoings);
    }

    // Die Ausgangs-Kanten aller generierten Quellen (Init + erreichbare, nicht-[notimplemented] Task-Knoten
    // + Trigger) — deckungsgleich mit der Quellen-Auswahl von GetInit/Exit/TriggerTransitions.
    static IEnumerable<IEdge> SourceRootEdges(ITaskDefinitionSymbol taskDefinition) {

        var exitRootEdges = taskDefinition.NodeDeclarations
                                          .OfType<ITaskNodeSymbol>()
                                          .Where(taskNode => taskNode.IsReachable())
                                          .Where(taskNode => !taskNode.CodeNotImplemented())
                                          .Distinct()
                                          .SelectMany(taskNode => taskNode.Outgoings);

        return InitRootEdges(taskDefinition)
              .Concat(exitRootEdges)
              .Concat(taskDefinition.TriggerTransitions);
    }

    // Alle von den Wurzel-Kanten aus (direkt oder über Choice-Ketten) erreichbaren Choices; Zyklenschutz
    // über die besuchte Menge. Nur der EINE Schritt „Kante → Choice-Ziel" wird verfolgt — Views/Tasks/Exits
    // sind terminal und tragen die Init-/Quellen-Erreichbarkeit einer Choice nicht weiter.
    static HashSet<IChoiceNodeSymbol> CollectReachableChoices(IEnumerable<IEdge> rootEdges) {

        var reached = new HashSet<IChoiceNodeSymbol>();
        var queue   = new Queue<IChoiceNodeSymbol>();

        foreach (var choice in ChoiceTargets(rootEdges)) {
            if (reached.Add(choice)) {
                queue.Enqueue(choice);
            }
        }

        while (queue.Count > 0) {
            foreach (var choice in ChoiceTargets(queue.Dequeue().Outgoings)) {
                if (reached.Add(choice)) {
                    queue.Enqueue(choice);
                }
            }
        }

        return reached;

        static IEnumerable<IChoiceNodeSymbol> ChoiceTargets(IEnumerable<IEdge> edges) {
            return edges.Select(edge => edge.TargetReference?.Declaration)
                        .OfType<IChoiceNodeSymbol>();
        }
    }

}
