using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Eine <c>do</c>-Klausel, z.B. <c>View --&gt; Ziel on Trigger do "Daten speichern";</c> — eine freie
/// Handlungsanweisung als Bezeichner oder String-Literal, rein dokumentierend und ohne Einfluss auf den
/// generierten Code. Tritt am Ende einer Transition (<see cref="TransitionDefinitionSyntax.DoClause"/>),
/// einer Exit-Transition (<see cref="ExitTransitionDefinitionSyntax.DoClause"/>) sowie einer
/// Init-Knoten-Deklaration (<see cref="InitNodeDeclarationSyntax.DoClause"/>) auf.
/// </summary>
[Serializable]
[SampleSyntax("do \"instruction\"")]
public partial class DoClauseSyntax: SyntaxNode {

    readonly IdentifierOrStringSyntax? _identifierOrString;

    internal DoClauseSyntax(TextExtent extent, IdentifierOrStringSyntax? identifierOrString): base(extent) {
        AddChildNode(_identifierOrString = identifierOrString);
    }

    /// <summary>Das Schlüsselwort <c>do</c>.</summary>
    public SyntaxToken DoKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.DoKeyword);

    /// <summary>Die Anweisung als Bezeichner oder String-Literal — <c>null</c>, wenn sie im Quelltext fehlt.</summary>
    public IdentifierOrStringSyntax? IdentifierOrString => _identifierOrString;

}