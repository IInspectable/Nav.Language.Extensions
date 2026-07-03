#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.References;

/// <summary>
/// VS-freier Service für die Referenz-Hervorhebung („Highlight all occurrences") innerhalb **einer**
/// <see cref="CodeGenerationUnit"/> — Grundlage für LSP <c>textDocument/documentHighlight</c>. Liefert zu
/// einer Caret-Position das Deklarations-Symbol und alle Referenzen darauf im selben Dokument.
/// </summary>
/// <remarks>
/// Solution-weite Referenzsuche (für <c>textDocument/references</c>) läuft NICHT hierüber, sondern über
/// die bestehende Engine-API <see cref="FindReferences.ReferenceFinder.FindReferencesAsync"/>, die
/// <c>NavSolution.ProcessCodeGenerationUnitsAsync</c> cross-file iteriert.
/// </remarks>
public static class NavReferenceService {

    /// <summary>
    /// Liefert die hervorzuhebenden Symbole zur angegebenen Zeichen-Position (0-basierter Offset):
    /// das erste Symbol ist die Deklaration, alle weiteren sind Referenzen. Leere Liste, wenn an der
    /// Position kein referenzierbares Symbol liegt. Duplikate (Datei + Startposition) werden entfernt.
    /// </summary>
    public static IReadOnlyList<ISymbol> GetHighlightSymbols(CodeGenerationUnit unit, int position,
                                                             bool includeReferencesUnderInclude = true) {

        // Vom spezifischsten Symbol unter dem Caret ausgehen ("Symbol unter Cursor").
        foreach (var origin in SymbolsAt(unit, position)) {

            var seen    = new HashSet<(string?, int)>();
            var symbols = new List<ISymbol>();

            foreach (var symbol in HighlightSymbolFinder.Find(origin, includeReferencesUnderInclude)) {
                if (symbol?.Location != null && seen.Add((symbol.Location.FilePath, symbol.Location.Start))) {
                    symbols.Add(symbol);
                }
            }

            if (symbols.Count > 0) {
                return symbols;
            }
        }

        return new List<ISymbol>();
    }

    /// <summary>
    /// Das spezifischste Symbol, dessen Bereich die Position enthält — oder null, wenn der Caret auf
    /// keinem Symbol steht. Grundlage für die solution-weite Referenzsuche (Ursprungssymbol).
    /// </summary>
    public static ISymbol? FindSymbol(CodeGenerationUnit unit, int position) {
        return SymbolPosition.SymbolsAt(unit, position).FirstOrDefault();
    }

    static IEnumerable<ISymbol> SymbolsAt(CodeGenerationUnit unit, int position) {
        return SymbolPosition.SymbolsAt(unit, position);
    }

}
