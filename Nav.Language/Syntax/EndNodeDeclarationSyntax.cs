using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Deklaration des <c>end</c>-Knotens (<c>end;</c>) — der reguläre Abschluss des Workflows und zugleich
/// Verbindungspunkt (siehe <see cref="ConnectionPointNodeSyntax"/>). Der Knoten ist namenlos; im
/// Semantikmodell dient das Schlüsselwort selbst als Name.
/// </summary>
[Serializable]
[SampleSyntax("end;")]
public partial class EndNodeDeclarationSyntax: ConnectionPointNodeSyntax {

    internal EndNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>end</c>.</summary>
    public SyntaxToken EndKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.EndKeyword);

}