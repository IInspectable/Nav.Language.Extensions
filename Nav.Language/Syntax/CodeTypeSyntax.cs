#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class CodeTypeSyntax: SyntaxNode {

    protected CodeTypeSyntax(TextExtent extent): base(extent) {
    }

}