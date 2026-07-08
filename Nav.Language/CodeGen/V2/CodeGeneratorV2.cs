#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Der Codegenerator der Sprach-Generation 2 — der <b>CallContext</b>-Codegen (Continuation, Choices
/// in C#). Er erzeugt dieselben vier Artefakte wie <see cref="CodeGeneratorV1"/> und teilt sich dessen
/// <b>invariante</b> Schnittstellen-Familien <c>I{Task}WFS</c> und <c>IBegin{Task}WFS</c> (unverändert,
/// damit Cross-Version-<c>taskref</c> weiter funktioniert, §5); nur die Maschinerie-Basisklasse
/// <c>{Task}WFSBase</c> und die OneShot-Datei <c>{Task}WFS</c> tragen die neue CallContext-Gestalt
/// (<see cref="WfsBaseEmitterV2"/>/<see cref="WfsOneShotEmitterV2"/>). Die Auswahl je
/// <see cref="CodeGenerationUnit"/> trifft der <see cref="VersionDispatchingCodeGenerator"/>.
/// </summary>
/// <remarks>
/// S5-Gerüst: CallContext-Grundform für <b>alle</b> Transitionen, aber <b>ohne</b> Continuation und
/// <b>ohne</b> Choice-Forward (S6/S7). Die neue Gestalt betrifft die V1-Regression nicht — der
/// Dispatcher schaltet V2 nur für <c>#version 2</c>-Units.
/// </remarks>
// ReSharper disable InconsistentNaming
public class CodeGeneratorV2: Generator, ICodeGenerator {

    public CodeGeneratorV2(GenerationOptions? options = null, IPathProviderFactory? pathProviderFactory = null): base(options) {
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
                                 .Select(GenerateCode)
                                 .ToImmutableArray();

        string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) {
            return diagnostics.Aggregate(new StringBuilder(), (sb, d) => sb.AppendLine(FormatDiagnostic(d)), sb => sb.ToString());
        }

        string FormatDiagnostic(Diagnostic diagnostic) {
            return $"{diagnostic.Descriptor.Id}: {diagnostic.Location} {diagnostic.Message}";
        }
    }

    CodeGenerationResult GenerateCode(ITaskDefinitionSymbol taskDefinition) {

        var pathProvider = PathProviderFactory.CreatePathProvider(taskDefinition, Options);
        var context      = new CodeGeneratorContext(Options, taskDefinition.CodeGenerationUnit!.LanguageVersion);

        // Reihenfolge wie V1 (nur log-/statistikrelevant): IWfs, IBeginWfs, WfsBase, Wfs. Die beiden
        // Interface-Familien sind invariant und werden aus der V1-Emitter-/CodeModel-Schicht geteilt.
        var specs = new[] {
                GenerateIWfsCodeSpec(taskDefinition, pathProvider, context),
                GenerateIBeginWfsCodeSpec(taskDefinition, pathProvider, context),
                GenerateWfsBaseCodeSpec(taskDefinition, pathProvider, context),
                GenerateWfsCodeSpec(taskDefinition, pathProvider, context)
            }.Where(spec => !spec.IsEmpty)
             .ToImmutableArray();

        return new CodeGenerationResult(taskDefinition, specs);
    }

    CodeGenerationSpec GenerateIWfsCodeSpec(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, CodeGeneratorContext context) {

        if (!Options.GenerateIwflClasses) {
            return CodeGenerationSpec.Empty;
        }

        var model   = IWfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options);
        var content = IWfsEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    CodeGenerationSpec GenerateIBeginWfsCodeSpec(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, CodeGeneratorContext context) {

        if (!Options.GenerateWflClasses) {
            return CodeGenerationSpec.Empty;
        }

        var model   = IBeginWfsCodeModel.FromTaskDefinition(taskDefinition, pathProvider, Options);
        var content = IBeginWfsEmitter.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    CodeGenerationSpec GenerateWfsBaseCodeSpec(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, CodeGeneratorContext context) {

        if (!Options.GenerateWflClasses) {
            return CodeGenerationSpec.Empty;
        }

        var model   = WfsBaseCodeModelV2.FromTaskDefinition(taskDefinition, pathProvider, Options);
        var content = WfsBaseEmitterV2.Emit(model, context);

        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.WhenChanged);
    }

    CodeGenerationSpec GenerateWfsCodeSpec(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, CodeGeneratorContext context) {

        if (!Options.GenerateWflClasses) {
            return CodeGenerationSpec.Empty;
        }

        var model   = WfsCodeModelV2.FromTaskDefinition(taskDefinition, pathProvider, Options);
        var content = WfsOneShotEmitterV2.Emit(model, context);

        // Benutzer-Datei: nur einmalig anlegen, danach nie überschreiben.
        return new CodeGenerationSpec(content, model.FilePath, OverwritePolicy.Never);
    }

}
