#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Der Codegenerator der Sprach-Generation 1 — das historische, byte-identisch eingefrorene
/// Verhalten (siehe <see cref="NavLanguageVersion.Version1"/>). Nach der Ablösung von StringTemplate
/// CodeBuilder-basiert (Emitter unter <c>CodeGen/CodeBuilder/</c>). Die Auswahl je
/// <see cref="CodeGenerationUnit"/> trifft der <see cref="VersionDispatchingCodeGenerator"/>.
/// </summary>
// ReSharper disable InconsistentNaming
public class CodeGeneratorV1: Generator, ICodeGenerator {

    public CodeGeneratorV1(GenerationOptions? options = null, IPathProviderFactory? pathProviderFactory = null): base(options) {
        PathProviderFactory = pathProviderFactory ?? Language.PathProviderFactory.Default;
    }

    public IPathProviderFactory PathProviderFactory { get; }

    public ImmutableArray<CodeGenerationResult> Generate(CodeGenerationUnit codeGenerationUnit) {

        if (codeGenerationUnit == null) {
            throw new ArgumentNullException(nameof(codeGenerationUnit));
        }

        if (codeGenerationUnit.Syntax.SyntaxTree.Diagnostics.HasErrors()) {
            throw new ArgumentException($"The CodeGenerationUnit has syntax errors:\r\n{FormatDiagnostics(codeGenerationUnit.Syntax.SyntaxTree.Diagnostics.Errors())}");
        }

        if (codeGenerationUnit.Diagnostics.HasErrors()) {
            throw new ArgumentException($"The CodeGenerationUnit has semantic errors:\r\n{FormatDiagnostics(codeGenerationUnit.Diagnostics.Errors())}");
        }

        if (codeGenerationUnit.Includes.Any(i => i.Diagnostics.HasErrors())) {
            throw new ArgumentException($"An included file has syntax or semantic errors:\r\n{FormatDiagnostics(codeGenerationUnit.Includes.SelectMany(i => i.Diagnostics).Errors())}");
        }

        return codeGenerationUnit.TaskDefinitions
                                 .Select(GenerateCodeModel)
                                 .Select(GenerateCode)
                                 .ToImmutableArray();

        string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) {
            return diagnostics.Aggregate(new StringBuilder(), (sb, d) => sb.AppendLine(FormatDiagnostic(d)), sb => sb.ToString());
        }

        string FormatDiagnostic(Diagnostic diagnostic) {
            return $"{diagnostic.Descriptor.Id}: {diagnostic.Location} {diagnostic.Message}";
        }
    }

    CodeModelResult GenerateCodeModel(ITaskDefinitionSymbol taskDefinition) {

        var pathProvider = PathProviderFactory.CreatePathProvider(taskDefinition, Options);

        var codeModelResult = new CodeModelResult(
            taskDefinition: taskDefinition,
            beginWfsCodeModel: Options.GenerateWflClasses ? IBeginWfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options) : null,
            iwfsCodeModel: Options.GenerateIwflClasses ? IWfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options) : null,
            wfsBaseCodeModel: Options.GenerateWflClasses ? WfsBaseCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options) : null,
            wfsCodeModel: Options.GenerateWflClasses ? WfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options) : null,
            toCodeModels: Options.GenerateToClasses ? TOCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options) : null
        );

        return codeModelResult;
    }

    CodeGenerationResult GenerateCode(CodeModelResult codeModelResult) {

        // Das TaskDefinition stammt aus codeGenerationUnit.TaskDefinitions; dessen CodeGenerationUnit
        // ist nach FinalConstruct gesetzt und hier stets vorhanden.
        var context = new CodeGeneratorContext(Options, codeModelResult.TaskDefinition.CodeGenerationUnit!.LanguageVersion);

        // Reihenfolge wie beim bisherigen FileGenerator (nur log-/statistikrelevant, nicht
        // inhaltsrelevant): IWfs, IBeginWfs, WfsBase, Wfs, TOs. Leere Specs (ausgeschaltete
        // Options-Flags) werden schon beim Bau herausgefiltert — die Liste enthält nur die
        // tatsächlich zu schreibenden Artefakte.
        var specs = new[] {
                GenerateIWfsCodeSpec(codeModelResult.IWfsCodeModel, context),
                GenerateIBeginWfsCodeSpec(codeModelResult.IBeginWfsCodeModel, context),
                GenerateWfsBaseCodeSpec(codeModelResult.WfsBaseCodeModel, context),
                GenerateWfsCodeSpec(codeModelResult.WfsCodeModel, context)
            }.Concat(GenerateToCodeSpecs(codeModelResult.TOCodeModels, context))
             .Where(spec => !spec.IsEmpty)
             .ToImmutableArray();

        return new CodeGenerationResult(codeModelResult.TaskDefinition, specs);
    }

    static CodeGenerationSpec GenerateIBeginWfsCodeSpec(IBeginWfsCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var content = IBeginWfsEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    static CodeGenerationSpec GenerateIWfsCodeSpec(IWfsCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var content = IWfsEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    static CodeGenerationSpec GenerateWfsBaseCodeSpec(WfsBaseCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var content = WfsBaseEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    static CodeGenerationSpec GenerateWfsCodeSpec(WfsCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var content = WfsOneShotEmitter.Emit(model, context);

        // Benutzer-Datei: nur einmalig anlegen, danach nie überschreiben.
        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.Never);
    }

    static IEnumerable<CodeGenerationSpec> GenerateToCodeSpecs(IEnumerable<TOCodeModel> models, CodeGeneratorContext context) {
        return models.Select(model => GenerateToCodeSpec(model, context));

        static CodeGenerationSpec GenerateToCodeSpec(TOCodeModel model, CodeGeneratorContext context) {

            var content = TOEmitter.Emit(model, context);

            // TO-Stub: nur einmalig anlegen; den Inhalt pflegt der externe GUI-Generator.
            return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.Never);
        }
    }

}
