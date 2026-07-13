using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Deklaration eines <c>exit</c>-Knotens, z.B. <c>exit Fertig;</c> — ein benannter Ausgang des Tasks
/// und zugleich Verbindungspunkt (von außen referenzierbar, siehe
/// <see cref="ConnectionPointNodeSyntax"/>).
/// </summary>
[Serializable]
[SampleSyntax("exit Identifier;")]
public partial class ExitNodeDeclarationSyntax: ConnectionPointNodeSyntax {

    internal ExitNodeDeclarationSyntax(TextExtent extent)
        : base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>exit</c>.</summary>
    public SyntaxToken ExitKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ExitKeyword);
    /// <summary>Der Name des Exit-Knotens.</summary>
    public SyntaxToken Identifier  => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}