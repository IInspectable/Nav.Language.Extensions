#region Using Directives

using Microsoft.VisualStudio.Imaging.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Images; 

public static partial class ImageMonikers {

    /// <summary>
    /// <see cref="SymbolVisitor{TResult}"/>, der jedem Nav-<see cref="ISymbol"/> das passende Icon
    /// (<see cref="ImageMoniker"/>) zuordnet. Referenz-Symbole (Knoten-/ConnectionPoint-Referenzen,
    /// Aliase) delegieren an ihre Deklaration, sodass eine Referenz dasselbe Icon wie ihr Ziel erhält.
    /// </summary>
    sealed class SymbolImageVisitor: SymbolVisitor<ImageMoniker> {

        /// <summary>
        /// Ermittelt das Icon für <paramref name="symbol"/> durch Anwendung des Visitors.
        /// </summary>
        /// <param name="symbol">Das zu besuchende Nav-Symbol.</param>
        /// <returns>Das dem Symbol zugeordnete Icon.</returns>
        public static ImageMoniker FindImageMoniker(ISymbol symbol) {
            var finder = new SymbolImageVisitor();
            return finder.Visit(symbol);
        }

        /// <summary>Liefert das Icon für eine Task-Deklaration.</summary>
        public override ImageMoniker VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {
            return TaskDeclaration;
        }

        /// <summary>Liefert das Icon für eine Task-Definition.</summary>
        public override ImageMoniker VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {
            return TaskDefinition;
        }

        /// <summary>Liefert das Icon für eine Include-Direktive.</summary>
        public override ImageMoniker VisitIncludeSymbol(IIncludeSymbol includeSymbol) {
            return Include;
        }

        /// <summary>Liefert das Icon für einen Signal-Trigger.</summary>
        public override ImageMoniker VisitSignalTriggerSymbol(ISignalTriggerSymbol signalTriggerSymbol) {
            return SignalTrigger;
        }

        /// <summary>
        /// Liefert das Icon für eine Kante — abhängig vom <see cref="IEdgeModeSymbol.EdgeMode"/> und davon,
        /// ob es sich um eine Continuation handelt.
        /// </summary>
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

        /// <summary>
        /// Liefert das Icon der referenzierten Exit-ConnectionPoint-Deklaration; ohne aufgelöste
        /// Deklaration das Default-Icon.
        /// </summary>
        public override ImageMoniker VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {
            if (exitConnectionPointReferenceSymbol.Declaration == null) {
                return DefaultVisit(exitConnectionPointReferenceSymbol);
            }

            return Visit(exitConnectionPointReferenceSymbol.Declaration);
        }

        /// <summary>Liefert das Icon für einen Init-ConnectionPoint.</summary>
        public override ImageMoniker VisitInitConnectionPointSymbol(IInitConnectionPointSymbol initConnectionPointSymbol) {
            return InitConnectionPoint;
        }

        /// <summary>Liefert das Icon für einen Exit-ConnectionPoint.</summary>
        public override ImageMoniker VisitExitConnectionPointSymbol(IExitConnectionPointSymbol exitConnectionPointSymbol) {
            return ExitConnectionPoint;
        }

        /// <summary>Liefert das Icon für einen End-ConnectionPoint.</summary>
        public override ImageMoniker VisitEndConnectionPointSymbol(IEndConnectionPointSymbol endConnectionPointSymbol) {
            return EndConnectionPoint;
        }

        #endregion

        #region Nodes

        /// <summary>
        /// Liefert das Icon der referenzierten Knoten-Deklaration; ohne aufgelöste Deklaration das
        /// Default-Icon.
        /// </summary>
        public override ImageMoniker VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {
            if (nodeReferenceSymbol.Declaration == null) {
                return DefaultVisit(nodeReferenceSymbol);
            }

            return Visit(nodeReferenceSymbol.Declaration);
        }

        /// <summary>Liefert das Icon für einen Init-Knoten.</summary>
        public override ImageMoniker VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {
            return InitNode;
        }

        /// <summary>Liefert das Icon des Init-Knotens, auf den der Alias verweist.</summary>
        public override ImageMoniker VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
            return Visit(initNodeAliasSymbol.InitNode);
        }

        /// <summary>Liefert das Icon für einen Exit-Knoten.</summary>
        public override ImageMoniker VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {
            return ExitNode;
        }

        /// <summary>Liefert das Icon für einen End-Knoten.</summary>
        public override ImageMoniker VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {
            return EndNode;
        }

        /// <summary>Liefert das Icon für einen Task-Knoten.</summary>
        public override ImageMoniker VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
            return TaskNode;
        }

        /// <summary>Liefert das Icon des Task-Knotens, auf den der Alias verweist.</summary>
        public override ImageMoniker VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAlias) {
            return Visit(taskNodeAlias.TaskNode);
        }

        /// <summary>Liefert das Icon für einen Choice-Knoten.</summary>
        public override ImageMoniker VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {
            return ChoiceNode;
        }

        /// <summary>Liefert das Icon für einen View-Knoten.</summary>
        public override ImageMoniker VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {
            return ViewNode;
        }

        /// <summary>Liefert das Icon für einen Dialog-Knoten.</summary>
        public override ImageMoniker VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {
            return DialogNode;
        }

        #endregion

    }

}