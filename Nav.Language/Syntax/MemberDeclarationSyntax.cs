using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class MemberDeclarationSyntax: SyntaxNode {

    internal MemberDeclarationSyntax(TextExtent extent): base(extent) {
    }

}