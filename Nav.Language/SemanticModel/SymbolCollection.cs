using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Lese-Sicht auf eine namens-indizierte Symbolmenge: Listenzugriff in Einfügereihenfolge
/// (<see cref="IReadOnlyList{T}"/>) plus Schlüsselzugriff über den Symbolnamen
/// (<see cref="ISymbol.Name"/>). In dieser Form publizieren die Modell-Aggregate ihre Symbole,
/// z.B. <see cref="CodeGenerationUnit.TaskDefinitions"/> oder
/// <see cref="ITaskDefinitionSymbol.NodeDeclarations"/>; kovariant, damit eine
/// <see cref="SymbolCollection{T}"/> konkreter Symbole als Collection ihres Interfaces
/// durchgereicht werden kann.
/// </summary>
/// <typeparam name="T">Der Symboltyp der Einträge.</typeparam>
public interface IReadOnlySymbolCollection<out T>: IReadOnlyList<T> where T : ISymbol {

    /// <summary>
    /// Liefert das Symbol mit dem Namen <paramref name="key"/>. Für unbekannte Schlüssel wird —
    /// wie beim Dictionary-Zugriff — eine <see cref="KeyNotFoundException"/> geworfen; wer eine
    /// Fehl-Suche erwartet, nutzt <see cref="TryFindSymbol"/>.
    /// </summary>
    /// <param name="key">Der Symbolname.</param>
    T this[string key] { get; }

    /// <summary>
    /// Sucht das Symbol mit dem Namen <paramref name="key"/> und liefert <c>null</c>
    /// (bzw. <c>default</c>), wenn kein solches Symbol enthalten ist — auch für <c>null</c> oder
    /// leere Schlüssel, statt wie der Indexer zu werfen.
    /// </summary>
    /// <param name="key">Der Symbolname, oder <c>null</c>.</param>
    T? TryFindSymbol(string? key);

}

/// <summary>
/// Die veränderbare Standard-Implementierung von <see cref="IReadOnlySymbolCollection{T}"/> auf
/// Basis von <see cref="KeyedCollection{TKey,TItem}"/>: der Schlüssel eines Eintrags ist sein
/// <see cref="ISymbol.Name"/> (siehe <see cref="GetKeyForItem"/>). In dieser Form sammeln die
/// Builder (<see cref="TaskDefinitionSymbolBuilder"/>, <see cref="TaskDeclarationSymbolBuilder"/>,
/// <see cref="CodeGenerationUnitBuilder"/>) ihre Symbole ein; die Eindeutigkeit der Namen
/// erzwingt die <see cref="KeyedCollection{TKey,TItem}"/> beim <see cref="Collection{T}.Add"/>.
/// </summary>
/// <typeparam name="T">Der Symboltyp der Einträge.</typeparam>
public class SymbolCollection<T>: KeyedCollection<string, T>, IReadOnlySymbolCollection<T> where T : ISymbol {

    /// <summary>Erzeugt eine leere Collection.</summary>
    public SymbolCollection() {

    }

    /// <summary>
    /// Erzeugt eine Collection mit den Symbolen aus <paramref name="source"/> (in deren
    /// Reihenfolge).
    /// </summary>
    /// <param name="source">Die aufzunehmenden Symbole.</param>
    public SymbolCollection(IEnumerable<T> source) {
        AddRange(source);
    }

    /// <summary>
    /// Der Schlüssel eines Symbols ist sein <see cref="ISymbol.Name"/>.
    /// </summary>
    /// <param name="symbol">Das Symbol, dessen Schlüssel bestimmt wird; darf nicht <c>null</c> sein.</param>
    /// <returns>Der Name des Symbols.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> ist <c>null</c>.</exception>
    protected override string GetKeyForItem(T symbol) {
        if (symbol == null) {
            throw new ArgumentNullException(nameof(symbol));
        }

        return symbol.Name;
    }

    /// <summary>Fügt die angegebenen Symbole der Reihe nach hinzu.</summary>
    /// <param name="values">Die hinzuzufügenden Symbole.</param>
    public void AddRange(params T[] values) {
        foreach (var value in values) {
            Add(value);
        }
    }

    /// <summary>Fügt die angegebenen Symbole der Reihe nach hinzu.</summary>
    /// <param name="values">Die hinzuzufügenden Symbole.</param>
    public void AddRange(IEnumerable<T> values) {
        foreach (var value in values) {
            Add(value);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Auch auf einer leeren Collection sicher — die <see cref="KeyedCollection{TKey,TItem}"/>
    /// legt ihr Lookup-Dictionary erst beim ersten <see cref="Collection{T}.Add"/> an.
    /// </remarks>
    public T? TryFindSymbol(string? key) {
        // Dictionary ist null, solange die Collection leer ist (KeyedCollection legt es erst beim ersten Add an)
        if (key is { Length: > 0 } && Dictionary != null && Dictionary.TryGetValue(key, out var symbol)) {
            return symbol;
        }

        return default;
    }

    /// <summary>
    /// Sucht das Symbol, das unter dem Namen von <paramref name="value"/> registriert ist —
    /// nicht notwendig dieselbe Instanz. Liefert <c>null</c> (bzw. <c>default</c>), wenn kein
    /// Symbol dieses Namens enthalten ist.
    /// </summary>
    /// <param name="value">Das Symbol, dessen Name als Suchschlüssel dient; darf nicht <c>null</c> sein.</param>
    public T? TryFindSymbol(T value) {
        return TryFindSymbol(GetKeyForItem(value));
    }

}
