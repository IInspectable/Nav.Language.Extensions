#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("taskref Task { };")]
public partial class TaskDeclarationSyntax: MemberDeclarationSyntax {

    internal TaskDeclarationSyntax(TextExtent extent,
                                   CodeNamespaceDeclarationSyntax? codeNamespaceDeclaration,
                                   CodeNotImplementedDeclarationSyntax? codeNotImplementedDeclaration,
                                   CodeResultDeclarationSyntax? codeResultDeclaration,
                                   IReadOnlyList<ConnectionPointNodeSyntax> connectionPoints)
        : base(extent) {

        AddChildNode(CodeNamespaceDeclaration      = codeNamespaceDeclaration);
        AddChildNode(CodeNotImplementedDeclaration = codeNotImplementedDeclaration);
        AddChildNode(CodeResultDeclaration         = codeResultDeclaration);
        AddChildNodes(ConnectionPoints             = connectionPoints);
    }

    public SyntaxToken TaskrefKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskrefKeyword);
    public SyntaxToken Identifier     => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    public SyntaxToken OpenBrace      => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBrace);
    public SyntaxToken CloseBrace     => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBrace);

    public CodeNamespaceDeclarationSyntax? CodeNamespaceDeclaration { get; }

    public CodeNotImplementedDeclarationSyntax? CodeNotImplementedDeclaration { get; }

    public CodeResultDeclarationSyntax? CodeResultDeclaration { get; }

    public IReadOnlyList<ConnectionPointNodeSyntax> ConnectionPoints { get; }

    public IEnumerable<InitNodeDeclarationSyntax> InitNodes() {
        return ConnectionPoints.OfType<InitNodeDeclarationSyntax>();
    }

    public IEnumerable<ExitNodeDeclarationSyntax> ExitNodes() {
        return ConnectionPoints.OfType<ExitNodeDeclarationSyntax>();
    }

    public IEnumerable<EndNodeDeclarationSyntax> EndNodes() {
        return ConnectionPoints.OfType<EndNodeDeclarationSyntax>();
    }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}