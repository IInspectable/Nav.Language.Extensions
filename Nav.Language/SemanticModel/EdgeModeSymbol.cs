namespace Pharmatechnik.Nav.Language;

sealed partial class EdgeModeSymbol: Symbol, IEdgeModeSymbol {

    public EdgeModeSymbol(SyntaxTree syntaxTree, string name, Location location, EdgeMode edgeMode)
        : base(name, location) {

        SyntaxTree = syntaxTree;
        EdgeMode   = edgeMode;
    }

    public override SyntaxTree SyntaxTree { get; }

    public EdgeMode EdgeMode { get; }

    // Wird im Ctor der Edge während der Initialisierung gesetzt — in der "freien Wildbahn" darf
    // der Null-Fall nicht auftreten.
    public IEdge Edge { get; internal set; } = null!;

    public string DisplayName {
        get {
            // TODO Evtl. Strings wo anders hinpacken
            switch (EdgeMode) {
                case EdgeMode.Modal:
                    return "Modal Edge";
                case EdgeMode.NonModal:
                    return "NonModal Edge";
                case EdgeMode.Goto:
                    return "GoTo Edge";
                default:
                    return Name;
            }
        }
    }

    public string Verb {
        get {
            // TODO Evtl. Strings wo anders hinpacken
            switch (EdgeMode) {

                case EdgeMode.Modal:
                    return "modal";
                case EdgeMode.NonModal:
                    return "non-modal";
                case EdgeMode.Goto:
                    return "go to";
                default:
                    return "";
            }
        }
    }

}
