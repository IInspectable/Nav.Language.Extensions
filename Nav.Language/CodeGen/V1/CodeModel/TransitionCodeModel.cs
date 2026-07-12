#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

abstract class TransitionCodeModel: CodeModel {

    protected TransitionCodeModel(IEnumerable<Call> reachableCalls) {

        if (reachableCalls == null) {
            throw new ArgumentNullException(nameof(reachableCalls));
        }

        var distinctReachableCalls = reachableCalls.Distinct(CallComparer.FoldExits).ToImmutableList();
        var implementedCalls       = distinctReachableCalls.Where(c => !c.Node.CodeNotImplemented()).ToList();
        var injectedCalls          = implementedCalls.Where(c => !c.Node.CodeDoNotInject()).ToList();

        var reachableCallsModels = CallCodeModelBuilder.FromCalls(distinctReachableCalls)
                                                        // Cancel ist immer implizit erreichbar
                                                       .Concat(new[] {new CanceCallCodeModel()})
                                                       .OrderBy(c => c.SortOrder);

        // Die Task-Begin-Parameter einmal berechnen (das Auflösen der Deklarationen und das Bilden der
        // voll qualifizierten Begin-Interface-Namen ist der teuerste Teil); die Feld-Modelle sind eine
        // reine Projektion derselben Parameter und werden daraus abgeleitet, nicht erneut berechnet.
        var taskBegins = GetTaskBegins(injectedCalls);

        ReachableCalls  = reachableCallsModels.ToImmutableList();
        TaskBegins      = taskBegins;
        TaskBeginFields = taskBegins.Select(p => new FieldCodeModel(p.ParameterType, p.ParameterName))
                                    .ToImmutableList();
    }

    public ImmutableList<CallCodeModel>      ReachableCalls  { get; }
    public ImmutableList<ParameterCodeModel> TaskBegins      { get; }
    public ImmutableList<FieldCodeModel>     TaskBeginFields { get; }

    static ImmutableList<ParameterCodeModel> GetTaskBegins(IEnumerable<Call> reachableCalls) {

        var taskDeclarations = GetTaskDeclarations(reachableCalls);
        return ParameterCodeModel.GetTaskBeginsAsParameter(taskDeclarations)
                                 .OrderBy(p => p.ParameterName)
                                 .ToImmutableList();
    }

    static IEnumerable<ITaskDeclarationSymbol> GetTaskDeclarations(IEnumerable<Call> reachableCalls) {

        return reachableCalls.Select(call => call.Node)
                             .OfType<ITaskNodeSymbol>()
                             .Select(node => node.Declaration)
                             .WhereNotNull()
                             .Distinct();
    }

}