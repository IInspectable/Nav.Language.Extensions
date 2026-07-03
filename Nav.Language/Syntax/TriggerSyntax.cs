using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
public abstract class TriggerSyntax: SyntaxNode {

    protected TriggerSyntax(TextExtent extent): base(extent) {
    }

}

[Serializable]
[SampleSyntax("spontaneous")]
public partial class SpontaneousTriggerSyntax: TriggerSyntax {

    internal SpontaneousTriggerSyntax(TextExtent extent): base(extent) {
    }

    public SyntaxToken SpontaneousKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.SpontaneousKeyword);

    public const string Keyword = "spontaneous";

}

[Serializable]
[SampleSyntax("on Trigger")]
public partial class SignalTriggerSyntax: TriggerSyntax {

    internal SignalTriggerSyntax(TextExtent extent, IdentifierSyntax? identifier)
        : base(extent) {
        AddChildNode(Identifier = identifier);
    }

    public SyntaxToken OnKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.OnKeyword);

    public IdentifierSyntax? Identifier { get; }

}