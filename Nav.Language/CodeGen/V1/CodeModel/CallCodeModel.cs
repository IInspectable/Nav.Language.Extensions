#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Ein einzelner von einer Transition aus erreichbarer Aufruf — ein Fall der <c>switch(body)</c>-Weiche in der
/// generierten Transitions-Methode (<see cref="WfsBaseEmitter"/>). Die konkreten Ableitungen decken die
/// Kanten-Arten ab: <see cref="TaskCallCodeModel"/> (Sub-Task), <see cref="GuiCallCodeModel"/> (View/Dialog),
/// <see cref="ExitCallCodeModel"/> (Task-Ergebnis), <see cref="EndCallCodeModel"/> (Workflow-Ende) und
/// <see cref="CanceCallCodeModel"/> (immer implizit). <see cref="TemplateName"/> wählt den <c>case</c>-Zweig,
/// <see cref="SortOrder"/> die Reihenfolge der Fälle.
/// </summary>
abstract class CallCodeModel: CodeModel {

    protected CallCodeModel(string? name, EdgeMode edgeMode) {
        Name     = name ?? String.Empty;
        EdgeMode = edgeMode;
    }

    /// <summary>Der Kanten-Modus (<c>goto</c> / modal / non-modal) — bestimmt bei Task-/Gui-Aufrufen die Engine-Methode und den <see cref="TemplateName"/>.</summary>
    public EdgeMode EdgeMode       { get; }
    /// <summary>Der Name des Ziel-Knotens der Kante (leer beim <see cref="CanceCallCodeModel"/>).</summary>
    public string   Name           { get; }
    /// <summary>Der Ziel-Knotenname in Pascalcase — bildet u.a. den <c>{Node}NodeName</c>-Konstantenbezug und den <c>{Node}TO</c>-Typ im <c>case</c>.</summary>
    public string   PascalcaseName => Name.ToPascalcase();
    /// <summary>Der Ziel-Knotenname in Camelcase — bildet den Case-Bindungsnamen (z.B. <c>{node}TO</c>).</summary>
    public string   CamelcaseName  => Name.ToCamelcase();

    /// <summary>Der Bezeichner des zu emittierenden <c>case</c>-Zweigs (früher der StringTemplate-Name), ausgewertet vom Emitter-<c>switch</c> in <see cref="WfsBaseEmitter"/>.</summary>
    public abstract string TemplateName { get; }
    /// <summary>Die Sortierstufe des Falls in der Weiche (Task=1, Gui=2, Exit=3, End=4, Cancel=<see cref="Int32.MaxValue"/>).</summary>
    public abstract int    SortOrder    { get; }

}

/// <summary>Der stets implizit erreichbare <c>case CANCEL cancel: return cancel;</c>-Fall — jede Weiche trägt ihn als letzten (<see cref="SortOrder"/> = <see cref="Int32.MaxValue"/>).</summary>
sealed class CanceCallCodeModel: CallCodeModel {

    public CanceCallCodeModel(): base(String.Empty, EdgeMode.Goto) {
    }

    public override string TemplateName => "cancel";
    public override int    SortOrder    => Int32.MaxValue;

}

/// <summary>
/// Der Aufruf eines Sub-Task-Knotens — der <c>case TaskCall taskCall when taskCall.NodeName == {Node}NodeName</c>-Fall.
/// Der implementierte Zweig ruft je nach <see cref="CallCodeModel.EdgeMode"/> <c>GotoTask</c>/<c>OpenModalTask</c>/
/// <c>StartNonModalTask</c> mit dem <see cref="TaskResult"/>-Typargument und der <c>After{Node}</c>-Fortsetzung auf;
/// ist der Ziel-Task <c>[notimplemented]</c> (<see cref="NotImplemented"/>), wirft der Fall stattdessen.
/// </summary>
sealed class TaskCallCodeModel: CallCodeModel {

    public TaskCallCodeModel(string name, EdgeMode edgeMode, ParameterCodeModel taskResult, bool notImplemented): base(name, edgeMode) {
        TaskResult     = taskResult ?? throw new ArgumentNullException(nameof(taskResult));
        NotImplemented = notImplemented;
    }

    /// <summary>Der Ergebnistyp des Sub-Tasks — das Typargument der generischen <c>GotoTask&lt;…&gt;</c>/<c>OpenModalTask&lt;…&gt;</c>-Engine-Methode.</summary>
    public ParameterCodeModel TaskResult     { get; }
    /// <summary>Ob der Ziel-Task <c>[notimplemented]</c> ist — der Fall wirft dann <c>NotImplementedException</c> statt zu navigieren.</summary>
    public bool               NotImplemented { get; }

    public override string TemplateName {
        get {
            switch (EdgeMode) {
                case EdgeMode.Modal:
                    return "openModalTask";
                case EdgeMode.NonModal:
                    return "startNonModalTask";
                case EdgeMode.Goto:
                    return "gotoTask";
                default:
                    return "";
            }
        }
    }

    public override int SortOrder => 1;

}

/// <summary>
/// Der Aufruf eines View-/Dialog-Knotens — der <c>case {Node}TO {node}TO:</c>-Fall, der je nach
/// <see cref="CallCodeModel.EdgeMode"/> <c>GotoGUI</c>/<c>OpenModalGUI</c>/<c>StartNonModalGUI</c> mit dem
/// Transfer-Objekt aufruft.
/// </summary>
sealed class GuiCallCodeModel: CallCodeModel {

    public GuiCallCodeModel(string name, EdgeMode edgeMode): base(name, edgeMode) {
    }

    public override string TemplateName {
        get {
            switch (EdgeMode) {
                case EdgeMode.Modal:
                    return "openModalGUI";
                case EdgeMode.NonModal:
                    return "startNonModalGUI";
                case EdgeMode.Goto:
                    return "gotoGUI";
                default:
                    return "";
            }
        }
    }

    public override int SortOrder => 2;

}

/// <summary>Der Aufruf eines <c>exit</c>-Knotens — der <c>case TASK_RESULT taskResult: return taskResult;</c>-Fall, der das Task-Ergebnis an den Aufrufer zurückgibt.</summary>
sealed class ExitCallCodeModel: CallCodeModel {

    public ExitCallCodeModel(string name, EdgeMode edgeMode): base(name, edgeMode) {
    }

    public override string TemplateName => "goToExit";
    public override int    SortOrder    => 3;

}

/// <summary>Der Aufruf eines <c>end</c>-Knotens — der <c>case END _: return EndNonModal();</c>-Fall, der den Workflow beendet.</summary>
sealed class EndCallCodeModel: CallCodeModel {

    public EndCallCodeModel(string name, EdgeMode edgeMode): base(name, edgeMode) {
    }

    public override string TemplateName => "goToEnd";
    public override int    SortOrder    => 4;

}