using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("taskref \"file.nav\";")]
public sealed partial class IncludeDirectiveSyntax: MemberDeclarationSyntax {

    internal IncludeDirectiveSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken TaskrefKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskrefKeyword);
    public SyntaxToken StringLiteral  => ChildTokens().FirstOrMissing(SyntaxTokenType.StringLiteral);
    public SyntaxToken Semicolon      => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}