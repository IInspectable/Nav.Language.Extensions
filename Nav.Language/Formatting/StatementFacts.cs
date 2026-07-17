using System.Collections.Generic;
using System.Linq;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Die pro Anweisung <b>einmal</b> erhobenen, formatierungs-invarianten Fakten, die sich der
/// Fehler-Toleranz-Vor-Durchlauf (<see cref="FormatterSuppression"/>) und alle Ausrichtungs-Vor-Durchläufe
/// (<see cref="AlignmentMapBuilder"/>) teilen: die signifikante Token-Liste der Anweisung
/// (<c>syntaxTree.Tokens[statement.Extent]</c>) und die Klassifikation ihrer inneren Lücken-Trivia. Zuvor
/// las jeder Vor-Durchlauf die Token-Liste und scannte die Lücken selbst — pro Transition bis zu fünfmal (Pfeil,
/// Trigger, Condition, Trailing-Kommentar im Ausrichtungs-Vor-Durchlauf sowie strukturgleich in der
/// Unterdrückung). Beide Erhebungen fallen hier zu einer zusammen.
/// </summary>
/// <remarks>
/// Alle drei Fakten sind reine Funktionen der Token-Texte und der Trivia-Klassen — formatierungs-invariant,
/// also über die Durchläufe stabil (Grundlage der Idempotenz). <see cref="HasStructuralBreakTrivia"/> und
/// <see cref="SpansMultipleLines"/> werden getrennt gehalten, weil die beiden Konsumenten sie verschieden
/// kombinieren: die Unterdrückung trennt „Strukturbruch → verbatim" (Skiped/Direktive) von „mehrzeilig, aber
/// gültig → manuell umbrochen", der Ausrichtungs-Vor-Durchlauf fasst beide zu <see cref="BreaksSingleLineForm"/>
/// zusammen (jede nicht mehr einzeilig-kanonische Anweisung fällt aus der Spalte).
/// </remarks>
sealed class StatementFacts {

    StatementFacts(SyntaxNode statement, IReadOnlyList<SyntaxToken> tokens, bool endsWithSemicolon,
                   bool hasStructuralBreakTrivia, bool spansMultipleLines) {
        Statement                = statement;
        Tokens                   = tokens;
        EndsWithSemicolon        = endsWithSemicolon;
        HasStructuralBreakTrivia = hasStructuralBreakTrivia;
        SpansMultipleLines       = spansMultipleLines;
    }

    /// <summary>Der vermessene Anweisungsknoten (Transition, Exit-Transition oder Node-Deklaration).</summary>
    public SyntaxNode Statement { get; }

    /// <summary>Die signifikanten Token der Anweisung (<c>syntaxTree.Tokens[statement.Extent]</c>).</summary>
    public IReadOnlyList<SyntaxToken> Tokens { get; }

    /// <summary>Ob das letzte reale Token ein <c>;</c> ist — sonst Strukturbruch (fehlendes Semikolon).</summary>
    public bool EndsWithSemicolon { get; }

    /// <summary>
    /// Ob eine <b>innere</b> Lücke der Anweisung eine <see cref="SyntaxTokenType.SkippedTokensTrivia"/> oder
    /// eine Direktive trägt — ein Strukturbruch, der die Anweisung verbatim stehen lässt (eine Direktive
    /// mitten in der Anweisung ließe sich nicht per Delta-Shift auf Spalte 0 halten).
    /// </summary>
    public bool HasStructuralBreakTrivia { get; }

    /// <summary>
    /// Ob eine <b>innere</b> Lücke einen Newline oder einen zeilen-erzwingenden Kommentar trägt — die
    /// Anweisung ist dann nicht einzeilig (manuell umbrochen).
    /// </summary>
    public bool SpansMultipleLines { get; }

    /// <summary>
    /// Ob die Anweisung nicht mehr einzeilig-kanonisch ist (mehrzeilig <b>oder</b> Skiped/Direktive im
    /// Inneren) — die Bedingung, unter der ein Ausrichtungs-Vor-Durchlauf sie aus der Spalte nimmt und die Gruppe
    /// bricht. Deckt exakt die frühere <c>AlignmentMapBuilder.IsHandLaid</c>-Erkennung ab.
    /// </summary>
    public bool BreaksSingleLineForm => HasStructuralBreakTrivia || SpansMultipleLines;

    /// <summary>
    /// Vermisst eine einzelne Anweisung: Token-Liste holen und ihre inneren Lücken einmal scannen. Beide
    /// Trivia-Befunde werden immer vollständig erhoben (die frühere <c>Classify</c> brach beim ersten
    /// Strukturbruch ab — eine reine Optimierung; das abgeleitete Ergebnis ist identisch).
    /// </summary>
    static StatementFacts Measure(SyntaxTree syntaxTree, SyntaxNode statement) {

        var tokens = syntaxTree.Tokens[statement.Extent].ToList();

        var endsWithSemicolon = tokens.Count > 0 &&
                                tokens[tokens.Count - 1].Type == SyntaxTokenType.Semicolon;

        var hasStructuralBreakTrivia = false;
        var spansMultipleLines       = false;

        for (var i = 1; i < tokens.Count; i++) {

            var trivia = GapTrivia.Create(tokens[i - 1], tokens[i], syntaxTree.SourceText);

            if (trivia.HasSkippedTokens || trivia.HasDirective) {
                hasStructuralBreakTrivia = true;
            }

            if (trivia.NewLineCount > 0 || trivia.HasLineBreakingComment) {
                spansMultipleLines = true;
            }
        }

        return new StatementFacts(statement, tokens, endsWithSemicolon, hasStructuralBreakTrivia, spansMultipleLines);
    }

    /// <summary>
    /// Vermisst alle Anweisungen des Baumes einmal und legt sie in einer über den Knoten adressierbaren
    /// <see cref="Map"/> ab.
    /// </summary>
    public static Map Compute(SyntaxTree syntaxTree) {

        var all         = new List<StatementFacts>();
        var byStatement = new Dictionary<SyntaxNode, StatementFacts>();

        foreach (var statement in EnumerateStatements(syntaxTree)) {
            var facts = Measure(syntaxTree, statement);
            all.Add(facts);
            byStatement[statement] = facts;
        }

        return new Map(all, byStatement);
    }

    /// <summary>
    /// Das flache Anweisungs-Set, auf dem sowohl die Unterdrückung als auch der Ausrichtungs-Vor-Durchlauf
    /// operieren: Transitionen, Exit-Transitionen, Node-Deklarationen (Letztere schließen die
    /// <c>taskref</c>-Verbindungspunkte mit ein, da <c>ConnectionPointNodeSyntax</c> von
    /// <see cref="NodeDeclarationSyntax"/> erbt). Der einzige gemeinsame Aufzähler dieser Knoten.
    /// </summary>
    internal static IEnumerable<SyntaxNode> EnumerateStatements(SyntaxTree syntaxTree) {

        var root = syntaxTree.Root;

        foreach (var transition in root.DescendantNodes<TransitionDefinitionSyntax>()) {
            yield return transition;
        }

        foreach (var exitTransition in root.DescendantNodes<ExitTransitionDefinitionSyntax>()) {
            yield return exitTransition;
        }

        foreach (var nodeDeclaration in root.DescendantNodes<NodeDeclarationSyntax>()) {
            yield return nodeDeclaration;
        }
    }

    /// <summary>
    /// Die vermessenen Anweisungen: als Liste (für die Unterdrückung, die jede klassifiziert) und über den
    /// Anweisungsknoten adressierbar (für den Ausrichtungs-Vor-Durchlauf, der die Anweisungen block-weit
    /// aufsucht). Da beide Sichten aus derselben <see cref="EnumerateStatements"/>-Aufzählung stammen, ist
    /// jeder vom Ausrichtungs-Vor-Durchlauf adressierte Knoten in der Map enthalten.
    /// </summary>
    public sealed class Map {

        readonly IReadOnlyDictionary<SyntaxNode, StatementFacts> _byStatement;

        internal Map(IReadOnlyList<StatementFacts> all, IReadOnlyDictionary<SyntaxNode, StatementFacts> byStatement) {
            All          = all;
            _byStatement = byStatement;
        }

        /// <summary>Alle vermessenen Anweisungen in Aufzählungsreihenfolge.</summary>
        public IReadOnlyList<StatementFacts> All { get; }

        /// <summary>Die Fakten eines Anweisungsknotens (per Konstruktion stets vorhanden, s. Typ-Doku).</summary>
        public StatementFacts For(SyntaxNode statement) => _byStatement[statement];

    }

}
