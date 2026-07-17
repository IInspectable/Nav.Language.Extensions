#region Using Directives

using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Common; 

/// <summary>
/// Interop-Helfer rund um den Roslyn-<see cref="TextSpan"/>: Übersetzung in den engine-eigenen
/// <see cref="TextExtent"/> sowie Zuschnitt eines Spans auf einen Bereich.
/// </summary>
public static class TextSpanExtensions {

    /// <summary>Übersetzt einen Roslyn-<see cref="TextSpan"/> in den engine-eigenen <see cref="TextExtent"/> (Start und Länge).</summary>
    /// <param name="span">Der zu übersetzende Roslyn-Span.</param>
    public static TextExtent ToTextExtent(this TextSpan span) {
        return new(start: span.Start, length: span.Length);
    }

    /// <summary>
    /// Beschneidet <paramref name="span"/> auf die Grenzen von <paramref name="range"/>: Start und Ende
    /// werden in den Bereich hineingeklemmt, sodass das Ergebnis vollständig innerhalb von
    /// <paramref name="range"/> liegt.
    /// </summary>
    /// <param name="span">Der zuzuschneidende Span.</param>
    /// <param name="range">Der begrenzende Bereich.</param>
    public static TextSpan Trim(this TextSpan span, TextSpan range) {

        int start = span.Start.Trim(start: range.Start, end: range.End);
        int end   = span.End.Trim(start: range.Start, end: range.End);

        return TextSpan.FromBounds(start, end);

    }

}