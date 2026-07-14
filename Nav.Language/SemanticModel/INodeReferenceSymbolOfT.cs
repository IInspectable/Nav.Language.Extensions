namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Typisierte Knoten-Referenz: verengt <see cref="INodeReferenceSymbol.Declaration"/> auf die
/// konkrete Knoten-Art <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Die Knoten-Art, auf die sich die Referenz auflöst.</typeparam>
public interface INodeReferenceSymbol<out T>: INodeReferenceSymbol where T : INodeSymbol {

    /// <summary>
    /// Der deklarierte Knoten, auf den sich diese Referenz auflöst — typisiert; <c>null</c>, wenn
    /// der Name nicht auflösbar ist.
    /// </summary>
    new T? Declaration { get; }

}

/// <summary>Referenz auf einen <c>init</c>-Knoten (<see cref="IInitNodeSymbol"/>).</summary>
public interface IInitNodeReferenceSymbol: INodeReferenceSymbol<IInitNodeSymbol> {

}

/// <summary>Referenz auf einen <c>choice</c>-Knoten (<see cref="IChoiceNodeSymbol"/>).</summary>
public interface IChoiceNodeReferenceSymbol: INodeReferenceSymbol<IChoiceNodeSymbol> {

}

/// <summary>Referenz auf einen GUI-Knoten (<c>view</c>/<c>dialog</c>, <see cref="IGuiNodeSymbol"/>).</summary>
public interface IGuiNodeReferenceSymbol: INodeReferenceSymbol<IGuiNodeSymbol> {

}

/// <summary>Referenz auf einen <c>task</c>-Knoten (<see cref="ITaskNodeSymbol"/>).</summary>
public interface ITaskNodeReferenceSymbol: INodeReferenceSymbol<ITaskNodeSymbol> {

}

/// <summary>Referenz auf einen <c>exit</c>-Knoten (<see cref="IExitNodeSymbol"/>).</summary>
public interface IExitNodeReferenceSymbol: INodeReferenceSymbol<IExitNodeSymbol> {

}

/// <summary>Referenz auf den <c>end</c>-Knoten (<see cref="IEndNodeSymbol"/>).</summary>
public interface IEndNodeReferenceSymbol: INodeReferenceSymbol<IEndNodeSymbol> {

}
