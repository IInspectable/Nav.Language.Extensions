#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("[using Namespace]")]
public sealed partial class CodeUsingDeclarationSyntax: CodeSyntax {

    internal CodeUsingDeclarationSyntax(TextExtent extent, IdentifierOrStringSyntax? namespaceSyntax): base(extent) {
        AddChildNode(Namespace = namespaceSyntax);
    }

    public SyntaxToken UsingKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.UsingKeyword);

    public IdentifierOrStringSyntax? Namespace { get; }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}