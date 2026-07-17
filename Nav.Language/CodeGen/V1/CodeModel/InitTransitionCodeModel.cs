#region Using Directives

using System;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das Transitions-Modell eines <c>init</c>-Knotens (<see cref="IInitNodeSymbol"/>) — trägt die generierte
/// Einstiegs-Kante des Workflows: die <c>public virtual IINIT_TASK Begin(…)</c>-Methode samt der abstrakten
/// <c>BeginLogic(…)</c>, in die der handgeschriebene Body einhängt (<see cref="WfsBaseEmitter"/>). Ist der Init
/// als abstrakt markiert (<see cref="GenerateAbstractMethod"/>), entfällt der Rumpf und es entsteht nur die
/// <c>public abstract IINIT_TASK Begin(…)</c>-Deklaration.
/// </summary>
sealed class InitTransitionCodeModel: TransitionCodeModel {

    InitTransitionCodeModel(ImmutableList<ParameterCodeModel> parameter, ImmutableList<Call> reachableCalls,
                            bool generateAbstractMethod, string? nodeName)
        : base(reachableCalls) {

        Parameter              = parameter ?? throw new ArgumentNullException(nameof(parameter));
        GenerateAbstractMethod = generateAbstractMethod;
        NodeName               = nodeName ?? String.Empty;
    }

    /// <summary>Ob der Init <c>[abstract]</c> ist — dann emittiert der Emitter nur die abstrakte <c>Begin(…)</c>-Deklaration ohne Weiche.</summary>
    public bool   GenerateAbstractMethod { get; }
    /// <summary>Der Name des Init-Knotens (Inhalt der <c>&lt;NavInit&gt;</c>-Annotation an der <c>Begin(…)</c>-Methode).</summary>
    public string NodeName               { get; }

    /// <summary>Die Parameter des <c>init</c>-Knotens (<c>{Typ} {Name}</c>) — vorderer Teil der <c>Begin(…)</c>- und <c>BeginLogic(…)</c>-Signatur.</summary>
    public ImmutableList<ParameterCodeModel> Parameter { get; }

    /// <summary>
    /// Baut das Modell aus einem <see cref="IInitNodeSymbol"/>: die Parameter aus der Code-Params-Deklaration,
    /// die erreichbaren Aufrufe aus den ausgehenden Kanten (<see cref="TransitionCodeModel"/>) und das
    /// <c>[abstract]</c>-Flag aus dem Knoten.
    /// </summary>
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