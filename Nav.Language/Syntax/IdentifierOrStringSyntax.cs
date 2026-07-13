using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Ein Wert, der wahlweise als nackter Bezeichner oder als String-Literal geschrieben werden kann
/// (Grammatikregel <c>identifierOrString</c>), z.B. <c>[namespaceprefix My.Namespace]</c> oder
/// <c>[namespaceprefix "My.Namespace"]</c>. Verwendet in <c>[namespaceprefix …]</c>/<c>[using …]</c>
/// (<see cref="CodeNamespaceDeclarationSyntax"/>, <see cref="CodeUsingDeclarationSyntax"/>) sowie in
/// <c>if</c>- und <c>do</c>-Klauseln (<see cref="IfConditionClauseSyntax"/>, <see cref="DoClauseSyntax"/>).
/// Die beiden Ausprägungen sind <see cref="IdentifierSyntax"/> und <see cref="StringLiteralSyntax"/>.
/// </summary>
[Serializable]
public abstract class IdentifierOrStringSyntax: SyntaxNode {

    internal IdentifierOrStringSyntax(TextExtent extent)
        : base(extent) {
    }

    // Bewusst nicht-nullable (das frühere [CanBeNull] war zu breit): beide Ausprägungen leiten den Text
    // aus SyntaxToken.ToString() ab, das für fehlende Token String.Empty liefert — nie null.
    /// <summary>
    /// Der Nutztext des Werts: beim Bezeichner dessen Text, beim String-Literal der Inhalt ohne die
    /// umschließenden Anführungszeichen. Nie <c>null</c> — für fehlende Token <see cref="String.Empty"/>.
    /// </summary>
    public abstract string Text { get; }

    /// <summary>
    /// Die Position des Nutztexts (<see cref="Text"/>) im Quelltext — beim String-Literal ohne die
    /// umschließenden Anführungszeichen.
    /// </summary>
    public abstract Location GetTextLocation();

}

/// <summary>
/// Die Bezeichner-Ausprägung von <see cref="IdentifierOrStringSyntax"/>: ein nackter Name ohne
/// Anführungszeichen, z.B. das Signal in <c>on Speichern</c>.
/// </summary>
[Serializable]
[SampleSyntax("Identifier")]
public sealed partial class IdentifierSyntax: IdentifierOrStringSyntax {

    internal IdentifierSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Der Text des Bezeichners (<see cref="Identifier"/>).</summary>
    public override string Text => Identifier.ToString();

    /// <summary>Die Position des Bezeichners — identisch mit <see cref="SyntaxNode.GetLocation"/>.</summary>
    public override Location GetTextLocation() {
        return GetLocation();
    }

    /// <summary>Das Bezeichner-Token.</summary>
    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}

/// <summary>
/// Die String-Literal-Ausprägung von <see cref="IdentifierOrStringSyntax"/>: ein Wert in doppelten
/// Anführungszeichen, z.B. <c>"My.Namespace"</c> in <c>[namespaceprefix "My.Namespace"]</c>.
/// </summary>
[Serializable]
[SampleSyntax("\"StringLiteral\"")]
public sealed partial class StringLiteralSyntax: IdentifierOrStringSyntax {

    internal StringLiteralSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Der Inhalt des Literals ohne die umschließenden Anführungszeichen.</summary>
    public override string Text => StringLiteral.ToString().Trim('"');

    /// <summary>
    /// Die Position des Inhalts: der Bereich zwischen den Anführungszeichen — bei leerem oder
    /// fehlendem Extent die Knoten-Position (<see cref="SyntaxNode.GetLocation"/>).
    /// </summary>
    public override Location GetTextLocation() {

        if (Extent.IsEmpty || Extent.IsMissing) {
            return GetLocation();
        }

        var extent = TextExtent.FromBounds(Extent.Start + 1, Extent.End - 1);

        return SyntaxTree.SourceText.GetLocation(extent);
    }

    /// <summary>Das String-Literal-Token (inklusive Anführungszeichen).</summary>
    public SyntaxToken StringLiteral => ChildTokens().FirstOrMissing(SyntaxTokenType.StringLiteral);

}