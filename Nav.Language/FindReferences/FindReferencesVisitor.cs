#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

class FindReferencesVisitor: SymbolVisitor<Task> {

    readonly FindReferencesArgs _args;

    FindReferencesVisitor(FindReferencesArgs args) {
        _args = args;

    }

    private IFindReferencesContext Context => _args.Context;

    public static Task Invoke(FindReferencesArgs args) {
        var finder = new FindReferencesVisitor(args);

        return finder.Visit(args.OriginatingSymbol);
    }

    protected override Task DefaultVisit(ISymbol symbol) {
        return Task.CompletedTask;
    }

    #region Task Declaration

    public override async Task VisitInitConnectionPointSymbol(IInitConnectionPointSymbol initConnectionPointSymbol) {

        var initReferences = initConnectionPointSymbol.TaskDeclaration
                                                      .References
                                                      .SelectMany(tn => tn.Incomings)
                                                      .Select(edge => edge.TargetReference)
                                                      .WhereNotNull()
                                                      .OrderByLocation();

        var definitionItem = DefinitionItem.CreateInitConnectionPointDefinition(initConnectionPointSymbol, true);

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in initReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }
    }

    public override async Task VisitExitConnectionPointSymbol(IExitConnectionPointSymbol exitConnectionPointSymbol) {

        var exitReferences = exitConnectionPointSymbol.TaskDeclaration
                                                      .References
                                                      .SelectMany(tn => tn.Outgoings)
                                                      .Select(exitTrans => exitTrans.ExitConnectionPointReference)
                                                      .WhereNotNull()
                                                      .Where(ep => ep.Declaration == exitConnectionPointSymbol);

        var definitionItem = DefinitionItem.CreateExitConnectionPointDefinition(exitConnectionPointSymbol, true);

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in exitReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

    }

    public override Task VisitEndConnectionPointSymbol(IEndConnectionPointSymbol endConnectionPointSymbol) {
        // Hat keine Referenzen...
        return Task.CompletedTask;
    }

    public override async Task VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclaration) {

        var taskrefDefinition              = DefinitionItem.CreateTaskDeclarationItem(taskDeclaration);
        var initConnectionPointDefinition  = DefinitionItem.CreateInitConnectionPointDefinition(taskDeclaration, false);
        var exitConnectionPointDefinitions = DefinitionItem.CreateExitConnectionPointDefinitions(taskDeclaration, false);

        // Auch wenn wir keine Referenzen auf den Task finden sollten, soll zumindest
        // Ein Eintrag "No References found..." für erscheinen.
        await Context.OnDefinitionFoundAsync(taskrefDefinition).ConfigureAwait(false);

        await FindReferencesAsync(taskDeclaration,
                                  taskDeclaration.CodeGenerationUnit,
                                  taskrefDefinition,
                                  initConnectionPointDefinition,
                                  exitConnectionPointDefinitions,
                                  Context).ConfigureAwait(false);

    }

    #endregion

    public override async Task VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinition) {

        var nodeDefinition                 = DefinitionItem.CreateTaskDefinitionItem(taskDefinition);
        var initConnectionPointDefinition  = DefinitionItem.CreateInitConnectionPointDefinition(taskDefinition, false);
        var exitConnectionPointDefinitions = DefinitionItem.CreateExitConnectionPointDefinitions(taskDefinition, false);

        // Auch wenn wir keine Referenzen auf den Task finden sollten, soll zumindest
        // Ein Eintrag "No References found..." erscheinen.
        await Context.OnDefinitionFoundAsync(nodeDefinition).ConfigureAwait(false);

        await _args.Solution.ProcessCodeGenerationUnitsAsync(
            codeGenerationUnit => FindReferencesAsync(taskDefinition.AsTaskDeclaration,
                                                      codeGenerationUnit,
                                                      nodeDefinition,
                                                      initConnectionPointDefinition,
                                                      exitConnectionPointDefinitions,
                                                      Context),
            _args.OriginatingCodeGenerationUnit, Context.CancellationToken).ConfigureAwait(false);

    }

    // TODO find taskref "Pfad zum file"?

    // WICHTIG: Diese Methode muss Thread safe sein!
    static async Task FindReferencesAsync(ITaskDeclarationSymbol? taskDeclaration,
                                          CodeGenerationUnit? codeGenerationUnit,
                                          DefinitionItem taskDefinitionItem,
                                          DefinitionItem? initConnectionPointDefinitionItem,
                                          ImmutableDictionary<Location, DefinitionItem> exitConnectionPointDefinitionsItems,
                                          IFindReferencesContext context) {

        // null taskDeclaration: TaskDefinition ohne zugehörige Declaration (fehlerhafter/uneindeutiger
        // Quelltext); null codeGenerationUnit: importierte (included) TaskDeclaration ohne eigenen
        // Syntaxbaum. In beiden Fällen gibt es keine dateilokalen Referenzen aufzusammeln.
        if (taskDeclaration == null || codeGenerationUnit == null) {
            return;
        }

        var taskNodeReferences = FindTaskNodeReferences(taskDeclaration, codeGenerationUnit, taskDefinitionItem);
        var initNodeReferences = FindInitNodeReferences(taskDeclaration, codeGenerationUnit, initConnectionPointDefinitionItem);
        var exitNodeReferences = FindExitNodeReferences(taskDeclaration, codeGenerationUnit, exitConnectionPointDefinitionsItems);

        var referenceItems = taskNodeReferences.Concat(initNodeReferences).Concat(exitNodeReferences).OrderByLocation();

        foreach (var referenceItem in referenceItems) {

            if (context.CancellationToken.IsCancellationRequested) {
                break;
            }

            await context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);
        }

        // Taskrefs aufsammeln wäre schön, ist aber komplett unvollständig, und praktisch unmöglich
        //foreach (var taskDeclaration in codeGeneration.TaskDeclarations
        //                                              .Where(td => td.Origin == TaskDeclarationOrigin.TaskDeclaration)) {

        //    if (taskDeclaration.Name          == TaskDefinition.Name &&
        //        taskDeclaration.CodeNamespace == TaskDefinition.CodeNamespace) {
        //        yield return taskDeclaration;
        //    }

        //}

    }

    static IEnumerable<ReferenceItem> FindTaskNodeReferences(ITaskDeclarationSymbol taskDeclaration,
                                                             CodeGenerationUnit codeGenerationUnit,
                                                             DefinitionItem taskDefinitionItem) {

        foreach (var task in codeGenerationUnit.TaskDefinitions.OrderByLocation()) {

            foreach (var taskNode in task.NodeDeclarations
                                         .OfType<ITaskNodeSymbol>()
                                         .Where(taskNode => taskNode.Declaration?.Location == taskDeclaration.Location)
                                         .OrderByLocation()) {

                var referenceItem = ReferenceItemBuilder.Invoke(taskDefinitionItem, taskNode);
                yield return referenceItem;
            }
        }
    }

    static IEnumerable<ReferenceItem> FindInitNodeReferences(ITaskDeclarationSymbol taskDeclaration,
                                                             CodeGenerationUnit codeGenerationUnit,
                                                             DefinitionItem? initConnectionPointDefinitionItem) {

        if (initConnectionPointDefinitionItem == null) {
            yield break;
        }

        // Init Calls aufsammeln 
        foreach (var task in codeGenerationUnit.TaskDefinitions.OrderByLocation()) {

            foreach (var taskNode in task.NodeDeclarations
                                         .OfType<ITaskNodeSymbol>()
                                         .Where(taskNode => taskNode.Declaration?.Location == taskDeclaration.Location)
                                         .OrderByLocation()) {

                foreach (var targetReference in taskNode.Incomings
                                                        .Select(edge => edge.TargetReference)
                                                        .WhereNotNull()
                                                        .OrderByLocation()) {

                    var initReference = ReferenceItemBuilder.Invoke(initConnectionPointDefinitionItem, targetReference);

                    yield return initReference;
                }
            }
        }
    }

    static IEnumerable<ReferenceItem> FindExitNodeReferences(ITaskDeclarationSymbol taskDeclaration,
                                                             CodeGenerationUnit codeGenerationUnit,
                                                             ImmutableDictionary<Location, DefinitionItem> exitConnectionPointDefinitionsItems) {
           
        if (!exitConnectionPointDefinitionsItems.Any()) {
            yield break;
        }

        // Exits in Exit Transitions aufsammeln
        foreach (var task in codeGenerationUnit.TaskDefinitions.OrderByLocation()) {

            foreach (var taskNode in task.NodeDeclarations
                                         .OfType<ITaskNodeSymbol>()
                                         .Where(taskNode => taskNode.Declaration?.Location == taskDeclaration.Location)
                                         .OrderByLocation()) {

                foreach (var exitConnectionPointReference in taskNode.Outgoings
                                                                     .Select(edge => edge.ExitConnectionPointReference)
                                                                     .WhereNotNull()
                                                                     .OrderByLocation()) {

                    var exitConnectionPoint = exitConnectionPointReference.Declaration;
                    if (exitConnectionPoint == null) {
                        continue;
                    }

                    if (exitConnectionPointDefinitionsItems.TryGetValue(exitConnectionPoint.Location, out var exitConnectionPointDefinition)) {

                        // TODO Hier wird je das falsche Symbol ge "highlightet"
                        var exitReference = ReferenceItemBuilder.Invoke(exitConnectionPointDefinition, exitConnectionPointReference);

                        yield return exitReference;
                    }

                }

            }
        }
    }

    #region Nodes

    public override Task VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return VisitInitNodeSymbol(initNodeAliasSymbol.InitNode);
    }

    public override async Task VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {

        var initReferences = FindReferences().WhereNotNull()
                                             .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            initNodeSymbol,
            initNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in initReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {
            foreach (var transition in initNodeSymbol.Outgoings) {
                yield return transition.SourceReference;
            }
        }
    }

    public override async Task VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {

        var exitReferences = FindReferences().WhereNotNull()
                                             .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            exitNodeSymbol,
            exitNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in exitReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {
            foreach (var edge in exitNodeSymbol.Incomings) {
                yield return edge.TargetReference;
            }
        }
    }

    public override async Task VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {

        var endReferences = FindReferences().WhereNotNull()
                                            .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            endNodeSymbol,
            endNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in endReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {
            foreach (var edge in endNodeSymbol.Incomings) {
                yield return edge.TargetReference;
            }
        }
    }

    public override Task VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAliasSymbol) {
        return Visit(taskNodeAliasSymbol.TaskNode);
    }

    public override async Task VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {

        var taskReferences = FindReferences().WhereNotNull()
                                             .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            taskNodeSymbol,
            taskNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in taskReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {

            foreach (var exitTransition in taskNodeSymbol.Outgoings) {
                yield return exitTransition.SourceReference;
            }

            foreach (var edge in taskNodeSymbol.Incomings) {
                yield return edge.TargetReference;
            }
        }
    }

    public override async Task VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {

        var dialogReferences = FindReferences().WhereNotNull()
                                               .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            dialogNodeSymbol,
            dialogNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in dialogReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {

            foreach (var transition in dialogNodeSymbol.Outgoings) {
                yield return transition.SourceReference;
            }

            foreach (var edge in dialogNodeSymbol.Incomings) {
                yield return edge.TargetReference;
            }
        }
    }

    public override async Task VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {

        var viewReferences = FindReferences().WhereNotNull()
                                             .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            viewNodeSymbol,
            viewNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in viewReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {

            foreach (var transition in viewNodeSymbol.Outgoings) {
                yield return transition.SourceReference;
            }

            foreach (var edge in viewNodeSymbol.Incomings) {
                yield return edge.TargetReference;
            }
        }
    }

    public override async Task VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {

        var viewReferences = FindReferences().WhereNotNull()
                                             .OrderByLocation();

        var definitionItem = DefinitionItem.Create(
            choiceNodeSymbol,
            choiceNodeSymbol.ToDisplayParts());

        await Context.OnDefinitionFoundAsync(definitionItem).ConfigureAwait(false);

        foreach (var reference in viewReferences) {

            var referenceItem = ReferenceItemBuilder.Invoke(definitionItem, reference);
            await Context.OnReferenceFoundAsync(referenceItem).ConfigureAwait(false);

        }

        IEnumerable<ISymbol?> FindReferences() {

            foreach (var transition in choiceNodeSymbol.Outgoings) {
                yield return transition.SourceReference;
            }

            foreach (var edge in choiceNodeSymbol.Incomings) {
                yield return edge.TargetReference;
            }
        }
    }

    #endregion

    #region Node References

    public override Task VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {

        if (nodeReferenceSymbol.Declaration != null) {
            return Visit(nodeReferenceSymbol.Declaration);
        }

        return DefaultVisit(nodeReferenceSymbol);
    }

    public override Task VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {
        if (exitConnectionPointReferenceSymbol.Declaration != null) {
            return Visit(exitConnectionPointReferenceSymbol.Declaration);
        }

        return DefaultVisit(exitConnectionPointReferenceSymbol);
    }

    #endregion

}