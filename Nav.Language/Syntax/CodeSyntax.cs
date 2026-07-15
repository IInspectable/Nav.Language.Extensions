using System;

using Pharmatechnik.Nav.Language.Internal;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Gemeinsame Basisklasse aller Code-Deklarationen — der eckig geklammerten
/// <c>[keyword …]</c>-Annotationen der Nav-Sprache (z.B. <c>[params string msg]</c>,
/// <c>[base StandardWFS]</c>, <c>[abstractmethod]</c>), die den generierten C#-Code steuern.
/// Der Aufbau ist stets <see cref="OpenBracket"/>, <see cref="Keyword"/>, deklarationsspezifischer
/// Inhalt, <see cref="CloseBracket"/>. Welche Deklaration in welchem Host zulässig ist,
/// bestimmt allein <see cref="CodeBlockFacts"/>.
/// </summary>
[Serializable]
public abstract class CodeSyntax: SyntaxNode {

    /// <summary>Initialisiert den Knoten mit seiner Ausdehnung im Quelltext.</summary>
    /// <param name="extent">Die Ausdehnung des Knotens im Quelltext.</param>
    protected CodeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Die öffnende Klammer <c>[</c> der Deklaration.</summary>
    public SyntaxToken OpenBracket => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBracket);

    /// <summary>
    /// Das Code-Schlüsselwort der Deklaration (z.B. <c>params</c>) — typunabhängig ermittelt als erstes
    /// Kind-Token mit der Klassifikation <see cref="TextClassification.Keyword"/>; die abgeleiteten Klassen
    /// bieten daneben eine typisierte Property (etwa <see cref="CodeParamsDeclarationSyntax.ParamsKeyword"/>).
    /// Ein fehlendes Token (<see cref="SyntaxToken.IsMissing"/>), wenn das Schlüsselwort im Quelltext fehlt
    /// (z.B. bei einem leeren <c>[]</c>).
    /// </summary>
    [SuppressCodeSanityCheck("Der Name Keyword ist hier ausdrücklich gewollt.")]
    public SyntaxToken Keyword => ChildTokens().FirstOrMissing(TextClassification.Keyword);

    /// <summary>
    /// Die schließende Klammer <c>]</c> der Deklaration — ein fehlendes Token
    /// (<see cref="SyntaxToken.IsMissing"/>), wenn sie im Quelltext fehlt (z.B. beim Tippen).
    /// </summary>
    public SyntaxToken CloseBracket => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBracket);

}
