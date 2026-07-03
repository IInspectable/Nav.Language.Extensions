namespace Pharmatechnik.Nav.Language;

public interface IEdgeModeSymbol: ISymbol {

    EdgeMode EdgeMode { get; }

    IEdge Edge { get; }

    string DisplayName { get; }
    string Verb        { get; }

}
