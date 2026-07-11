#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.QuickInfo;

/// <summary>
/// VS-freier Hover-/QuickInfo-Service auf Engine-Ebene — Grundlage für LSP <c>textDocument/hover</c>.
/// Ermittelt zu einer Caret-Position das Symbol „unter dem Cursor" und dessen klassifizierte
/// Signatur-Bestandteile (<see cref="DisplayPartsBuilder"/>). Gemeinsam von VS-Extension (QuickInfo)
/// und LSP-Server nutzbar („eine Engine").
/// </summary>
public static class NavHoverService {

    /// <summary>
    /// Liefert die Hover-Information zur angegebenen Zeichen-Position (0-basierter Offset) innerhalb
    /// der <paramref name="unit"/> — oder <c>null</c>, wenn an der Position kein Symbol mit anzeigbarer
    /// Signatur liegt. Es wird vom spezifischsten Symbol unter dem Caret ausgegangen und das erste mit
    /// nicht-leeren Anzeige-Bestandteilen verwendet.
    /// </summary>
    public static NavHoverInfo? GetHover(CodeGenerationUnit unit, int position) {

        foreach (var symbol in SymbolPosition.SymbolsAt(unit, position)) {

            // Für das init-Keyword mit Alias zeigt auch die VS-QuickInfo bewusst nichts an (der Alias
            // selbst trägt die Information). Steht der Caret hingegen auf dem Alias, greift dieser Zweig
            // nicht — dann liefert DisplayPartsBuilder die Signatur des zugehörigen init-Knotens.
            if (symbol is IInitNodeSymbol { Alias: not null }) {
                continue;
            }

            var parts         = GetDisplayParts(symbol);
            var calls         = GetReachableCalls(symbol);
            var documentation = NavSymbolDocumentation.GetDocumentation(symbol);

            if (!parts.IsDefaultOrEmpty || calls.Count > 0 || !String.IsNullOrEmpty(documentation)) {
                return new NavHoverInfo(parts, symbol.Location, calls, documentation);
            }
        }

        // Kein Symbol unter dem Caret — steht dort ein Keyword-Token, erklärt der Hover dessen Bedeutung.
        // Kanten tragen als IEdgeModeSymbol bereits oben ein Symbol; hierher fallen die reinen Wort-Keywords
        // (task/init/if/do/…) und — mangels tragender Kante — vereinzelt ein blanker Edge-Operator.
        return GetKeywordHover(unit, position);
    }

    /// <summary>
    /// Hover für ein Keyword-Token unter dem Caret: seine Bedeutung aus <see cref="SyntaxFacts"/> (die
    /// einzige Autorität). <c>null</c>, wenn dort kein Keyword-Token steht bzw. keine Beschreibung
    /// hinterlegt ist. Die Klassifikations-Prüfung grenzt echte Keyword-Token von gleichnamigen
    /// Bezeichnern ab (die Direktiv-Keywords <c>version</c>/<c>pragma</c> sind nicht reserviert).
    /// </summary>
    static NavHoverInfo? GetKeywordHover(CodeGenerationUnit unit, int position) {

        var token = unit.Syntax.SyntaxTree.Tokens.FindAtPosition(position);
        if (token.IsMissing || !IsKeywordClassification(token.Classification)) {
            return null;
        }

        var description = SyntaxFacts.GetKeywordDescription(token.ToString());
        if (description.Length == 0) {
            return null;
        }

        var parts = ImmutableArray.Create(new ClassifiedText(token.ToString(), token.Classification));

        return new NavHoverInfo(parts, token.GetLocation(), Array.Empty<Call>(), description);
    }

    static bool IsKeywordClassification(TextClassification classification) {
        return classification is TextClassification.Keyword
                              or TextClassification.ControlKeyword
                              or TextClassification.PreprocessorKeyword;
    }

    static ImmutableArray<ClassifiedText> GetDisplayParts(ISymbol symbol) {

        // Eine Choice-Referenz selbst hat keine eigene Signatur — wie in VS zeigen wir die ihrer Deklaration.
        if (symbol is IChoiceNodeReferenceSymbol { Declaration: { } choiceDecl }) {
            return choiceDecl.ToDisplayParts();
        }

        return symbol.ToDisplayParts();
    }

    /// <summary>
    /// Die von der Position aus erreichbaren Knoten — nur an einer <b>Choice</b> anzeigbar: die Choice wird
    /// transitiv aufgelöst, sodass ihr Fan-out auf die tatsächlich erreichbaren Zielknoten (mit ihrem
    /// Edge-Mode) sichtbar wird. Für alle anderen Symbole — insbesondere gewöhnliche Kanten — leer: dort
    /// steht das Ziel bereits sichtbar neben dem Pfeil, die Kante selbst erklärt statt dessen ihre Bedeutung
    /// (siehe <see cref="NavSymbolDocumentation"/>). Sortiert nach Knotennamen (wie VS).
    /// </summary>
    static IReadOnlyList<Call> GetReachableCalls(ISymbol symbol) {

        var calls = symbol switch {
            IChoiceNodeSymbol choiceNode                         => choiceNode.ExpandCalls(),
            IChoiceNodeReferenceSymbol { Declaration: { } decl } => decl.ExpandCalls(),
            _                                                    => Enumerable.Empty<Call>()
        };

        return calls.OrderBy(call => call.Node.Name, StringComparer.Ordinal).ToList();
    }

}

/// <summary>
/// Ergebnis von <see cref="NavHoverService.GetHover"/>: die klassifizierte Signatur des Symbols unter
/// dem Caret sowie dessen <see cref="Language.Location"/> (für den Hover-Bereich). Protokoll-frei.
/// </summary>
public sealed class NavHoverInfo {

    public NavHoverInfo(ImmutableArray<ClassifiedText> displayParts, Location? location,
                        IReadOnlyList<Call> calls, string? documentation = null) {
        DisplayParts  = displayParts;
        Location      = location;
        Calls         = calls;
        Documentation = documentation;
    }

    /// <summary>Die klassifizierten Signatur-Bestandteile (z.B. <c>task</c> + <c> </c> + <c>Foo</c>).</summary>
    public ImmutableArray<ClassifiedText> DisplayParts { get; }

    /// <summary>Der Namens-Bereich des Symbols unter dem Caret; kann <c>null</c> sein.</summary>
    public Location? Location { get; }

    /// <summary>
    /// Der aufbereitete Kommentartext direkt über der Deklaration des Symbols (siehe
    /// <see cref="NavSymbolDocumentation"/>); <c>null</c>, wenn dort kein Kommentar steht.
    /// </summary>
    public string? Documentation { get; }

    /// <summary>
    /// Die von hier aus erreichbaren Knoten (Choices/Edges); leer für gewöhnliche Symbole. Jeder
    /// <see cref="Call"/> trägt Zielknoten und Edge-Mode für eine Zeile „Verb Zielsignatur".
    /// </summary>
    public IReadOnlyList<Call> Calls { get; }

}
