using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[abstractmethod]</c> an einem <c>init</c>- oder <c>task</c>-Knoten
/// (<see cref="InitNodeDeclarationSyntax.CodeAbstractMethodDeclaration"/> bzw.
/// <see cref="TaskNodeDeclarationSyntax.CodeAbstractMethodDeclaration"/>), z.B.
/// <c>init I1 [abstractmethod];</c> — der Codegenerator erzeugt die zugehörige Logik-Methode
/// als <c>abstract</c>, die Implementierung obliegt der abgeleiteten WFS-Klasse. Ausgewertet über
/// <see cref="TaskNodeSymbolExtensions.CodeGenerateAbstractMethod(IInitNodeSymbol)"/> bzw.
/// <see cref="TaskNodeSymbolExtensions.CodeGenerateAbstractMethod(ITaskNodeSymbol)"/>; die
/// zulässigen Hosts bestimmt <see cref="CodeBlockFacts"/>.
/// </summary>
[Serializable]
[SampleSyntax("[abstractmethod]")]
public partial class CodeAbstractMethodDeclarationSyntax: CodeSyntax {

    internal CodeAbstractMethodDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>abstractmethod</c>.</summary>
    public SyntaxToken AbstractmethodKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.AbstractmethodKeyword);

}