namespace Pharmatechnik.Nav.Language;

sealed partial class EdgeModeSymbol: Symbol, IEdgeModeSymbol {

    public EdgeModeSymbol(SyntaxTree syntaxTree, string name, Location location, EdgeMode edgeMode)
        : base(name, location) {

        SyntaxTree = syntaxTree;
        EdgeMode   = edgeMode;
    }

    public override SyntaxTree SyntaxTree { get; }

    public EdgeMode EdgeMode { get; }

    // Wird im Ctor der Edge während der Initialisierung gesetzt — in der "freien Wildbahn" darf
    // der Null-Fall nicht auftreten.
    public IEdge Edge { get; internal set; } = null!;

    /// <summary>
    /// Ob dieser Kantenmodus zu einer Continuation (<c>o-^</c>/<c>--^</c>) gehört statt zu einer
    /// gewöhnlichen Transition. Die <see cref="Language.EdgeMode"/>-Werte selbst sind identisch
    /// (Modal/Goto) — erst die tragende Kante unterscheidet Continuation von regulärer Transition.
    /// </summary>
    public bool IsContinuation => Edge is IContinuationTransition;

    /// <summary>
    /// Menschenlesbare Kanten-Art (z.B. „Modal Edge" bzw. „Modal Continuation") — Kopfzeile der QuickInfo.
    /// Ein Non-Modal-Continuation gibt es in der Sprache nicht (<c>==></c> ist stets eine reguläre Kante).
    /// </summary>
    public string DisplayName => EdgeMode switch {
        EdgeMode.Modal    => IsContinuation ? "Modal Continuation" : "Modal Edge",
        EdgeMode.NonModal => "NonModal Edge",
        EdgeMode.Goto     => IsContinuation ? "GoTo Continuation" : "GoTo Edge",
        _                 => Name
    };

    /// <summary>
    /// Ein Satz, der die Bedeutung des Kantenmodus erklärt — die Erläuterungszeile der QuickInfo (Doku-Stil).
    /// Delegiert an <see cref="SyntaxFacts.GetKeywordDescription"/>: jede Kante ist ein konkretes
    /// Keyword-Literal mit fester Bedeutung, dort liegt die einzige Autorität.
    /// </summary>
    public string Description => SyntaxFacts.GetKeywordDescription(EdgeKeyword);

    /// <summary>
    /// Das konkrete Kanten-Keyword dieses Modus — die Continuation-Varianten (<c>--^</c>/<c>o-^</c>)
    /// eingeschlossen. Ein Non-Modal-Continuation gibt es in der Sprache nicht (<c>==></c> ist stets eine
    /// reguläre Kante), daher fällt <see cref="EdgeMode.NonModal"/> unabhängig von <see cref="IsContinuation"/>
    /// auf <c>==></c>.
    /// </summary>
    string EdgeKeyword => EdgeMode switch {
        EdgeMode.Modal    => IsContinuation ? SyntaxFacts.ContinuationModalEdgeKeyword : SyntaxFacts.ModalEdgeKeyword,
        EdgeMode.NonModal => SyntaxFacts.NonModalEdgeKeyword,
        EdgeMode.Goto     => IsContinuation ? SyntaxFacts.ContinuationGoToEdgeKeyword : SyntaxFacts.GoToEdgeKeyword,
        _                 => Name
    };

}
