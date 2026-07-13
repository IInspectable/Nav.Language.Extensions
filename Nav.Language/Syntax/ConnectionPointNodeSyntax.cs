using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Abstrakte Basisklasse der Verbindungspunkt-Deklarationen <c>init</c>
/// (<see cref="InitNodeDeclarationSyntax"/>), <c>exit</c> (<see cref="ExitNodeDeclarationSyntax"/>) und
/// <c>end</c> (<see cref="EndNodeDeclarationSyntax"/>). Verbindungspunkte bilden die von außen sichtbare
/// Schnittstelle eines Tasks: Sie sind die einzigen zulässigen Deklarationen im Rumpf einer
/// <c>taskref</c>-Deklaration (<see cref="TaskDeclarationSyntax.ConnectionPoints"/>) und stehen ebenso im
/// Knoten-Deklarationsblock einer <c>task</c>-Definition
/// (<see cref="NodeDeclarationBlockSyntax.ConnectionPoints"/>).
/// </summary>
[Serializable]
public abstract class ConnectionPointNodeSyntax: NodeDeclarationSyntax {

    /// <summary>Initialisiert die Deklaration mit ihrem Quelltext-Bereich.</summary>
    protected ConnectionPointNodeSyntax(TextExtent extent): base(extent) {
    }

}