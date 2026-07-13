using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Definition eines Tasks (Workflows), z.B. <c>task Name { … }</c> — das zentrale Konstrukt der
/// Nav-Sprache, aus dem der C#-Code generiert wird. Nach <c>task Name</c> folgen optionale
/// Code-Deklarationen (<c>[code …]</c>, <c>[base …]</c>, <c>[generateto …]</c>, <c>[params …]</c>,
/// <c>[result …]</c>), dann der Rumpf aus Knoten-Deklarationsblock
/// (<see cref="NodeDeclarationBlock"/>) und Transitions-Block (<see cref="TransitionDefinitionBlock"/>).
/// Semantisches Gegenstück ist <see cref="ITaskDefinitionSymbol"/>.
/// </summary>
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

    /// <summary>Das Schlüsselwort <c>task</c>.</summary>
    public SyntaxToken TaskKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskKeyword);
    /// <summary>Der Name des Tasks.</summary>
    public SyntaxToken Identifier  => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    /// <summary>Die öffnende geschweifte Klammer <c>{</c> des Task-Rumpfs.</summary>
    public SyntaxToken OpenBrace   => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBrace);
    /// <summary>Die schließende geschweifte Klammer <c>}</c> des Task-Rumpfs.</summary>
    public SyntaxToken CloseBrace  => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBrace);

    /// <summary>Die optionale <c>[code …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeDeclarationSyntax? CodeDeclaration { get; }

    /// <summary>Die optionale <c>[base …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeBaseDeclarationSyntax? CodeBaseDeclaration { get; }

    /// <summary>Die optionale <c>[generateto …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeGenerateToDeclarationSyntax? CodeGenerateToDeclaration { get; }

    /// <summary>Die optionale <c>[params …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeParamsDeclarationSyntax? CodeParamsDeclaration { get; }

    /// <summary>Die optionale <c>[result …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeResultDeclarationSyntax? CodeResultDeclaration { get; }

    // Bewusst nicht-nullable (das frühere [CanBeNull] war zu breit): der Parser erzeugt die beiden
    // Blöcke immer — ein leerer Block hat lediglich den Extent TextExtent.Missing.
    /// <summary>
    /// Der Knoten-Deklarationsblock des Tasks. Nie <c>null</c>: der Parser erzeugt den Block immer —
    /// ein leerer Block hat lediglich den Extent <see cref="Text.TextExtent.Missing"/>.
    /// </summary>
    public NodeDeclarationBlockSyntax NodeDeclarationBlock { get; }

    /// <summary>
    /// Der Transitions-Block des Tasks. Nie <c>null</c>: der Parser erzeugt den Block immer —
    /// ein leerer Block hat lediglich den Extent <see cref="Text.TextExtent.Missing"/>.
    /// </summary>
    public TransitionDefinitionBlockSyntax TransitionDefinitionBlock { get; }

    /// <summary>Eine Task-Definition enthält nie ihresgleichen — beschleunigt <see cref="SyntaxNode.DescendantNodes{T}()"/>.</summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}