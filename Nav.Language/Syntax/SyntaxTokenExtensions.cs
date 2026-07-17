using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Selektoren über Folgen von <see cref="SyntaxToken"/>: Filtern nach
/// <see cref="TextClassification"/> bzw. <see cref="SyntaxTokenType"/> sowie First-/Last-Zugriffe.
/// Die <c>FirstOrMissing</c>-Varianten sind die Grundlage der Token-Properties der Syntax-Knoten
/// (Muster: <c>ChildTokens().FirstOrMissing(SyntaxTokenType.…)</c>).
/// </summary>
public static class SyntaxTokenExtensions {

    /// <summary>Filtert die Folge auf Token der Klassifikation <paramref name="classification"/>.</summary>
    public static IEnumerable<SyntaxToken> OfClassification(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        return source.Where(t => t.Classification == classification);
    }

    /// <summary>Filtert die Folge auf Token des Typs <paramref name="type"/>.</summary>
    public static IEnumerable<SyntaxToken> OfType(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        return source.Where(t => t.Type == type);
    }

    // Diese Selektoren sitzen auf dem heißen Pfad (jeder Token-Property-Accessor eines Syntax-Knotens ruft
    // FirstOrMissing auf) — daher bewusst als allokationsfreie Schleifen statt LINQ (Where/DefaultIfEmpty
    // würden pro Aufruf Iterator- und Closure-Objekte erzeugen).
    /// <summary>
    /// Das erste Token der Klassifikation <paramref name="classification"/> —
    /// <see cref="SyntaxToken.Missing"/>, wenn keins vorhanden ist.
    /// </summary>
    public static SyntaxToken FirstOrMissing(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        foreach (var token in source) {
            if (token.Classification == classification) {
                return token;
            }
        }

        return SyntaxToken.Missing;
    }

    /// <summary>
    /// Das erste Token der Klassifikation <paramref name="classification"/> —
    /// <c>default(SyntaxToken)</c>, wenn keins vorhanden ist.
    /// </summary>
    public static SyntaxToken FirstOrDefault(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        foreach (var token in source) {
            if (token.Classification == classification) {
                return token;
            }
        }

        return default;
    }

    /// <summary>
    /// Das erste Token des Typs <paramref name="type"/> — <see cref="SyntaxToken.Missing"/>, wenn
    /// keins vorhanden ist. Das Arbeitspferd der Token-Properties der Syntax-Knoten.
    /// </summary>
    public static SyntaxToken FirstOrMissing(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        foreach (var token in source) {
            if (token.Type == type) {
                return token;
            }
        }

        return SyntaxToken.Missing;
    }

    /// <summary>
    /// Das erste Token des Typs <paramref name="type"/> — <c>default(SyntaxToken)</c>, wenn keins
    /// vorhanden ist.
    /// </summary>
    public static SyntaxToken FirstOrDefault(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        foreach (var token in source) {
            if (token.Type == type) {
                return token;
            }
        }

        return default;
    }

    /// <summary>
    /// Das letzte Token der Klassifikation <paramref name="classification"/> —
    /// <c>default(SyntaxToken)</c>, wenn keins vorhanden ist.
    /// </summary>
    public static SyntaxToken LastOrDefault(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        var result = default(SyntaxToken);
        foreach (var token in source) {
            if (token.Classification == classification) {
                result = token;
            }
        }

        return result;
    }

    /// <summary>
    /// Das letzte Token des Typs <paramref name="type"/> — <c>default(SyntaxToken)</c>, wenn keins
    /// vorhanden ist.
    /// </summary>
    public static SyntaxToken LastOrDefault(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        var result = default(SyntaxToken);
        foreach (var token in source) {
            if (token.Type == type) {
                result = token;
            }
        }

        return result;
    }

}