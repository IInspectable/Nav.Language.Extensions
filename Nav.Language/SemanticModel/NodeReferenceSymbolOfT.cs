namespace Pharmatechnik.Nav.Language;

abstract class NodeReferenceSymbol<T>: NodeReferenceSymbol, INodeReferenceSymbol<T> where T : INodeSymbol {

    protected NodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, T? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
        Declaration = declaration;
    }

    public new T? Declaration { get; }

}

sealed partial class InitNodeReferenceSymbol: NodeReferenceSymbol<IInitNodeSymbol>, IInitNodeReferenceSymbol {

    public InitNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IInitNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

sealed partial class ChoiceNodeReferenceSymbol: NodeReferenceSymbol<IChoiceNodeSymbol>, IChoiceNodeReferenceSymbol {

    public ChoiceNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IChoiceNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

sealed partial class GuiNodeReferenceSymbol: NodeReferenceSymbol<IGuiNodeSymbol>, IGuiNodeReferenceSymbol {

    public GuiNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IGuiNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

sealed partial class TaskNodeReferenceSymbol: NodeReferenceSymbol<ITaskNodeSymbol>, ITaskNodeReferenceSymbol {

    public TaskNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, ITaskNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

sealed partial class ExitNodeReferenceSymbol: NodeReferenceSymbol<IExitNodeSymbol>, IExitNodeReferenceSymbol {

    public ExitNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IExitNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}

sealed partial class EndNodeReferenceSymbol: NodeReferenceSymbol<IEndNodeSymbol>, IEndNodeReferenceSymbol {

    public EndNodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IEndNodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(syntaxTree, name, location, declaration, nodeReferenceType) {
    }

}
