#nullable enable

#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

public sealed partial class NavCodeGeneratorPipeline {

    NavCodeGeneratorPipeline(GenerationOptions? options,
                             ILogger? logger,
                             IPathProviderFactory? pathProviderFactory,
                             ISyntaxProviderFactory? syntaxProviderFactory,
                             ISemanticModelProviderFactory? semanticModelProviderFactory,
                             ICodeGeneratorProvider? codeGeneratorProvider,
                             IFileGeneratorProvider? fileGeneratorProvider) {

        Logger                       = logger;
        Options                      = options                      ?? GenerationOptions.Default;
        PathProviderFactory          = pathProviderFactory          ?? Language.PathProviderFactory.Default;
        SyntaxProviderFactory        = syntaxProviderFactory        ?? Language.SyntaxProviderFactory.Default;
        SemanticModelProviderFactory = semanticModelProviderFactory ?? Language.SemanticModelProviderFactory.Default;
        CodeGeneratorProvider        = codeGeneratorProvider        ?? CodeGen.CodeGeneratorProvider.Default;
        FileGeneratorProvider        = fileGeneratorProvider        ?? CodeGen.FileGeneratorProvider.Default;
    }

    public static NavCodeGeneratorPipeline CreateDefault() => Create();

    public static NavCodeGeneratorPipeline Create(GenerationOptions? options = null,
                                                  ILogger? logger = null,
                                                  IPathProviderFactory? pathProviderFactory = null,
                                                  ISyntaxProviderFactory? syntaxProviderFactory = null,
                                                  ISemanticModelProviderFactory? semanticModelProviderFactory = null,
                                                  ICodeGeneratorProvider? codeGeneratorProvider = null,
                                                  IFileGeneratorProvider? fileGeneratorProvider = null)
        => new(options                     : options,
               logger                      : logger,
               pathProviderFactory         : pathProviderFactory,
               syntaxProviderFactory       : syntaxProviderFactory,
               semanticModelProviderFactory: semanticModelProviderFactory,
               codeGeneratorProvider       : codeGeneratorProvider,
               fileGeneratorProvider       : fileGeneratorProvider);

    public GenerationOptions Options { get; }

    public ISyntaxProviderFactory SyntaxProviderFactory { get; }

    public IPathProviderFactory PathProviderFactory { get; }

    public ICodeGeneratorProvider CodeGeneratorProvider { get; }

    public IFileGeneratorProvider FileGeneratorProvider { get; }

    public ILogger? Logger { get; }

    public ISemanticModelProviderFactory SemanticModelProviderFactory { get; }

    public RunResult Run(IEnumerable<FileSpec> fileSpecs) {

        using var logger                = new LoggerAdapter(Logger);
        using var syntaxProvider        = SyntaxProviderFactory.CreateProvider();
        using var semanticModelProvider = SemanticModelProviderFactory.CreateProvider(syntaxProvider);
        using var codeGenerator         = CodeGeneratorProvider.Create(Options, PathProviderFactory);
        using var fileGenerator         = FileGeneratorProvider.Create(Options);

        var statistic      = new Statistic();
        var generatedFiles = ImmutableArray.CreateBuilder<FileGeneratorResult>();
        var includedFiles  = ImmutableArray.CreateBuilder<string>();

        logger.LogProcessBegin();

        foreach (var fileSpec in fileSpecs) {

            statistic.UpdatePerFile();

            logger.LogProcessFileBegin(fileSpec);

            // 1. SyntaxTree
            var syntax = syntaxProvider.GetSyntax(fileSpec.FilePath);
            if (syntax == null) {
                logger.LogError(String.Format(DiagnosticDescriptors.Semantic.Nav0004File0NotFound.MessageFormat, fileSpec));
                continue;
            }

            // 2. Semantic Model
            var codeGenerationUnit = semanticModelProvider.GetSemanticModel(syntax);

            if (logger.LogErrors(syntax.SyntaxTree.Diagnostics)  ||
                logger.LogErrors(codeGenerationUnit.Diagnostics) ||
                logger.LogErrors(codeGenerationUnit.Includes.SelectMany(include => include.Diagnostics))) {
                continue;
            }

            logger.LogWarnings(syntax.SyntaxTree.Diagnostics);
            logger.LogWarnings(codeGenerationUnit.Diagnostics);

            // Per taskref eingelesene Abhängigkeitsdateien festhalten. Sie sind selbst keine Eingabe-
            // dateien des Laufs, fließen aber in den erzeugten Code ein (aufgelöste Task-Deklarationen)
            // ⇒ der inkrementelle Build muss sie als zusätzliche Inputs tracken. Nav-Includes sind genau
            // eine Ebene tief (inkludierte Dateien verarbeiten ihre eigenen taskrefs nicht), daher genügt
            // die direkte Include-Menge — keine transitive Hülle nötig.
            foreach (var include in codeGenerationUnit.Includes) {
                // FileLocation eines Includes wird stets aus dem aufgelösten (non-null) Include-Pfad
                // erzeugt (TaskDeclarationSymbolBuilder: new Location(filePath) nach Path.GetFullPath) —
                // FilePath ist hier also nie null.
                includedFiles.Add(include.FileLocation.FilePath!);
            }

            // 3. Generate Code
            var codeGenerationResults = codeGenerator.Generate(codeGenerationUnit);
            foreach (var codeGenerationResult in codeGenerationResults) {

                // 4. Write Code into appropriate files
                var fileGeneratorResults = fileGenerator.Generate(codeGenerationResult);

                logger.LogFileGeneratorResults(fileGeneratorResults);

                statistic.UpdatePerTask(fileGeneratorResults);

                generatedFiles.AddRange(fileGeneratorResults);
            }

            logger.LogProcessFileEnd(fileSpec);
        }

        logger.LogProcessEnd(statistic);

        return logger.HasLoggedErrors
            ? RunResult.Failed
            : RunResult.Success(generatedFiles.ToImmutable(), includedFiles.ToImmutable());

    }

}