#nullable enable

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

    public static SyntaxToken FirstOrMissing(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        return source.Where(t => t.Classification == classification).DefaultIfEmpty(SyntaxToken.Missing).First();
    }

    public static SyntaxToken FirstOrDefault(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        return source.FirstOrDefault(t => t.Classification == classification);
    }

    public static SyntaxToken FirstOrMissing(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        return source.Where(t => t.Type == type).DefaultIfEmpty(SyntaxToken.Missing).First();
    }

    public static SyntaxToken FirstOrDefault(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        return source.FirstOrDefault(t => t.Type == type);
    }

    public static SyntaxToken LastOrDefault(this IEnumerable<SyntaxToken> source, TextClassification classification) {
        return source.LastOrDefault(t => t.Classification == classification);
    }

    public static SyntaxToken LastOrDefault(this IEnumerable<SyntaxToken> source, SyntaxTokenType type) {
        return source.LastOrDefault(t => t.Type == type);
    }

}