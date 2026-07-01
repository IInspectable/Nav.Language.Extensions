#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language; 

public sealed class CodeGenerationUnit {

    internal CodeGenerationUnit(CodeGenerationUnitSyntax syntax,
                                ImmutableArray<string> codeUsings,
                                IReadOnlySymbolCollection<ITaskDeclarationSymbol> taskDeclarations,
                                IReadOnlySymbolCollection<ITaskDefinitionSymbol> taskDefinitions,
                                IReadOnlySymbolCollection<IIncludeSymbol> includes,
                                IEnumerable<ISymbol> symbols,
                                ImmutableArray<Diagnostic> diagnostics) {

        CodeUsings       = codeUsings;
        Syntax           = syntax           ?? throw new ArgumentNullException(nameof(syntax));
        TaskDeclarations = taskDeclarations ?? new SymbolCollection<ITaskDeclarationSymbol>();
        TaskDefinitions  = taskDefinitions  ?? new SymbolCollection<ITaskDefinitionSymbol>();
        Includes         = includes         ?? new SymbolCollection<IIncludeSymbol>();
        Symbols          = new SymbolList(symbols ?? Enumerable.Empty<IIncludeSymbol>());
        Diagnostics      = diagnostics;
    }

    internal CodeGenerationUnit WithDiagnostics(ImmutableArray<Diagnostic> diagnostics) {
        return new CodeGenerationUnit(
            Syntax,
            CodeUsings,
            TaskDeclarations,
            TaskDefinitions,
            Includes,
            Symbols,
            diagnostics);
    }

    [NotNull]
    public CodeGenerationUnitSyntax Syntax { get; }

    /// <summary>
    /// Die Sprach-Version dieser Datei (aus <c>#pragma version</c>, sonst
    /// <see cref="NavLanguageVersion.Default"/>) — der Ankerpunkt künftiger versionsabhängiger Syntax-
    /// und Codegen-Entscheidungen.
    /// </summary>
    public NavLanguageVersion LanguageVersion => Syntax.LanguageVersion;

    [NotNull]
    public string CodeNamespace => Syntax.CodeNamespace?.ToString() ?? String.Empty;

    public ImmutableArray<string> CodeUsings { get; }

    [NotNull]
    public IReadOnlySymbolCollection<IIncludeSymbol> Includes { get; }

    [NotNull]
    public IReadOnlySymbolCollection<ITaskDeclarationSymbol> TaskDeclarations { get; }

    [NotNull]
    public IReadOnlySymbolCollection<ITaskDefinitionSymbol> TaskDefinitions { get; }

    [NotNull]
    public SymbolList Symbols { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    [NotNull]
    public static CodeGenerationUnit FromCodeGenerationUnitSyntax(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default, ISyntaxProvider syntaxProvider = null) {
        return CodeGenerationUnitBuilder.FromCodeGenerationUnitSyntax(syntax, cancellationToken, syntaxProvider);
    }

}