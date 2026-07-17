using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Deklaration eines <c>view</c>-Knotens, z.B. <c>view AuswahlDialog;</c> — ein GUI-Knoten, der eine
/// View (Ansicht) anzeigt. Wie der <c>dialog</c>-Knoten (<see cref="DialogNodeDeclarationSyntax"/>)
/// hat er im Transitionsblock Trigger-Transitionen als ausgehende Kanten; Code-Annotationen sind hier
/// nicht zulässig.
/// </summary>
[Serializable]
[SampleSyntax("view Identifier;")]
public partial class ViewNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal ViewNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>view</c>.</summary>
    public SyntaxToken ViewKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ViewKeyword);
    /// <summary>Der Name des View-Knotens.</summary>
    public SyntaxToken Identifier  => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}