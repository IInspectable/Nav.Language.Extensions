#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Das Transitions-Modell eines Signal-Triggers (<see cref="ISignalTriggerSymbol"/>) einer View-Kante — trägt
/// die generierte <c>public virtual INavCommand {Trigger}(ViewTO to)</c>-Methode samt der abstrakten
/// <c>{Trigger}Logic(…)</c> (<see cref="WfsBaseEmitter"/>). Der Emitter ruft vor der Weiche noch den
/// <c>BeforeTriggerLogic(to)</c>-Hook auf dem View-Parameter (<see cref="ViewParameter"/>) auf. Je
/// <see cref="ISignalTriggerSymbol"/> einer Trigger-Transition entsteht genau ein Modell.
/// </summary>
class TriggerTransitionCodeModel: TransitionCodeModel {

    readonly SignalTriggerCodeInfo _triggerCodeInfo;

    public TriggerTransitionCodeModel(SignalTriggerCodeInfo triggerCodeInfo, ImmutableList<Call> reachableCalls)
        : base(reachableCalls) {
        _triggerCodeInfo = triggerCodeInfo ?? throw new ArgumentNullException(nameof(triggerCodeInfo));
        ViewParameter    = new ParameterCodeModel(triggerCodeInfo.TOClassName, CodeGenFacts.ToParamtername);
    }

    /// <summary>Der Name des Signal-Triggers — bildet den Methodennamen <c>{Trigger}</c> und die <c>&lt;NavTrigger&gt;</c>-Annotation.</summary>
    public string TriggerName => _triggerCodeInfo.TriggerName;

    /// <summary>Der View-Transfer-Objekt-Parameter der Trigger-Methode (<c>{View}TO to</c>) — Eingang der Weiche und Argument des <c>BeforeTriggerLogic</c>-Hooks.</summary>
    public ParameterCodeModel ViewParameter { get; }

    /// <summary>
    /// Erzeugt je Signal-Trigger der <paramref name="triggerTransition"/> ein Modell: der
    /// <see cref="SignalTriggerCodeInfo"/> liefert Triggername und View-TO-Typ, die erreichbaren Aufrufe kommen
    /// aus der Trigger-Transition (<see cref="TransitionCodeModel"/>).
    /// </summary>
    public static IEnumerable<TriggerTransitionCodeModel> FromTriggerTransition(TaskCodeInfo taskCodeInfo, ITriggerTransition triggerTransition) {

        foreach (var signalTrigger in triggerTransition.Triggers.OfType<ISignalTriggerSymbol>()) {

            var triggerCodeInfo = SignalTriggerCodeInfo.FromSignalTrigger(signalTrigger, taskCodeInfo);

            yield return new TriggerTransitionCodeModel(
                triggerCodeInfo: triggerCodeInfo,
                reachableCalls : triggerTransition.GetReachableCalls().ToImmutableList());
        }
    }

}