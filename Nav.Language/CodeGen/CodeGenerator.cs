#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Antlr4.StringTemplate;

using JetBrains.Annotations;

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

    ImmutableArray<CodeGenerationResult> Generate(CodeGenerationUnit codeGenerationUnit);

}

// ReSharper disable InconsistentNaming
public class CodeGenerator: Generator, ICodeGenerator {

    const string TemplateBeginName    = "Begin";
    const string ModelAttributeName   = "model";
    const string ContextAttributeName = "context";

    public CodeGenerator(GenerationOptions options = null, IPathProviderFactory pathProviderFactory = null): base(options) {
        PathProviderFactory = pathProviderFactory ?? Language.PathProviderFactory.Default;
    }

    [NotNull]
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
            taskDefinition   : taskDefinition,
            beginWfsCodeModel: Options.GenerateWflClasses ? IBeginWfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options) : null,
            iwfsCodeModel    : Options.GenerateIwflClasses ? IWfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options)     : null,
            wfsBaseCodeModel : Options.GenerateWflClasses ? WfsBaseCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options)   : null,
            wfsCodeModel     : Options.GenerateWflClasses ? WfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options)       : null,
            toCodeModels     : Options.GenerateToClasses ? TOCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options)         : null
        );

        return codeModelResult;
    }

    CodeGenerationResult GenerateCode(CodeModelResult codeModelResult) {

        var context = new CodeGeneratorContext(this, codeModelResult.TaskDefinition.CodeGenerationUnit.LanguageVersion);

        var codeGenerationResult = new CodeGenerationResult(
            taskDefinition   : codeModelResult.TaskDefinition,
            iBeginWfsCodeSpec: GenerateIBeginWfsCodeSpec(codeModelResult.IBeginWfsCodeModel, context),
            iWfsCodeSpec     : GenerateIWfsCodeSpec(codeModelResult.IWfsCodeModel, context),
            wfsBaseCodeSpec  : GenerateWfsBaseCodeSpec(codeModelResult.WfsBaseCodeModel, context),
            wfsCodeSpec      : GenerateWfsCodeSpec(codeModelResult.WfsCodeModel, context),
            toCodeSpecs      : GenerateToCodeSpecs(codeModelResult.TOCodeModels, context));

        return codeGenerationResult;
    }

    static readonly ThreadLocal<TemplateGroup> IBeginWfsTemplateGroup = new(() => LoadTemplateGroup(Resources.IBeginWfsTemplate));

    static CodeGenerationSpec GenerateIBeginWfsCodeSpec([CanBeNull] IBeginWfsCodeModel model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var template = GetTemplate(IBeginWfsTemplateGroup.Value, model, context);
        var content  = template.Render();

        return new CodeGenerationSpec(content, model.FilePath);
    }

    static readonly ThreadLocal<TemplateGroup> IWfsTemplateGroup = new(() => LoadTemplateGroup(Resources.IWfsTemplate));

    static CodeGenerationSpec GenerateIWfsCodeSpec([CanBeNull] IWfsCodeModel model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var template = GetTemplate(IWfsTemplateGroup.Value, model, context);
        var content  = template.Render();

        return new CodeGenerationSpec(content, model.FilePath);
    }

    static readonly ThreadLocal<TemplateGroup> WfsBaseTemplateGroup = new(() => LoadTemplateGroup(Resources.WfsBaseTemplate));

    static CodeGenerationSpec GenerateWfsBaseCodeSpec([CanBeNull] WfsBaseCodeModel model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var template = GetTemplate(WfsBaseTemplateGroup.Value, model, context);
        var content  = template.Render();

        return new CodeGenerationSpec(content, model.FilePath);
    }

    static readonly ThreadLocal<TemplateGroup> WfsTemplateGroup = new(() => LoadTemplateGroup(Resources.WFSOneShotTemplate));

    static CodeGenerationSpec GenerateWfsCodeSpec([CanBeNull] WfsCodeModel model, CodeGeneratorContext context) {

        if (model == null) {
            return CodeGenerationSpec.Empty;
        }

        var template = GetTemplate(WfsTemplateGroup.Value, model, context);
        var content  = template.Render();

        return new CodeGenerationSpec(content, model.FilePath);
    }

    static readonly ThreadLocal<TemplateGroup> ToTemplateGroup = new(() => LoadTemplateGroup(Resources.TOTemplate));

    static IEnumerable<CodeGenerationSpec> GenerateToCodeSpecs(IEnumerable<TOCodeModel> models, CodeGeneratorContext context) {
        return models.Select(model => GenerateToCodeSpec(model, context));

        static CodeGenerationSpec GenerateToCodeSpec(TOCodeModel model, CodeGeneratorContext context) {

            var template = GetTemplate(ToTemplateGroup.Value, model, context);
            var content  = template.Render();

            return new CodeGenerationSpec(content, model.FilePath);
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