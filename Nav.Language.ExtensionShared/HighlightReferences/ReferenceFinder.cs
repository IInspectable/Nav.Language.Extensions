#region Using Directives

using System.Collections.Generic;
using System.Linq;
using Pharmatechnik.Nav.Language.Extension.Options;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

// Das erste zurückgeliferte Symbol hat immer den Charakter der "Definition", alle weiteren
// stellen die Referenzen auf diese Definition dar
sealed class ReferenceFinder : SymbolVisitor<IEnumerable<ISymbol>> {

    readonly IAdvancedOptions _advancedOptions;

    ReferenceFinder(IAdvancedOptions advancedOptions) {
        _advancedOptions = advancedOptions;
    }

    public static IEnumerable<ISymbol> FindReferences(ISymbol symbol, IAdvancedOptions advancedOptions) {

        if( !advancedOptions.HighlightReferencesUnderCursor) {
            return Enumerable.Empty<ISymbol>();
        }

        var rootSymbol = ReferenceRootFinder.FindRoot(symbol);
        var finder     = new ReferenceFinder(advancedOptions);
        return finder.Visit(rootSymbol);
    }

    protected override IEnumerable<ISymbol> DefaultVisit(ISymbol symbol) {
        yield break;
    }

    // Auskommentiert, da Navigation nicht stabil.
    // Bsp.: Es wird runter zum ersten Target Knoten navigiert. Ab da steht der Cursor auf den Task Knoten, und es werden alle Knotenreferenzen
    // im Task selektiert, aber nicht mehr der Init der Task Deklaration. Wozu auch...
    //public override IEnumerable<ISymbol> VisitInitConnectionPointSymbol(IInitConnectionPointSymbol initConnectionPointSymbol) {

    //    yield return initConnectionPointSymbol;

    //    foreach (var taskCall in initConnectionPointSymbol.TaskDeclaration
    //                                                    .References
    //                                                    .SelectMany(tn => tn.Incomings)
    //                                                    .Select(edge => edge.TargetReference)) {
    //        yield return taskCall;
    //    }
    //}

    public override IEnumerable<ISymbol> VisitExitConnectionPointSymbol(IExitConnectionPointSymbol exitConnectionPointSymbol) {

        if (!exitConnectionPointSymbol.TaskDeclaration.IsIncluded) {
            yield return exitConnectionPointSymbol;
        }
            
        foreach (var exitConnectionPointReference in exitConnectionPointSymbol.TaskDeclaration
                                                                              .References
                                                                              .SelectMany(tn => tn.Outgoings)
                                                                              .Select(exitTrans => exitTrans.ExitConnectionPointReference)
                                                                              .Where(ep => ep?.Declaration == exitConnectionPointSymbol)) {
            yield return exitConnectionPointReference;
        }
    }

    public override IEnumerable<ISymbol> VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {

        if (initNodeSymbol.Alias != null) {
            yield return initNodeSymbol.Alias;
        }
        else {
            yield return initNodeSymbol;
        }

        foreach (var transition in initNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }
    }

    public override IEnumerable<ISymbol> VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return Visit(initNodeAliasSymbol.InitNode);
    }

    public override IEnumerable<ISymbol> VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
            
        if(taskNodeSymbol.Alias != null) {
            yield return taskNodeSymbol.Alias;
        } else {
            yield return taskNodeSymbol;
        }

        foreach(var exitTransition in taskNodeSymbol.Outgoings) {
            yield return exitTransition.SourceReference;
        }

        foreach (var edge in taskNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol> VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {

        yield return exitNodeSymbol;

        foreach (var edge in exitNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol> VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {

        yield return endNodeSymbol;

        foreach (var edge in endNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol> VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {

        yield return dialogNodeSymbol;

        foreach (var transition in dialogNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in dialogNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol> VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {

        yield return viewNodeSymbol;

        foreach (var transition in viewNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in viewNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol> VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {

        yield return choiceNodeSymbol;

        foreach (var transition in choiceNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in choiceNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol> VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {

        yield return taskDeclarationSymbol;
            
        foreach (var taskNode in taskDeclarationSymbol.References) {
            // Wenn der alias null ist, dann steigen wir direkt runter bis auf Node Reference Ebene
            if (taskNode.Alias == null) {
                foreach (var symbol in VisitTaskNodeSymbol(taskNode)) {
                    yield return symbol;
                }
            } else {
                yield return taskNode;
            }                
        }
    }

    public override IEnumerable<ISymbol> VisitIncludeSymbol(IIncludeSymbol includeSymbol) {

        if (!_advancedOptions.HighlightReferencesUnderInclude) {
            yield break;
        }

        yield return includeSymbol;

        foreach(var taskNode in includeSymbol.TaskDeclarations.SelectMany(td => td.References)) {
            yield return taskNode;
        }
    }
}