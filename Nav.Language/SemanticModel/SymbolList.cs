#region Using Directives

using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Unveränderliche, nach <see cref="IExtent.Start"/> sortierte Liste von Symbolen — der flache
/// Symbol-Strom einer Datei, wie ihn <see cref="CodeGenerationUnit.Symbols"/> publiziert. Die
/// Sortierung ist die Grundlage der Binärsuche-basierten Positions-Lookups
/// (<see cref="FindAtPosition"/>, Bereichs-Indexer).
/// </summary>
public sealed class SymbolList: IReadOnlyList<ISymbol> {

    readonly IReadOnlyList<ISymbol> _symbols;

    /// <summary>Erzeugt eine leere Liste.</summary>
    public SymbolList(): this(null) {
    }

    /// <summary>
    /// Erzeugt die Liste aus den angegebenen Symbolen; sie werden kopiert und aufsteigend nach
    /// <see cref="IExtent.Start"/> sortiert.
    /// </summary>
    /// <param name="symbols">Die aufzunehmenden Symbole, oder <c>null</c> für eine leere Liste.</param>
    public SymbolList(IEnumerable<ISymbol>? symbols) {

        var symbolList = new List<ISymbol>(symbols ?? Enumerable.Empty<ISymbol>());
        symbolList.Sort((x, y) => x.Start - y.Start);

        _symbols = symbolList;
    }

    /// <summary>Enumeriert die Symbole in Quelltext-Reihenfolge (aufsteigender <see cref="IExtent.Start"/>).</summary>
    public IEnumerator<ISymbol> GetEnumerator() {
        return _symbols.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>Die Anzahl der Symbole.</summary>
    public int Count => _symbols.Count;

    /// <summary>Das Symbol an der angegebenen Listenposition (in Quelltext-Reihenfolge).</summary>
    /// <param name="index">Der 0-basierte Listenindex.</param>
    public ISymbol this[int index] => _symbols[index];

    /// <summary>
    /// Alle Symbole im angegebenen Quelltext-Bereich: ohne <paramref name="includeOverlapping"/>
    /// nur vollständig in <paramref name="extent"/> enthaltene Symbole, mit
    /// <paramref name="includeOverlapping"/> auch die den Rand berührenden bzw. überlappenden.
    /// Vorsicht bei Null-Längen-Extents (Caret-Position): die überlappende Punktsuche liefert
    /// das nächstgelegene Symbol <em>an oder vor</em> der Position — auch wenn der Bereich es
    /// gar nicht schneidet; echte Enthaltenseins-Prüfung leistet die Caret-Auflösung über
    /// <see cref="SymbolPosition.SymbolsAt"/>.
    /// </summary>
    /// <param name="extent">Der Quelltext-Bereich.</param>
    /// <param name="includeOverlapping">Ob den Bereich nur schneidende Symbole mitgeliefert werden.</param>
    public IEnumerable<ISymbol> this[TextExtent extent, bool includeOverlapping = false] => _symbols.GetElements(extent, includeOverlapping);

    /// <summary>
    /// Der exakte Positions-Lookup: liefert das Symbol, das die Position tatsächlich abdeckt
    /// (<c>Start ≤ position &lt; End</c>), sonst <c>null</c> — auch außerhalb des gültigen
    /// Bereichs wird nicht geworfen.
    /// </summary>
    /// <param name="position">Der 0-basierte Zeichen-Offset im Quelltext.</param>
    /// <returns>Das Symbol an der Position, oder <c>null</c>.</returns>
    public ISymbol? FindAtPosition(int position) {
        return _symbols.FindElementAtPosition(position, defaultIfNotFound: true);
    }

}
