using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

public interface INodeSymbol: ISymbol {

    NodeDeclarationSyntax Syntax { get; }

    ITaskDefinitionSymbol ContainingTask { get; }

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

    IReadOnlyList<IEdge> Incomings { get; }

}

public interface ISourceNodeSymbol: INodeSymbol {

    IReadOnlyList<IEdge> Outgoings { get; }

}

public interface IInitNodeSymbol: ISourceNodeSymbol {

    new InitNodeDeclarationSyntax Syntax { get; }

    IInitNodeAliasSymbol? Alias { get; }

    new IReadOnlyList<IInitTransition> Outgoings { get; }

}

public interface IExitNodeSymbol: ITargetNodeSymbol {

    new ExitNodeDeclarationSyntax Syntax { get; }

}

public interface IEndNodeSymbol: ITargetNodeSymbol {

    new EndNodeDeclarationSyntax Syntax { get; }

}

public interface IChoiceNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    new ChoiceNodeDeclarationSyntax Syntax { get; }

    new IReadOnlyList<IChoiceTransition> Outgoings { get; }

}

public interface IGuiNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    new IReadOnlyList<ITriggerTransition> Outgoings { get; }

}

public interface IViewNodeSymbol: IGuiNodeSymbol {

    new ViewNodeDeclarationSyntax Syntax { get; }

}

public interface IDialogNodeSymbol: IGuiNodeSymbol {

    new DialogNodeDeclarationSyntax Syntax { get; }

}

public interface ITaskNodeSymbol: ISourceNodeSymbol, ITargetNodeSymbol {

    new TaskNodeDeclarationSyntax Syntax { get; }

    ITaskDeclarationSymbol? Declaration { get; }

    ITaskNodeAliasSymbol? Alias { get; }

    new IReadOnlyList<IExitTransition> Outgoings { get; }

}
