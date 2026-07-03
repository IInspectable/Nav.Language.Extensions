namespace Pharmatechnik.Nav.Language.References;

/// <summary>
/// Steigt vom Symbol unter dem Cursor zum „Wurzel"-Symbol auf, von dem aus die Referenzen aufgesammelt
/// werden (z.B. Node-Referenz → Node-Deklaration, Task-Node → Task-Deklaration). VS-frei portiert aus
/// <c>Nav.Language.ExtensionShared/HighlightReferences/ReferenceRootFinder.cs</c>.
/// </summary>
sealed class ReferenceRootFinder: SymbolVisitor<ISymbol> {

    ReferenceRootFinder(ISymbol originatingSymbol) {
        OriginatingSymbol = originatingSymbol;
    }

    ISymbol OriginatingSymbol { get; }

    public static ISymbol FindRoot(ISymbol symbol) {
        var finder = new ReferenceRootFinder(symbol);
        return finder.Visit(symbol);
    }

    protected override ISymbol DefaultVisit(ISymbol symbol) {
        return symbol;
    }

    public override ISymbol VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {

        var taskDeclaration = taskDefinitionSymbol.AsTaskDeclaration;
        if (taskDeclaration != null && !taskDeclaration.IsIncluded) {
            return Visit(taskDeclaration);
        }

        return DefaultVisit(taskDefinitionSymbol);
    }

    public override ISymbol VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return Visit(initNodeAliasSymbol.InitNode);
    }

    public override ISymbol VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
        // Wenn die Tasknode selbst der Ursprung ist, oder es keinen Alias gibt, dann laufen wir hoch zur
        // Deklaration - sofern sie in unserem File liegt.
        var declaration = taskNodeSymbol.Declaration;
        if ((OriginatingSymbol == taskNodeSymbol || taskNodeSymbol.Alias == null) && declaration != null && !declaration.IsIncluded) {
            return Visit(declaration);
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

    public override ISymbol VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {

        if (exitConnectionPointReferenceSymbol.Declaration != null) {
            return Visit(exitConnectionPointReferenceSymbol.Declaration);
        }

        return DefaultVisit(exitConnectionPointReferenceSymbol);
    }

}
