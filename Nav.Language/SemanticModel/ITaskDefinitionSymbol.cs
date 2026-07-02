#nullable enable

#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public interface ITaskDefinitionSymbol: ISymbol {

    /// <summary>
    /// Sollte in der Praxis nie null sein.
    /// </summary>
    CodeGenerationUnit? CodeGenerationUnit { get; }

    string CodeNamespace { get; }

    TaskDefinitionSyntax Syntax { get; }

    ITaskDeclarationSymbol? AsTaskDeclaration { get; }

    IReadOnlySymbolCollection<INodeSymbol> NodeDeclarations { get; }

    IReadOnlyList<IInitTransition> InitTransitions { get; }

    IReadOnlyList<IChoiceTransition> ChoiceTransitions { get; }

    IReadOnlyList<ITriggerTransition> TriggerTransitions { get; }

    IReadOnlyList<IExitTransition> ExitTransitions { get; }

    IEnumerable<IEdge> Edges();

}
