#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Gemeinsame Basis der drei V1-Transitions-CodeModels (<see cref="InitTransitionCodeModel"/>,
/// <see cref="ExitTransitionCodeModel"/>, <see cref="TriggerTransitionCodeModel"/>) — jedes trägt eine
/// generierte Zustandsübergangs-Methode der <c>{Task}WFSBase</c>-Klasse (<c>Begin(…)</c> / <c>After{Node}(…)</c>
/// / <c>On{Trigger}(…)</c>). Die Basis modelliert den drei Formen gemeinsamen Kern: die von der Kante aus
/// <b>erreichbaren</b> Aufrufe (<see cref="ReachableCalls"/>) als Fälle der <c>switch(body)</c>-Weiche des
/// <see cref="WfsBaseEmitter"/> sowie die Task-Begin-Wrapper, die die erreichbaren Task-Ziele einmal als
/// Konstruktor-Parameter (<see cref="TaskBegins"/>) und einmal als Backing-Felder
/// (<see cref="TaskBeginFields"/>) in die Basisklasse einspeisen.
/// </summary>
abstract class TransitionCodeModel: CodeModel {

    /// <summary>
    /// Projiziert die erreichbaren Aufrufe der Kante (<paramref name="reachableCalls"/>) in das gemeinsame
    /// Transitions-Modell. <see cref="ReachableCalls"/> entsteht aus den entdoppelten Aufrufen
    /// (<see cref="CallComparer.FoldExits"/> — mehrere Exit-Ziele kollabieren auf einen Fall) plus dem stets
    /// implizit erreichbaren <see cref="CanceCallCodeModel"/>, sortiert nach <see cref="CallCodeModel.SortOrder"/>.
    /// <see cref="TaskBegins"/> berechnet er nur aus den <b>injizierten</b> Task-Zielen (weder
    /// <c>[notimplemented]</c> noch <c>[donotinject]</c>); die <see cref="TaskBeginFields"/> sind eine reine
    /// Projektion derselben Parameter und werden nicht erneut aufgelöst.
    /// </summary>
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

    /// <summary>
    /// Die von dieser Kante aus erreichbaren Aufrufe als Fälle der <c>switch(body)</c>-Weiche (inkl. dem stets
    /// vorhandenen <c>case CANCEL</c>), in stabiler Emitter-Reihenfolge (<see cref="CallCodeModel.SortOrder"/>).
    /// </summary>
    public ImmutableList<CallCodeModel>      ReachableCalls  { get; }
    /// <summary>
    /// Die injizierten Task-Begin-Wrapper der erreichbaren Task-Ziele als Konstruktor-Parameter
    /// (<c>IBegin{Task}WFS {task}</c>) — sie fließen in die <c>{Task}WFSBase</c>-Konstruktoren und als
    /// letzte Argumente in die <c>…Logic(…)</c>-Signatur der Transition.
    /// </summary>
    public ImmutableList<ParameterCodeModel> TaskBegins      { get; }
    /// <summary>
    /// Dieselben Task-Begin-Wrapper als readonly Backing-Felder (<c>_{task}</c>) der <c>{Task}WFSBase</c> —
    /// reine Projektion von <see cref="TaskBegins"/>, die der Emitter als <c>…Logic(…)</c>-Argumente durchreicht.
    /// </summary>
    public ImmutableList<FieldCodeModel>     TaskBeginFields { get; }

    /// <summary>
    /// Löst die erreichbaren Task-Ziele in ihre voll qualifizierten Begin-Interface-Parameter auf
    /// (<see cref="ParameterCodeModel.GetTaskBeginsAsParameter"/>), nach Parametername sortiert — der teuerste
    /// Teil des Modellaufbaus, daher im Konstruktor nur einmal berechnet.
    /// </summary>
    static ImmutableList<ParameterCodeModel> GetTaskBegins(IEnumerable<Call> reachableCalls) {

        var taskDeclarations = GetTaskDeclarations(reachableCalls);
        return ParameterCodeModel.GetTaskBeginsAsParameter(taskDeclarations)
                                 .OrderBy(p => p.ParameterName)
                                 .ToImmutableList();
    }

    /// <summary>
    /// Die distinkten Task-Deklarationen hinter den erreichbaren Task-Knoten — Eingabe für die Auflösung der
    /// Begin-Interface-Parameter in <see cref="GetTaskBegins"/>.
    /// </summary>
    static IEnumerable<ITaskDeclarationSymbol> GetTaskDeclarations(IEnumerable<Call> reachableCalls) {

        return reachableCalls.Select(call => call.Node)
                             .OfType<ITaskNodeSymbol>()
                             .Select(node => node.Declaration)
                             .WhereNotNull()
                             .Distinct();
    }

}