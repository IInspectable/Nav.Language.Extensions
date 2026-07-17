using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Der vorberechnete Kontext einer Lücke zwischen zwei aufeinanderfolgenden signifikanten Token —
/// ausschließlich reine, formatierungs-invariante Fakten (Token, Baumstruktur, Trivia-Klasse,
/// Newline-Anzahl), nie das aktuelle Whitespace. Regeln (<see cref="IGapRule"/>) entscheiden allein
/// hierüber; das macht sie pur, isoliert testbar und lokal idempotent.
/// </summary>
readonly struct GapContext {

    public GapContext(SyntaxToken prev, SyntaxToken next, int indentDepth, GapTrivia trivia,
                      bool isSuppressed, AlignmentMap alignment, NavFormattingOptions options) {
        Prev         = prev;
        Next         = next;
        IndentDepth  = indentDepth;
        Trivia       = trivia;
        IsSuppressed = isSuppressed;
        Alignment    = alignment;
        Options      = options;
    }

    /// <summary>Das Token vor der Lücke.</summary>
    public SyntaxToken Prev { get; }

    /// <summary>Das Token hinter der Lücke.</summary>
    public SyntaxToken Next { get; }

    /// <summary>Der Syntax-Knoten, zu dem <see cref="Prev"/> gehört.</summary>
    public SyntaxNode? PrevParent => Prev.Parent;

    /// <summary>Der Syntax-Knoten, zu dem <see cref="Next"/> gehört.</summary>
    public SyntaxNode? NextParent => Next.Parent;

    /// <summary>
    /// Die Einzugstiefe von <see cref="Next"/> — des Tokens, das eine etwaige neue Zeile eröffnet
    /// (aus der Ahnenkette abgeleitet, nie aus Nachbar-Operationen). Newline-Layouts richten sich
    /// immer nach der beginnenden Zeile; die Tiefe von <see cref="Prev"/> wird für die Einrückung nie
    /// gebraucht.
    /// </summary>
    public int IndentDepth { get; }

    /// <summary>Die vorberechneten Fakten über den Lückeninhalt (Kommentare, Skiped, Direktiven, Newlines).</summary>
    public GapTrivia Trivia { get; }

    /// <summary>Ob die Lücke in einer unterdrückten Region liegt (dann verbatim, siehe <see cref="GapRules"/>).</summary>
    public bool IsSuppressed { get; }

    /// <summary>Die vorberechnete Ausrichtungs-Tabelle (Lücke → aufgelöste Space-Zahl).</summary>
    public AlignmentMap Alignment { get; }

    /// <summary>
    /// Die Formatter-Optionen des Durchlaufs — für Regeln, deren Zuständigkeit ein Feature-Schalter ist
    /// (z.B. Task-Kopf-Kanonisierung). Durchlauf-konstant und damit genauso formatierungs-invariant wie die
    /// übrigen Fakten.
    /// </summary>
    public NavFormattingOptions Options { get; }

    /// <summary>
    /// Der Quelltext-Ausschnitt der Lücke: exakt <c>[Prev.End, Next.Start)</c>. Die FullSpans der Token
    /// kacheln den Text lückenlos und überlappungsfrei — aufeinanderfolgende Lücken sind daher paarweise
    /// disjunkt und geordnet (die tragende Ein-Change-pro-Lücke-Invariante).
    /// </summary>
    public TextExtent Extent => TextExtent.FromBounds(Prev.End, Next.Start);

}
