using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der abstrakte Basistyp aller Knoten, die als strukturierte Trivia geführt werden — nach dem Roslyn-Vorbild
/// der <c>StructuredTriviaSyntax</c>: Präprozessor-Direktiven (<see cref="DirectiveTriviaSyntax"/>) und vom
/// Parser übersprungener Quelltext (<see cref="SkippedTokensTriviaSyntax"/>). Ein solcher Knoten ist kein
/// Kindknoten der Wurzel, sondern über <see cref="SyntaxTrivia.GetStructure"/> an einer Trivia erreichbar;
/// seine Token führt er in einer <b>eigenen, lokalen</b> <see cref="SyntaxTokenList"/> (nicht im flachen
/// <see cref="SyntaxTree.Tokens"/>-Strom).
/// </summary>
[Serializable]
public abstract class StructuredTriviaSyntax: SyntaxNode {

    SyntaxTokenList? _localTokens;

    private protected StructuredTriviaSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>
    /// Legt die lokale Token-Liste dieses Knotens fest (einmalig während des Baum-Aufbaus). Die Token
    /// verweisen als <see cref="SyntaxToken.Parent"/> auf diesen Knoten; da sie ihn zur Konstruktion bereits
    /// brauchen, wird die Liste hier nachgereicht statt im Konstruktor übergeben.
    /// </summary>
    internal void SetLocalTokens(SyntaxTokenList localTokens) {
        _localTokens = localTokens;
    }

    /// <summary>
    /// Die Token dieses Knotens. Liegen sie lokal vor (Zielmodell strukturierter Trivia), wird die eigene
    /// Liste geliefert; andernfalls (etwa vor dem Nachreichen) das Verhalten der Basis.
    /// </summary>
    public override IEnumerable<SyntaxToken> ChildTokens() {
        return _localTokens ?? base.ChildTokens();
    }

}
