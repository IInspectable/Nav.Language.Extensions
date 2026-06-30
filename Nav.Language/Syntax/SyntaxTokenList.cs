#region Using Directives

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public sealed class SyntaxTokenList: IReadOnlyList<SyntaxToken> {

    static readonly IReadOnlyList<SyntaxToken> EmptyTokens = new List<SyntaxToken>(Enumerable.Empty<SyntaxToken>()).AsReadOnly();
    readonly        IReadOnlyList<SyntaxToken> _tokens;

    public SyntaxTokenList(List<SyntaxToken> tokens): this(tokens, attachSorted: false) {
    }

    SyntaxTokenList(IReadOnlyList<SyntaxToken> tokens, bool attachSorted) {

        if (attachSorted || tokens == null || tokens.Count == 0) {
            // Tokens sind bereits sortiert oder es gibt keine Tokens
            _tokens = tokens ?? EmptyTokens;
        } else {
            var tokenList = new List<SyntaxToken>(tokens);
            tokenList.Sort(SyntaxTokenComparer.Default);
            _tokens = tokenList;
        }
    }

    internal static SyntaxTokenList AttachSortedTokens(IReadOnlyList<SyntaxToken> tokens) {
        return new SyntaxTokenList(tokens, attachSorted: true);
    }

    public static readonly SyntaxTokenList Empty = new(null);

    public IEnumerator<SyntaxToken> GetEnumerator() {
        return _tokens.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public int Count => _tokens.Count;

    public SyntaxToken this[int index] => _tokens[index];

    [NotNull]
    public IEnumerable<SyntaxToken> this[TextExtent extent, bool includeOverlapping = false] => _tokens.GetElements(extent, includeOverlapping);

    /// <summary>
    /// Das Token, dessen <b>eigener</b> Extent (Halbintervall <c>[Start, End)</c>) die <paramref name="position"/>
    /// abdeckt, oder <see cref="SyntaxToken.Missing"/>, wenn dort kein Token steht. Liegt die Position in
    /// angehängter Trivia (Whitespace/Kommentar), wird das die Trivia tragende signifikante Token <b>nicht</b>
    /// mitgeliefert — dafür gibt es <see cref="FindOwningToken"/>. Dies ist der exakte Low-Level-Lookup
    /// (z.B. für BraceMatching), der an einer Trivia-Position <see cref="SyntaxToken.Missing"/> liefert,
    /// sobald die Trivia nicht mehr im flachen Token-Strom geführt wird.
    /// </summary>
    public SyntaxToken FindAtPosition(int position) {
        if (position < 0) {
            return SyntaxToken.Missing;
        }

        return _tokens.FindElementAtPosition(position, defaultIfNotFound: true);
    }

    /// <summary>
    /// Das Token, zu dem die <paramref name="position"/> gehört — nach Roslyn-Vorbild (<c>FindToken</c>): liegt
    /// die Position auf dem Extent eines Tokens, ist es dieses; liegt sie in angehängter Trivia
    /// (Whitespace/Zeilenende/Kommentar), ist es das <b>signifikante Token, an dem die Trivia hängt</b>. Damit
    /// liefert dieser Lookup — anders als <see cref="FindAtPosition"/> — im gültigen Bereich nie eine
    /// Trivia-Position als „leer" zurück. Für Positionen außerhalb des Texts (negativ oder ohne tragendes
    /// Token) wird <see cref="SyntaxToken.Missing"/> geliefert (Nav wirft hier — anders als Roslyn — nicht).
    /// </summary>
    public SyntaxToken FindOwningToken(int position) {
        if (position < 0) {
            return SyntaxToken.Missing;
        }

        // Exakter Treffer auf dem eigenen Extent eines Tokens. Ein Trivia-Token (solange Trivia noch im flachen
        // Strom liegt) darf hier NICHT für sich selbst stehen — es muss auf seinen Eigentümer auflösen; daher
        // fällt es durch zum Trivia-Scan unten.
        var exact = FindAtPosition(position);
        if (!exact.IsMissing && !SyntaxFacts.IsTrivia(exact.Type)) {
            return exact;
        }

        // Die Position liegt in Trivia. Sie hängt an genau einem Token (signifikant, Trenner oder EOF) — als
        // dessen Leading- oder Trailing-Trivia. Trivia-Token selbst tragen keine angehängte Trivia und werden
        // übersprungen (solange sie noch im flachen Strom mitlaufen): erst dadurch ist der Trivia-Vorlauf der
        // verbleibenden Token monoton steigend, sodass ab dem ersten Token, dessen Vorlauf bereits hinter der
        // Position liegt, abgebrochen werden kann.
        foreach (var token in _tokens) {
            if (SyntaxFacts.IsTrivia(token.Type)) {
                continue;
            }

            var leading        = token.LeadingTrivia;
            var footprintStart = leading.IsEmpty ? token.Start : leading[0].Start;
            if (footprintStart > position) {
                break;
            }

            foreach (var trivia in leading) {
                if (trivia.Start <= position && position < trivia.End) {
                    return token;
                }
            }

            foreach (var trivia in token.TrailingTrivia) {
                if (trivia.Start <= position && position < trivia.End) {
                    return token;
                }
            }
        }

        return SyntaxToken.Missing;
    }

    internal SyntaxToken NextOrPrevious(SyntaxNode node, SyntaxToken currentToken, SyntaxTokenType type, bool nextToken) {
        SyntaxToken token = currentToken;
        while (!(token = NextOrPrevious(node, token, nextToken)).IsMissing) {
            if (token.Type == type) {
                return token;
            }
        }

        return SyntaxToken.Missing;
    }

    internal SyntaxToken NextOrPrevious(SyntaxNode node, SyntaxToken currentToken, TextClassification classification, bool nextToken) {
        SyntaxToken token = currentToken;
        while (!(token = NextOrPrevious(node, token, nextToken)).IsMissing) {
            if (token.Classification == classification) {
                return token;
            }
        }

        return SyntaxToken.Missing;
    }

    internal SyntaxToken NextOrPrevious(SyntaxNode node, SyntaxToken currentToken, bool nextToken) {
        return _tokens.NextOrPreviousElement(node, currentToken, nextToken, SyntaxToken.Missing);
    }

}