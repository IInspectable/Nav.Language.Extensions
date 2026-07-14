namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol des Kanten-Operators einer Kante — in <c>Start --&gt; Auswahl;</c> das <c>--&gt;</c>.
/// Der <see cref="ISymbol.Name"/> ist das Operator-Literal, wie es im Quelltext steht; welche
/// Aufruf-Art es kodiert, sagt <see cref="EdgeMode"/>. Die Bedeutung je Operator-Literal ist in
/// <see cref="SyntaxFacts.KeywordDescriptions"/> hinterlegt (siehe <see cref="Description"/>).
/// </summary>
public interface IEdgeModeSymbol: ISymbol {

    /// <summary>Die Aufruf-Art, die der Operator kodiert (Modal/NonModal/Goto).</summary>
    EdgeMode EdgeMode { get; }

    /// <summary>Die Kante, deren Operator dieses Symbol ist.</summary>
    IEdge Edge { get; }

    /// <summary>
    /// Ob dieser Kantenmodus zu einer Continuation (<c>o-^</c>/<c>--^</c>) gehört statt zu einer
    /// gewöhnlichen Transition. Die <see cref="EdgeMode"/>-Werte selbst sind identisch (Modal/Goto) —
    /// erst die tragende Kante unterscheidet Continuation von regulärer Transition.
    /// </summary>
    bool IsContinuation { get; }

    /// <summary>Menschenlesbare Kanten-Art (z.B. „Modal Edge" bzw. „Modal Continuation") — Kopfzeile der QuickInfo.</summary>
    string DisplayName { get; }
    /// <summary>Ein Satz, der die Bedeutung des Kantenmodus erklärt — die Erläuterungszeile der QuickInfo.</summary>
    string Description { get; }

}
