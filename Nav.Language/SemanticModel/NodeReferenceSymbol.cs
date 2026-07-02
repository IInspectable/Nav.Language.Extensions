#nullable enable

using Pharmatechnik.Nav.Language.Internal;

namespace Pharmatechnik.Nav.Language;

[SuppressCodeSanityCheck("NodeReferenceSymbol darf hier unversiegelt bestehen.")]
partial class NodeReferenceSymbol: Symbol, INodeReferenceSymbol {

    public NodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, INodeSymbol? declaration, NodeReferenceType nodeReferenceType)
        : base(name, location) {
        SyntaxTree        = syntaxTree;
        NodeReferenceType = nodeReferenceType;
        Declaration       = declaration;
    }

    public override SyntaxTree SyntaxTree { get; }

    public INodeSymbol? Declaration { get; }

    public NodeReferenceType NodeReferenceType { get; }

    // Wird im Ctor der Edge während der Initialisierung gesetzt — in der "freien Wildbahn" darf
    // der Null-Fall nicht auftreten.
    public IEdge Edge { get; internal set; } = null!;

}
