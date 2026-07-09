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
    /// </summary>
    public string Description => EdgeMode switch {
        EdgeMode.Modal => IsContinuation
            ? "Zeigt die GUI an und ruft unmittelbar den Folge-Task modal auf."
            : "Ruft das Ziel modal auf.",
        EdgeMode.NonModal => "Ruft das Ziel nicht-modal auf.",
        EdgeMode.Goto => IsContinuation
            ? "Zeigt die GUI an und ruft unmittelbar den Folge-Task auf (nicht modal)."
            : "Ruft das Ziel auf (nicht modal).",
        _ => ""
    };

}
