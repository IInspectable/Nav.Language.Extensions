using System;
using System.Collections.Immutable;
using System.Diagnostics;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

[Serializable]
[DebuggerDisplay("{" + nameof(ToDebuggerDisplayString) + "(), nq}")]
public readonly struct SyntaxToken: IExtent, IEquatable<SyntaxToken> {

    const int BitMask                = 0xFF;
    const int TypeBitShift           = 8;
    const int ClassificationBitShift = 0;

    readonly int                          _classificationAndType;
    readonly ImmutableArray<SyntaxTrivia> _leadingTrivia;
    readonly ImmutableArray<SyntaxTrivia> _trailingTrivia;

    internal SyntaxToken(SyntaxNode parent, SyntaxTokenType type, TextClassification classification, TextExtent extent)
        : this(parent, type, classification, extent, leadingTrivia: default, trailingTrivia: default) {
    }

    internal SyntaxToken(SyntaxNode parent, SyntaxTokenType type, TextClassification classification, TextExtent extent,
                         ImmutableArray<SyntaxTrivia> leadingTrivia, ImmutableArray<SyntaxTrivia> trailingTrivia) {
        Extent          = extent;
        Parent          = parent;
        _leadingTrivia  = leadingTrivia;
        _trailingTrivia = trailingTrivia;

        _classificationAndType = ((int) type << TypeBitShift) | ((int) classification << ClassificationBitShift);
    }

    public static readonly SyntaxToken Missing = new(null, SyntaxTokenType.Unknown, TextClassification.Unknown, TextExtent.Missing);
    public static readonly SyntaxToken Empty   = new(null, SyntaxTokenType.Unknown, TextClassification.Unknown, TextExtent.Empty);

    /// <summary>Der Quelltext-Ausschnitt dieses Tokens (ohne angehängte Trivia; ≙ Roslyn <c>Span</c>).</summary>
    public TextExtent Extent { get; }

    /// <summary>
    /// Der Quelltext-Ausschnitt dieses Tokens samt angehängter Trivia (≙ Roslyn <c>FullSpan</c>) — das
    /// Gegenstück zum trivia-freien <see cref="Extent"/> (≙ Roslyn <c>Span</c>). Reicht vom Anfang der
    /// <see cref="LeadingTrivia"/> bis zum Ende der <see cref="TrailingTrivia"/>; hängt keine Trivia an,
    /// fällt er mit <see cref="Extent"/> zusammen. Für ein fehlendes Token ist er ebenfalls
    /// <see cref="TextExtent.Missing"/>.
    /// </summary>
    /// <remarks>
    /// Anders als <see cref="SyntaxNode.GetFullExtent"/> sind hier <i>genau</i> die am Token angehängten
    /// Trivia-Stücke maßgeblich (keine zeilenbasierten Grenzen, keine <c>onlyWhiteSpace</c>-Variante) —
    /// das ist die unmittelbare Token-Sicht, aus der das Knoten-Pendant abgeleitet wird.
    /// </remarks>
    public TextExtent FullExtent {
        get {
            if (Extent.IsMissing) {
                return Extent;
            }

            var leading  = LeadingTrivia;
            var trailing = TrailingTrivia;

            var start = leading.IsEmpty  ? Start : leading[0].Start;
            var end   = trailing.IsEmpty ? End   : trailing[trailing.Length - 1].End;

            return TextExtent.FromBounds(start, end);
        }
    }

    /// <summary>
    /// Die Leading-Trivia dieses Tokens (Whitespace/Zeilenende/Kommentare bis hierher) — das echte
    /// Roslyn-Modell. Liefert nie <c>default</c>, sondern eine leere Sequenz, wenn keine Trivia anhängt.
    /// </summary>
    public ImmutableArray<SyntaxTrivia> LeadingTrivia => _leadingTrivia.IsDefault ? ImmutableArray<SyntaxTrivia>.Empty : _leadingTrivia;

    /// <summary>
    /// Die Trailing-Trivia dieses Tokens (Whitespace/Kommentare bis einschließlich des ersten Zeilenendes) —
    /// das echte Roslyn-Modell. Liefert nie <c>default</c>, sondern eine leere Sequenz, wenn keine Trivia anhängt.
    /// </summary>
    public ImmutableArray<SyntaxTrivia> TrailingTrivia => _trailingTrivia.IsDefault ? ImmutableArray<SyntaxTrivia>.Empty : _trailingTrivia;

    [CanBeNull]
    public Location GetLocation() {
        return SyntaxTree?.SourceText.GetLocation(Extent);
    }

    public TextClassification Classification => (TextClassification) ((_classificationAndType >> ClassificationBitShift) & BitMask);

    public SyntaxTokenType Type => (SyntaxTokenType) ((_classificationAndType >> TypeBitShift) & BitMask);

    public int  Start     => Extent.Start;
    public int  Length    => Extent.Length;
    public int  End       => Extent.End;
    public bool IsMissing => Parent == null || Extent.IsMissing;

    [CanBeNull]
    public SyntaxNode Parent { get; }

    [CanBeNull]
    public SyntaxTree SyntaxTree => Parent?.SyntaxTree;

    public SyntaxToken NextToken() {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, nextToken: true) ?? Missing;
    }

    public SyntaxToken NextToken(SyntaxTokenType type) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, type, nextToken: true) ?? Missing;
    }

    public SyntaxToken NextToken(TextClassification tokenClassification) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, tokenClassification, nextToken: true) ?? Missing;
    }

    public SyntaxToken PreviousToken() {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, nextToken: false) ?? Missing;
    }

    public SyntaxToken PreviousToken(SyntaxTokenType type) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, type, nextToken: false) ?? Missing;
    }

    public SyntaxToken PreviousToken(TextClassification tokenClassification) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, tokenClassification, nextToken: false) ?? Missing;
    }

    public override string ToString() {
        if (IsMissing) {
            return String.Empty;
        }

        return SyntaxTree?.SourceText.Substring(Start, Length) ?? String.Empty;
    }

    public string ToDebuggerDisplayString() {
        return $"{Extent} {Type} ({Classification})";
    }

    // Gleichheit bewusst nur über Identität des Tokens (Parent, Extent, Typ/Klassifikation) — die
    // angehängte Trivia bleibt ausgeklammert. So bleibt die Gleichheits-Semantik trotz der zusätzlichen
    // Felder exakt wie vor der Trivia-Erweiterung; außerdem vermeidet das die reflektive (und für
    // ImmutableArray fehleranfällige) Default-Struct-Gleichheit.
    public bool Equals(SyntaxToken other) {
        return _classificationAndType == other._classificationAndType &&
               Extent.Equals(other.Extent)                            &&
               ReferenceEquals(Parent, other.Parent);
    }

    public override bool Equals(object obj) {
        return obj is SyntaxToken other && Equals(other);
    }

    public override int GetHashCode() {
        unchecked {
            var hash = _classificationAndType;
            hash = (hash * 397) ^ Extent.GetHashCode();
            hash = (hash * 397) ^ (Parent?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public static bool operator ==(SyntaxToken left, SyntaxToken right) => left.Equals(right);
    public static bool operator !=(SyntaxToken left, SyntaxToken right) => !left.Equals(right);

}