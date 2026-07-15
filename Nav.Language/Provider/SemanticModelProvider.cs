#region Using Directives

using System;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der Standard-<see cref="ISemanticModelProvider"/>: baut das semantische Modell direkt aus dem
/// Syntaxbaum, den ein <see cref="ISyntaxProvider"/> liefert. Ohne eigenen Cache (siehe
/// <see cref="CachedSemanticModelProvider"/>).
/// </summary>
public class SemanticModelProvider: ISemanticModelProvider {

    private readonly ISyntaxProvider _syntaxProvider;

    /// <summary>
    /// Erzeugt einen Provider über <paramref name="syntaxProvider"/>.
    /// </summary>
    /// <param name="syntaxProvider">Der zugrunde liegende Syntax-Provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="syntaxProvider"/> ist <c>null</c>.</exception>
    public SemanticModelProvider(ISyntaxProvider syntaxProvider) {
        _syntaxProvider = syntaxProvider ?? throw new ArgumentNullException(nameof(syntaxProvider));
    }

    /// <summary>Die gemeinsam nutzbare Standard-Instanz über dem <see cref="SyntaxProvider.Default"/>.</summary>
    public static readonly ISemanticModelProvider Default = new SemanticModelProvider(SyntaxProvider.Default);

    /// <inheritdoc/>
    public CodeGenerationUnit? GetSemanticModel(string filePath, CancellationToken cancellationToken = default) {

        var syntax = _syntaxProvider.GetSyntax(filePath, cancellationToken);
        if (syntax == null) {
            return null;
        }

        return GetSemanticModel(syntax, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public CodeGenerationUnit GetSemanticModel(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default) {
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax, syntaxProvider: _syntaxProvider, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public virtual void Dispose() {
    }

}