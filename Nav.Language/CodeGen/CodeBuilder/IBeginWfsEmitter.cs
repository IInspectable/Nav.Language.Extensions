#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// CodeBuilder-Emitter der <c>IBegin{Task}WFS</c>-Interface-Familie — der technische Ersatz für
/// <c>IBeginWFS.stg</c>. Das Begin-Interface ist die versions-<b>invariante</b> Schnittstelle zum
/// Workflow-Code (<see cref="CodeGenInvariants"/>): Präfix, Suffix und WFL-Ablage stammen deshalb aus
/// den Invarianten. Der Name der Begin-Methode ist ein versionierbares Fakt und kommt aus den
/// <see cref="ICodeGenFacts"/> der Generation des Tasks.
/// </summary>
// ReSharper disable once InconsistentNaming
static class IBeginWfsEmitter {

    public static string Emit(IBeginWfsCodeModel model, CodeGeneratorContext context) {

        var cb = new CodeBuilder();

        EmitterCommon.WriteFileHeader(cb, context);
        EmitterCommon.WriteUsingDirectives(cb, model.UsingNamespaces);

        cb.Write($"""

                  namespace {model.Namespace} 
                  """);
        using (cb.Block()) {

            cb.WriteLine("""

                         // Redeklarationen von Methoden ohne new sind ok - um in manuell erstellten Oberinterfaces Begins definieren zu können
                         #pragma warning disable 0108

                         """);

            if (model.CodeDeclarations.Count > 0) {
                cb.WriteJoin(model.CodeDeclarations, (b, decl) => b.Write(decl), separator: cb.NewLine);
                cb.WriteLine();
                cb.WriteLine();
            }

            EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
            cb.Write($"public interface {CodeGenInvariants.BeginInterfacePrefix}{model.Task.TaskNamePascalcase}{CodeGenInvariants.InterfaceSuffix}: {model.BaseInterfaceName} ");

            using (cb.Block()) {
                WriteBeginMethodDeclarations(cb, model.InitTransitions, model.Task.Facts);
            }
        }

        return cb.ToString();
    }

    static void WriteBeginMethodDeclarations(CodeBuilder cb, IReadOnlyList<InitTransitionCodeModel> initTransitions, ICodeGenFacts facts) {

        // Interface-Rumpf ohne Init ist im gültigen Modell nicht möglich (eine Task-Definition trägt
        // stets mindestens einen init-Knoten). Der Vollständigkeit halber bleibt der Rumpf dann leer.
        cb.WriteJoin(
            initTransitions,
            (b, initTransition) => WriteBeginMethodDeclaration(b, initTransition, facts),
            separator: cb.NewLine);

        if (initTransitions.Count > 0) {
            cb.WriteLine();
        }
    }

    static void WriteBeginMethodDeclaration(CodeBuilder cb, InitTransitionCodeModel initTransition, ICodeGenFacts facts) {

        EmitterCommon.WriteNavInitAnnotation(cb, initTransition.NodeName);

        cb.Write($"IINIT_TASK {facts.BeginMethodPrefix}(");
        cb.WriteAlignedJoin(
            initTransition.Parameter,
            (b, parameter) => b.Write($"{parameter.ParameterType} {parameter.ParameterName}"),
            separator: $",{cb.NewLine}");

        cb.Write(");");
    }

}