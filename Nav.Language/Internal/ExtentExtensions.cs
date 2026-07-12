#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Internal;

static class ExtentExtensions {

    public static TElement NextOrPreviousElement<TExtent, TElement>(this IReadOnlyList<TElement> tokens, TExtent? extent, TElement currentToken, bool nextToken, TElement missing)
        where TExtent : IExtent where TElement : IExtent {

        // Defensiv: `currentToken` ist zwar non-null annotiert, der Guard bleibt bewusst stehen.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if(extent ==null || currentToken ==null) {
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

    public static IEnumerable<TElement> GetElements<TElement, TExtent>(this IReadOnlyList<TElement> tokens,
                                                                       TExtent extent, bool includeOverlapping)
        where TElement : IExtent
        where TExtent : IExtent {

        if (!includeOverlapping) {
            return GetElementsInside(tokens, extent);
        }

        return GetElementsIncludeOverlapping(tokens, extent);
    }

    static IEnumerable<TElement> GetElementsIncludeOverlapping<TElement, TExtent>(this IReadOnlyList<TElement> tokens,
                                                                                  TExtent extent)
        where TElement : IExtent
        where TExtent : IExtent {

        int startIndex = tokens.FindIndexAtOrBeforePosition(extent.Start);
        if (startIndex < 0) {
            yield break;
        }

        // Sonderlocke für "Punktsuche"
        if(extent.Start == extent.End) {
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

    static IEnumerable<TElement> GetElementsInside<TElement, TExtent>(this IReadOnlyList<TElement> tokens, 
                                                                      TExtent extent) 
        where TElement: IExtent
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

    public static T? FindElementAtPosition<T>(this IReadOnlyList<T> tokens, int position, bool defaultIfNotFound=false) where T : IExtent {

        int index = tokens.FindIndexAtOrBeforePosition(position);
        if (index < 0) {
            if(defaultIfNotFound) {
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
    /// Findet den Index des ersten Tokens, dessen Start gleich der angegebenen Position ist. 
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