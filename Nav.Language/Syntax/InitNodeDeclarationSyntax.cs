using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("init Identifier [abstractmethod] [params T1 param1, T2<T3, T4<T5>> param2, T6[][] param3] do Instruction;")]
public partial class InitNodeDeclarationSyntax: ConnectionPointNodeSyntax {

    internal InitNodeDeclarationSyntax(TextExtent extent,
                                       CodeAbstractMethodDeclarationSyntax? codeAbstractMethodDeclaration,
                                       CodeParamsDeclarationSyntax? codeParamsDeclaration,
                                       DoClauseSyntax? doClause)
        : base(extent) {

        AddChildNode(CodeAbstractMethodDeclaration = codeAbstractMethodDeclaration);
        AddChildNode(CodeParamsDeclaration         = codeParamsDeclaration);
        AddChildNode(DoClause                      = doClause);
    }

    public SyntaxToken InitKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.InitKeyword);

    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    public CodeParamsDeclarationSyntax? CodeParamsDeclaration { get; }

    public CodeAbstractMethodDeclarationSyntax? CodeAbstractMethodDeclaration { get; }

    public DoClauseSyntax? DoClause { get; }

}