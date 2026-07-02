#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class ConnectionPointNodeSyntax: NodeDeclarationSyntax {

    protected ConnectionPointNodeSyntax(TextExtent extent): base(extent) {
    }

}