#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Auswertung der Code-Annotationen (<c>[abstractmethod]</c>, <c>[notimplemented]</c>,
/// <c>[donotinject]</c>) und der Exit-Verdrahtung an Task- und Init-Knoten — die Abfrageseite für
/// Codegenerierung, Completion und Code-Fixes.
/// </summary>
public static class TaskNodeSymbolExtensions {

    /// <summary>
    /// Ob der <c>init</c>-Knoten als <c>[abstractmethod]</c> deklariert ist
    /// (<see cref="CodeAbstractMethodDeclarationSyntax"/>) — der Codegenerator erzeugt die
    /// zugehörige Logik-Methode dann <c>abstract</c>; <c>false</c> bei <c>null</c>.
    /// </summary>
    /// <param name="initNode">Der zu prüfende <c>init</c>-Knoten, oder <c>null</c>.</param>
    public static bool CodeGenerateAbstractMethod(this IInitNodeSymbol? initNode) {
        return initNode?.Syntax.CodeAbstractMethodDeclaration?.Keyword.IsMissing == false;
    }

    /// <summary>
    /// Ob der <c>task</c>-Knoten als <c>[abstractmethod]</c> deklariert ist
    /// (<see cref="CodeAbstractMethodDeclarationSyntax"/>) — der Codegenerator erzeugt die
    /// zugehörige Logik-Methode dann <c>abstract</c>; <c>false</c> bei <c>null</c>.
    /// </summary>
    /// <param name="taskNode">Der zu prüfende <c>task</c>-Knoten, oder <c>null</c>.</param>
    public static bool CodeGenerateAbstractMethod(this ITaskNodeSymbol? taskNode) {
        return taskNode?.Syntax.CodeAbstractMethodDeclaration?.Keyword.IsMissing == false;
    }

    /// <summary>
    /// Ob der Knoten ein Task-Knoten ist, dessen aufgelöste Deklaration als
    /// <c>[notimplemented]</c> markiert ist (<see cref="ITaskDeclarationSymbol.CodeNotImplemented"/>)
    /// — der Codegenerator übergeht so markierte Task-Knoten. <c>false</c> für andere
    /// Knoten-Arten, unaufgelöste Deklarationen und <c>null</c>.
    /// </summary>
    /// <param name="taskNode">Der zu prüfende Knoten, oder <c>null</c>.</param>
    public static bool CodeNotImplemented(this INodeSymbol? taskNode) {
        return (taskNode as ITaskNodeSymbol)?.Declaration?.CodeNotImplemented == true;
    }

    /// <summary>
    /// Ob der Knoten ein Task-Knoten mit <c>[donotinject]</c>-Deklaration ist
    /// (<see cref="CodeDoNotInjectDeclarationSyntax"/>) — der aufgerufene Unter-Workflow wird dann
    /// von der Dependency-Injection ausgenommen. <c>false</c> für andere Knoten-Arten und
    /// <c>null</c>.
    /// </summary>
    /// <param name="node">Der zu prüfende Knoten, oder <c>null</c>.</param>
    public static bool CodeDoNotInject(this INodeSymbol? node) {
        return (node as ITaskNodeSymbol)?.Syntax.CodeDoNotInjectDeclaration?.Keyword.IsMissing == false;
    }

    /// <summary>
    /// Liefert die Exit-Verbindungspunkte der Deklaration des Task-Knotens
    /// (<see cref="ITaskDeclarationSymbol.Exits"/>), für die an diesem Knoten noch keine
    /// Exit-Transition existiert — nach Name sortiert; leer, wenn die Deklaration unaufgelöst ist.
    /// Grundlage des Code-Fixes „fehlende Exit-Transition ergänzen" und der Exit-Completion.
    /// </summary>
    /// <param name="taskNode">Der Task-Knoten, dessen offene Exits ermittelt werden.</param>
    public static IEnumerable<IConnectionPointSymbol> GetUnconnectedExits(this ITaskNodeSymbol taskNode) {

        if (taskNode.Declaration != null) {

            var expectedExits  = taskNode.Declaration.Exits().OrderBy(cp => cp.Name);
            var connectedExits = GetConnectedExits(taskNode).ToList();

            foreach (var expectedExit in expectedExits) {

                if (!connectedExits.Exists(connectedExit => connectedExit == expectedExit)) {
                    yield return expectedExit;
                }
            }

        }

    }

    /// <summary>
    /// Liefert die Exit-Verbindungspunkte, die an diesem Task-Knoten bereits über eine
    /// Exit-Transition verbunden sind — aus den aufgelösten
    /// <see cref="IExitTransition.ExitConnectionPointReference"/>n seiner ausgehenden Kanten,
    /// dedupliziert; leer, wenn die Deklaration unaufgelöst ist.
    /// </summary>
    /// <param name="taskNode">Der Task-Knoten, dessen verbundene Exits ermittelt werden.</param>
    public static IEnumerable<IConnectionPointSymbol> GetConnectedExits(this ITaskNodeSymbol taskNode) {

        if (taskNode.Declaration == null) {
            return Enumerable.Empty<IConnectionPointSymbol>();
        }

        return GetConnectedExitsImpl().Distinct();

        IEnumerable<IConnectionPointSymbol> GetConnectedExitsImpl() {
            return taskNode.Outgoings
                           .Select(et => et.ExitConnectionPointReference?.Declaration)
                           .OfType<IConnectionPointSymbol>();

        }

    }

}
