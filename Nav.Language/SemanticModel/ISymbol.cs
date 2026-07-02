#nullable enable

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

public partial interface ISymbol: IExtent {

    string Name { get; }

    Location Location { get; }

    /// <summary>
    /// Liefert den Syntaxbaum, aus dem dieses Symbol entstanden ist.
    /// Kann bei importierten TaskDeclarations null sein!
    /// </summary>
    SyntaxTree? SyntaxTree { get; }

}
