namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Basisklasse der typisierten Knoten-Referenzen: hält die aufgelöste Deklaration zusätzlich als
/// <typeparamref name="T"/> (die <c>new</c>-Property verdeckt die untypisierte Basis-Property,
/// beide tragen denselben Wert).
/// </summary>
/// <typeparam name="T">Die Knoten-Art, auf die sich die Referenz auflöst.</typeparam>
abstract class NodeReferenceSymbol<T>: NodeReferenceSymbol, INodeReferenceSymbol<T> where T : INodeSymbol {

    protected NodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, T? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
        Declaration = declaration;
    }

    /// <inheritdoc/>
    public new T? Declaration { get; }

}

/// <summary>Implementierung von <see cref="IInitNodeReferenceSymbol"/>.</summary>
sealed partial class InitNodeReferenceSymbol: NodeReferenceSymbol<IInitNodeSymbol>, IInitNodeReferenceSymbol {

    public InitNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IInitNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

/// <summary>Implementierung von <see cref="IChoiceNodeReferenceSymbol"/>.</summary>
sealed partial class ChoiceNodeReferenceSymbol: NodeReferenceSymbol<IChoiceNodeSymbol>, IChoiceNodeReferenceSymbol {

    public ChoiceNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IChoiceNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

/// <summary>Implementierung von <see cref="IGuiNodeReferenceSymbol"/>.</summary>
sealed partial class GuiNodeReferenceSymbol: NodeReferenceSymbol<IGuiNodeSymbol>, IGuiNodeReferenceSymbol {

    public GuiNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IGuiNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

/// <summary>Implementierung von <see cref="ITaskNodeReferenceSymbol"/>.</summary>
sealed partial class TaskNodeReferenceSymbol: NodeReferenceSymbol<ITaskNodeSymbol>, ITaskNodeReferenceSymbol {

    public TaskNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, ITaskNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

/// <summary>Implementierung von <see cref="IExitNodeReferenceSymbol"/>.</summary>
sealed partial class ExitNodeReferenceSymbol: NodeReferenceSymbol<IExitNodeSymbol>, IExitNodeReferenceSymbol {

    public ExitNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IExitNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

/// <summary>Implementierung von <see cref="IEndNodeReferenceSymbol"/>.</summary>
sealed partial class EndNodeReferenceSymbol: NodeReferenceSymbol<IEndNodeSymbol>, IEndNodeReferenceSymbol {

    public EndNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IEndNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}
