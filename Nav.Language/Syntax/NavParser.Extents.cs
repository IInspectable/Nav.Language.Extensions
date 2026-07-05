#region Using Directives

using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;
// ReSharper disable UnusedMember.Local

#endregion

namespace Pharmatechnik.Nav.Language;

// Extent-Hilfen des Parsers: die Span(...)-Überladungen samt ExtentPart/ExtentBuilder bauen den
// umschließenden TextExtent eines Knotens aus seinen konsumierten Token und Kindknoten auf.
sealed partial class NavParser {

    /// <summary>
    /// Umschließender <see cref="TextExtent"/> über die konsumierten Token und Kindknoten eines Knotens.
    /// Die Fixed-Arity-Überladungen decken die vorkommenden Stelligkeiten ab und vermeiden das
    /// <see cref="ExtentPart"/>-Array, das die <c>params</c>-Fassung sonst bei jedem Knoten-Aufbau
    /// allozieren würde (ein Array pro Knoten auf dem Happy Path); die <c>params</c>-Fassung bleibt als
    /// Fallback für künftige höhere Stelligkeiten.
    /// </summary>
    static TextExtent Span(ExtentPart p1) {
        var builder = new ExtentBuilder();
        builder.Add(p1.Extent);
        return builder.ToExtent();
    }

    static TextExtent Span(ExtentPart p1, ExtentPart p2) {
        var builder = new ExtentBuilder();
        builder.Add(p1.Extent); builder.Add(p2.Extent);
        return builder.ToExtent();
    }

    static TextExtent Span(ExtentPart p1, ExtentPart p2, ExtentPart p3) {
        var builder = new ExtentBuilder();
        builder.Add(p1.Extent); builder.Add(p2.Extent); builder.Add(p3.Extent);
        return builder.ToExtent();
    }

    static TextExtent Span(ExtentPart p1, ExtentPart p2, ExtentPart p3, ExtentPart p4) {
        var builder = new ExtentBuilder();
        builder.Add(p1.Extent); builder.Add(p2.Extent); builder.Add(p3.Extent); builder.Add(p4.Extent);
        return builder.ToExtent();
    }

    // static TextExtent Span(ExtentPart p1, ExtentPart p2, ExtentPart p3, ExtentPart p4, ExtentPart p5) {
    //     var builder = new ExtentBuilder();
    //     builder.Add(p1.Extent); builder.Add(p2.Extent); builder.Add(p3.Extent); builder.Add(p4.Extent);
    //     builder.Add(p5.Extent);
    //     return builder.ToExtent();
    // }

    static TextExtent Span(ExtentPart p1, ExtentPart p2, ExtentPart p3, ExtentPart p4, ExtentPart p5, ExtentPart p6) {
        var builder = new ExtentBuilder();
        builder.Add(p1.Extent); builder.Add(p2.Extent); builder.Add(p3.Extent); builder.Add(p4.Extent);
        builder.Add(p5.Extent); builder.Add(p6.Extent);
        return builder.ToExtent();
    }

    static TextExtent Span(ExtentPart p1, ExtentPart p2, ExtentPart p3, ExtentPart p4, ExtentPart p5, ExtentPart p6, ExtentPart p7) {
        var builder = new ExtentBuilder();
        builder.Add(p1.Extent); builder.Add(p2.Extent); builder.Add(p3.Extent); builder.Add(p4.Extent);
        builder.Add(p5.Extent); builder.Add(p6.Extent); builder.Add(p7.Extent);
        return builder.ToExtent();
    }

    /// <summary>
    /// Ein Bestandteil eines <see cref="Span(ExtentPart)"/>-Aufrufs: trägt nur den <see cref="TextExtent"/>
    /// eines (optionalen) Tokens oder Kindknotens. Die impliziten Konvertierungen halten die Aufrufstellen
    /// unverändert lesbar (<c>Span(keyword, name, …)</c>), vermeiden aber das Boxing, das ein
    /// <c>params object[]</c> für jedes <see cref="RawToken"/>? (Nullable-Struct) erzeugen würde. Fehlende
    /// (<c>null</c>) Bestandteile tragen <see cref="TextExtent.Missing"/> bei — <see cref="ExtentBuilder"/>
    /// überspringt sie.
    /// </summary>
    readonly struct ExtentPart {

        ExtentPart(TextExtent extent) {
            Extent = extent;
        }

        public TextExtent Extent { get; }

        public static implicit operator ExtentPart(RawToken? raw)    => new(raw?.Extent  ?? TextExtent.Missing);
        public static implicit operator ExtentPart(SyntaxNode? node) => new(node?.Extent ?? TextExtent.Missing);
    }

    /// <summary>
    /// Sammelt den umschließenden <see cref="TextExtent"/> über konsumierte Token und Kindknoten. Fehlende
    /// (optionale, nicht vorhandene) Bestandteile tragen nichts bei; bleibt alles leer, ist der Extent
    /// <see cref="TextExtent.Missing"/> (z.B. ein leerer Knoten-/Transitionsblock).
    /// </summary>
    struct ExtentBuilder {

        int _start;
        int _end;
        bool _any;

        public void Add(RawToken? raw) {
            if (raw == null) {
                return;
            }

            Add(raw.Value.Extent);
        }

        public void Add(SyntaxNode? node) {
            if (node != null) {
                Add(node.Extent);
            }
        }

        public void AddRange<T>(IEnumerable<T> nodes) where T : SyntaxNode {
            foreach (var node in nodes) {
                Add(node);
            }
        }

        public void AddRange(IEnumerable<RawToken> tokens) {
            foreach (var token in tokens) {
                Add(token.Extent);
            }
        }

        public void Add(TextExtent extent) {
            if (extent.IsMissing) {
                return;
            }

            if (!_any) {
                _start = extent.Start;
                _end   = extent.End;
                _any   = true;
                return;
            }

            if (extent.Start < _start) {
                _start = extent.Start;
            }

            if (extent.End > _end) {
                _end = extent.End;
            }
        }

        public TextExtent ToExtent() {
            return _any ? TextExtent.FromBounds(_start, _end) : TextExtent.Missing;
        }
    }

}
