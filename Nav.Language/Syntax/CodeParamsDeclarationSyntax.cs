using Pharmatechnik.Nav.Language.Text;

using System;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[params …]</c>, z.B. <c>[params string msg, bool flag]</c> — die
/// Parameterliste einer Deklaration. Drei Wirte (<see cref="CodeBlockFacts"/>): am Kopf einer
/// <c>task</c>-Definition (<see cref="TaskDefinitionSyntax.CodeParamsDeclaration"/>) die Parameter
/// des Workflows, an einem <c>init</c>-Knoten (<see cref="InitNodeDeclarationSyntax.CodeParamsDeclaration"/>)
/// die des Init-Knotens, an einem <c>choice</c>-Knoten
/// (<see cref="ChoiceNodeDeclarationSyntax.CodeParamsDeclaration"/>, ab Sprachversion 2) die des
/// Choice-Knotens.
/// </summary>
[Serializable]
[SampleSyntax("[params Type1 p1, Type2 p2]")]
public partial class CodeParamsDeclarationSyntax: CodeSyntax {

    internal CodeParamsDeclarationSyntax(TextExtent extent, ParameterListSyntax? parameterList)
        : base(extent) {
        AddChildNode(ParameterList = parameterList);
    }

    /// <summary>Das Schlüsselwort <c>params</c>.</summary>
    public SyntaxToken ParamsKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ParamsKeyword);

    /// <summary>
    /// Die komma-getrennte Parameterliste — <c>null</c> bei einem leeren <c>[params]</c>
    /// (die Liste ist in der Grammatik optional).
    /// </summary>
    public ParameterListSyntax? ParameterList { get; }

}