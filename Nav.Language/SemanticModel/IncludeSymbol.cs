#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language; 

/// <remarks>
/// <see cref="ISymbol.Name"/> ist hier der kleingeschriebene vollständige Dateipfad und dient als
/// case-insensitiver Dedup-Schlüssel in der nach Name gekeyten <see cref="SymbolCollection{T}"/>
/// der Includes — er weicht damit bewusst von der Name-Semantik der übrigen Symbole ab.
/// Die originale Schreibweise des Pfads steht in <see cref="FileName"/>.
/// </remarks>
sealed partial class IncludeSymbol: Symbol, IIncludeSymbol {

    public IncludeSymbol(string fileName,
                         Location location,
                         Location fileLocation,
                         IncludeDirectiveSyntax syntax,
                         IReadOnlyList<Diagnostic> diagnostics,
                         SymbolCollection<TaskDeclarationSymbol> taskDeclarations)
        : base(fileName.ToLowerInvariant(), location) {

        FileName         = fileName;
        FileLocation     = fileLocation     ?? throw new ArgumentNullException(nameof(fileLocation));
        Syntax           = syntax           ?? throw new ArgumentNullException(nameof(syntax));
        Diagnostics      = diagnostics      ?? new List<Diagnostic>();
        TaskDeclarations = taskDeclarations ?? new SymbolCollection<TaskDeclarationSymbol>();
    }

    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    public string                                  FileName         { get; }
    public Location                                FileLocation     { get; }
    public IncludeDirectiveSyntax                  Syntax           { get; }
    public IReadOnlyList<Diagnostic>               Diagnostics      { get; }
    public SymbolCollection<TaskDeclarationSymbol> TaskDeclarations { get; }

    IReadOnlySymbolCollection<ITaskDeclarationSymbol> IIncludeSymbol.TaskDeclarations => TaskDeclarations;

}