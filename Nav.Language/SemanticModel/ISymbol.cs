#region Using Directives

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

public partial interface ISymbol: IExtent {

    // TODO NotNull?
    string Name { get; }

    [NotNull]
    Location Location { get; }

    /// <summary>
    /// Liefert den Syntaxbaum, aus dem dieses Symbol entstanden ist.
    /// Kann bei importierten TaskDeclarations null sein!
    /// </summary>
    [CanBeNull]
    SyntaxTree SyntaxTree { get; }

}