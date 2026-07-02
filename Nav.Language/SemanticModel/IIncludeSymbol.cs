#region Using Directives

using System.Collections.Generic;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language; 

public interface IIncludeSymbol: ISymbol {

    [NotNull]
    string FileName { get; }

    [NotNull]
    Location FileLocation { get; }

    [NotNull]
    IncludeDirectiveSyntax Syntax { get; }

    [NotNull]
    IReadOnlyList<Diagnostic> Diagnostics { get; }

    [NotNull]
    IReadOnlySymbolCollection<ITaskDeclarationSymbol> TaskDeclarations { get; }

}