using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[notimplemented]</c> an einer <c>taskref</c>-Deklaration
/// (<see cref="TaskDeclarationSyntax.CodeNotImplementedDeclaration"/>), z.B.
/// <c>taskref Auswahl [notimplemented];</c> — markiert den referenzierten Task als (noch) nicht
/// implementiert: der Codegenerator erzeugt für Aufrufe solcher Task-Knoten keinen Code
/// (<see cref="ITaskDeclarationSymbol.CodeNotImplemented"/>). Das Schlüsselwort ist versteckt
/// (<see cref="SyntaxFacts.IsHiddenKeyword"/>) — es erscheint weder in der Completion noch in den
/// <c>expected …</c>-Diagnosen; die Zuordnung zum Wirt bestimmt <see cref="CodeBlockFacts"/>.
/// </summary>
[Serializable]
[SampleSyntax("[notimplemented]")]
public partial class CodeNotImplementedDeclarationSyntax: CodeSyntax {

    internal CodeNotImplementedDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>notimplemented</c>.</summary>
    public SyntaxToken NotimplementedKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.NotimplementedKeyword);

}