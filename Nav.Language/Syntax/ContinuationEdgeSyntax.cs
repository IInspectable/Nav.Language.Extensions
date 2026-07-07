using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Kante einer Continuation (<c>--^</c>/<c>o-^</c>, ab Sprachversion 2): der tragende GUI-Knoten zeigt
/// eine View <b>und</b> setzt den Übergang in einen Folge-Task fort. Der <see cref="EdgeMode"/> bestimmt die
/// Fortsetzungs-Art (<c>o-^</c> → <see cref="EdgeMode.Modal"/>, <c>--^</c> → <see cref="EdgeMode.Goto"/>) —
/// dieselben Modi wie die regulären Transitions-Kanten, aber ein eigener Kanten-Typ.
/// </summary>
[Serializable]
public abstract class ContinuationEdgeSyntax: SyntaxNode {

    protected ContinuationEdgeSyntax(TextExtent extent): base(extent) {
    }

    public abstract SyntaxToken Keyword { get; }
    public abstract EdgeMode    Mode    { get; }

}

[Serializable]
[SampleSyntax("o-^")]
public partial class ContinuationModalEdgeSyntax: ContinuationEdgeSyntax {

    internal ContinuationModalEdgeSyntax(TextExtent extent): base(extent) {
    }

    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => ContinuationModalEdgeKeyword;

    public override EdgeMode Mode => EdgeMode.Modal;

    public SyntaxToken ContinuationModalEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ContinuationModalEdgeKeyword);

}

[Serializable]
[SampleSyntax("--^")]
public partial class ContinuationGoToEdgeSyntax: ContinuationEdgeSyntax {

    internal ContinuationGoToEdgeSyntax(TextExtent extent): base(extent) {
    }

    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => ContinuationGoToEdgeKeyword;

    public override EdgeMode Mode => EdgeMode.Goto;

    public SyntaxToken ContinuationGoToEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ContinuationGoToEdgeKeyword);

}
