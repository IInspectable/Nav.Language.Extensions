using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[result …]</c>, z.B. <c>[result bool ergebnis]</c> — der Rückgabewert
/// eines Tasks als Typ mit optionalem Namen. Zwei Wirte (<see cref="CodeBlockFacts"/>): am Kopf
/// einer <c>task</c>-Definition (<see cref="TaskDefinitionSyntax.CodeResultDeclaration"/>) der
/// Rückgabewert des Workflows, an einer <c>taskref</c>-Deklaration
/// (<see cref="TaskDeclarationSyntax.CodeResultDeclaration"/>) der des referenzierten Tasks.
/// </summary>
[Serializable]
[SampleSyntax("[result Type p]")]
public partial class CodeResultDeclarationSyntax: CodeSyntax {

    internal CodeResultDeclarationSyntax(TextExtent extent, ParameterSyntax result): base(extent) {
        AddChildNode(Result = result);
    }

    /// <summary>Das Schlüsselwort <c>result</c>.</summary>
    public SyntaxToken ResultKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ResultKeyword);

    /// <summary>Die Ergebnis-Angabe: Typ mit optionalem Namen (genau eine, anders als bei <c>[params …]</c>).</summary>
    public ParameterSyntax Result { get; }

}