#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("task Task { };")]
public partial class TaskDefinitionSyntax: MemberDeclarationSyntax {

    internal TaskDefinitionSyntax(TextExtent extent,
                                  CodeDeclarationSyntax? codeDeclaration,
                                  CodeBaseDeclarationSyntax? codeBaseDeclaration,
                                  CodeGenerateToDeclarationSyntax? codeGenerateToDeclaration,
                                  CodeParamsDeclarationSyntax? codeParamsDeclaration,
                                  CodeResultDeclarationSyntax? codeResultDeclaration,
                                  NodeDeclarationBlockSyntax nodeDeclarationBlock,
                                  TransitionDefinitionBlockSyntax transitionDefinitionBlock)
        : base(extent) {

        AddChildNode(CodeDeclaration           = codeDeclaration);
        AddChildNode(CodeBaseDeclaration       = codeBaseDeclaration);
        AddChildNode(CodeGenerateToDeclaration = codeGenerateToDeclaration);
        AddChildNode(CodeParamsDeclaration     = codeParamsDeclaration);
        AddChildNode(CodeResultDeclaration     = codeResultDeclaration);
        AddChildNode(NodeDeclarationBlock      = nodeDeclarationBlock);
        AddChildNode(TransitionDefinitionBlock = transitionDefinitionBlock);
    }

    public SyntaxToken TaskKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskKeyword);
    public SyntaxToken Identifier  => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    public SyntaxToken OpenBrace   => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBrace);
    public SyntaxToken CloseBrace  => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBrace);

    public CodeDeclarationSyntax? CodeDeclaration { get; }

    public CodeBaseDeclarationSyntax? CodeBaseDeclaration { get; }

    public CodeGenerateToDeclarationSyntax? CodeGenerateToDeclaration { get; }

    public CodeParamsDeclarationSyntax? CodeParamsDeclaration { get; }

    public CodeResultDeclarationSyntax? CodeResultDeclaration { get; }

    // Bewusst nicht-nullable (das frühere [CanBeNull] war zu breit): der Parser erzeugt die beiden
    // Blöcke immer — ein leerer Block hat lediglich den Extent TextExtent.Missing.
    public NodeDeclarationBlockSyntax NodeDeclarationBlock { get; }

    public TransitionDefinitionBlockSyntax TransitionDefinitionBlock { get; }

    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}