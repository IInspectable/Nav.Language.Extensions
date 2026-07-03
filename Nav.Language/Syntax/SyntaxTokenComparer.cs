#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language; 

sealed class SyntaxTokenComparer: IComparer<SyntaxToken> {

    public int Compare(SyntaxToken x, SyntaxToken y) {
        return x.Start - y.Start;
    }

    public static readonly IComparer<SyntaxToken> Default = new SyntaxTokenComparer();

}