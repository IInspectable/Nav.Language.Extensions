using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Der Quellknoten einer Transition — das erste Glied von z.B. <c>View --&gt; Ziel;</c>. Zwei Formen:
/// das Schlüsselwort <c>init</c> (<see cref="InitSourceNodeSyntax"/>) oder der Name eines deklarierten
/// Knotens (<see cref="IdentifierSourceNodeSyntax"/>).
/// </summary>
[Serializable]
public abstract class SourceNodeSyntax: SyntaxNode {

    /// <summary>Initialisiert die Basisklasse mit dem Quelltext-Bereich des Quellknotens.</summary>
    protected SourceNodeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Der referenzierte Knotenname als Text (Schlüsselwort bzw. Bezeichner).</summary>
    public abstract string Name { get; }

}

/// <summary>
/// Das Schlüsselwort <c>init</c> als Quellknoten, z.B. <c>init --&gt; View;</c> — die Transition geht
/// vom Init-Knoten des Tasks aus (→ <see cref="IInitTransition"/>).
/// </summary>
[Serializable]
[SampleSyntax("init")]
public partial class InitSourceNodeSyntax: SourceNodeSyntax {

    internal InitSourceNodeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>init</c>.</summary>
    public SyntaxToken InitKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.InitKeyword);

    /// <summary>Der Text des <c>init</c>-Schlüsselworts.</summary>
    public override string Name => InitKeyword.ToString();

}

/// <summary>
/// Ein benannter Knoten als Quellknoten, z.B. <c>View --&gt; Ziel;</c> — referenziert einen im
/// Knoten-Deklarationsteil deklarierten Knoten über seinen Namen. Auch der Task-Knoten-Teil einer
/// <see cref="ExitTransitionDefinitionSyntax"/> (<c>TaskKnoten:Exit …</c>) nutzt diese Form.
/// </summary>
[Serializable]
[SampleSyntax("Identifier")]
public partial class IdentifierSourceNodeSyntax: SourceNodeSyntax {

    internal IdentifierSourceNodeSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Der Bezeichner des referenzierten Knotens.</summary>
    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    /// <summary>Der Text des Bezeichners.</summary>
    public override string Name => Identifier.ToString();

}