namespace Pharmatechnik.Nav.Language;

public interface IEdgeModeSymbol: ISymbol {

    EdgeMode EdgeMode { get; }

    IEdge Edge { get; }

    /// <summary>
    /// Ob dieser Kantenmodus zu einer Continuation (<c>o-^</c>/<c>--^</c>) gehört statt zu einer
    /// gewöhnlichen Transition. Die <see cref="EdgeMode"/>-Werte selbst sind identisch (Modal/Goto) —
    /// erst die tragende Kante unterscheidet Continuation von regulärer Transition.
    /// </summary>
    bool IsContinuation { get; }

    string DisplayName { get; }
    string Description { get; }

}
