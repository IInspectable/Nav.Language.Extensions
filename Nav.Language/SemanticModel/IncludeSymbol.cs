#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Implementierung von <see cref="IIncludeSymbol"/> — entsteht im
/// <see cref="TaskDeclarationSymbolBuilder"/> beim Verarbeiten einer Include-Direktive
/// (<c>taskref "datei.nav";</c>).
/// </summary>
/// <remarks>
/// <see cref="ISymbol.Name"/> ist hier der kleingeschriebene vollständige Dateipfad und dient als
/// case-insensitiver Dedup-Schlüssel in der nach Name gekeyten <see cref="SymbolCollection{T}"/>
/// der Includes — er weicht damit bewusst von der Name-Semantik der übrigen Symbole ab.
/// Die originale Schreibweise des Pfads steht in <see cref="FileName"/>.
/// </remarks>
sealed partial class IncludeSymbol: Symbol, IIncludeSymbol {

    /// <summary>
    /// Erzeugt das Include-Symbol; <c>null</c> für <paramref name="diagnostics"/> bzw.
    /// <paramref name="taskDeclarations"/> wird auf leere Kollektionen normalisiert.
    /// </summary>
    public IncludeSymbol(string fileName,
                         Location location,
                         Location fileLocation,
                         IncludeDirectiveSyntax syntax,
                         IReadOnlyList<Diagnostic>? diagnostics,
                         SymbolCollection<TaskDeclarationSymbol>? taskDeclarations)
        : base(fileName.ToLowerInvariant(), location) {

        FileName         = fileName;
        FileLocation     = fileLocation ?? throw new ArgumentNullException(nameof(fileLocation));
        Syntax           = syntax       ?? throw new ArgumentNullException(nameof(syntax));
        Diagnostics      = diagnostics      ?? new List<Diagnostic>();
        TaskDeclarations = taskDeclarations ?? new SymbolCollection<TaskDeclarationSymbol>();
    }

    /// <summary>
    /// Der Syntaxbaum der einbindenden Datei — dort steht die Direktive (<see cref="Syntax"/>);
    /// der Baum der eingebundenen Datei wird nicht gehalten.
    /// </summary>
    public override SyntaxTree SyntaxTree => Syntax.SyntaxTree;

    /// <inheritdoc/>
    public string                                  FileName         { get; }
    /// <inheritdoc/>
    public Location                                FileLocation     { get; }
    /// <inheritdoc/>
    public IncludeDirectiveSyntax                  Syntax           { get; }
    /// <inheritdoc/>
    public IReadOnlyList<Diagnostic>               Diagnostics      { get; }
    /// <inheritdoc cref="IIncludeSymbol.TaskDeclarations"/>
    public SymbolCollection<TaskDeclarationSymbol> TaskDeclarations { get; }

    IReadOnlySymbolCollection<ITaskDeclarationSymbol> IIncludeSymbol.TaskDeclarations => TaskDeclarations;

}
