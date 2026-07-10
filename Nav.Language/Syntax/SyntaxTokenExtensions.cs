using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

public static class SyntaxTokenExtensions {

    public static IEnumerable<SyntaxToken> OfClassification(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        return source.Where(t => t.Classification == classification);
    }

    public static IEnumerable<SyntaxToken> OfType(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        return source.Where(t => t.Type == type);
    }

    // Diese Selektoren sitzen auf dem heißen Pfad (jeder Token-Property-Accessor eines Syntax-Knotens ruft
    // FirstOrMissing auf) — daher bewusst als allokationsfreie Schleifen statt LINQ (Where/DefaultIfEmpty
    // würden pro Aufruf Iterator- und Closure-Objekte erzeugen).
    public static SyntaxToken FirstOrMissing(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        foreach (var token in source) {
            if (token.Classification == classification) {
                return token;
            }
        }

        return SyntaxToken.Missing;
    }

    public static SyntaxToken FirstOrDefault(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        foreach (var token in source) {
            if (token.Classification == classification) {
                return token;
            }
        }

        return default;
    }

    public static SyntaxToken FirstOrMissing(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        foreach (var token in source) {
            if (token.Type == type) {
                return token;
            }
        }

        return SyntaxToken.Missing;
    }

    public static SyntaxToken FirstOrDefault(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        foreach (var token in source) {
            if (token.Type == type) {
                return token;
            }
        }

        return default;
    }

    public static SyntaxToken LastOrDefault(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        var result = default(SyntaxToken);
        foreach (var token in source) {
            if (token.Classification == classification) {
                result = token;
            }
        }

        return result;
    }

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