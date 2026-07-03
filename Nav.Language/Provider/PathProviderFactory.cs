#nullable enable

#region Using Directives

using System;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language;

public class PathProviderFactory: IPathProviderFactory {

    public static readonly PathProviderFactory Default = new();

    public virtual IPathProvider CreatePathProvider(ITaskDefinitionSymbol taskDefinition, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        var syntax     = taskDefinition.Syntax;
        var syntaxFile = syntax.SyntaxTree.SourceText.FileInfo;
        if (syntaxFile == null) {
            throw new ArgumentException("No FileInfo available", nameof(taskDefinition));
        }

        var syntaxFileName = syntaxFile.FullName;
        var taskName       = taskDefinition.Name;

        string? generateToInfo  = null;
        var     generateToToken = syntax.CodeGenerateToDeclaration?.StringLiteral ?? SyntaxToken.Missing;
        if (!generateToToken.IsMissing) {
            generateToInfo = generateToToken.ToString().Trim('"');
        }

        return new PathProvider(syntaxFileName, taskName, generateToInfo, options);
    }

}