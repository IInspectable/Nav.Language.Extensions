#nullable enable

#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class CallCodeModelBuilder: SymbolVisitor<CallCodeModel> {

    public CallCodeModelBuilder(EdgeMode edgeMode) {
        EdgeMode = edgeMode;
    }

    public EdgeMode EdgeMode { get; }

    public static IEnumerable<CallCodeModel> FromCalls(IEnumerable<Call> calls) {
        return calls.Select(call => GetCallCodeModel(call.Node, call.EdgeMode));
    }

    static CallCodeModel GetCallCodeModel(INodeSymbol node, IEdgeModeSymbol edgeEdgeMode) {
        var builder = new CallCodeModelBuilder(edgeEdgeMode.EdgeMode);
        return builder.Visit(node);
    }

    public override CallCodeModel VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {
        return new ExitCallCodeModel(exitNodeSymbol.Name, EdgeMode);
    }

    public override CallCodeModel VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {
        return new EndCallCodeModel(endNodeSymbol.Name, EdgeMode);
    }

    public override CallCodeModel VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
        return new TaskCallCodeModel(taskNodeSymbol.Name,
                                     EdgeMode,
                                     ParameterCodeModel.TaskResult(taskNodeSymbol.Declaration),
                                     taskNodeSymbol.Declaration?.CodeNotImplemented ?? false);
    }

    public override CallCodeModel VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {
        return new GuiCallCodeModel(dialogNodeSymbol.Name, EdgeMode);
    }

    public override CallCodeModel VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {
        return new GuiCallCodeModel(viewNodeSymbol.Name, EdgeMode);
    }

}