using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class IdentifierOrStringSyntax: SyntaxNode {

    internal IdentifierOrStringSyntax(TextExtent extent)
        : base(extent) {
    }

    // Bewusst nicht-nullable (das frühere [CanBeNull] war zu breit): beide Ausprägungen leiten den Text
    // aus SyntaxToken.ToString() ab, das für fehlende Token String.Empty liefert — nie null.
    public abstract string Text { get; }

    public abstract Location GetTextLocation();

}

[Serializable]
[SampleSyntax("Identifier")]
public sealed partial class IdentifierSyntax: IdentifierOrStringSyntax {

    internal IdentifierSyntax(TextExtent extent): base(extent) {
    }

    public override string Text => Identifier.ToString();

    public override Location GetTextLocation() {
        return GetLocation();
    }

    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}

[Serializable]
[SampleSyntax("\"StringLiteral\"")]
public sealed partial class StringLiteralSyntax: IdentifierOrStringSyntax {

    internal StringLiteralSyntax(TextExtent extent): base(extent) {
    }

    public override string Text => StringLiteral.ToString().Trim('"');

    public override Location GetTextLocation() {

        if (Extent.IsEmpty || Extent.IsMissing) {
            return GetLocation();
        }

        var extent = TextExtent.FromBounds(Extent.Start + 1, Extent.End - 1);

        return SyntaxTree.SourceText.GetLocation(extent);
    }

    public SyntaxToken StringLiteral => ChildTokens().FirstOrMissing(SyntaxTokenType.StringLiteral);

}