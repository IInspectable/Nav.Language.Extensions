namespace Pharmatechnik.Nav.Language;

sealed partial class InitNodeAliasSymbol: Symbol, IInitNodeAliasSymbol {

    public InitNodeAliasSymbol(SyntaxTree syntaxTree, string name, Location location): base(name, location) {
        SyntaxTree = syntaxTree;
    }

    public override SyntaxTree SyntaxTree { get; }

    // Wird im Ctor der InitNode festgelegt — in der "freien Wildbahn" darf der Null-Fall nicht auftreten.
    public IInitNodeSymbol InitNode { get; internal set; } = null!;

}
