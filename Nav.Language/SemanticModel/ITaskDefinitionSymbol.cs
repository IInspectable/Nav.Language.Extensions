#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol einer Task-Definition — das eigentliche Workflow-Artefakt der Nav-Sprache, aus dem der
/// C#-Code generiert wird:
/// <code>
/// task Auswahl
/// {
///     init I1;
///     view Liste;
///     exit Ok;
///
///     I1    --> Liste;
///     Liste --> Ok on Übernehmen;
/// }
/// </code>
/// Es fasst die Knoten des Deklarationsblocks (<see cref="NodeDeclarations"/>) und die Kanten des
/// Transitionsblocks (<see cref="InitTransitions"/>, <see cref="ChoiceTransitions"/>,
/// <see cref="TriggerTransitions"/>, <see cref="ExitTransitions"/>; gesamt <see cref="Edges"/>)
/// zusammen. Zu jeder Definition entsteht zugleich eine Task-Deklaration ihrer Schnittstelle
/// (<see cref="AsTaskDeclaration"/>), über die andere Tasks sie als Task-Knoten einbinden.
/// Konstruktionsstelle ist der <c>TaskDefinitionSymbolBuilder</c>; Syntax:
/// <see cref="TaskDefinitionSyntax"/>.
/// </summary>
public interface ITaskDefinitionSymbol: ISymbol {

    /// <summary>
    /// Das semantische Modell der Datei, zu dem diese Definition gehört. Nullable nur, weil die
    /// Rückreferenz erst am Ende des Modellbaus gesetzt wird — an einem fertig gebauten Modell in
    /// der Praxis nie null.
    /// </summary>
    CodeGenerationUnit? CodeGenerationUnit { get; }

    /// <summary>
    /// Der C#-Ziel-Namespace aus der <c>[namespaceprefix …]</c>-Angabe im Kopf der Datei — leer,
    /// wenn nicht angegeben.
    /// </summary>
    string CodeNamespace { get; }

    /// <summary>Die zugrunde liegende <c>task</c>-Definition im Syntaxbaum.</summary>
    TaskDefinitionSyntax Syntax { get; }

    /// <summary>
    /// Die zu dieser Definition implizit erzeugte Task-Deklaration ihrer Schnittstelle —
    /// Verbindungspunkte, <c>[result …]</c> usw.
    /// (<see cref="TaskDeclarationOrigin.TaskDefinition"/>). <c>null</c>, wenn unter dem Namen
    /// bereits eine andere Deklaration eingetragen ist (der Namenskonflikt ist dann als Nav0020
    /// gemeldet).
    /// </summary>
    ITaskDeclarationSymbol? AsTaskDeclaration { get; }

    /// <summary>
    /// Die im Deklarationsblock deklarierten Knoten (<c>init</c>, <c>exit</c>, <c>end</c>,
    /// <c>choice</c>, <c>dialog</c>, <c>view</c>, <c>task</c>) — die Knoten des Workflow-Graphen
    /// (<see cref="INodeSymbol"/>); Namensduplikate meldet Nav0022.
    /// </summary>
    IReadOnlySymbolCollection<INodeSymbol> NodeDeclarations { get; }

    /// <summary>Die Kanten mit einem <c>init</c>-Knoten als Quelle (<see cref="IInitTransition"/>).</summary>
    IReadOnlyList<IInitTransition> InitTransitions { get; }

    /// <summary>Die Kanten mit einem <c>choice</c>-Knoten als Quelle (<see cref="IChoiceTransition"/>).</summary>
    IReadOnlyList<IChoiceTransition> ChoiceTransitions { get; }

    /// <summary>Die Kanten mit einem GUI-Knoten (View/Dialog) als Quelle (<see cref="ITriggerTransition"/>).</summary>
    IReadOnlyList<ITriggerTransition> TriggerTransitions { get; }

    /// <summary>Die Exit-Transitionen — Kanten von den Exit-Verbindungspunkten der Task-Knoten (<see cref="IExitTransition"/>).</summary>
    IReadOnlyList<IExitTransition> ExitTransitions { get; }

    /// <summary>
    /// Liefert alle Kanten des Transitionsblocks: Init-, Choice-, Trigger- und Exit-Transitionen
    /// (in dieser Reihenfolge). Continuations hängen an ihrer tragenden Kante
    /// (<see cref="IContinuableEdge.ContinuationTransition"/>) und erscheinen hier nicht als
    /// eigene Einträge.
    /// </summary>
    IEnumerable<IEdge> Edges();

}
