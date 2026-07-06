#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// CodeBuilder-Emitter der <c>I{Task}WFS</c>-Interface-Familie — der technische Ersatz für
/// <c>IWFS.stg</c>. Wie das Begin-Interface ist auch dieses die versions-<b>invariante</b>
/// Schnittstelle zum Workflow-Code (<see cref="CodeGenInvariants"/>); der Interface-Name stammt
/// deshalb aus den Invarianten (über <see cref="TaskCodeInfo.IWfsTypeName"/>, das Präfix und Suffix
/// aus <see cref="CodeGenInvariants"/> zieht). Je Trigger-Transition entsteht eine
/// <c>INavCommand</c>-Methode.
/// </summary>
// ReSharper disable once InconsistentNaming
static class IWfsEmitter {

    public static string Emit(IWfsCodeModel model, CodeGeneratorContext context) {

        var cb = new CodeBuilder();

        EmitterCommon.WriteFileHeader(cb, context);
        EmitterCommon.WriteUsingDirectives(cb, model.UsingNamespaces);

        cb.Write($"""

                  namespace {model.Namespace} 
                  """);
        using (cb.Block()) {

            EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
            cb.Write($"public interface {model.InterfaceName}: {model.BaseInterfaceName} ");

            using (cb.Block()) {
                WriteTriggerMethodDeclarations(cb, model.TriggerTransitions);
            }
        }

        return cb.ToString();
    }

    static void WriteTriggerMethodDeclarations(CodeBuilder cb, IReadOnlyList<TriggerTransitionCodeModel> triggerTransitions) {

        cb.WriteJoin(
            triggerTransitions,
            triggerTransition => WriteTriggerMethodDeclaration(cb, triggerTransition),
            separator: cb.NewLine);

        if (triggerTransitions.Count > 0) {
            cb.WriteLine();
        }
    }

    static void WriteTriggerMethodDeclaration(CodeBuilder cb, TriggerTransitionCodeModel triggerTransition) {

        EmitterCommon.WriteTriggerAnnotation(cb, triggerTransition.TriggerName);

        var parameter = triggerTransition.ViewParameter;
        cb.Write($"INavCommand {triggerTransition.TriggerName}({parameter.ParameterType} {parameter.ParameterName});");
    }

}
