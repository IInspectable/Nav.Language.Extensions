#nullable enable

using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

public enum EdgeMode {

    Modal,
    NonModal,
    Goto

}

[Serializable]
public abstract class EdgeSyntax: SyntaxNode {

    protected EdgeSyntax(TextExtent extent): base(extent) {
    }

    public abstract SyntaxToken Keyword { get; }
    public abstract EdgeMode    Mode    { get; }

}

[Serializable]
[SampleSyntax("o->")]
public partial class ModalEdgeSyntax: EdgeSyntax {

    internal ModalEdgeSyntax(TextExtent extent): base(extent) {
    }

    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => ModalEdgeKeyword;

    public override EdgeMode Mode => EdgeMode.Modal;

    public SyntaxToken ModalEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ModalEdgeKeyword);

}

[Serializable]
[SampleSyntax("==>")]
public partial class NonModalEdgeSyntax: EdgeSyntax {

    internal NonModalEdgeSyntax(TextExtent extent): base(extent) {
    }

    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => NonModalEdgeKeyword;

    public override EdgeMode Mode => EdgeMode.NonModal;

    public SyntaxToken NonModalEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.NonModalEdgeKeyword);

}

[Serializable]
[SampleSyntax("-->")]
public partial class GoToEdgeSyntax: EdgeSyntax {

    internal GoToEdgeSyntax(TextExtent extent): base(extent) {
    }

    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => GoToEdgeKeyword;

    public override EdgeMode Mode => EdgeMode.Goto;

    public SyntaxToken GoToEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.GoToEdgeKeyword);

}
