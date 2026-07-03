#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.References;

/// <summary>
/// Sammelt — ausgehend vom Wurzel-Symbol (siehe <see cref="ReferenceRootFinder"/>) — die Deklaration
/// und alle Referenzen darauf innerhalb des Symbol-Graphen einer <see cref="CodeGenerationUnit"/>.
/// Das erste zurückgelieferte Symbol hat den Charakter der „Definition", alle weiteren sind Referenzen.
/// VS-frei portiert aus <c>Nav.Language.ExtensionShared/HighlightReferences/ReferenceFinder.cs</c>; die
/// VS-Option <c>HighlightReferencesUnderInclude</c> ist als Parameter erhalten geblieben.
/// </summary>
sealed class HighlightSymbolFinder: SymbolVisitor<IEnumerable<ISymbol?>> {

    readonly bool _includeReferencesUnderInclude;

    HighlightSymbolFinder(bool includeReferencesUnderInclude) {
        _includeReferencesUnderInclude = includeReferencesUnderInclude;
    }

    public static IEnumerable<ISymbol?> Find(ISymbol symbol, bool includeReferencesUnderInclude = true) {
        var rootSymbol = ReferenceRootFinder.FindRoot(symbol);
        var finder     = new HighlightSymbolFinder(includeReferencesUnderInclude);
        return finder.Visit(rootSymbol);
    }

    protected override IEnumerable<ISymbol?> DefaultVisit(ISymbol symbol) {
        yield break;
    }

    public override IEnumerable<ISymbol?> VisitExitConnectionPointSymbol(IExitConnectionPointSymbol exitConnectionPointSymbol) {

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

    public override IEnumerable<ISymbol?> VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {

        if (initNodeSymbol.Alias != null) {
            yield return initNodeSymbol.Alias;
        } else {
            yield return initNodeSymbol;
        }

        foreach (var transition in initNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return Visit(initNodeAliasSymbol.InitNode);
    }

    public override IEnumerable<ISymbol?> VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {

        if (taskNodeSymbol.Alias != null) {
            yield return taskNodeSymbol.Alias;
        } else {
            yield return taskNodeSymbol;
        }

        foreach (var exitTransition in taskNodeSymbol.Outgoings) {
            yield return exitTransition.SourceReference;
        }

        foreach (var edge in taskNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {

        yield return exitNodeSymbol;

        foreach (var edge in exitNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {

        yield return endNodeSymbol;

        foreach (var edge in endNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {

        yield return dialogNodeSymbol;

        foreach (var transition in dialogNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in dialogNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {

        yield return viewNodeSymbol;

        foreach (var transition in viewNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in viewNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {

        yield return choiceNodeSymbol;

        foreach (var transition in choiceNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in choiceNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    public override IEnumerable<ISymbol?> VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {

        yield return taskDeclarationSymbol;

        foreach (var taskNode in taskDeclarationSymbol.References) {
            // Wenn der Alias null ist, dann steigen wir direkt runter bis auf Node-Reference-Ebene.
            if (taskNode.Alias == null) {
                foreach (var symbol in VisitTaskNodeSymbol(taskNode)) {
                    yield return symbol;
                }
            } else {
                yield return taskNode;
            }
        }
    }

    public override IEnumerable<ISymbol?> VisitIncludeSymbol(IIncludeSymbol includeSymbol) {

        if (!_includeReferencesUnderInclude) {
            yield break;
        }

        yield return includeSymbol;

        foreach (var taskNode in includeSymbol.TaskDeclarations.SelectMany(td => td.References)) {
            yield return taskNode;
        }
    }

}
