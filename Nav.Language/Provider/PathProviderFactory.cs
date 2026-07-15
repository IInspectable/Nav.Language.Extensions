#region Using Directives

using System;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Standard-Implementierung von <see cref="IPathProviderFactory"/>. Ermittelt aus der Task-Definition
/// den Quelldateipfad, den Task-Namen, ein etwaiges <c>generate to</c>-Ziel und die zur Sprach-Version
/// passenden Codegen-Fakten (nicht unterstützte Versionen fallen auf die Default-Generation zurück) und
/// erzeugt daraus einen <see cref="PathProvider"/>.
/// </summary>
public class PathProviderFactory: IPathProviderFactory {

    /// <summary>Die gemeinsam nutzbare Standard-Factory.</summary>
    public static readonly PathProviderFactory Default = new();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="taskDefinition"/> ist <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Die Task-Definition hat keine Datei-Information.</exception>
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

        // Die Ablage-Namen richten sich nach der Sprach-Version der Datei; eine (noch) nicht
        // unterstützte Version fällt — wie bei den *CodeInfo — bewusst auf die Default-Generation zurück.
        var version = taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default;
        var facts   = NavCodeGenFacts.For(version.IsSupported ? version : NavLanguageVersion.Default);

        return new PathProvider(syntaxFileName, taskName, generateToInfo, options, facts);
    }

}