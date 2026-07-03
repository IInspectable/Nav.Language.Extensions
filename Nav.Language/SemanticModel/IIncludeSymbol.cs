#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public interface IIncludeSymbol: ISymbol {

    string FileName { get; }

    Location FileLocation { get; }

    IncludeDirectiveSyntax Syntax { get; }

    IReadOnlyList<Diagnostic> Diagnostics { get; }

    IReadOnlySymbolCollection<ITaskDeclarationSymbol> TaskDeclarations { get; }

}
