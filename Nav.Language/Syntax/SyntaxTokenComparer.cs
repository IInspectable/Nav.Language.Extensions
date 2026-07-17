#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Ordnet <see cref="SyntaxToken"/> ausschließlich nach ihrer Start-Position im Quelltext — die
/// Sortierordnung des flachen Token-Stroms (<c>NavParser.TakeSortedTokens</c>,
/// <see cref="SyntaxTokenList"/>).
/// </summary>
sealed class SyntaxTokenComparer: IComparer<SyntaxToken> {

    /// <summary>Vergleicht zwei Token über die Differenz ihrer <see cref="SyntaxToken.Start"/>-Positionen.</summary>
    public int Compare(SyntaxToken x, SyntaxToken y) {
        return x.Start - y.Start;
    }

    /// <summary>Die einzige Instanz des Vergleichs.</summary>
    public static readonly IComparer<SyntaxToken> Default = new SyntaxTokenComparer();

}