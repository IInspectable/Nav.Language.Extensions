#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

public class SyntaxTree {

    internal SyntaxTree(SourceText sourceText,
                        SyntaxNode root,
                        SyntaxTokenList tokens,
                        ImmutableArray<Diagnostic> diagnostics) {

        Root        = root       ?? throw new ArgumentNullException(nameof(root));
        Tokens      = tokens     ?? SyntaxTokenList.Empty;
        SourceText  = sourceText ?? SourceText.Empty;
        Diagnostics = diagnostics;
    }

    [NotNull]
    public SyntaxNode Root { get; }

    [NotNull]
    public SourceText SourceText { get; }

    [NotNull]
    public SyntaxTokenList Tokens { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Alle an die <see cref="SyntaxToken"/> angehängten Trivia (Whitespace, Zeilenenden, Kommentare) der
    /// gesamten Datei in Quelltext-Reihenfolge — das echte Roslyn-Modell. Jede Trivia hängt als Leading-
    /// oder Trailing-Trivia an genau einem Token und erscheint hier daher genau einmal.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxTrivia> DescendantTrivia() {
        foreach (var token in Tokens) {
            foreach (var trivia in token.LeadingTrivia) {
                yield return trivia;
            }

            foreach (var trivia in token.TrailingTrivia) {
                yield return trivia;
            }
        }
    }

    /// <summary>
    /// Alle Kommentar-Trivia der Datei (ein- und mehrzeilig) in Quelltext-Reihenfolge — die einzige
    /// semantisch tragende Trivia-Art. Filtert <see cref="DescendantTrivia"/> auf <see cref="SyntaxTrivia.IsComment"/>.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxTrivia> Comments() {
        return DescendantTrivia().Where(trivia => trivia.IsComment);
    }

    /// <summary>
    /// Die Trivia, die die angegebene <paramref name="position"/> abdeckt — Halbintervall
    /// <c>[Start, End)</c>, also dieselbe Regel wie <see cref="SyntaxNode.FindToken"/> bei den Token —, oder
    /// <c>default</c>, wenn an der Position keine Trivia liegt.
    /// </summary>
    public SyntaxTrivia FindTrivia(int position) {
        if (position < 0) {
            return default;
        }

        foreach (var trivia in DescendantTrivia()) {
            if (trivia.Start > position) {
                break; // Trivia kommen aufsteigend — ab hier liegt nichts mehr an oder vor der Position.
            }

            if (trivia.Start <= position && trivia.End > position) {
                return trivia;
            }
        }

        return default;
    }

    /// <summary>
    /// Ob die angegebene <paramref name="position"/> innerhalb einer Kommentar-Trivia liegt — gedacht, um
    /// (z.B. in der Vervollständigung) das Auslösen innerhalb von Kommentaren zu unterdrücken.
    /// </summary>
    public bool IsPositionInComment(int position) {
        return FindTrivia(position).IsComment;
    }

    public static SyntaxTree ParseText(string text, string filePath = null, CancellationToken cancellationToken = default) {
        return NavParser.Parse(text, filePath, cancellationToken);
    }

}
