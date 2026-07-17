using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden für <see cref="IDictionary{TKey,TValue}"/>.
/// </summary>
static class DictionaryExtensions {

    /// <summary>
    /// Liefert den Wert zum angegebenen Schlüssel oder den Standardwert von
    /// <typeparamref name="TValue"/>, wenn der Schlüssel nicht enthalten ist.
    /// </summary>
    /// <param name="dictionary">Das Wörterbuch, in dem gesucht wird.</param>
    /// <param name="key">Der nachzuschlagende Schlüssel.</param>
    /// <returns>Der gefundene Wert oder <c>default</c>.</returns>
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) {
        return dictionary.TryGetValue(key, out var value) ? value : default;
    }
}