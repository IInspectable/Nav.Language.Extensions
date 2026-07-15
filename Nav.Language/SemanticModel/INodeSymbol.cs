using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol eines Knotens, der im Deklarationsblock einer <c>task</c>-Definition deklariert ist —
/// eine der Knoten-Arten <c>init</c>, <c>exit</c>, <c>end</c>, <c>choice</c>, <c>dialog</c>,
/// <c>view</c> oder <c>task</c>, z.B. <c>view Auswahl;</c>. Die Knoten bilden zusammen mit den
/// Kanten (<see cref="IEdge"/>) den Workflow-Graphen; ihre Verdrahtung beschreiben
/// <see cref="ISourceNodeSymbol"/> und <see cref="ITargetNodeSymbol"/>, die konkrete Knoten-Art
/// die abgeleiteten Interfaces (<see cref="IInitNodeSymbol"/>, <see cref="IExitNodeSymbol"/>,
/// <see cref="IEndNodeSymbol"/>, <see cref="IChoiceNodeSymbol"/>, <see cref="IViewNodeSymbol"/>,
/// <see cref="IDialogNodeSymbol"/>, <see cref="ITaskNodeSymbol"/>).
/// </summary>
public interface INodeSymbol: ISymbol {

    /// <summary>
    /// Die Knoten-Deklaration im Syntaxbaum, aus der dieses Symbol entstanden ist. Die abgeleiteten
    /// Interfaces verengen den Typ auf die konkrete Deklarations-Syntax (z.B.
    /// <see cref="InitNodeDeclarationSyntax"/> an <see cref="IInitNodeSymbol.Syntax"/>).
    /// </summary>
    NodeDeclarationSyntax Syntax { get; }

    /// <summary>Die <c>task</c>-Definition, in deren Deklarationsblock dieser Knoten deklariert ist.</summary>
    ITaskDefinitionSymbol ContainingTask { get; }

    /// <summary>
    /// Alle beim Binden aufgelösten Referenzen auf diesen Knoten aus dem Transitionsblock — sowohl von
    /// der Quell- als auch von der Zielseite der Kanten (siehe
    /// <see cref="INodeReferenceSymbol.NodeReferenceType"/>); Grundlage z.B. für Find References.
    /// </summary>
    IReadOnlyList<INodeReferenceSymbol> References { get; }

    /// <summary>
    /// Liefert dieses Symbol samt seiner unmittelbaren Kind-Symbole — bei Init- und Task-Knoten
    /// zusätzlich den Alias (<see cref="IInitNodeSymbol.Alias"/> bzw.
    /// <see cref="ITaskNodeSymbol.Alias"/>). Hierüber werden die Symbole einer
    /// <see cref="CodeGenerationUnit"/> eingesammelt.
    /// </summary>
    IEnumerable<ISymbol> SymbolsAndSelf();

    /// <summary>
    /// Bestimmt, ob dieser Knoten im Workflow-Graphen erreichbar ist: Zielknoten sind erreichbar,
    /// wenn mindestens eine ihrer eingehenden Kanten erreichbar ist (transitiv rückwärts bis zu
    /// einem reinen Quellknoten, siehe <see cref="EdgeExtensions.IsReachable(IEdge)"/>); reine
    /// Quellknoten (<c>init</c>) gelten stets als erreichbar. Der Codegen übergeht unerreichbare
    /// Task-Knoten.
    /// </summary>
    bool IsReachable();

}

/// <summary>Erweiterungsmethoden für <see cref="INodeSymbol"/>.</summary>
public static class NodeSymbolExtension {

    /// <summary>
    /// Bestimmt, ob der Knoten ein Verbindungspunkt seines Tasks ist — also einer der von außen
    /// ansprechbaren Knoten <c>init</c>, <c>exit</c> oder <c>end</c> (vgl.
    /// <see cref="ConnectionPointNodeSyntax"/>).
    /// </summary>
    /// <param name="node">Der zu prüfende Knoten.</param>
    public static bool IsConnectionPoint(this INodeSymbol node) {
        return node is IInitNodeSymbol || node is IExitNodeSymbol || node is IEndNodeSymbol;
    }

}

/// <summary>
/// Ein Knoten, der Ziel von Kanten sein kann — alle Knoten-Arten außer dem <c>init</c>-Knoten
/// (eingehende Kanten auf <c>init</c> sind unzulässig, Diagnose Nav0103).
/// </summary>
public interface ITargetNodeSymbol: INodeSymbol {

    /// <summary>Die eingehenden Kanten dieses Knotens, wie sie der Transitionsblock verdrahtet.</summary>
    IReadOnlyList<IEdge> Incomings { get; }

}

/// <summary>
/// Ein Knoten, von dem Kanten ausgehen können — <c>init</c>, <c>choice</c>, <c>dialog</c>,
/// <c>view</c> und <c>task</c>.
/// </summary>
public interface ISourceNodeSymbol: INodeSymbol {

    /// <summary>
    /// Die von diesem Knoten ausgehenden Kanten; die abgeleiteten Interfaces verengen den
    /// Kanten-Typ (Init-, Choice-, Trigger- bzw. Exit-Transitionen).
    /// </summary>
    IReadOnlyList<IEdge> Outgoings { get; }

}

/// <summary>
/// Symbol eines <c>init</c>-Knotens, z.B. <c>init Start;</c> — ein Einstiegspunkt des Tasks (siehe
/// <see cref="InitNodeDeclarationSyntax"/>) und reiner Quellknoten. Der deklarierte Symbol-Name ist
/// stets <c>Init</c> (<see cref="SyntaxFacts.InitKeywordAlt"/>); der optionale Bezeichner hinter
/// <c>init</c> ist ein Alias (<see cref="Alias"/>), dessen Name dann als effektiver
/// <see cref="ISymbol.Name"/> gilt.
/// </summary>
public interface IInitNodeSymbol: ISourceNodeSymbol {

    /// <summary>Die zugrunde liegende <c>init</c>-Deklaration.</summary>
    new InitNodeDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Der optionale Alias des Init-Knotens — der Bezeichner hinter <c>init</c> (z.B. <c>I1</c> in
    /// <c>init I1;</c>), oder <c>null</c>, wenn keiner vergeben ist. Ist ein Alias vorhanden,
    /// liefert <see cref="ISymbol.Name"/> dessen Namen.
    /// </summary>
    IInitNodeAliasSymbol? Alias { get; }

    /// <summary>Die ausgehenden Init-Transitionen (<c>init --&gt; …</c>).</summary>
    new IReadOnlyList<IInitTransition> Outgoings { get; }

}

/// <summary>
/// Symbol eines <c>exit</c>-Knotens, z.B. <c>exit Fertig;</c> — ein benannter Ausgang des Tasks
/// (siehe <see cref="ExitNodeDeclarationSyntax"/>) und reiner Zielknoten.
/// </summary>
public interface IExitNodeSymbol: ITargetNodeSymbol {

    /// <summary>Die zugrunde liegende <c>exit</c>-Deklaration.</summary>
    new ExitNodeDeclarationSyntax Syntax { get; }

}

/// <summary>
/// Symbol des <c>end</c>-Knotens (<c>end;</c>) — der reguläre Abschluss des Workflows (siehe
/// <see cref="EndNodeDeclarationSyntax"/>) und reiner Zielknoten; als Name dient das Schlüsselwort
/// selbst.
/// </summary>
public interface IEndNodeSymbol: ITargetNodeSymbol {

    /// <summary>Die zugrunde liegende <c>end</c>-Deklaration.</summary>
    new EndNodeDeclarationSyntax Syntax { get; }

}

/// <summary>
/// Symbol eines <c>choice</c>-Knotens, z.B. <c>choice C_Auswahl;</c> — ein Verzweigungsknoten, der
/// anhand von Bedingungen einen von mehreren Folgewegen wählt (siehe
/// <see cref="ChoiceNodeDeclarationSyntax"/>); Quelle wie Ziel von Kanten.
/// </summary>
public interface IChoiceNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    /// <summary>Die zugrunde liegende <c>choice</c>-Deklaration.</summary>
    new ChoiceNodeDeclarationSyntax Syntax { get; }

    /// <summary>Die ausgehenden Choice-Transitionen.</summary>
    new IReadOnlyList<IChoiceTransition> Outgoings { get; }

}

/// <summary>
/// Gemeinsamer Vertrag der GUI-Knoten (<see cref="IViewNodeSymbol"/>,
/// <see cref="IDialogNodeSymbol"/>): Knoten, die eine Oberfläche anzeigen; ihre ausgehenden Kanten
/// sind Trigger-Transitionen (<see cref="ITriggerTransition"/>).
/// </summary>
public interface IGuiNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    /// <summary>Die ausgehenden Trigger-Transitionen.</summary>
    new IReadOnlyList<ITriggerTransition> Outgoings { get; }

}

/// <summary>
/// Symbol eines <c>view</c>-Knotens, z.B. <c>view AuswahlDialog;</c> — ein GUI-Knoten, der eine
/// View (Ansicht) anzeigt (siehe <see cref="ViewNodeDeclarationSyntax"/>).
/// </summary>
public interface IViewNodeSymbol: IGuiNodeSymbol {

    /// <summary>Die zugrunde liegende <c>view</c>-Deklaration.</summary>
    new ViewNodeDeclarationSyntax Syntax { get; }

}

/// <summary>
/// Symbol eines <c>dialog</c>-Knotens, z.B. <c>dialog Rückfrage;</c> — ein GUI-Knoten, der einen
/// Dialog anzeigt (siehe <see cref="DialogNodeDeclarationSyntax"/>).
/// </summary>
public interface IDialogNodeSymbol: IGuiNodeSymbol {

    /// <summary>Die zugrunde liegende <c>dialog</c>-Deklaration.</summary>
    new DialogNodeDeclarationSyntax Syntax { get; }

}

/// <summary>
/// Symbol eines <c>task</c>-Knotens, z.B. <c>task Unteraufgabe A1;</c> — bindet einen anderen Task
/// als Knoten in den Workflow ein (siehe <see cref="TaskNodeDeclarationSyntax"/>). Quelle wie Ziel
/// von Kanten; die ausgehenden Kanten sind Exit-Transitionen, die an den Exit-Verbindungspunkten
/// des referenzierten Tasks ansetzen.
/// </summary>
public interface ITaskNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    /// <summary>Die zugrunde liegende <c>task</c>-Knoten-Deklaration.</summary>
    new TaskNodeDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Die über den Task-Namen aufgelöste Task-Deklaration — <c>null</c>, wenn kein Task dieses
    /// Namens bekannt ist.
    /// </summary>
    ITaskDeclarationSymbol? Declaration { get; }

    /// <summary>
    /// Der optionale Alias des Task-Knotens — der zweite Bezeichner hinter dem Task-Namen (z.B.
    /// <c>A1</c> in <c>task Unteraufgabe A1;</c>), oder <c>null</c>, wenn keiner vergeben ist. Ist
    /// ein Alias vorhanden, liefert <see cref="ISymbol.Name"/> dessen Namen.
    /// </summary>
    ITaskNodeAliasSymbol? Alias { get; }

    /// <summary>Die ausgehenden Exit-Transitionen (z.B. <c>Unteraufgabe:Fertig --&gt; end;</c>).</summary>
    new IReadOnlyList<IExitTransition> Outgoings { get; }

}
