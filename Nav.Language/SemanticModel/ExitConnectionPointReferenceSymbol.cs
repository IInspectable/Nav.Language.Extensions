namespace Pharmatechnik.Nav.Language;

sealed partial class ExitConnectionPointReferenceSymbol: Symbol, IExitConnectionPointReferenceSymbol {

    public ExitConnectionPointReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, IExitConnectionPointSymbol? connectionPoint)
        : base(name, location) {
        SyntaxTree  = syntaxTree;
        Declaration = connectionPoint;
    }

    public override SyntaxTree SyntaxTree { get; }

    public IExitConnectionPointSymbol? Declaration { get; }

    // Wird definitiv im Ctor der ExitTransition gesetzt — in der "freien Wildbahn" darf der
    // Null-Fall nicht auftreten.
    public IExitTransition ExitTransition { get; internal set; } = null!;

}
