using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[code "…"]</c> am Kopf einer <c>task</c>-Definition
/// (<see cref="TaskDefinitionSyntax.CodeDeclaration"/>), z.B. <c>[code "using System.Text;"]</c> —
/// ihre String-Literale (<see cref="GetGetStringLiterals"/>) werden vom Codegenerator wörtlich in den
/// generierten C#-Code übernommen. Zulässig nur am Task-Definitions-Kopf
/// (<see cref="CodeBlockFacts"/>).
/// </summary>
[Serializable]
[SampleSyntax("[code \"code goes here\"]")]
public partial class CodeDeclarationSyntax: CodeSyntax {

    internal CodeDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>code</c>.</summary>
    public SyntaxToken CodeKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.CodeKeyword);

    /// <summary>
    /// Die String-Literale mit den Code-Schnipseln in Quelltext-Reihenfolge — die Grammatik erlaubt
    /// beliebig viele (auch keines). Die Anführungszeichen sind Teil des Token-Texts; der Codegenerator
    /// entfernt sie beim Übernehmen des Inhalts.
    /// </summary>
    public IEnumerable<SyntaxToken> GetGetStringLiterals() {
        return ChildTokens().OfType(SyntaxTokenType.StringLiteral);
    }

}