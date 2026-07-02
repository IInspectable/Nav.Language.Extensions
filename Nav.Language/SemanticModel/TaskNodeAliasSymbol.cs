namespace Pharmatechnik.Nav.Language; 

sealed partial class TaskNodeAliasSymbol: Symbol, ITaskNodeAliasSymbol {

    // ReSharper disable once NotNullMemberIsNotInitialized Wird im Builder festgelegt
    public TaskNodeAliasSymbol(SyntaxTree syntaxTree, string name, Location location): base(name, location) {
        SyntaxTree = syntaxTree;
    }

    public override SyntaxTree SyntaxTree { get; }

    public ITaskNodeSymbol TaskNode { get; internal set; }

}