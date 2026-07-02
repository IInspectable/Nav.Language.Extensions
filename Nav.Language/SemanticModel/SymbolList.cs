#region Using Directives

using System.Linq;
using System.Collections;
using System.Collections.Generic;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

public sealed class SymbolList: IReadOnlyList<ISymbol> {

    readonly IReadOnlyList<ISymbol> _symbols;

    public SymbolList(): this(null) {
    }

    public SymbolList(IEnumerable<ISymbol> symbols) {

        var symbolList = new List<ISymbol>(symbols ?? Enumerable.Empty<ISymbol>());
        symbolList.Sort((x, y) => x.Start - y.Start);

        _symbols = symbolList;
    }

    public IEnumerator<ISymbol> GetEnumerator() {
        return _symbols.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public int Count => _symbols.Count;

    public ISymbol this[int index] => _symbols[index];

    [NotNull]
    public IEnumerable<ISymbol> this[TextExtent extent, bool includeOverlapping = false] => _symbols.GetElements(extent, includeOverlapping);

    [CanBeNull]
    public ISymbol FindAtPosition(int position) {
        return _symbols.FindElementAtPosition(position, defaultIfNotFound: true);
    }

}