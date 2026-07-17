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

    /// <summary>
    /// Einstiegspunkt: bestimmt zunächst über <see cref="ReferenceRootFinder.FindRoot"/> das Wurzel-Symbol
    /// zu <paramref name="symbol"/> und besucht es, um Deklaration + Referenzen einzusammeln.
    /// </summary>
    /// <param name="symbol">Das Symbol unter dem Caret (Ausgangspunkt der Highlight-Suche).</param>
    /// <param name="includeReferencesUnderInclude">
    /// Ob Referenzen unterhalb eines <c>using</c>/Include auch berücksichtigt werden (VS-Option
    /// <c>HighlightReferencesUnderInclude</c>).
    /// </param>
    /// <returns>Deklaration (erstes Element) gefolgt von allen Referenzen; kann <c>null</c>-Einträge enthalten.</returns>
    public static IEnumerable<ISymbol?> Find(ISymbol symbol, bool includeReferencesUnderInclude = true) {
        var rootSymbol = ReferenceRootFinder.FindRoot(symbol);
        var finder     = new HighlightSymbolFinder(includeReferencesUnderInclude);
        return finder.Visit(rootSymbol);
    }

    /// <summary>Symbol-Arten ohne eigene Highlight-Regel liefern keine Treffer.</summary>
    protected override IEnumerable<ISymbol?> DefaultVisit(ISymbol symbol) {
        yield break;
    }

    /// <summary>
    /// Highlight für einen Exit-Connection-Point: der Punkt selbst (sofern nicht inkludiert) plus alle
    /// Exit-Connection-Point-Referenzen, die über die Task-Deklaration auf ihn zeigen (die
    /// <c>Instanz:exit --&gt; …</c>-Kanten).
    /// </summary>
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

    /// <summary>
    /// Highlight für einen Init-Knoten: der Alias (falls vorhanden, sonst der Knoten selbst) plus die
    /// Quell-Referenzen aller ausgehenden Transitionen.
    /// </summary>
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

    /// <summary>Ein Init-Alias wird auf seinen Init-Knoten zurückgeführt und wie dieser behandelt.</summary>
    public override IEnumerable<ISymbol?> VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        return Visit(initNodeAliasSymbol.InitNode);
    }

    /// <summary>
    /// Highlight für einen Task-Knoten (Task-Instanz): der Alias (falls vorhanden, sonst der Knoten selbst)
    /// plus die Quell-Referenzen der ausgehenden und die Ziel-Referenzen der eingehenden Kanten.
    /// </summary>
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

    /// <summary>Highlight für einen Exit-Knoten: der Knoten selbst plus die Ziel-Referenzen der eingehenden Kanten.</summary>
    public override IEnumerable<ISymbol?> VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {

        yield return exitNodeSymbol;

        foreach (var edge in exitNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    /// <summary>Highlight für einen End-Knoten: der Knoten selbst plus die Ziel-Referenzen der eingehenden Kanten.</summary>
    public override IEnumerable<ISymbol?> VisitEndNodeSymbol(IEndNodeSymbol endNodeSymbol) {

        yield return endNodeSymbol;

        foreach (var edge in endNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    /// <summary>
    /// Highlight für einen Dialog-Knoten: der Knoten selbst plus die Quell-Referenzen der ausgehenden und die
    /// Ziel-Referenzen der eingehenden Kanten.
    /// </summary>
    public override IEnumerable<ISymbol?> VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {

        yield return dialogNodeSymbol;

        foreach (var transition in dialogNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in dialogNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    /// <summary>
    /// Highlight für einen View-Knoten: der Knoten selbst plus die Quell-Referenzen der ausgehenden und die
    /// Ziel-Referenzen der eingehenden Kanten.
    /// </summary>
    public override IEnumerable<ISymbol?> VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {

        yield return viewNodeSymbol;

        foreach (var transition in viewNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in viewNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    /// <summary>
    /// Highlight für einen Choice-Knoten: der Knoten selbst plus die Quell-Referenzen der ausgehenden und die
    /// Ziel-Referenzen der eingehenden Kanten.
    /// </summary>
    public override IEnumerable<ISymbol?> VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {

        yield return choiceNodeSymbol;

        foreach (var transition in choiceNodeSymbol.Outgoings) {
            yield return transition.SourceReference;
        }

        foreach (var edge in choiceNodeSymbol.Incomings) {
            yield return edge.TargetReference;
        }
    }

    /// <summary>
    /// Highlight für eine Task-Deklaration (<c>taskref</c>): die Deklaration selbst plus alle Task-Knoten, die
    /// sie referenzieren. Knoten ohne Alias werden bis auf ihre Referenz-Ebene aufgelöst
    /// (<see cref="VisitTaskNodeSymbol"/>), Knoten mit Alias direkt zurückgegeben.
    /// </summary>
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

    /// <summary>
    /// Highlight für ein Include (<c>using</c>): nur wenn <c>includeReferencesUnderInclude</c> gesetzt ist —
    /// dann das Include-Symbol selbst plus die Task-Knoten, die die inkludierten Task-Deklarationen referenzieren.
    /// </summary>
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
