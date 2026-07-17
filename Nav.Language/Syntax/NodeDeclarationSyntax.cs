using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Abstrakte Basisklasse aller Knoten-Deklarationen der Nav-Sprache. Im Knoten-Deklarationsblock
/// (<see cref="NodeDeclarationBlockSyntax"/>) einer <c>task</c>-Definition deklarieren sie die Knoten
/// des Workflows — <c>init</c>, <c>exit</c>, <c>end</c>, <c>task</c>, <c>choice</c>, <c>dialog</c>,
/// <c>view</c> —, die anschließend im Transitionsblock verdrahtet werden; die Verbindungspunkte
/// (<see cref="ConnectionPointNodeSyntax"/>) stehen außerdem im Rumpf einer <c>taskref</c>-Deklaration.
/// Jede Knoten-Deklaration wird mit einem Semikolon (<see cref="Semicolon"/>) abgeschlossen.
/// </summary>
[Serializable]
public abstract class NodeDeclarationSyntax: SyntaxNode {

    /// <summary>Initialisiert die Deklaration mit ihrem Quelltext-Bereich.</summary>
    protected NodeDeclarationSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>
    /// Das abschließende Semikolon der Deklaration — ein Missing-Token
    /// (<see cref="SyntaxToken.IsMissing"/>), wenn es im Quelltext fehlt.
    /// </summary>
    public SyntaxToken Semicolon => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

}