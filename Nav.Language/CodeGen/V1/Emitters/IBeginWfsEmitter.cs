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

    /// <summary>
    /// Erzeugt die vollständige <c>IBegin{Task}WFS.cs</c>-Datei aus dem <see cref="IBeginWfsCodeModel"/>:
    /// Dateikopf, Using-Direktiven, den Namespace-Rahmen samt <c>#pragma warning disable 0108</c>
    /// (Redeklaration ererbter Begins ohne <c>new</c>), die nutzerdeklarierten <c>code</c>-Blöcke
    /// (<see cref="IBeginWfsCodeModel.CodeDeclarations"/>) sowie das eigentliche
    /// <c>public interface IBegin{Task}WFS</c> mit je einer <c>Begin</c>-Methode pro Init-Transition.
    /// Liefert den fertigen Quelltext als Zeichenkette.
    /// </summary>
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
                cb.WriteJoin(model.CodeDeclarations, decl => cb.Write(decl), separator: cb.NewLine);
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

    /// <summary>
    /// Schreibt die <c>Begin</c>-Methoden-Deklarationen des Interface-Rumpfs — je eine pro
    /// Init-Transition, durch je eine Leerzeile getrennt. Der Methodenname stammt aus
    /// <see cref="ICodeGenFacts.BeginMethodPrefix"/> (versionierbares Fakt).
    /// </summary>
    static void WriteBeginMethodDeclarations(CodeBuilder cb, IReadOnlyList<InitTransitionCodeModel> initTransitions, ICodeGenFacts facts) {

        // Interface-Rumpf ohne Init ist im gültigen Modell nicht möglich (eine Task-Definition trägt
        // stets mindestens einen init-Knoten). Der Vollständigkeit halber bleibt der Rumpf dann leer.
        cb.WriteJoin(
            initTransitions,
            initTransition => WriteBeginMethodDeclaration(cb, initTransition, facts),
            separator: cb.NewLine);

        if (initTransitions.Count > 0) {
            cb.WriteLine();
        }
    }

    /// <summary>
    /// Schreibt eine einzelne <c>Begin</c>-Deklaration: die <c>NavInit</c>-Annotation (Rückweg auf den
    /// init-Knoten) und die Signatur <c>IINIT_TASK {BeginMethodPrefix}(…)</c> mit der an der öffnenden
    /// Klammer ausgerichteten Parameterliste der Init-Transition.
    /// </summary>
    static void WriteBeginMethodDeclaration(CodeBuilder cb, InitTransitionCodeModel initTransition, ICodeGenFacts facts) {

        EmitterCommon.WriteNavInitAnnotation(cb, initTransition.NodeName);

        cb.Write($"IINIT_TASK {facts.BeginMethodPrefix}(");
        cb.WriteAlignedJoin(
            initTransition.Parameter,
            parameter => cb.Write($"{parameter.ParameterType} {parameter.ParameterName}"),
            separator: $",{cb.NewLine}");

        cb.Write(");");
    }

}