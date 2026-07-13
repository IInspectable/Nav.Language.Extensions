using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Ein einzelner Parameter — eine Typ-Angabe mit optionalem Namen, z.B. <c>string[] args</c> in
/// <c>[params string[] args]</c>. Vorkommen: als Element einer <see cref="ParameterListSyntax"/>
/// (in <c>[params …]</c>, siehe <see cref="CodeParamsDeclarationSyntax"/>) sowie einzeln als
/// Ergebnis-Angabe eines <c>[result …]</c> (siehe <see cref="CodeResultDeclarationSyntax"/>).
/// </summary>
[Serializable]
[SampleSyntax("Type param")]
public partial class ParameterSyntax: SyntaxNode {

    readonly CodeTypeSyntax _type;

    internal ParameterSyntax(TextExtent extent, CodeTypeSyntax type): base(extent) {
        AddChildNode(_type = type);
    }

    /// <summary>Die Typ-Angabe des Parameters (z.B. <c>string[]</c> in <c>string[] args</c>).</summary>
    public CodeTypeSyntax Type => _type;

    /// <summary>Der Parametername hinter der Typ-Angabe, oder ein fehlendes Token, wenn der Parameter unbenannt ist — der Name ist in der Grammatik optional.</summary>
    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

}