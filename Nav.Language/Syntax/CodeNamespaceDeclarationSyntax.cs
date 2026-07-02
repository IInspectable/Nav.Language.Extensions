#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[namespaceprefix Namespace]")]
public sealed partial class CodeNamespaceDeclarationSyntax: CodeSyntax {

    internal CodeNamespaceDeclarationSyntax(TextExtent extent, IdentifierOrStringSyntax? namespaceSyntax)
        : base(extent) {

        AddChildNode(Namespace = namespaceSyntax);
    }

    public SyntaxToken NamespaceprefixKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.NamespaceprefixKeyword);

    public IdentifierOrStringSyntax? Namespace { get; }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}