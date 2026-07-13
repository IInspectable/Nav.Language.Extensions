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

    /// <summary>Erzeugt die Kante mit ihrem Quelltext-<paramref name="extent"/> (nur für die konkreten Kanten-Klassen).</summary>
    protected ContinuationEdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Kanten-Operator-Token der konkreten Continuation-Kante (<c>o-^</c> bzw. <c>--^</c>).</summary>
    public abstract SyntaxToken Keyword { get; }
    /// <summary>Die Fortsetzungs-Art der Kante: <see cref="EdgeMode.Modal"/> für <c>o-^</c>, <see cref="EdgeMode.Goto"/> für <c>--^</c>.</summary>
    public abstract EdgeMode    Mode    { get; }

}

/// <summary>
/// Die modale Continuation-Kante <c>o-^</c>: zeigt die GUI an und ruft unmittelbar den Folge-Task
/// <b>modal</b> auf — das Continuation-Gegenstück zur regulären Kante <c>o-&gt;</c> (<see cref="EdgeMode.Modal"/>).
/// </summary>
[Serializable]
[SampleSyntax("o-^")]
public partial class ContinuationModalEdgeSyntax: ContinuationEdgeSyntax {

    internal ContinuationModalEdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das <c>o-^</c>-Token (Alias für <see cref="ContinuationModalEdgeKeyword"/>).</summary>
    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => ContinuationModalEdgeKeyword;

    /// <summary>Stets <see cref="EdgeMode.Modal"/>.</summary>
    public override EdgeMode Mode => EdgeMode.Modal;

    /// <summary>Das Kanten-Operator-Token <c>o-^</c>.</summary>
    public SyntaxToken ContinuationModalEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ContinuationModalEdgeKeyword);

}

/// <summary>
/// Die Goto-Continuation-Kante <c>--^</c>: zeigt die GUI an und ruft unmittelbar den Folge-Task
/// <b>nicht-modal</b> auf — das Continuation-Gegenstück zur regulären Kante <c>--&gt;</c> (<see cref="EdgeMode.Goto"/>).
/// </summary>
[Serializable]
[SampleSyntax("--^")]
public partial class ContinuationGoToEdgeSyntax: ContinuationEdgeSyntax {

    internal ContinuationGoToEdgeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das <c>--^</c>-Token (Alias für <see cref="ContinuationGoToEdgeKeyword"/>).</summary>
    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public override SyntaxToken Keyword => ContinuationGoToEdgeKeyword;

    /// <summary>Stets <see cref="EdgeMode.Goto"/>.</summary>
    public override EdgeMode Mode => EdgeMode.Goto;

    /// <summary>Das Kanten-Operator-Token <c>--^</c>.</summary>
    public SyntaxToken ContinuationGoToEdgeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ContinuationGoToEdgeKeyword);

}
