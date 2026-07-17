using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Deklaration eines <c>task</c>-Knotens, z.B. <c>task Unteraufgabe;</c> — bindet einen anderen Task
/// als Knoten in den Workflow ein. Ein optionaler zweiter Bezeichner vergibt einen Alias
/// (<see cref="IdentifierAlias"/>), unter dessen Namen der Knoten dann angesprochen wird. Als
/// Code-Annotationen sind <c>[donotinject]</c> und <c>[abstractmethod]</c> zulässig (Autorität:
/// <see cref="CodeBlockFacts"/>).
/// </summary>
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

    /// <summary>Das Schlüsselwort <c>task</c>.</summary>
    public SyntaxToken TaskKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskKeyword);

    /// <summary>Der Name des referenzierten Tasks.</summary>
    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    /// <summary>
    /// Der optionale Alias des Task-Knotens — das zweite Identifier-Token hinter dem Task-Namen,
    /// ein Missing-Token (<see cref="SyntaxToken.IsMissing"/>), wenn kein Alias vergeben ist.
    /// Ist ein Alias vorhanden, trägt der Knoten dessen Namen statt <see cref="Identifier"/>.
    /// </summary>
    [SuppressCodeSanityCheck("Der Name IdentifierAlias ist hier ausdrücklich gewollt.")]
    public SyntaxToken IdentifierAlias => Identifier.NextToken(SyntaxTokenType.Identifier);

    /// <summary>Die optionale <c>[donotinject]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeDoNotInjectDeclarationSyntax? CodeDoNotInjectDeclaration { get; }

    /// <summary>Die optionale <c>[abstractmethod]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeAbstractMethodDeclarationSyntax? CodeAbstractMethodDeclaration { get; }

}
