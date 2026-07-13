using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Deklaration eines <c>dialog</c>-Knotens, z.B. <c>dialog AuswahlDialog;</c> — ein GUI-Knoten, der
/// einen Dialog anzeigt. Wie der <c>view</c>-Knoten (<see cref="ViewNodeDeclarationSyntax"/>) hat er
/// im Transitionsblock Trigger-Transitionen als ausgehende Kanten; Code-Annotationen sind hier nicht
/// zulässig.
/// </summary>
[Serializable]
[SampleSyntax("dialog Identifier;")]
public partial class DialogNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal DialogNodeDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>dialog</c>.</summary>
    public SyntaxToken DialogKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.DialogKeyword);
    /// <summary>Der Name des Dialog-Knotens.</summary>
    public SyntaxToken Identifier    => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}