using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Ein einfacher (nicht-generischer) Typname wie <c>int</c> oder <c>MyClass</c> in einer
/// Code-Annotation, optional als Nullable-Typ mit angehängtem <c>?</c> (<c>int?</c>) — die
/// Blatt-Form von <see cref="CodeTypeSyntax"/> und zugleich der Basistyp-Baustein für
/// <see cref="GenericTypeSyntax"/>-Argumente und <see cref="ArrayTypeSyntax"/>.
/// </summary>
[Serializable]
[SampleSyntax("int?")]
public partial class SimpleTypeSyntax: CodeTypeSyntax {

    internal SimpleTypeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Der Typname, oder ein fehlendes Token, wenn er im Quelltext fehlt.</summary>
    public SyntaxToken Identifier   => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    /// <summary>Das optionale <c>?</c>-Token des Nullable-Typs (<c>int?</c>), oder ein fehlendes Token bei nicht-nullable Typen.</summary>
    public SyntaxToken Questionmark => ChildTokens().FirstOrMissing(SyntaxTokenType.Questionmark);

}