#nullable enable

#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

static class EnumerableExtensions {

    public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> source, int expectedCapacity) {
        var result = new List<T>(expectedCapacity);
        result.AddRange(source);
        return result;
    }

    public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector) {
        return source.GroupBy(selector).Select(x => x.First());
    }

    /// <summary>
    /// Filtert aus einer Sequenz von Elementen alle Null-Objekte heraus.
    /// </summary>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?>? source) where T : class {
        if (source == null) {
            return Enumerable.Empty<T>();
        }

        // t! ist durch das vorangehende Where(t => t != null) belegt — die Flussanalyse
        // trägt die Filterung nicht über den Where-Aufruf hinweg.
        return source.Where(t => t != null).Select(t => t!);
    }

}