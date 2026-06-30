#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// VS-freier Vervollständigungs-Service auf Engine-Ebene — Grundlage für LSP <c>textDocument/completion</c>
/// und (über dieselbe Logik) die VS-Quellen. Die grammatische Situation an der Cursor-Position wird über den
/// recovery-festen Syntaxbaum bestimmt (<see cref="NavCompletionContext"/>) statt über einen zeilenbegrenzten
/// Text-Rückwärtsscan; je nach Situation werden nur die dort tatsächlich sinnvollen Kategorien angeboten:
/// auf Member-Ebene <c>task</c>/<c>taskref</c>; hinter <c>task</c> die deklarierten Tasks; am Satzanfang im
/// Body die Knoten-Deklarations-Keywords samt vorhandenen Knoten; hinter einem Quellknoten die Edge-Keywords;
/// hinter einer Edge die Zielknoten (plus <c>end</c>); hinter <c>knoten:</c> die Exit-Connection-Points; hinter
/// einem Ziel die Folge-Klauseln <c>on</c>/<c>if</c>/<c>do</c>. Keine Vorschläge in Kommentaren, Zeichenketten
/// (<c>"…"</c>) oder Code-Blöcken (<c>[ … ]</c>); innerhalb von <c>taskref "…"</c> die Pfad-Vervollständigung.
/// </summary>
public static class NavCompletionService {

    /// <summary>
    /// Liefert die Vervollständigungs-Vorschläge zur angegebenen Zeichen-Position (0-basierter Offset)
    /// in der Reihenfolge, in der sie dem Nutzer angeboten werden sollen — oder eine leere Liste, wenn
    /// an der Position nichts vorgeschlagen werden soll.
    /// </summary>
    [NotNull]
    public static IReadOnlyList<NavCompletionItem> GetCompletions([NotNull] CodeGenerationUnit unit, int position, [CanBeNull] NavSolution solution = null) {

        var source = unit.Syntax.SyntaxTree.SourceText;

        // Pfad-Vervollständigung läuft INNERHALB von "…" nach `taskref` — die normale Unterdrückung in
        // Zeichenketten greift hier also bewusst nicht. Liefert null, wenn kein taskref-String-Kontext.
        var pathItems = GetPathCompletions(source, position, solution);
        if (pathItems != null) {
            return pathItems;
        }

        var context = NavCompletionContext.Classify(unit, position);

        switch (context.Kind) {

            case NavCompletionContextKind.Suppress:
                return Array.Empty<NavCompletionItem>();

            case NavCompletionContextKind.MemberLevel:
                return KeywordItems(SyntaxFacts.TaskKeyword, SyntaxFacts.TaskrefKeyword);

            case NavCompletionContextKind.TaskNodeName:
                return TaskDeclarationItems(unit);

            case NavCompletionContextKind.ExitConnectionPoint:
                return ExitConnectionPointItems(context);

            case NavCompletionContextKind.EdgeSlot:
                return VisibleEdgeKeywordItems();

            case NavCompletionContextKind.TargetSlot:
                return TargetItems(context);

            case NavCompletionContextKind.StatementStart:
                return StatementStartItems(context);

            case NavCompletionContextKind.AfterTarget:
                return KeywordItems(SyntaxFacts.OnKeyword, SyntaxFacts.IfKeyword, SyntaxFacts.DoKeyword);

            case NavCompletionContextKind.AfterTrigger:
                return KeywordItems(SyntaxFacts.IfKeyword, SyntaxFacts.DoKeyword);

            case NavCompletionContextKind.AfterCondition:
                return KeywordItems(SyntaxFacts.DoKeyword);

            default:
                return FallbackItems(context);
        }
    }

    #region Kategorien

    // Knoten-Deklarations-Keywords (beginnen eine Knoten-Deklaration und taugen zugleich als Transitions-Quelle).
    static readonly string[] NodeDeclarationKeywords = {
        SyntaxFacts.InitKeyword,
        SyntaxFacts.InitKeywordAlt,
        SyntaxFacts.EndKeyword,
        SyntaxFacts.ExitKeyword,
        SyntaxFacts.ChoiceKeyword,
        SyntaxFacts.DialogKeyword,
        SyntaxFacts.ViewKeyword,
        SyntaxFacts.TaskKeyword
    };

    static List<NavCompletionItem> TaskDeclarationItems(CodeGenerationUnit unit) {
        var items = new List<NavCompletionItem>();
        foreach (var decl in unit.TaskDeclarations) {
            items.Add(FromSymbol(decl));
        }

        return items;
    }

    static List<NavCompletionItem> ExitConnectionPointItems(NavCompletionContext context) {

        var items = new List<NavCompletionItem>();

        if (string.IsNullOrEmpty(context.ExitNodeName)) {
            return items;
        }

        if (context.Task.TryFindNode(context.ExitNodeName) is ITaskNodeSymbol { Declaration: not null } exitNode) {
            // Erst die noch nicht verbundenen Exits, dann die bereits verbundenen.
            foreach (var cp in exitNode.GetUnconnectedExits()) {
                items.Add(FromSymbol(cp));
            }

            foreach (var cp in exitNode.GetConnectedExits()) {
                items.Add(FromSymbol(cp));
            }
        }

        return items;
    }

    // Hinter einer Edge: die Knoten (als Ziel) — unreferenzierte zuerst — plus das Ziel-Keyword `end`.
    static List<NavCompletionItem> TargetItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task);
        items.Add(new NavCompletionItem(SyntaxFacts.EndKeyword, NavCompletionItemKind.Keyword));
        return items;
    }

    // Satzanfang im Body: die vorhandenen Knoten (als Quelle) plus die Knoten-Deklarations-Keywords.
    static List<NavCompletionItem> StatementStartItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task);

        foreach (var keyword in NodeDeclarationKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        return items;
    }

    // Konservatives Alt-Verhalten für nicht eindeutig klassifizierbare Stellen: vorhandene Knoten +
    // sichtbare Nav-Keywords (ohne Edge-Keywords) + sichtbare Edge-Keywords. So wird nie weniger angeboten.
    static List<NavCompletionItem> FallbackItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task);

        foreach (var keyword in SyntaxFacts.NavKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k) && !SyntaxFacts.IsEdgeKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        items.AddRange(VisibleEdgeKeywordItems());
        return items;
    }

    static List<NavCompletionItem> VisibleEdgeKeywordItems() {
        var items = new List<NavCompletionItem>();
        foreach (var keyword in SyntaxFacts.EdgeKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        return items;
    }

    static IReadOnlyList<NavCompletionItem> KeywordItems(params string[] keywords) {
        return keywords.OrderBy(k => k, StringComparer.Ordinal)
                       .Select(k => new NavCompletionItem(k, NavCompletionItemKind.Keyword))
                       .ToList();
    }

    // Erst alle Knoten ohne Referenzen, dann die übrigen — je alphabetisch.
    static void AddNodeReferences(List<NavCompletionItem> items, ITaskDefinitionSymbol task) {

        if (task == null) {
            return;
        }

        foreach (var node in task.NodeDeclarations
                                 .Where(n => n.References.Count == 0)
                                 .OrderBy(n => n.Name, StringComparer.Ordinal)) {
            items.Add(FromSymbol(node));
        }

        foreach (var node in task.NodeDeclarations
                                 .Where(n => n.References.Count != 0)
                                 .OrderBy(n => n.Name, StringComparer.Ordinal)) {
            items.Add(FromSymbol(node));
        }
    }

    #endregion

    static NavCompletionItem FromSymbol(ISymbol symbol) {
        return new NavCompletionItem(symbol.Name, KindOf(symbol), symbol: symbol);
    }

    static NavCompletionItemKind KindOf(ISymbol symbol) => symbol switch {
        IChoiceNodeSymbol                                     => NavCompletionItemKind.Choice,
        IGuiNodeSymbol                                        => NavCompletionItemKind.GuiNode,
        ITaskNodeSymbol or ITaskDeclarationSymbol             => NavCompletionItemKind.Task,
        IInitNodeSymbol or IExitNodeSymbol or IEndNodeSymbol  => NavCompletionItemKind.ConnectionPoint,
        IConnectionPointSymbol                                => NavCompletionItemKind.ConnectionPoint,
        _                                                     => NavCompletionItemKind.Node
    };

    #region Pfad-Vervollständigung (Dateiname-basiert über die Solution)

    /// <summary>
    /// Vervollständigt Dateipfade innerhalb von <c>taskref "…"</c>. Liefert <c>null</c>, wenn die Position
    /// NICHT in einem solchen String-Kontext liegt (dann übernimmt die Nav-/Edge-Vervollständigung), sonst
    /// die (ggf. leere) Liste aller von der Solution bekannten Nav-Files. Es wird NICHT das Dateisystem
    /// durchsucht — die Solution kennt bereits alle <c>*.nav</c> unterhalb des Workspace-Roots. Gefiltert
    /// wird clientseitig über den <em>Dateinamen</em> (so findet „Messageb" auch ein tief verschachteltes
    /// „MessageBoxes.nav"); eingefügt wird der zum aktuellen Nav-File <em>relative</em> Pfad.
    /// </summary>
    static IReadOnlyList<NavCompletionItem> GetPathCompletions(SourceText source, int position, NavSolution solution) {

        if (position < 0 || position > source.Length) {
            return null;
        }

        var line         = source.GetTextLineAtPosition(position);
        var lineText     = source.Substring(line.ExtentWithoutLineEndings);
        var linePosition = position - line.Start;

        if (!lineText.IsInQuotation(linePosition)) {
            return null;
        }

        var quotedExtent = lineText.QuotedExtent(linePosition);
        if (quotedExtent.IsMissing) {
            return null;
        }

        var previousIdentifier = quotedExtent.Start > 0 ? lineText.GetPreviousIdentifier(quotedExtent.Start - 1) : string.Empty;
        if (previousIdentifier != SyntaxFacts.TaskrefKeyword) {
            return null;
        }

        var items        = new List<NavCompletionItem>();
        var navDirectory = source.FileInfo?.Directory;

        if (solution != null && navDirectory != null) {

            // Der Ersetzungsbereich ist der gesamte Inhalt zwischen den Anführungszeichen (absolute Offsets).
            var replacement   = TextExtent.FromBounds(line.Start + quotedExtent.Start, line.Start + quotedExtent.End);
            var directoryName = navDirectory.FullName + Path.DirectorySeparatorChar;
            var currentFile   = source.FileInfo?.FullName;

            foreach (var file in solution.SolutionFiles
                                         .Where(f => !string.Equals(f.FullName, currentFile, StringComparison.OrdinalIgnoreCase))
                                         .OrderBy(f => f.Name,     StringComparer.OrdinalIgnoreCase)
                                         .ThenBy(f  => f.FullName, StringComparer.OrdinalIgnoreCase)) {

                var relativePath = PathHelper.GetRelativePath(fromPath: directoryName, toPath: file.FullName);

                items.Add(new NavCompletionItem(
                              label: file.Name,
                              kind: NavCompletionItemKind.File,
                              insertText: relativePath,
                              replacementExtent: replacement,
                              detail: relativePath));
            }
        }

        return items;
    }

    #endregion

}
