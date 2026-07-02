#nullable enable

namespace Pharmatechnik.Nav.Language;

public interface INodeReferenceSymbol<out T>: INodeReferenceSymbol where T : INodeSymbol {

    new T? Declaration { get; }

}

public interface IInitNodeReferenceSymbol: INodeReferenceSymbol<IInitNodeSymbol> {

}

public interface IChoiceNodeReferenceSymbol: INodeReferenceSymbol<IChoiceNodeSymbol> {

}

public interface IGuiNodeReferenceSymbol: INodeReferenceSymbol<IGuiNodeSymbol> {

}

public interface ITaskNodeReferenceSymbol: INodeReferenceSymbol<ITaskNodeSymbol> {

}

public interface IExitNodeReferenceSymbol: INodeReferenceSymbol<IExitNodeSymbol> {

}

public interface IEndNodeReferenceSymbol: INodeReferenceSymbol<IEndNodeSymbol> {

}
