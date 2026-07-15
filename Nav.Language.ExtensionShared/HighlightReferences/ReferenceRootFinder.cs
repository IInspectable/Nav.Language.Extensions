namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

/// <summary>
/// Ermittelt zu einem Symbol unter dem Cursor dessen <b>Wurzel-Symbol</b> — jene Deklaration bzw.
/// Definition, von der aus der <see cref="ReferenceFinder"/> anschließend alle Referenzen einsammelt.
/// Der Visitor läuft im Symbolgraph gezielt „nach oben": von einer Fundstelle (Node-Referenz, Alias,
/// Exit-Verbindungspunkt-Referenz) zu ihrer Deklaration und — sofern die Task-Deklaration im aktuellen
/// File liegt (also nicht per <c>include</c> eingebunden ist) — bis zur Deklaration selbst. So beginnt
/// die Referenzsuche stets an derselben Stelle, egal wo im Symbolgraph der Cursor gerade steht.
/// </summary>
sealed class ReferenceRootFinder: SymbolVisitor<ISymbol> {

    ReferenceRootFinder(ISymbol originatingSymbol) {
        OriginatingSymbol = originatingSymbol;
    }

    /// <summary>Das Symbol, an dem die Suche begann (der ursprüngliche Treffer unter dem Cursor).</summary>
    public ISymbol OriginatingSymbol { get; }

    /// <summary>
    /// Liefert das Wurzel-Symbol zu <paramref name="symbol"/>, von dem aus die Referenzsuche startet.
    /// </summary>
    public static ISymbol FindRoot(ISymbol symbol) {
        var finder = new ReferenceRootFinder(symbol);
        return finder.Visit(symbol);
    }

    /// <summary>Fallback: Symbole ohne eigene Aufstiegsregel sind selbst bereits die Wurzel.</summary>
    protected override ISymbol DefaultVisit(ISymbol symbol) {
        return symbol;
    }

    /// <summary>
    /// Steigt von einer Task-<b>Definition</b> zu ihrer Task-<b>Deklaration</b> auf, sofern diese im
    /// aktuellen File liegt (nicht per <c>include</c> eingebunden); andernfalls bleibt die Definition
    /// selbst die Wurzel.
    /// </summary>
    public override ISymbol VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {

        var taskDeclaration = taskDefinitionSymbol.AsTaskDeclaration;
        if (taskDeclaration?.IsIncluded == false) {
            return Visit(taskDeclaration);
        }

        return DefaultVisit(taskDefinitionSymbol);
    }

    /// <summary>Ein Init-Alias verweist auf seinen Init-Knoten; die Suche steigt dorthin weiter.</summary>
    public override ISymbol VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return Visit(initNodeAliasSymbol.InitNode);
    }

    /// <summary>
    /// Steigt von einer Task-Knoten-Referenz zu ihrer Task-Deklaration auf, sofern die Knoten selbst der
    /// Ursprung ist oder es keinen Alias gibt — und die Deklaration im aktuellen File liegt.
    /// </summary>
    public override ISymbol VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
        // Wenn die Tasknode selbst der Ursprung ist, oder es keinen Alias gibt, dann laufen wir hoch zur Deklaration - sofern sie in unserem File liegt
        if ((OriginatingSymbol == taskNodeSymbol || taskNodeSymbol.Alias == null) && taskNodeSymbol.Declaration?.IsIncluded == false) {
            return Visit(taskNodeSymbol.Declaration);
        }

        return DefaultVisit(taskNodeSymbol);
    }

    /// <summary>Eine Knoten-Referenz steigt zu ihrer Deklaration auf, sofern vorhanden.</summary>
    public override ISymbol VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {

        if (nodeReferenceSymbol.Declaration != null) {
            return Visit(nodeReferenceSymbol.Declaration);
        }

        return DefaultVisit(nodeReferenceSymbol);
    }

    /// <summary>Ein Task-Knoten-Alias verweist auf seinen Task-Knoten; die Suche steigt dorthin weiter.</summary>
    public override ISymbol VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAliasSymbol) {
        return Visit(taskNodeAliasSymbol.TaskNode);
    }

    /// <summary>
    /// Eine Exit-Verbindungspunkt-Referenz steigt zu ihrer Deklaration auf, sofern vorhanden.
    /// </summary>
    public override ISymbol VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {

        if (exitConnectionPointReferenceSymbol.Declaration !=null) {

            return Visit(exitConnectionPointReferenceSymbol.Declaration);
        }

        return DefaultVisit(exitConnectionPointReferenceSymbol);
    }
       

}
