#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

/// <summary>
/// Der Einstiegspunkt für die Batch-Codegenerierung: fasst die vollständige Nav→C#-Pipeline
/// (Syntax → Semantikmodell → Codegenerierung → Dateiausgabe) zu einem wiederverwendbaren Objekt
/// zusammen und wendet sie in <see cref="Run"/> auf eine Menge von <see cref="FileSpec"/>-Eingaben
/// an. Genutzt von den Batch-Hosts <c>Nav.Cli</c> (Kommandozeilen-Generator/MSBuild-Task); die
/// austauschbaren Fabrik-/Provider-Bausteine (Syntax, Semantikmodell, Pfade, Code- und
/// Dateigenerator) werden im Konstruktor injiziert und fallen andernfalls auf ihre
/// <c>Default</c>-Instanzen zurück. Instanzen entstehen über <see cref="Create"/> bzw.
/// <see cref="CreateDefault"/>.
/// </summary>
public sealed partial class NavCodeGeneratorPipeline {

    /// <summary>
    /// Verdrahtet die Pipeline mit ihren Bausteinen. Jeder <see langword="null"/>-Parameter wird durch
    /// die zugehörige <c>Default</c>-Instanz ersetzt (<see cref="GenerationOptions.Default"/> sowie die
    /// Standard-Fabriken/-Provider). Privat — Instanzen entstehen über <see cref="Create"/>.
    /// </summary>
    /// <param name="options">Die Codegenerierungs-Optionen; <see langword="null"/> ⇒
    /// <see cref="GenerationOptions.Default"/>.</param>
    /// <param name="logger">Die optionale Ausgabesenke (<see cref="ILogger"/>); darf
    /// <see langword="null"/> bleiben.</param>
    /// <param name="pathProviderFactory">Fabrik für die Ziel-Pfadauflösung des erzeugten Codes.</param>
    /// <param name="syntaxProviderFactory">Fabrik für den Syntax-Provider (Lexer/Parser der
    /// <c>.nav</c>-Dateien).</param>
    /// <param name="semanticModelProviderFactory">Fabrik für den Semantikmodell-Provider.</param>
    /// <param name="codeGeneratorProvider">Provider für den eigentlichen C#-Codegenerator.</param>
    /// <param name="fileGeneratorProvider">Provider für das Schreiben des erzeugten Codes in
    /// Dateien.</param>
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

    /// <summary>
    /// Erzeugt eine Pipeline, die durchgängig die Standard-Bausteine verwendet — Kurzform für
    /// <see cref="Create"/> ohne Argumente.
    /// </summary>
    /// <returns>Eine mit lauter Default-Bausteinen verdrahtete Pipeline.</returns>
    public static NavCodeGeneratorPipeline CreateDefault() => Create();

    /// <summary>
    /// Erzeugt eine Pipeline und ersetzt jedes ausgelassene (<see langword="null"/>) Argument durch die
    /// zugehörige <c>Default</c>-Instanz. So lässt sich gezielt ein einzelner Baustein austauschen
    /// (z.B. <paramref name="syntaxProviderFactory"/> gegen die gecachte Variante), ohne die übrigen
    /// benennen zu müssen.
    /// </summary>
    /// <param name="options">Die Codegenerierungs-Optionen; <see langword="null"/> ⇒
    /// <see cref="GenerationOptions.Default"/>.</param>
    /// <param name="logger">Die optionale Ausgabesenke (<see cref="ILogger"/>).</param>
    /// <param name="pathProviderFactory">Fabrik für die Ziel-Pfadauflösung.</param>
    /// <param name="syntaxProviderFactory">Fabrik für den Syntax-Provider.</param>
    /// <param name="semanticModelProviderFactory">Fabrik für den Semantikmodell-Provider.</param>
    /// <param name="codeGeneratorProvider">Provider für den C#-Codegenerator.</param>
    /// <param name="fileGeneratorProvider">Provider für die Dateiausgabe.</param>
    /// <returns>Die verdrahtete Pipeline.</returns>
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

    /// <summary>Die für den Lauf wirksamen Codegenerierungs-Optionen.</summary>
    public GenerationOptions Options { get; }

    /// <summary>Die Fabrik, die je Lauf den Syntax-Provider (Lexer/Parser der <c>.nav</c>-Dateien)
    /// erzeugt.</summary>
    public ISyntaxProviderFactory SyntaxProviderFactory { get; }

    /// <summary>Die Fabrik für die Ziel-Pfadauflösung des erzeugten Codes.</summary>
    public IPathProviderFactory PathProviderFactory { get; }

    /// <summary>Der Provider, der den eigentlichen C#-Codegenerator liefert.</summary>
    public ICodeGeneratorProvider CodeGeneratorProvider { get; }

    /// <summary>Der Provider, der das Schreiben des erzeugten Codes in Dateien übernimmt.</summary>
    public IFileGeneratorProvider FileGeneratorProvider { get; }

    /// <summary>Die optionale Ausgabesenke; <see langword="null"/>, wenn kein Logger übergeben
    /// wurde.</summary>
    public ILogger? Logger { get; }

    /// <summary>Die Fabrik, die je Lauf den Semantikmodell-Provider erzeugt.</summary>
    public ISemanticModelProviderFactory SemanticModelProviderFactory { get; }

    /// <summary>
    /// Führt die vollständige Pipeline über alle <paramref name="fileSpecs"/> aus. Je Datei durchläuft
    /// der Lauf vier Stufen: <c>.nav</c> parsen (Syntaxbaum), das Semantikmodell
    /// (<c>CodeGenerationUnit</c>) bilden, den C#-Code erzeugen und schließlich in die Zieldateien
    /// schreiben. Fehler-Diagnosen (aus Syntax, Semantikmodell oder Includes) überspringen die
    /// betroffene Datei; Warnungen werden gemeldet, brechen aber nicht ab. Per <c>taskref</c>
    /// eingelesene Abhängigkeitsdateien werden mitgeführt, damit der inkrementelle Build sie als
    /// zusätzliche Eingaben tracken kann. Fortschritt und Diagnosen laufen über den internen
    /// <c>LoggerAdapter</c> an den <see cref="Logger"/>.
    /// </summary>
    /// <param name="fileSpecs">Die zu verarbeitenden Eingabedateien.</param>
    /// <returns><see cref="RunResult.Failed"/>, sobald mindestens ein Fehler protokolliert wurde;
    /// andernfalls ein <see cref="RunResult.Success"/> mit den erzeugten und den eingelesenen
    /// Abhängigkeitsdateien.</returns>
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