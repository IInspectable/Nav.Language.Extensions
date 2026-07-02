#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pharmatechnik.Nav.Language;

public interface IReadOnlySymbolCollection<out T>: IReadOnlyList<T> where T : ISymbol {

    T this[string key] { get; }

    T? TryFindSymbol(string? key);

}

public class SymbolCollection<T>: KeyedCollection<string, T>, IReadOnlySymbolCollection<T> where T : ISymbol {

    public SymbolCollection() {

    }

    public SymbolCollection(IEnumerable<T> source) {
        AddRange(source);
    }

    protected override string GetKeyForItem(T symbol) {
        if (symbol == null) {
            throw new ArgumentNullException(nameof(symbol));
        }

        return symbol.Name;
    }

    public void AddRange(params T[] values) {
        foreach (var value in values) {
            Add(value);
        }
    }

    public void AddRange(IEnumerable<T> values) {
        foreach (var value in values) {
            Add(value);
        }
    }

    public T? TryFindSymbol(string? key) {
        // Dictionary ist null, solange die Collection leer ist (KeyedCollection legt es erst beim ersten Add an)
        if (key is { Length: > 0 } && Dictionary != null && Dictionary.TryGetValue(key, out var symbol)) {
            return symbol;
        }

        return default;
    }

    public T? TryFindSymbol(T value) {
        return TryFindSymbol(GetKeyForItem(value));
    }

}
