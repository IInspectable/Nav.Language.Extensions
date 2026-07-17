using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Deklaration eines <c>choice</c>-Knotens, z.B. <c>choice C_Auswahl;</c> — ein Verzweigungsknoten,
/// der anhand von Bedingungen (<c>if</c>/<c>else</c>) einen von mehreren Folgewegen wählt. Ab
/// Sprachversion 2 ist eine <c>[params …]</c>-Deklaration zulässig (Autorität:
/// <see cref="CodeBlockFacts"/>).
/// </summary>
[Serializable]
[SampleSyntax("choice ChoiceName [params T1 p1, T2 p2];")]
public partial class ChoiceNodeDeclarationSyntax: NodeDeclarationSyntax {

    internal ChoiceNodeDeclarationSyntax(TextExtent extent,
                                         CodeParamsDeclarationSyntax? codeParamsDeclaration)
        : base(extent) {

        AddChildNode(CodeParamsDeclaration = codeParamsDeclaration);
    }

    /// <summary>Das Schlüsselwort <c>choice</c>.</summary>
    public SyntaxToken ChoiceKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ChoiceKeyword);
    /// <summary>Der Name des Choice-Knotens.</summary>
    public SyntaxToken Identifier    => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    /// <summary>
    /// Die optionale <c>[params …]</c>-Deklaration (ab Sprachversion 2) — <c>null</c>, wenn nicht
    /// angegeben.
    /// </summary>
    public CodeParamsDeclarationSyntax? CodeParamsDeclaration { get; }

}