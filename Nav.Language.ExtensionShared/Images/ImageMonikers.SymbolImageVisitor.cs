#region Using Directives

using Microsoft.VisualStudio.Imaging.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Images; 

public static partial class ImageMonikers {

    sealed class SymbolImageVisitor: SymbolVisitor<ImageMoniker> {

        public static ImageMoniker FindImageMoniker(ISymbol symbol) {
            var finder = new SymbolImageVisitor();
            return finder.Visit(symbol);
        }

        public override ImageMoniker VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {
            return TaskDeclaration;
        }

        public override ImageMoniker VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {
            return TaskDefinition;
        }

        public override ImageMoniker VisitIncludeSymbol(IIncludeSymbol includeSymbol) {
            return Include;
        }

        public override ImageMoniker VisitSignalTriggerSymbol(ISignalTriggerSymbol signalTriggerSymbol) {
            return SignalTrigger;
        }

        public override ImageMoniker VisitEdgeModeSymbol(IEdgeModeSymbol edgeModeSymbol) {
            switch (edgeModeSymbol.EdgeMode) {

                // Modal/Goto gibt es je als reguläre Kante (->/-->) und als Continuation (o-^/--^) mit
                // eigenem Icon. Eine NonModal-Continuation existiert sprachlich nicht (==> ist stets regulär).
                case EdgeMode.Modal:
                    return edgeModeSymbol.IsContinuation ? ModalContinuation : ModalEdge;
                case EdgeMode.NonModal:
                    return NonModalEdge;
                case EdgeMode.Goto:
                    return edgeModeSymbol.IsContinuation ? GoToContinuation : GoToEdge;
                default:
                    return Edge;
            }
        }

        #region ConnectionPoints

        public override ImageMoniker VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {
            if (exitConnectionPointReferenceSymbol.Declaration == null) {
                return DefaultVisit(exitConnectionPointReferenceSymbol);
            }

            return Visit(exitConnectionPointReferenceSymbol.Declaration);
        }

        public override ImageMoniker VisitInitConnectionPointSymbol(IInitConnectionPointSymbol initConnectionPointSymbol) {
            return InitConnectionPoint;
        }

        public override ImageMoniker VisitExitConnectionPointSymbol(IExitConnectionPointSymbol exitConnectionPointSymbol) {
            return ExitConnectionPoint;
        }

        public override ImageMoniker VisitEndConnectionPointSymbol(IEndConnectionPointSymbol endConnectionPointSymbol) {
            return EndConnectionPoint;
        }

        #endregion

        #region Nodes

        public override ImageMoniker VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {
            if (nodeReferenceSymbol.Declaration == null) {
                return DefaultVisit(nodeReferenceSymbol);
            }

            return Visit(nodeReferenceSymbol.Declaration);
        }

        public override ImageMoniker VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {
            return InitNode;
        }

        public override ImageMoniker VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
            return Visit(initNodeAliasSymbol.InitNode);
        }

        public override ImageMoniker VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {
            return ExitNode;
        }

        public override ImageMoniker VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {
            return EndNode;
        }

        public override ImageMoniker VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
            return TaskNode;
        }

        public override ImageMoniker VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAlias) {
            return Visit(taskNodeAlias.TaskNode);
        }

        public override ImageMoniker VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {
            return ChoiceNode;
        }

        public override ImageMoniker VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {
            return ViewNode;
        }

        public override ImageMoniker VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {
            return DialogNode;
        }

        #endregion

    }

}