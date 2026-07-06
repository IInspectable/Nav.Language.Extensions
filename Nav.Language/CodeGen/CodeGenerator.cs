#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Antlr4.StringTemplate;

using Pharmatechnik.Nav.Language.CodeGen.Templates;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public interface ICodeGeneratorProvider {

    ICodeGenerator Create(GenerationOptions options, IPathProviderFactory pathProviderFactory);

}

public sealed class CodeGeneratorProvider: ICodeGeneratorProvider {

    CodeGeneratorProvider() {

    }

    public static readonly ICodeGeneratorProvider Default = new CodeGeneratorProvider();

    public ICodeGenerator Create(GenerationOptions options, IPathProviderFactory pathProviderFactory) {
        return new CodeGenerator(options, pathProviderFactory);
    }

}

public interface ICodeGenerator: IDisposable {

    /// <summary>
    /// Generiert für jede Task-Definition der <paramref name="codeGenerationUnit"/> deren
    /// Artefakte als <see cref="CodeGenerationResult"/> (je eine Spec-Liste). Die Weiche zwischen
    /// den Sprach-Generationen liegt hinter dieser Schnittstelle.
    /// </summary>
    ImmutableArray<CodeGenerationResult> Generate(CodeGenerationUnit codeGenerationUnit);

}

// ReSharper disable InconsistentNaming
public class CodeGenerator: Generator, ICodeGenerator {

    const string TemplateBeginName    = "Begin";
    const string ModelAttributeName   = "model";
    const string ContextAttributeName = "context";

    public CodeGenerator(GenerationOptions? options = null, IPathProviderFactory? pathProviderFactory = null): base(options) {
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
        var context = new CodeGeneratorContext(this, codeModelResult.TaskDefinition.CodeGenerationUnit!.LanguageVersion);

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

        // Auf den CodeBuilder-Emitter migriert; die übrigen Familien rendern weiterhin per StringTemplate.
        var content = IBeginWfsEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    static CodeGenerationSpec GenerateIWfsCodeSpec(IWfsCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        // Auf den CodeBuilder-Emitter migriert; die übrigen Familien rendern weiterhin per StringTemplate.
        var content = IWfsEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    static CodeGenerationSpec GenerateWfsBaseCodeSpec(WfsBaseCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        // Auf den CodeBuilder-Emitter migriert; die übrigen Familien rendern weiterhin per StringTemplate.
        var content = WfsBaseEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    static readonly ThreadLocal<TemplateGroup> WfsTemplateGroup = new(() => LoadTemplateGroup(Resources.WFSOneShotTemplate));

    static CodeGenerationSpec GenerateWfsCodeSpec(WfsCodeModel? model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var template = GetTemplate(WfsTemplateGroup.Value, model, context);
        var content  = template.Render();

        // Benutzer-Datei: nur einmalig anlegen, danach nie überschreiben.
        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.Never);
    }

    static readonly ThreadLocal<TemplateGroup> ToTemplateGroup = new(() => LoadTemplateGroup(Resources.TOTemplate));

    static IEnumerable<CodeGenerationSpec> GenerateToCodeSpecs(IEnumerable<TOCodeModel> models, CodeGeneratorContext context) {
        return models.Select(model => GenerateToCodeSpec(model, context));

        static CodeGenerationSpec GenerateToCodeSpec(TOCodeModel model, CodeGeneratorContext context) {

            var template = GetTemplate(ToTemplateGroup.Value, model, context);
            var content  = template.Render();

            // TO-Stub: nur einmalig anlegen; den Inhalt pflegt der externe GUI-Generator.
            return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.Never);
        }
    }

    static TemplateGroup LoadTemplateGroup(string resourceName) {

        var codeGenFacts   = new TemplateGroupString(Resources.CodeGenFacts);
        var commonTemplate = new TemplateGroupString(Resources.CommonTemplate);
        var templateGroup  = new TemplateGroupString(resourceName);

        templateGroup.ImportTemplates(codeGenFacts);
        templateGroup.ImportTemplates(commonTemplate);

        return templateGroup;
    }

    static Template GetTemplate(TemplateGroup templateGroup, CodeModel model, CodeGeneratorContext context) {

        var st = templateGroup.GetInstanceOf(TemplateBeginName);

        st.Add(ModelAttributeName,   model);
        st.Add(ContextAttributeName, context);

        return st;
    }

}