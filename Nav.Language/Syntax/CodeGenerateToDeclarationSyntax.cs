using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[generateto "…"]</c> am Kopf einer <c>task</c>-Definition
/// (<see cref="TaskDefinitionSyntax.CodeGenerateToDeclaration"/>), z.B.
/// <c>[generateto "Verkauf\Auswahl"]</c> — legt den Ablageort der für den Task generierten
/// C#-Dateien fest (ausgewertet von <see cref="PathProviderFactory"/>). Zulässig nur am
/// Task-Definitions-Kopf (<see cref="CodeBlockFacts"/>).
/// </summary>
[Serializable]
[SampleSyntax("[generateto \"StringLiteral\"]")]
public partial class CodeGenerateToDeclarationSyntax: CodeSyntax {

    internal CodeGenerateToDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>generateto</c>.</summary>
    public SyntaxToken GeneratetoKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.GeneratetoKeyword);
    /// <summary>Das String-Literal mit dem Ablageort (Anführungszeichen sind Teil des Token-Texts).</summary>
    public SyntaxToken StringLiteral     => ChildTokens().FirstOrMissing(SyntaxTokenType.StringLiteral);

}