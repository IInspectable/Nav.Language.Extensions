#nullable enable

namespace Pharmatechnik.Nav.Language;

sealed partial class TaskNodeAliasSymbol: Symbol, ITaskNodeAliasSymbol {

    public TaskNodeAliasSymbol(SyntaxTree syntaxTree, string name, Location location): base(name, location) {
        SyntaxTree = syntaxTree;
    }

    public override SyntaxTree SyntaxTree { get; }

    // Wird im Ctor der TaskNode festgelegt — in der "freien Wildbahn" darf der Null-Fall nicht auftreten.
    public ITaskNodeSymbol TaskNode { get; internal set; } = null!;

}
