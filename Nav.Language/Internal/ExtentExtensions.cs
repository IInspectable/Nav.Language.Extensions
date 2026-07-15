#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal;

/// <summary>
/// Positionsbasierte Nachschlage-Helfer über eine nach <see cref="IExtent.Start"/> aufsteigend sortierte
/// Liste von <see cref="IExtent"/>-Elementen (Tokens, Symbole). Die Verfahren setzen diese Sortierung voraus
/// und arbeiten daher mit Binärsuche (<see cref="FindIndexAtPosition{T}"/>), sodass Positions-Abfragen
/// logarithmisch statt linear laufen. Genutzt von den positionsindizierten Sammlungen der Engine — etwa
/// <see cref="Text.SourceText"/>, <see cref="SyntaxTokenList"/> und <see cref="SymbolList"/>.
/// </summary>
static class ExtentExtensions {

    /// <summary>
    /// Liefert – ausgehend von <paramref name="currentToken"/> – das nächste bzw. vorige Element innerhalb
    /// des angegebenen <paramref name="extent"/>. Liegt kein solches Element vor oder verlässt es den Extent,
    /// wird <paramref name="missing"/> zurückgegeben.
    /// </summary>
    public static TElement NextOrPreviousElement<TExtent, TElement>(this IReadOnlyList<TElement> tokens, TExtent? extent, TElement currentToken, bool nextToken, TElement missing)
        where TExtent : IExtent
        where TElement : IExtent {

        // Defensiv: `currentToken` ist zwar non-null annotiert, der Guard bleibt bewusst stehen.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (extent == null || currentToken == null) {
            return missing;
        }

        int index = FindIndexAtPosition(tokens, currentToken.Start);
        if (index < 0) {
            return missing; // eingenlich könnte man hier auch eine ArgumentException werfen...
        }

        index = nextToken ? index + 1 : index - 1;

        if (index < 0 || index >= tokens.Count) {
            return missing;
        }

        var resultToken = tokens[index];

        if (resultToken.Start < extent.Start || resultToken.End > extent.End) {
            return missing;
        }

        return resultToken;
    }

    /// <summary>
    /// Liefert die Elemente, die im angegebenen <paramref name="extent"/> liegen. Mit
    /// <paramref name="includeOverlapping"/> werden auch Elemente einbezogen, die den Extent nur
    /// überlappen; andernfalls nur vollständig enthaltene Elemente.
    /// </summary>
    public static IEnumerable<TElement> GetElements<TElement, TExtent>(this IReadOnlyList<TElement> tokens,
                                                                       TExtent extent, bool includeOverlapping)
        where TElement : IExtent
        where TExtent : IExtent {

        if (!includeOverlapping) {
            return GetElementsInside(tokens, extent);
        }

        return GetElementsIncludeOverlapping(tokens, extent);
    }

    /// <summary>
    /// Liefert alle Elemente, die den angegebenen <paramref name="extent"/> berühren oder überlappen.
    /// Für eine Punktsuche (<c>Start == End</c>) wird das an dieser Position liegende Element geliefert.
    /// </summary>
    static IEnumerable<TElement> GetElementsIncludeOverlapping<TElement, TExtent>(this IReadOnlyList<TElement> tokens,
                                                                                  TExtent extent)
        where TElement : IExtent
        where TExtent : IExtent {

        int startIndex = tokens.FindIndexAtOrBeforePosition(extent.Start);
        if (startIndex < 0) {
            yield break;
        }

        // Sonderlocke für "Punktsuche"
        if (extent.Start == extent.End) {
            yield return tokens[startIndex];

            yield break;
        }

        for (int index = startIndex; index < tokens.Count; index++) {
            var token = tokens[index];
            if (token.Start >= extent.End) {
                break;
            }

            yield return token;
        }
    }

    /// <summary>
    /// Liefert alle Elemente, die vollständig innerhalb des angegebenen <paramref name="extent"/> liegen.
    /// </summary>
    static IEnumerable<TElement> GetElementsInside<TElement, TExtent>(this IReadOnlyList<TElement> tokens,
                                                                      TExtent extent)
        where TElement : IExtent
        where TExtent : IExtent {

        int startIndex = tokens.FindIndexAtOrBeforePosition(extent.Start);
        if (startIndex < 0) {
            yield break;
        }

        for (int index = startIndex; index < tokens.Count; index++) {
            var token = tokens[index];

            // FindIndexAtOrBeforePosition liefert bewusst das Element *an oder vor* extent.Start –
            // liegt der Start des gefundenen Elements noch vor dem Extent, gehört es nicht dazu und
            // wird übersprungen (Elemente liegen nicht zwingend lückenlos aneinander).
            if (token.Start < extent.Start) {
                continue;
            }

            if (token.End > extent.End) {
                break;
            }

            yield return token;
        }
    }

    /// <summary>
    /// Liefert das Element, das die angegebene Position enthält (<c>Start &lt;= position &lt; End</c>).
    /// Wird kein solches Element gefunden, wird bei <paramref name="defaultIfNotFound"/> = <c>true</c>
    /// <c>default</c> zurückgegeben, andernfalls eine <see cref="ArgumentOutOfRangeException"/> geworfen.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Kein Element enthält <paramref name="position"/> und <paramref name="defaultIfNotFound"/> ist <c>false</c>.
    /// </exception>
    public static T? FindElementAtPosition<T>(this IReadOnlyList<T> tokens, int position, bool defaultIfNotFound = false) where T : IExtent {

        int index = tokens.FindIndexAtOrBeforePosition(position);
        if (index < 0) {
            if (defaultIfNotFound) {
                return default;
            }

            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var token = tokens[index];
        if (token.Start <= position && token.End > position) {
            return token;
        }

        if (defaultIfNotFound) {
            return default;
        }

        throw new ArgumentOutOfRangeException(nameof(position));
    }

    /// <summary>
    /// Findet den Index des letzten Tokens, dessen Start kleiner oder gleich der angegebenen Position ist.
    /// Liefert <c>-1</c>, wenn alle Tokens hinter der Position beginnen – insbesondere auch für negative Positionen.
    /// </summary>
    public static int FindIndexAtOrBeforePosition<T>(this IReadOnlyList<T> tokens, int pos) where T : IExtent {
        // Negative Positionen liefern bewusst -1 (kein Token beginnt davor); die öffentlichen
        // Einstiegspunkte fangen ungültige Ränder ohnehin schon ab.
        var index = FindIndexAtPosition(tokens, pos);
        if (index < 0) {
            index = ~index - 1;
        }

        return index;
    }

    /// <summary>
    /// Binärsuche nach dem Token, dessen Start exakt der angegebenen Position entspricht. Liefert dessen
    /// Index oder – bei fehlendem Treffer – das bitweise Komplement des Einfüge-Index (analog zu
    /// <see cref="System.Collections.Generic.List{T}.BinarySearch(T)"/>), also stets einen negativen Wert.
    /// </summary>
    public static int FindIndexAtPosition<T>(this IReadOnlyList<T> tokens, int pos) where T : IExtent {

        int iMin = 0;
        int iMax = tokens.Count - 1;
        while (iMin <= iMax) {

            int iMid  = iMin + (iMax - iMin >> 1);
            int value = tokens[iMid].Start;

            if (value == pos) {
                return iMid;
            }

            if (value < pos) {
                iMin = iMid + 1;
            } else {
                iMax = iMid - 1;
            }
        }

        return ~iMin;
    }

}
