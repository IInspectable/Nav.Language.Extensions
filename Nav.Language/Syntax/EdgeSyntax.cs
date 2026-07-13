using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Aufruf-Art einer Kante — bestimmt, wie das Ziel des Übergangs aufgerufen wird. Gilt für die
/// regulären Transitions-Kanten (<see cref="EdgeSyntax"/>) wie für die Continuation-Kanten
/// (<see cref="ContinuationEdgeSyntax"/>); die menschenlesbare Bedeutung je Operator liefert
/// <see cref="SyntaxFacts.GetKeywordDescription(string)"/>.
/// </summary>
public enum EdgeMode {

    /// <summary>Das Ziel wird modal aufgerufen (<c>o-&gt;</c> bzw. Continuation <c>o-^</c>).</summary>
    Modal,
    /// <summary>Das Ziel wird nicht-modal gestartet (<c>==&gt;</c>).</summary>
    NonModal,
    /// <summary>Das Ziel wird direkt aufgerufen, nicht modal (<c>--&gt;</c> bzw. Continuation <c>--^</c>).</summary>
    Goto

}

/// <summary>
/// Die Kante einer Transition — einer der Operatoren <c>--&gt;</c> (<see cref="GoToEdgeSyntax"/>),
/// <c>o-&gt;</c> (<see cref="ModalEdgeSyntax"/>) oder <c>==&gt;</c> (<see cref="NonModalEdgeSyntax"/>)
/// zwischen Quell- und Zielknoten, z.B. <c>A --&gt; B on Trigger;</c>. Der Operator bestimmt den
/// <see cref="EdgeMode"/> und damit die Aufruf-Art des Ziels. Die Continuation-Kanten
/// (<c>--^</c>/<c>o-^</c>) sind bewusst ein eigener Kanten-Typ (<see cref="ContinuationEdgeSyntax"/>) —
/// sie leiten keine neue Transition ein (siehe <see cref="SyntaxFacts.IsEdgeKeyword(SyntaxTokenType)"/>).
/// </summary>
[Serializable]
public abstract class EdgeSyntax: SyntaxNode {

    /// <summary>Initialisiert die Basisklasse mit dem Quelltext-Bereich der Kante.</summary>
    protected EdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Operator-Token der Kante (<c>--&gt;</c>, <c>o-&gt;</c> oder <c>==&gt;</c>).</summary>
    public abstract SyntaxToken Keyword { get; }
    /// <summary>Die Aufruf-Art, die der Operator kodiert.</summary>
    public abstract EdgeMode    Mode    { get; }

}

/// <summary>
/// Die modale Kante <c>o-&gt;</c>: das Ziel wird modal aufgerufen, z.B. <c>View o-&gt; Task on Trigger;</c>.
/// </summary>
[Serializable]
[SampleSyntax("o->")]
public partial class ModalEdgeSyntax: EdgeSyntax {

    internal ModalEdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das <c>o-&gt;</c>-Token — Alias für <see cref="ModalEdgeKeyword"/>.</summary>
    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => ModalEdgeKeyword;

    /// <summary>Immer <see cref="EdgeMode.Modal"/>.</summary>
    public override EdgeMode Mode => EdgeMode.Modal;

    /// <summary>Das <c>o-&gt;</c>-Token (<see cref="SyntaxTokenType.ModalEdgeKeyword"/>).</summary>
    public SyntaxToken ModalEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ModalEdgeKeyword);

}

/// <summary>
/// Die nicht-modale Kante <c>==&gt;</c>: das Ziel wird nicht-modal gestartet, z.B. <c>View ==&gt; Task on Trigger;</c>.
/// </summary>
[Serializable]
[SampleSyntax("==>")]
public partial class NonModalEdgeSyntax: EdgeSyntax {

    internal NonModalEdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das <c>==&gt;</c>-Token — Alias für <see cref="NonModalEdgeKeyword"/>.</summary>
    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => NonModalEdgeKeyword;

    /// <summary>Immer <see cref="EdgeMode.NonModal"/>.</summary>
    public override EdgeMode Mode => EdgeMode.NonModal;

    /// <summary>Das <c>==&gt;</c>-Token (<see cref="SyntaxTokenType.NonModalEdgeKeyword"/>).</summary>
    public SyntaxToken NonModalEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.NonModalEdgeKeyword);

}

/// <summary>
/// Die GoTo-Kante <c>--&gt;</c>: das Ziel wird direkt (nicht modal) aufgerufen, z.B. <c>init --&gt; View;</c>.
/// Choice-, Exit- und End-Knoten dürfen semantisch ausschließlich über diese Kanten-Art erreicht werden.
/// </summary>
[Serializable]
[SampleSyntax("-->")]
public partial class GoToEdgeSyntax: EdgeSyntax {

    internal GoToEdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das <c>--&gt;</c>-Token — Alias für <see cref="GoToEdgeKeyword"/>.</summary>
    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => GoToEdgeKeyword;

    /// <summary>Immer <see cref="EdgeMode.Goto"/>.</summary>
    public override EdgeMode Mode => EdgeMode.Goto;

    /// <summary>Das <c>--&gt;</c>-Token (<see cref="SyntaxTokenType.GoToEdgeKeyword"/>).</summary>
    public SyntaxToken GoToEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.GoToEdgeKeyword);

}
