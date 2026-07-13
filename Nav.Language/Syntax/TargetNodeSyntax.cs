using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Der Zielknoten einer Transition — das Glied hinter der Kante in z.B. <c>View --&gt; Ziel;</c>.
/// Zwei Formen: das Schlüsselwort <c>end</c> (<see cref="EndTargetNodeSyntax"/>) oder der Name eines
/// deklarierten Knotens (<see cref="IdentifierTargetNodeSyntax"/>). Auch der Ziel-Task einer
/// <see cref="ContinuationTransitionSyntax"/> wird über diesen Knoten-Typ benannt.
/// </summary>
[Serializable]
public abstract class TargetNodeSyntax: SyntaxNode {

    /// <summary>Initialisiert die Basisklasse mit dem Quelltext-Bereich des Zielknotens.</summary>
    protected TargetNodeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Der referenzierte Knotenname als Text (Schlüsselwort bzw. Bezeichner).</summary>
    public abstract string Name { get; }

}

/// <summary>
/// Das Schlüsselwort <c>end</c> als Zielknoten, z.B. <c>View --&gt; end;</c> — die Transition führt
/// zum Endknoten des Tasks.
/// </summary>
[Serializable]
[SampleSyntax("end")]
public partial class EndTargetNodeSyntax: TargetNodeSyntax {

    internal EndTargetNodeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>end</c>.</summary>
    public          SyntaxToken EndKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.EndKeyword);
    /// <summary>Der Text des <c>end</c>-Schlüsselworts.</summary>
    public override string      Name       => EndKeyword.ToString();

}

/// <summary>
/// Ein benannter Knoten als Zielknoten, z.B. <c>init --&gt; View;</c> — referenziert einen im
/// Knoten-Deklarationsteil deklarierten Knoten über seinen Namen.
/// </summary>
[Serializable]
[SampleSyntax("Identifier (identifierOrStringList)")]
public partial class IdentifierTargetNodeSyntax: TargetNodeSyntax {

    internal IdentifierTargetNodeSyntax(TextExtent extent)
        : base(extent) {
    }

    /// <summary>Der Bezeichner des referenzierten Knotens.</summary>
    public          SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    /// <summary>Der Text des Bezeichners.</summary>
    public override string      Name       => Identifier.ToString();

}