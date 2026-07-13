using System;
using System.Diagnostics;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein signifikantes Token des Nav-Syntaxbaums — nach dem Roslyn-Vorbild des <c>SyntaxToken</c>: ein
/// Wert-Typ mit lexikalischem <see cref="Type"/>, kontextabhängiger <see cref="Classification"/> (vom
/// Parser vergeben, z.B. für die Einfärbung) und seinem <see cref="Extent"/> im Quelltext. Das Token kennt
/// den Knoten, der es konsumiert hat (<see cref="Parent"/>), und trägt seine umgebende Trivia als
/// <see cref="LeadingTrivia"/>/<see cref="TrailingTrivia"/>. Typ und Klassifikation teilen sich intern ein
/// <c>int</c>-Feld (je ein Byte).
/// </summary>
[Serializable]
[DebuggerDisplay("{" + nameof(ToDebuggerDisplayString) + "(), nq}")]
public readonly struct SyntaxToken: IExtent, IEquatable<SyntaxToken> {

    const int BitMask                = 0xFF;
    const int TypeBitShift           = 8;
    const int ClassificationBitShift = 0;

    readonly int              _classificationAndType;
    readonly SyntaxTriviaList _leadingTrivia;
    readonly SyntaxTriviaList _trailingTrivia;

    internal SyntaxToken(SyntaxNode? parent, SyntaxTokenType type, TextClassification classification, TextExtent extent)
        : this(parent, type, classification, extent, leadingTrivia: default, trailingTrivia: default) {
    }

    internal SyntaxToken(SyntaxNode? parent, SyntaxTokenType type, TextClassification classification, TextExtent extent,
                         SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia) {
        Extent          = extent;
        Parent          = parent;
        _leadingTrivia  = leadingTrivia;
        _trailingTrivia = trailingTrivia;

        _classificationAndType = ((int) type << TypeBitShift) | ((int) classification << ClassificationBitShift);
    }

    /// <summary>
    /// Das fehlende Token (<see cref="IsMissing"/> <c>true</c>): kein <see cref="Parent"/>, Extent
    /// <see cref="TextExtent.Missing"/> — das Ergebnis erfolgloser Token-Lookups (z.B.
    /// <see cref="SyntaxTokenList.FindAtPosition"/> oder <see cref="NextToken()"/> am Rand).
    /// </summary>
    public static readonly SyntaxToken Missing = new(null, SyntaxTokenType.Unknown, TextClassification.Unknown, TextExtent.Missing);
    /// <summary>
    /// Das leere Token: kein <see cref="Parent"/>, aber — anders als <see cref="Missing"/> — ein gültiger,
    /// nullbreiter Extent (<see cref="TextExtent.Empty"/>). <see cref="IsMissing"/> ist mangels Parent
    /// dennoch <c>true</c>.
    /// </summary>
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
    /// Die Leading-Trivia dieses Tokens (Whitespace/Zeilenende/Kommentare — auch strukturierte Trivia-Stücke,
    /// siehe <see cref="SyntaxTrivia.HasStructure"/> — bis hierher) — das echte
    /// Roslyn-Modell. Liefert nie <c>default</c>, sondern eine leere Sequenz, wenn keine Trivia anhängt.
    /// </summary>
    public SyntaxTriviaList LeadingTrivia => _leadingTrivia;

    /// <summary>
    /// Die Trailing-Trivia dieses Tokens (Whitespace/Kommentare — auch strukturierte Trivia-Stücke, siehe
    /// <see cref="SyntaxTrivia.HasStructure"/> — bis einschließlich des ersten Zeilenendes) —
    /// das echte Roslyn-Modell. Liefert nie <c>default</c>, sondern eine leere Sequenz, wenn keine Trivia anhängt.
    /// </summary>
    public SyntaxTriviaList TrailingTrivia => _trailingTrivia;

    /// <summary>
    /// Die <see cref="Location"/> (Datei + Zeilen-/Spaltenbereich) dieses Tokens, oder <c>null</c> ohne
    /// zugehörigen <see cref="SyntaxTree"/> (fehlendes Token).
    /// </summary>
    public Location? GetLocation() {
        return SyntaxTree?.SourceText.GetLocation(Extent);
    }

    /// <summary>
    /// Die kontextabhängige Klassifikation dieses Tokens — vom Parser anhand der Position im Baum vergeben
    /// (dasselbe Lexem kann je Kontext anders klassifiziert sein, z.B. ein Bezeichner als Task-Name).
    /// </summary>
    public TextClassification Classification => (TextClassification) ((_classificationAndType >> ClassificationBitShift) & BitMask);

    /// <summary>Der lexikalische Typ dieses Tokens (vom Lexer bestimmt, siehe <see cref="SyntaxTokenType"/>).</summary>
    public SyntaxTokenType Type => (SyntaxTokenType) ((_classificationAndType >> TypeBitShift) & BitMask);

    /// <summary>Die Startposition dieses Tokens im Quelltext (inklusiv).</summary>
    public int  Start     => Extent.Start;
    /// <summary>Die Länge dieses Tokens in Zeichen.</summary>
    public int  Length    => Extent.Length;
    /// <summary>Die Endposition dieses Tokens im Quelltext (exklusiv).</summary>
    public int  End       => Extent.End;
    /// <summary>
    /// Ob das Token fehlt: kein <see cref="Parent"/> (nie in einen Baum eingehängt, wie
    /// <see cref="Missing"/>/<see cref="Empty"/>) oder ein Missing-<see cref="Extent"/>.
    /// </summary>
    public bool IsMissing => Parent == null || Extent.IsMissing;

    /// <summary>
    /// Der Knoten, dem dieses Token direkt zugeordnet ist (der es konsumiert hat — bei Token strukturierter
    /// Trivia deren <see cref="StructuredTriviaSyntax"/>-Knoten), oder <c>null</c> für ein fehlendes Token.
    /// </summary>
    public SyntaxNode? Parent { get; }

    /// <summary>Der <see cref="SyntaxTree"/> des <see cref="Parent"/>-Knotens, oder <c>null</c> für ein fehlendes Token.</summary>
    public SyntaxTree? SyntaxTree => Parent?.SyntaxTree;

    /// <summary>
    /// Das nächste Token im flachen <see cref="SyntaxTree.Tokens"/>-Strom — <b>parent-lokal</b>: liegt es
    /// außerhalb des Extents des <see cref="Parent"/>-Knotens, ist das Ergebnis <see cref="Missing"/>
    /// (ebenso für ein fehlendes Token).
    /// </summary>
    public SyntaxToken NextToken() {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, nextToken: true) ?? Missing;
    }

    /// <summary>
    /// Das nächste Token vom Typ <paramref name="type"/> innerhalb des <see cref="Parent"/>-Extents
    /// (parent-lokal, siehe <see cref="NextToken()"/>), oder <see cref="Missing"/>.
    /// </summary>
    public SyntaxToken NextToken(SyntaxTokenType type) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, type, nextToken: true) ?? Missing;
    }

    /// <summary>
    /// Das nächste Token mit der Klassifikation <paramref name="tokenClassification"/> innerhalb des
    /// <see cref="Parent"/>-Extents (parent-lokal, siehe <see cref="NextToken()"/>), oder <see cref="Missing"/>.
    /// </summary>
    public SyntaxToken NextToken(TextClassification tokenClassification) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, tokenClassification, nextToken: true) ?? Missing;
    }

    /// <summary>
    /// Das vorige Token im flachen <see cref="SyntaxTree.Tokens"/>-Strom — <b>parent-lokal</b>: liegt es
    /// außerhalb des Extents des <see cref="Parent"/>-Knotens, ist das Ergebnis <see cref="Missing"/>
    /// (ebenso für ein fehlendes Token).
    /// </summary>
    public SyntaxToken PreviousToken() {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, nextToken: false) ?? Missing;
    }

    /// <summary>
    /// Das vorige Token vom Typ <paramref name="type"/> innerhalb des <see cref="Parent"/>-Extents
    /// (parent-lokal, siehe <see cref="PreviousToken()"/>), oder <see cref="Missing"/>.
    /// </summary>
    public SyntaxToken PreviousToken(SyntaxTokenType type) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, type, nextToken: false) ?? Missing;
    }

    /// <summary>
    /// Das vorige Token mit der Klassifikation <paramref name="tokenClassification"/> innerhalb des
    /// <see cref="Parent"/>-Extents (parent-lokal, siehe <see cref="PreviousToken()"/>), oder <see cref="Missing"/>.
    /// </summary>
    public SyntaxToken PreviousToken(TextClassification tokenClassification) {
        return SyntaxTree?.Tokens.NextOrPrevious(Parent, this, tokenClassification, nextToken: false) ?? Missing;
    }

    /// <summary>Der Quelltext dieses Tokens (sein <see cref="Extent"/>, ohne Trivia); leer für ein fehlendes Token.</summary>
    public override string ToString() {
        if (IsMissing) {
            return String.Empty;
        }

        return SyntaxTree?.SourceText.Substring(Start, Length) ?? String.Empty;
    }

    /// <summary>Kompakte Debug-Darstellung: Ausschnitt, Typ und Klassifikation.</summary>
    public string ToDebuggerDisplayString() {
        return $"{Extent} {Type} ({Classification})";
    }

    // Gleichheit bewusst nur über Identität des Tokens (Parent, Extent, Typ/Klassifikation) — die
    // angehängte Trivia bleibt ausgeklammert. So bleibt die Gleichheits-Semantik trotz der zusätzlichen
    // Felder exakt wie vor der Trivia-Erweiterung; außerdem vermeidet das die reflektive (und für die
    // Trivia-Sichten fehleranfällige) Default-Struct-Gleichheit.
    /// <summary>
    /// Wertgleichheit über die Token-Identität: <see cref="Type"/>/<see cref="Classification"/>,
    /// <see cref="Extent"/> und <see cref="Parent"/>-Referenz — die angehängte Trivia bleibt bewusst
    /// ausgeklammert.
    /// </summary>
    public bool Equals(SyntaxToken other) {
        return _classificationAndType == other._classificationAndType &&
               Extent.Equals(other.Extent)                            &&
               ReferenceEquals(Parent, other.Parent);
    }

    /// <summary>Gleichheit gemäß <see cref="Equals(SyntaxToken)"/> (Token-Identität ohne Trivia).</summary>
    public override bool Equals(object? obj) {
        return obj is SyntaxToken other && Equals(other);
    }

    /// <summary>Hashcode konsistent zu <see cref="Equals(SyntaxToken)"/> (Token-Identität ohne Trivia).</summary>
    public override int GetHashCode() {
        unchecked {
            var hash = _classificationAndType;
            hash = (hash * 397) ^ Extent.GetHashCode();
            hash = (hash * 397) ^ (Parent?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <summary>Wertgleichheit gemäß <see cref="Equals(SyntaxToken)"/>.</summary>
    public static bool operator ==(SyntaxToken left, SyntaxToken right) => left.Equals(right);
    /// <summary>Wert-Ungleichheit gemäß <see cref="Equals(SyntaxToken)"/>.</summary>
    public static bool operator !=(SyntaxToken left, SyntaxToken right) => !left.Equals(right);

}