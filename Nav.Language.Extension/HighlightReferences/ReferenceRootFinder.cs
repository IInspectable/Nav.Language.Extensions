namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences {

    sealed class ReferenceRootFinder : SymbolVisitor<ISymbol> {

        public static ISymbol FindRoot(ISymbol symbol) {
            var finder = new ReferenceRootFinder();
            return finder.Visit(symbol);
        }

        protected override ISymbol DefaultVisit(ISymbol symbol) {
            return symbol;
        }

        public override ISymbol VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {

            var taskDeclaration = taskDefinitionSymbol.AsTaskDeclaration;
            if (taskDeclaration?.IsIncluded == false) {
                return Visit(taskDeclaration);
            }
            return DefaultVisit(taskDefinitionSymbol);
        }

        public override ISymbol VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {

            if (taskNodeSymbol.Declaration?.IsIncluded == false) {
                return Visit(taskNodeSymbol.Declaration);
            }
            return DefaultVisit(taskNodeSymbol);
        }

        public override ISymbol VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {

            if (nodeReferenceSymbol.Declaration != null) {
                return Visit(nodeReferenceSymbol.Declaration);
            }
            return DefaultVisit(nodeReferenceSymbol);
        }

        public override ISymbol VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAliasSymbol) {
            return Visit(taskNodeAliasSymbol.TaskNode);
        }        
    }
}