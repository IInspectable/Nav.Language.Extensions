namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Ein zusammenhängender, halboffener Ausschnitt eines Quelltexts, adressiert über einen
/// nullbasierten Start- und Endindex — das Nav-Pendant zu Roslyns
/// <c>Microsoft.CodeAnalysis.Text.TextSpan</c>. <see cref="Start"/> ist inklusiv, <see cref="End"/>
/// exklusiv (der Index einer Position hinter dem letzten abgedeckten Zeichen); die Länge des
/// Ausschnitts ist damit <see cref="End"/> − <see cref="Start"/>.
/// Diese gemeinsame Schnittstelle erlaubt es, alles positionsbehaftete einheitlich zu adressieren:
/// den Wertetyp <see cref="TextExtent"/> selbst ebenso wie seine Träger — <see cref="SyntaxNode"/>,
/// <see cref="SyntaxToken"/>, <see cref="SyntaxTrivia"/>, <see cref="ISymbol"/> und
/// <see cref="SourceTextLine"/> — sodass etwa die positionsbasierte Suche generisch über
/// <see cref="IExtent"/> arbeiten kann.
/// </summary>
public interface IExtent {

    /// <summary>
    /// Der nullbasierte Startindex des Ausschnitts (inklusiv). <c>-1</c> kennzeichnet einen
    /// fehlenden/unbekannten Ausschnitt (siehe <see cref="TextExtent.Missing"/>).
    /// </summary>
    int Start { get; }
    /// <summary>
    /// Der nullbasierte Endindex des Ausschnitts (exklusiv) — die Position eine Stelle hinter dem
    /// letzten abgedeckten Zeichen. Die Länge des Ausschnitts ist <see cref="End"/> − <see cref="Start"/>.
    /// </summary>
    int End   { get; }

}