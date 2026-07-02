using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Internal;

namespace Pharmatechnik.Nav.Language; 

[SuppressCodeSanityCheck("NodeReferenceSymbol darf hier unversiegelt bestehen.")]
partial class NodeReferenceSymbol: Symbol, INodeReferenceSymbol {

    // ReSharper disable once NotNullMemberIsNotInitialized Transition wird im Ctor der Transition während der Initialisierung gesetzt 
    // In der "freien" Wildbahn" darf hingegen der Null Fall nicht auftreten
    public NodeReferenceSymbol(SyntaxTree syntaxTree, string name, Location location, INodeSymbol declaration, NodeReferenceType nodeReferenceType)
        : base(name, location) {
        SyntaxTree        = syntaxTree;
        NodeReferenceType = nodeReferenceType;
        Declaration       = declaration;
    }

    public override SyntaxTree SyntaxTree { get; }

    [CanBeNull]
    public INodeSymbol Declaration { get; }

    public NodeReferenceType NodeReferenceType { get; }
    public IEdge             Edge              { get; internal set; }

}