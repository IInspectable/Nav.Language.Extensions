namespace Pharmatechnik.Nav.Language; 

sealed partial class EdgeModeSymbol: Symbol, IEdgeModeSymbol {

    // ReSharper disable once NotNullMemberIsNotInitialized Transition wird im Ctor der Transition während der Initialisierung gesetzt 
    // In der "freien" Wildbahn" darf hingegen der Null Fall nicht auftreten
    public EdgeModeSymbol(SyntaxTree syntaxTree, string name, Location location, EdgeMode edgeMode)
        : base(name, location) {

        SyntaxTree = syntaxTree;
        EdgeMode   = edgeMode;
    }

    public override SyntaxTree SyntaxTree { get; }

    public EdgeMode EdgeMode { get; }
    public IEdge    Edge     { get; set; }

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