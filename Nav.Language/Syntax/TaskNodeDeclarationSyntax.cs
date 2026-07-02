#nullable enable

using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

[Serializable]
[SampleSyntax("task Identifier Alias [donotinject] [abstractmethod];")]
public partial class TaskNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal TaskNodeDeclarationSyntax(TextExtent extent,
                                       CodeDoNotInjectDeclarationSyntax? codeDoNotInjectDeclaration,
                                       CodeAbstractMethodDeclarationSyntax? codeAbstractMethodDeclaration)
        : base(extent) {

        AddChildNode(CodeDoNotInjectDeclaration    = codeDoNotInjectDeclaration);
        AddChildNode(CodeAbstractMethodDeclaration = codeAbstractMethodDeclaration);
    }

    public SyntaxToken TaskKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskKeyword);

    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    [SuppressCodeSanityCheck("Der Name IdentifierAlias ist hier ausdrücklich gewollt.")]
    public SyntaxToken IdentifierAlias => Identifier.NextToken(SyntaxTokenType.Identifier);

    public CodeDoNotInjectDeclarationSyntax? CodeDoNotInjectDeclaration { get; }

    public CodeAbstractMethodDeclarationSyntax? CodeAbstractMethodDeclaration { get; }

}
