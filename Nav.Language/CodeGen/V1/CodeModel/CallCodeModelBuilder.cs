#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Übersetzt die erreichbaren <see cref="Call"/>s einer Transition per <see cref="SymbolVisitor{TResult}"/>-Dispatch
/// über den Ziel-Knoten in die passende <see cref="CallCodeModel"/>-Ableitung (Task/Gui/Exit/End). Der
/// <see cref="EdgeMode"/> der jeweiligen Kante wird dabei mitgeführt und ins CodeModel übernommen.
/// </summary>
sealed class CallCodeModelBuilder: SymbolVisitor<CallCodeModel> {

    public CallCodeModelBuilder(EdgeMode edgeMode) {
        EdgeMode = edgeMode;
    }

    /// <summary>Der Kanten-Modus der gerade besuchten Kante — wird an das erzeugte <see cref="CallCodeModel"/> durchgereicht.</summary>
    public EdgeMode EdgeMode { get; }

    /// <summary>Bildet die erreichbaren Aufrufe einer Transition auf ihre <see cref="CallCodeModel"/>e ab (je Kante ein eigener Visitor).</summary>
    public static IEnumerable<CallCodeModel> FromCalls(IEnumerable<Call> calls) {
        return calls.Select(call => GetCallCodeModel(call.Node, call.EdgeMode));
    }

    static CallCodeModel GetCallCodeModel(INodeSymbol node, IEdgeModeSymbol edgeEdgeMode) {
        var builder = new CallCodeModelBuilder(edgeEdgeMode.EdgeMode);
        return builder.Visit(node);
    }

    /// <summary>Ein <c>exit</c>-Knoten wird zu einem <see cref="ExitCallCodeModel"/>.</summary>
    public override CallCodeModel VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {
        return new ExitCallCodeModel(exitNodeSymbol.Name, EdgeMode);
    }

    /// <summary>Ein <c>end</c>-Knoten wird zu einem <see cref="EndCallCodeModel"/>.</summary>
    public override CallCodeModel VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {
        return new EndCallCodeModel(endNodeSymbol.Name, EdgeMode);
    }

    /// <summary>Ein Task-Knoten wird zu einem <see cref="TaskCallCodeModel"/> — mit Ergebnistyp und <c>[notimplemented]</c>-Flag aus der Task-Deklaration.</summary>
    public override CallCodeModel VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
        return new TaskCallCodeModel(taskNodeSymbol.Name,
                                     EdgeMode,
                                     ParameterCodeModel.TaskResult(taskNodeSymbol.Declaration),
                                     taskNodeSymbol.Declaration?.CodeNotImplemented ?? false);
    }

    /// <summary>Ein Dialog-Knoten wird zu einem <see cref="GuiCallCodeModel"/>.</summary>
    public override CallCodeModel VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {
        return new GuiCallCodeModel(dialogNodeSymbol.Name, EdgeMode);
    }

    /// <summary>Ein View-Knoten wird zu einem <see cref="GuiCallCodeModel"/>.</summary>
    public override CallCodeModel VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {
        return new GuiCallCodeModel(viewNodeSymbol.Name, EdgeMode);
    }

}