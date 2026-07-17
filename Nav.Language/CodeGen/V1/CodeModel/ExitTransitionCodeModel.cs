#region Using Directives

using System;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das Transitions-Modell der Rückkehr-Kante eines aufgerufenen Task-Knotens (<see cref="ITaskNodeSymbol"/>) —
/// trägt die generierte <c>protected virtual INavCommand After{Node}(TaskResult result)</c>-Methode samt der
/// abstrakten <c>After{Node}Logic(…)</c>, die entscheidet, wie es nach dem Ergebnis des Sub-Tasks weitergeht
/// (<see cref="WfsBaseEmitter"/>). Der Ergebnis-Parameter kommt aus <see cref="TaskResult"/>; ist der Knoten als
/// abstrakt markiert (<see cref="GenerateAbstractMethod"/>), entsteht nur die abstrakte
/// <c>After{Node}Logic(…)</c>-Deklaration.
/// </summary>
class ExitTransitionCodeModel: TransitionCodeModel {

    public ExitTransitionCodeModel(ImmutableList<Call> calls,
                                   ParameterCodeModel taskResult, bool generateAbstractMethod, string? nodeName)
        : base(calls) {

        TaskResult             = taskResult ?? throw new ArgumentNullException(nameof(taskResult));
        GenerateAbstractMethod = generateAbstractMethod;
        NodeName               = nodeName ?? String.Empty;
    }

    /// <summary>Der Ergebnis-Parameter des Sub-Tasks (<c>{ResultTyp} result</c>) — einziger Parameter der <c>After{Node}(…)</c>-Methode.</summary>
    public ParameterCodeModel TaskResult             { get; }
    /// <summary>Ob der Task-Knoten <c>[abstract]</c> ist — dann emittiert der Emitter nur die abstrakte <c>After{Node}Logic(…)</c>-Deklaration ohne Weiche.</summary>
    public bool               GenerateAbstractMethod { get; }
    /// <summary>Der Name des Task-Knotens (Inhalt der <c>&lt;NavExit&gt;</c>-Annotation an der <c>After{Node}(…)</c>-Methode).</summary>
    public string             NodeName               { get; }
    /// <summary>Der Knotenname in Pascalcase — bildet den Methodennamen <c>After{Node}</c> (bzw. <c>After{Node}Logic</c>).</summary>
    public string             NodeNamePascalcase     => NodeName.ToPascalcase();

    /// <summary>
    /// Baut das Modell aus einem <see cref="ITaskNodeSymbol"/>: die erreichbaren Aufrufe aus den ausgehenden
    /// Kanten, den Ergebnis-Parameter aus der Task-Deklaration (<see cref="ParameterCodeModel.TaskResult(ITaskDeclarationSymbol)"/>)
    /// und das <c>[abstract]</c>-Flag aus dem Knoten.
    /// </summary>
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