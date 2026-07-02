using System.Collections.Generic;

using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

public interface INodeSymbol: ISymbol {

    [NotNull]
    NodeDeclarationSyntax Syntax { get; }

    [NotNull]
    ITaskDefinitionSymbol ContainingTask { get; }

    [NotNull]
    IReadOnlyList<INodeReferenceSymbol> References { get; }

    IEnumerable<ISymbol> SymbolsAndSelf();

    bool IsReachable();

}

public static class NodeSymbolExtension {

    public static bool IsConnectionPoint(this INodeSymbol node) {
        return node is IInitNodeSymbol || node is IExitNodeSymbol || node is IEndNodeSymbol;
    }

}

public interface ITargetNodeSymbol: INodeSymbol {

    [NotNull]
    IReadOnlyList<IEdge> Incomings { get; }

}

public interface ISourceNodeSymbol: INodeSymbol {

    [NotNull]
    IReadOnlyList<IEdge> Outgoings { get; }

}

public interface IInitNodeSymbol: ISourceNodeSymbol {

    [NotNull]
    new InitNodeDeclarationSyntax Syntax { get; }

    [CanBeNull]
    IInitNodeAliasSymbol Alias { get; }

    [NotNull]
    new IReadOnlyList<IInitTransition> Outgoings { get; }

}

public interface IExitNodeSymbol: ITargetNodeSymbol {

    [NotNull]
    new ExitNodeDeclarationSyntax Syntax { get; }

}

public interface IEndNodeSymbol: ITargetNodeSymbol {

    [NotNull]
    new EndNodeDeclarationSyntax Syntax { get; }

}

public interface IChoiceNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    [NotNull]
    new ChoiceNodeDeclarationSyntax Syntax { get; }

    [NotNull]
    new IReadOnlyList<IChoiceTransition> Outgoings { get; }

}

public interface IGuiNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    [NotNull]
    new IReadOnlyList<ITriggerTransition> Outgoings { get; }

}

public interface IViewNodeSymbol: IGuiNodeSymbol {

    [NotNull]
    new ViewNodeDeclarationSyntax Syntax { get; }

}

public interface IDialogNodeSymbol: IGuiNodeSymbol {

    [NotNull]
    new DialogNodeDeclarationSyntax Syntax { get; }

}

public interface ITaskNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    [NotNull]
    new TaskNodeDeclarationSyntax Syntax { get; }

    [CanBeNull]
    ITaskDeclarationSymbol Declaration { get; }

    [CanBeNull]
    ITaskNodeAliasSymbol Alias { get; }

    [NotNull]
    new IReadOnlyList<IExitTransition> Outgoings { get; }

}