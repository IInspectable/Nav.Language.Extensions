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

    /// <summary>
    /// Erzeugt die vollständige <c>I{Task}WFS.cs</c>-Datei aus dem <see cref="IWfsCodeModel"/>:
    /// Dateikopf, Using-Direktiven, die file-scoped Namespace-Deklaration und das
    /// <c>public interface I{Task}WFS</c> mit
    /// je einer <c>INavCommand</c>-Methode pro Trigger-Transition. Liefert den fertigen Quelltext als
    /// Zeichenkette.
    /// </summary>
    public static string Emit(IWfsCodeModel model, CodeGeneratorContext context) {

        var cb = new CodeBuilder();

        EmitterCommon.WriteFileHeader(cb, context);
        EmitterCommon.WriteUsingDirectives(cb, model.UsingNamespaces);

        EmitterCommon.WriteNamespace(cb, model.Namespace);

        EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
        cb.Write($"public interface {model.InterfaceName}: {model.BaseInterfaceName} ");

        using (cb.Block()) {
            WriteTriggerMethodDeclarations(cb, model.TriggerTransitions);
        }

        return cb.ToString();
    }

    /// <summary>
    /// Schreibt die Trigger-Methoden-Deklarationen des Interface-Rumpfs — je eine pro
    /// Trigger-Transition, durch je eine Leerzeile getrennt.
    /// </summary>
    static void WriteTriggerMethodDeclarations(CodeBuilder cb, IReadOnlyList<TriggerTransitionCodeModel> triggerTransitions) {

        cb.WriteJoin(
            triggerTransitions,
            triggerTransition => WriteTriggerMethodDeclaration(cb, triggerTransition),
            separator: cb.NewLine);

        if (triggerTransitions.Count > 0) {
            cb.WriteLine();
        }
    }

    /// <summary>
    /// Schreibt eine einzelne Trigger-Deklaration: die <c>NavTrigger</c>-Annotation und die Signatur
    /// <c>INavCommand {TriggerName}({ViewParameter})</c> — genau ein View-Parameter je Trigger.
    /// </summary>
    static void WriteTriggerMethodDeclaration(CodeBuilder cb, TriggerTransitionCodeModel triggerTransition) {

        EmitterCommon.WriteTriggerAnnotation(cb, triggerTransition.TriggerName);

        var parameter = triggerTransition.ViewParameter;
        cb.Write($"INavCommand {triggerTransition.TriggerName}({parameter.ParameterType} {parameter.ParameterName});");
    }

}
