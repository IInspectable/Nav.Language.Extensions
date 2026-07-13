#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Das unveränderliche Ergebnis eines Parse-Laufs — nach dem Roslyn-Vorbild des <c>SyntaxTree</c>: der
/// Wurzelknoten (<see cref="Root"/>), der flache Strom der signifikanten Token (<see cref="Tokens"/>), der
/// zugrunde liegende Quelltext (<see cref="SourceText"/>) und die syntaktischen Diagnosen
/// (<see cref="Diagnostics"/>). Erzeugt über <see cref="ParseText"/>.
/// </summary>
public class SyntaxTree {

    internal SyntaxTree(SourceText? sourceText,
                        SyntaxNode root,
                        SyntaxTokenList? tokens,
                        ImmutableArray<Diagnostic> diagnostics) {

        Root        = root       ?? throw new ArgumentNullException(nameof(root));
        Tokens      = tokens     ?? SyntaxTokenList.Empty;
        SourceText  = sourceText ?? SourceText.Empty;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Der Wurzelknoten des Baums — für eine ganze Datei (<see cref="ParseText"/>) die
    /// <see cref="CodeGenerationUnitSyntax"/>; die Snippet-Einstiege der Klasse <see cref="Syntax"/> liefern
    /// Bäume mit dem jeweiligen Regel-Knoten als Wurzel.
    /// </summary>
    public SyntaxNode Root { get; }

    /// <summary>Der zugrunde liegende Quelltext; nie <c>null</c> (<see cref="SourceText.Empty"/> als Rückfall).</summary>
    public SourceText SourceText { get; }

    /// <summary>
    /// Der flache Strom der signifikanten Token in Quelltext-Reihenfolge. Trivia steht nicht darin (sie
    /// hängt an den Token, siehe <see cref="SyntaxToken.LeadingTrivia"/>/<see cref="SyntaxToken.TrailingTrivia"/>);
    /// ebenso wenig die Präprozessor-Token der Direktiven und die vom Parser übersprungenen Token — beide
    /// liegen lokal an ihren strukturierten Trivia-Knoten (siehe <see cref="Directives"/> bzw.
    /// <see cref="SkippedTokens"/>).
    /// </summary>
    public SyntaxTokenList Tokens { get; }

    /// <summary>Die beim Lexen/Parsen (inklusive Recovery und Direktiven-Vorlauf) gemeldeten syntaktischen Diagnosen.</summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Alle an die <see cref="SyntaxToken"/> angehängten Trivia (Whitespace, Zeilenenden, Kommentare) der
    /// gesamten Datei in Quelltext-Reihenfolge — das echte Roslyn-Modell. Jede Trivia hängt als Leading-
    /// oder Trailing-Trivia an genau einem Token und erscheint hier daher genau einmal.
    /// </summary>
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
    public IEnumerable<SyntaxTrivia> Comments() {
        return DescendantTrivia().Where(trivia => trivia.IsComment);
    }

    /// <summary>
    /// Die strukturiert erkannten Präprozessor-Direktiven (<c>#…</c>) der Datei in Quelltext-Reihenfolge —
    /// die wirksame <see cref="VersionDirectiveSyntax"/> ebenso wie jede
    /// <see cref="BadDirectiveTriviaSyntax"/> (unbekannte, deplatzierte oder wiederholte Direktive). Direktiven
    /// sind strukturierte <see cref="SyntaxTokenType.DirectiveTrivia"/> und keine Kindknoten der Wurzel; sie
    /// werden daher über die angehängte Trivia erreicht (<see cref="SyntaxTrivia.GetStructure"/>).
    /// </summary>
    public IEnumerable<DirectiveTriviaSyntax> Directives() {
        return DescendantTrivia().Where(trivia => trivia.HasStructure)
                                 .Select(trivia => trivia.GetStructure())
                                 .OfType<DirectiveTriviaSyntax>();
    }

    /// <summary>
    /// Die vom Parser übersprungenen Läufe (Panic-Mode-Recovery, unbekannte Zeichen) der Datei in
    /// Quelltext-Reihenfolge — je Lauf ein <see cref="SkippedTokensTriviaSyntax"/>, der seine Token lokal
    /// trägt (Klassifikation <see cref="TextClassification.Skiped"/>). Wie die Direktiven sind sie
    /// strukturierte Trivia (<see cref="SyntaxTokenType.SkippedTokensTrivia"/>) und keine Kindknoten der
    /// Wurzel; sie werden daher über die angehängte Trivia erreicht (<see cref="SyntaxTrivia.GetStructure"/>).
    /// </summary>
    public IEnumerable<SkippedTokensTriviaSyntax> SkippedTokens() {
        return DescendantTrivia().Where(trivia => trivia.HasStructure)
                                 .Select(trivia => trivia.GetStructure())
                                 .OfType<SkippedTokensTriviaSyntax>();
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

    /// <summary>
    /// Parst den Nav-Quelltext <paramref name="text"/> zu einem <see cref="SyntaxTree"/> — der öffentliche
    /// Einstiegspunkt (delegiert an <see cref="NavParser.Parse"/>).
    /// </summary>
    /// <param name="text">Der Quelltext; <c>null</c> zählt wie leerer Text.</param>
    /// <param name="filePath">Der Dateipfad für <see cref="Location"/>-Angaben, oder <c>null</c>.</param>
    /// <param name="cancellationToken">Bricht den Parse-Lauf ab.</param>
    /// <returns>Der Syntaxbaum; nie <c>null</c> — Fehler landen in <see cref="Diagnostics"/>.</returns>
    public static SyntaxTree ParseText(string? text, string? filePath = null, CancellationToken cancellationToken = default) {
        return NavParser.Parse(text, filePath, cancellationToken);
    }

}
