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

    /// <summary>Das Symbol unter dem Caret, von dem der Aufstieg ausging (steuert einzelne Aufstiegs-Entscheidungen).</summary>
    ISymbol OriginatingSymbol { get; }

    /// <summary>
    /// Bestimmt zum übergebenen Symbol das Wurzel-Symbol, von dem aus <see cref="HighlightSymbolFinder"/> die
    /// Referenzen einsammelt.
    /// </summary>
    /// <param name="symbol">Das Symbol unter dem Caret.</param>
    /// <returns>Das Wurzel-Symbol (Deklaration) oder das Ausgangssymbol selbst, wenn kein Aufstieg möglich ist.</returns>
    public static ISymbol FindRoot(ISymbol symbol) {
        var finder = new ReferenceRootFinder(symbol);
        return finder.Visit(symbol);
    }

    /// <summary>Symbol-Arten ohne eigene Aufstiegsregel sind selbst schon die Wurzel.</summary>
    protected override ISymbol DefaultVisit(ISymbol symbol) {
        return symbol;
    }

    /// <summary>
    /// Eine Task-Definition steigt zu ihrer <c>taskref</c>-Deklaration auf, sofern diese existiert und nicht
    /// inkludiert ist — so laufen Definition und alle Referenzen über dieselbe Wurzel.
    /// </summary>
    public override ISymbol VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {

        var taskDeclaration = taskDefinitionSymbol.AsTaskDeclaration;
        if (taskDeclaration != null && !taskDeclaration.IsIncluded) {
            return Visit(taskDeclaration);
        }

        return DefaultVisit(taskDefinitionSymbol);
    }

    /// <summary>Ein Init-Alias steigt zu seinem Init-Knoten auf.</summary>
    public override ISymbol VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return Visit(initNodeAliasSymbol.InitNode);
    }

    /// <summary>
    /// Ein Task-Knoten steigt zu seiner Task-Deklaration auf, wenn er selbst der Ursprung ist oder keinen Alias
    /// besitzt — jeweils nur, solange die Deklaration im selben File liegt (nicht inkludiert). Andernfalls
    /// bleibt der Knoten die Wurzel (der Alias hat einen eigenen Referenz-Bereich).
    /// </summary>
    public override ISymbol VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
        // Wenn die Tasknode selbst der Ursprung ist, oder es keinen Alias gibt, dann laufen wir hoch zur
        // Deklaration - sofern sie in unserem File liegt.
        var declaration = taskNodeSymbol.Declaration;
        if ((OriginatingSymbol == taskNodeSymbol || taskNodeSymbol.Alias == null) && declaration != null && !declaration.IsIncluded) {
            return Visit(declaration);
        }

        return DefaultVisit(taskNodeSymbol);
    }

    /// <summary>Eine Knoten-Referenz steigt zu ihrer Knoten-Deklaration auf, sofern diese aufgelöst ist.</summary>
    public override ISymbol VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {

        if (nodeReferenceSymbol.Declaration != null) {
            return Visit(nodeReferenceSymbol.Declaration);
        }

        return DefaultVisit(nodeReferenceSymbol);
    }

    /// <summary>Ein Task-Knoten-Alias steigt zu seinem Task-Knoten auf.</summary>
    public override ISymbol VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAliasSymbol) {
        return Visit(taskNodeAliasSymbol.TaskNode);
    }

    /// <summary>
    /// Eine Exit-Connection-Point-Referenz (der <c>exit</c> in einer <c>Instanz:exit --&gt; …</c>-Kante) steigt
    /// zu ihrer Exit-Connection-Point-Deklaration auf, sofern diese aufgelöst ist.
    /// </summary>
    public override ISymbol VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {

        if (exitConnectionPointReferenceSymbol.Declaration != null) {
            return Visit(exitConnectionPointReferenceSymbol.Declaration);
        }

        return DefaultVisit(exitConnectionPointReferenceSymbol);
    }

}
