using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[donotinject]</c> an einem <c>task</c>-Knoten
/// (<see cref="TaskNodeDeclarationSyntax.CodeDoNotInjectDeclaration"/>), z.B.
/// <c>task Auswahl [donotinject];</c> — nimmt den aufgerufenen Unter-Workflow von der
/// Dependency-Injection aus: der Codegenerator stellt dessen Aufruf-Wrapper nicht automatisch
/// bereit. Ausgewertet über <see cref="TaskNodeSymbolExtensions.CodeDoNotInject"/>; zulässig nur
/// am Task-Knoten (<see cref="CodeBlockFacts"/>).
/// </summary>
[Serializable]
[SampleSyntax("[donotinject]")]
public partial class CodeDoNotInjectDeclarationSyntax: CodeSyntax {

    internal CodeDoNotInjectDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>donotinject</c>.</summary>
    public SyntaxToken DonotinjectKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.DonotinjectKeyword);

}