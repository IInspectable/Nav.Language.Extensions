#nullable enable

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein im Vorlauf (<see cref="NavDirectiveParser"/>) erkannter Präprozessor-Direktiv-Lauf: sein Roh-Index-
/// Bereich <c>[RawStart, RawEnd)</c>, sein Inhalts-Extent (die <see cref="SyntaxTokenType.DirectiveTrivia"/>-
/// Breite ohne Zeilenende), das terminierende Zeilenende (oder <see cref="TextExtent.Missing"/>) und der
/// zugehörige Knoten. <see cref="NavParser.BuildTrivia"/> faltet daraus das strukturierte Trivia-Stück.
/// </summary>
internal readonly struct DirectiveRun {

    /// <summary>Erzeugt einen Direktiv-Lauf.</summary>
    /// <param name="rawStart">Index des <c>#</c>-Tokens im Roh-Token-Strom (inklusiver Beginn des Laufs).</param>
    /// <param name="rawEnd">Index hinter dem letzten Roh-Token des Laufs (exklusives Ende).</param>
    /// <param name="contentExtent">Inhalts-Extent des Laufs — <c>#</c> bis zum letzten Inhalts-Token, ohne Zeilenende.</param>
    /// <param name="newLineExtent">Extent des terminierenden Zeilenendes, oder <see cref="TextExtent.Missing"/> am Dateiende.</param>
    /// <param name="node">Der aus dem Lauf gebildete Direktiv-Knoten.</param>
    public DirectiveRun(int rawStart, int rawEnd, TextExtent contentExtent, TextExtent newLineExtent, DirectiveTriviaSyntax node) {
        RawStart      = rawStart;
        RawEnd        = rawEnd;
        ContentExtent = contentExtent;
        NewLineExtent = newLineExtent;
        Node          = node;
    }

    /// <summary>Index des <c>#</c>-Tokens im Roh-Token-Strom (inklusiver Beginn des Laufs).</summary>
    public int RawStart { get; }

    /// <summary>Index hinter dem letzten Roh-Token des Laufs (exklusives Ende).</summary>
    public int RawEnd { get; }

    /// <summary>Inhalts-Extent des Laufs — <c>#</c> bis zum letzten Inhalts-Token, ohne Zeilenende.</summary>
    public TextExtent ContentExtent { get; }

    /// <summary>Extent des terminierenden Zeilenendes, oder <see cref="TextExtent.Missing"/> am Dateiende.</summary>
    public TextExtent NewLineExtent { get; }

    /// <summary>Der aus dem Lauf gebildete Direktiv-Knoten.</summary>
    public DirectiveTriviaSyntax Node { get; }

}