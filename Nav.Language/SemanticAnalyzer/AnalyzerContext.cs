namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Gemeinsamer Kontext eines Analyse-Laufs, der jedem Analyzer an
/// <see cref="INavAnalyzer.Analyze"/> übergeben wird (erzeugt im
/// <see cref="CodeGenerationUnitBuilder"/>, eine Instanz pro Modellbau). Einziger Dienst ist die
/// Unterdrückung einzelner Diagnosen per Quelltext-Kommentar (<see cref="IsWarningDisabled"/>).
/// </summary>
public class AnalyzerContext {

    /// <summary>
    /// Bestimmt, ob die Diagnose des <paramref name="descriptor"/>s für den Knoten
    /// <paramref name="node"/> per Kommentar unterdrückt ist: Gesucht wird der Text
    /// <c>// disable Nav####</c> in der Trailing-Trivia der Knoten-Deklaration
    /// (<see cref="SyntaxNode.GetTrailingTriviaExtent"/>), z.B.
    /// <c>exit e1; // disable Nav0107</c>.
    /// </summary>
    /// <param name="node">Der Knoten, an dessen Deklaration nach dem Disable-Kommentar gesucht wird.</param>
    /// <param name="descriptor">Der Deskriptor der Diagnose, deren Unterdrückung geprüft wird.</param>
    /// <returns><c>true</c>, wenn der Disable-Kommentar für die Diagnose-Id vorhanden ist —
    /// beachtet von den Analyzern, die dieses Opt-out anbieten (z.B. Nav0107, Nav1012).</returns>
    public bool IsWarningDisabled(INodeSymbol node, DiagnosticDescriptor descriptor) {
        var source = node.SyntaxTree?.SourceText;
        if (source == null)
            return false;

        var disableString = $"{SyntaxFacts.SingleLineComment} disable {descriptor.Id}";
        var triviaExtent  = node.Syntax.GetTrailingTriviaExtent();

        return source.Substring(triviaExtent).Contains(disableString);
    }

}