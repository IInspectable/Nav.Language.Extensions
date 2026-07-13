using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Basisklasse der Top-Level-Member einer <c>.nav</c>-Datei (Grammatikregel <c>memberDeclaration</c>):
/// eine Include-Direktive <c>taskref "datei.nav";</c> (<see cref="IncludeDirectiveSyntax"/>), eine
/// Task-Deklaration <c>taskref Name { … }</c> (<see cref="TaskDeclarationSyntax"/>) oder eine
/// Task-Definition <c>task Name { … }</c> (<see cref="TaskDefinitionSyntax"/>). Die Member einer Datei
/// sammelt <see cref="CodeGenerationUnitSyntax.Members"/>.
/// </summary>
[Serializable]
public abstract class MemberDeclarationSyntax: SyntaxNode {

    internal MemberDeclarationSyntax(TextExtent extent): base(extent) {
    }

}