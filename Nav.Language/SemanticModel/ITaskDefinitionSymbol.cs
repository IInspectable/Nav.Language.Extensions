#region Using Directives

using System.Collections.Generic;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language; 

public interface ITaskDefinitionSymbol: ISymbol {

    /// <summary>
    /// Sollte in der Praxis nie null sein.
    /// </summary>
    [CanBeNull]
    CodeGenerationUnit CodeGenerationUnit { get; }

    [NotNull]
    string CodeNamespace {get;}

    [NotNull]
    TaskDefinitionSyntax Syntax { get; }

    [CanBeNull]
    ITaskDeclarationSymbol AsTaskDeclaration { get; }

    [NotNull]
    IReadOnlySymbolCollection<INodeSymbol> NodeDeclarations { get; }

    [NotNull]
    IReadOnlyList<IInitTransition> InitTransitions { get; }

    [NotNull]
    IReadOnlyList<IChoiceTransition> ChoiceTransitions { get; }

    [NotNull]
    IReadOnlyList<ITriggerTransition> TriggerTransitions { get; }

    [NotNull]
    IReadOnlyList<IExitTransition> ExitTransitions { get; }

    IEnumerable<IEdge> Edges();

}