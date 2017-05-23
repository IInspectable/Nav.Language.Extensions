﻿#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen {
    class TriggerTransitionCodeModel : TransitionCodeModel {

        public TriggerTransitionCodeModel(ImmutableList<CallCodeModel> calls, string viewName, string triggerName)
            : base(calls) {
            TriggerName = triggerName;
            ViewName    = viewName ?? String.Empty;
        }

        public string ViewName { get; }
        public string ViewNamePascalcase => ViewName.ToPascalcase();
        public string TriggerName { get; }
        public string TriggerNamePascalcase => TriggerName.ToPascalcase();

        public static IEnumerable<TriggerTransitionCodeModel> FromNode(IGuiNodeSymbol node) {

            foreach (var trans in node.Outgoings) {
                foreach (var signalTrigger in trans.Triggers.OfType<ISignalTriggerSymbol>()) {

                    var calls = CallCodeModelBuilder.FromCalls(trans.Target.Declaration.GetDistinctOutgoingCalls());
                    yield return new TriggerTransitionCodeModel(
                        calls: calls.ToImmutableList(), 
                        viewName: node.Name, 
                        triggerName: signalTrigger.Name);
                }
            }
        }
    }
}