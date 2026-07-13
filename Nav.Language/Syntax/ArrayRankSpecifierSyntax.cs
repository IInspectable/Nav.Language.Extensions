using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Eine Array-Dimensionsangabe <c>[]</c> — das leere Klammerpaar hinter dem Elementtyp eines
/// <see cref="ArrayTypeSyntax"/>, z.B. das <c>[]</c> in <c>string[]</c>. Ein Spezifizierer ist stets
/// ein leeres Paar <c>[</c> <c>]</c> (keine Kommas, keine Rang-Angaben); mehrere Spezifizierer
/// hintereinander ergeben verschachtelte Arrays (<c>string[][]</c>), siehe
/// <see cref="ArrayTypeSyntax.RankSpecifiers"/>.
/// </summary>
[Serializable]
[SampleSyntax("[]")]
public partial class ArrayRankSpecifierSyntax: SyntaxNode {

    internal ArrayRankSpecifierSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das öffnende <c>[</c>-Token, oder ein fehlendes Token, wenn es im Quelltext fehlt.</summary>
    public SyntaxToken OpenBracket  => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBracket);
    /// <summary>Das schließende <c>]</c>-Token, oder ein fehlendes Token, wenn es im Quelltext fehlt.</summary>
    public SyntaxToken CloseBracket => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBracket);

}