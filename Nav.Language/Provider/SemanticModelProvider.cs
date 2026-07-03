#nullable enable

#region Using Directives

using System;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

public class SemanticModelProvider: ISemanticModelProvider {

    private readonly ISyntaxProvider _syntaxProvider;

    public SemanticModelProvider(ISyntaxProvider syntaxProvider) {
        _syntaxProvider = syntaxProvider ?? throw new ArgumentNullException(nameof(syntaxProvider));
    }

    public static readonly ISemanticModelProvider Default = new SemanticModelProvider(SyntaxProvider.Default);

    public CodeGenerationUnit? GetSemanticModel(string filePath, CancellationToken cancellationToken = default) {

        var syntax = _syntaxProvider.GetSyntax(filePath, cancellationToken);
        if (syntax == null) {
            return null;
        }

        return GetSemanticModel(syntax, cancellationToken: cancellationToken);
    }

    public CodeGenerationUnit GetSemanticModel(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default) {
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax, syntaxProvider: _syntaxProvider, cancellationToken: cancellationToken);
    }

    public virtual void Dispose() {
    }

}