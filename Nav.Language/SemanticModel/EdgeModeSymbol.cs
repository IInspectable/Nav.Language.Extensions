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
    bool IsContinuation => Edge is IContinuationTransition;

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
    /// </summary>
    public string Description => EdgeMode switch {
        EdgeMode.Modal => IsContinuation
            ? "Zeigt den tragenden GUI-Knoten modal an und setzt anschließend im Ziel-Task fort."
            : "Zeigt das Ziel modal (blockierend) an.",
        EdgeMode.NonModal => "Zeigt das Ziel nicht-modal (nebenläufig) an.",
        EdgeMode.Goto => IsContinuation
            ? "Setzt vom tragenden GUI-Knoten in den Ziel-Task fort (ohne Rückkehr)."
            : "Kontrollfluss zum Ziel — ohne Rückkehr.",
        _ => ""
    };

}
