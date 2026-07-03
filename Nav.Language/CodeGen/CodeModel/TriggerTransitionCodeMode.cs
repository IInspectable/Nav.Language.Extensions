#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

class TriggerTransitionCodeModel: TransitionCodeModel {

    readonly SignalTriggerCodeInfo _triggerCodeInfo;

    public TriggerTransitionCodeModel(SignalTriggerCodeInfo triggerCodeInfo, ImmutableList<Call> reachableCalls)
        : base(reachableCalls) {
        _triggerCodeInfo = triggerCodeInfo ?? throw new ArgumentNullException(nameof(triggerCodeInfo));
        ViewParameter    = new ParameterCodeModel(triggerCodeInfo.TOClassName, CodeGenFacts.ToParamtername);
    }

    public string TriggerName => _triggerCodeInfo.TriggerName;

    public ParameterCodeModel ViewParameter { get; }

    public static IEnumerable<TriggerTransitionCodeModel> FromTriggerTransition(TaskCodeInfo taskCodeInfo, ITriggerTransition triggerTransition) {

        foreach (var signalTrigger in triggerTransition.Triggers.OfType<ISignalTriggerSymbol>()) {

            var triggerCodeInfo = SignalTriggerCodeInfo.FromSignalTrigger(signalTrigger, taskCodeInfo);

            yield return new TriggerTransitionCodeModel(
                triggerCodeInfo: triggerCodeInfo,
                reachableCalls : triggerTransition.GetReachableCalls().ToImmutableList());
        }
    }

}