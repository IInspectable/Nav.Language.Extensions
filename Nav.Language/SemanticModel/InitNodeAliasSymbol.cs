namespace Pharmatechnik.Nav.Language; 

sealed partial class InitNodeAliasSymbol: Symbol, IInitNodeAliasSymbol {

    // ReSharper disable once NotNullMemberIsNotInitialized Wird im Ctor der InitNode festgelegt
    public InitNodeAliasSymbol(SyntaxTree syntaxTree, string name, Location location): base(name, location) {
        SyntaxTree = syntaxTree;
    }

    public override SyntaxTree SyntaxTree { get; }

    public IInitNodeSymbol InitNode { get; set; }

}