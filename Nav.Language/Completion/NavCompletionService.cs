#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// VS-freier Vervollständigungs-Service auf Engine-Ebene — Grundlage für LSP <c>textDocument/completion</c>.
/// Vereint die Logik der VS-Quellen <c>NavCompletionSource</c> und <c>EdgeCompletionSource</c> (die VS als
/// getrennte Quellen mischt): nach <c>task</c> die deklarierten Tasks, nach <c>knoten:</c> die
/// Exit-Connection-Points, innerhalb einer Task-Definition die Knoten (unreferenzierte zuerst), die
/// Nav-Keywords (ohne versteckte/Edge-Keywords) sowie — wenn vor der (angefangenen) Edge ein Whitespace
/// bzw. Zeilenanfang steht — die Edge-Keywords. Keine Vorschläge in Kommentaren, in Zeichenketten
/// (<c>"…"</c>) oder in Code-Blöcken (<c>[ … ]</c>).
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

        if (!ShouldProvideCompletions(unit, source, position)) {
            return Array.Empty<NavCompletionItem>();
        }

        var line      = source.GetTextLineAtPosition(position);
        var lineStart = line.Start;

        var items = GetSymbolAndKeywordCompletions(unit, source, lineStart, position);

        // Edge-Keywords (VS-Quelle EdgeCompletionSource): nur anbieten, wenn vor der (evtl. bereits
        // angefangenen) Edge ein Whitespace oder der Zeilenanfang steht — sonst keine Edge-Mitten.
        if (IsEdgeContext(source, lineStart, position)) {

            foreach (var keyword in SyntaxFacts.EdgeKeywords
                                               .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                               .OrderBy(k => k, StringComparer.Ordinal)) {
                items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
            }
        }

        return items;
    }

    static List<NavCompletionItem> GetSymbolAndKeywordCompletions(CodeGenerationUnit unit, SourceText source, int lineStart, int position) {

        var startOfIdentifier     = GetStartOfIdentifier(source, lineStart, position);
        var previousNonWhitespace = PreviousNonWhitespaceChar(source, lineStart, startOfIdentifier);
        var previousIdentifier    = GetPreviousIdentifier(source, lineStart, startOfIdentifier);

        var items = new List<NavCompletionItem>();

        // Task-Knoten: nach dem Schlüsselwort `task` die deklarierten Tasks anbieten.
        if (previousIdentifier == SyntaxFacts.TaskKeyword) {

            foreach (var decl in unit.TaskDeclarations) {
                items.Add(FromSymbol(decl));
            }

            if (items.Count > 0) {
                return items;
            }
        }

        var caret          = TextExtent.FromBounds(position, position);
        var taskDefinition = unit.TaskDefinitions.FirstOrDefault(td => td.Syntax.Extent.IntersectsWith(caret))
                          ?? unit.TaskDefinitions.LastOrDefault(td => caret.Start > td.Syntax.Start);

        if (taskDefinition != null) {

            // Exit-Connection-Points: nach `knoten:` die Exits des referenzierten Task-Knotens.
            if (previousNonWhitespace == SyntaxFacts.Colon) {

                var exitNodeEnd = startOfIdentifier - 1;
                var nodeName    = GetPreviousIdentifier(source, lineStart, exitNodeEnd);

                if (!string.IsNullOrEmpty(nodeName)) {

                    var exitNode = taskDefinition.TryFindNode(nodeName) as ITaskNodeSymbol;

                    if (exitNode?.Declaration != null) {
                        // Erst die noch nicht verbundenen Exits, dann die bereits verbundenen.
                        foreach (var cp in exitNode.GetUnconnectedExits()) {
                            items.Add(FromSymbol(cp));
                        }

                        foreach (var cp in exitNode.GetConnectedExits()) {
                            items.Add(FromSymbol(cp));
                        }
                    }

                    if (items.Count > 0) {
                        return items;
                    }
                }
            }

            // Erst alle Knoten ohne Referenzen, dann die übrigen — je alphabetisch.
            foreach (var node in taskDefinition.NodeDeclarations
                                               .Where(n => n.References.Count == 0)
                                               .OrderBy(n => n.Name, StringComparer.Ordinal)) {
                items.Add(FromSymbol(node));
            }

            foreach (var node in taskDefinition.NodeDeclarations
                                               .Where(n => n.References.Count != 0)
                                               .OrderBy(n => n.Name, StringComparer.Ordinal)) {
                items.Add(FromSymbol(node));
            }
        }

        // Nav-Keywords (ohne versteckte und ohne Edge-Keywords — letztere kommen aus dem Edge-Zweig).
        foreach (var keyword in SyntaxFacts.NavKeywords
                                           .Where(k => !SyntaxFacts.IsHiddenKeyword(k) && !SyntaxFacts.IsEdgeKeyword(k))
                                           .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        return items;
    }

    static bool ShouldProvideCompletions(CodeGenerationUnit unit, SourceText source, int position) {

        if (position < 0 || position > source.Length) {
            return false;
        }

        // Keine Vervollständigung in Kommentaren — aus der angehängten Trivia (Roslyn-Modell), nicht über
        // ein FindToken auf den flachen Strom.
        if (unit.Syntax.SyntaxTree.IsPositionInComment(position)) {
            return false;
        }

        var line         = source.GetTextLineAtPosition(position);
        var lineText      = source.Substring(line.ExtentWithoutLineEndings);
        var linePosition = position - line.Start;

        // Keine Vervollständigung in Zeichenketten ("…").
        if (lineText.IsInQuotation(linePosition)) {
            return false;
        }

        // Keine Vervollständigung in Code-Blöcken ([ … ]); betrachtet (wie in VS) nur die aktuelle Zeile.
        if (lineText.IsInTextBlock(linePosition, SyntaxFacts.OpenBracket, SyntaxFacts.CloseBracket)) {
            return false;
        }

        return true;
    }

    static NavCompletionItem FromSymbol(ISymbol symbol) {
        return new NavCompletionItem(symbol.Name, KindOf(symbol));
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

    #region Zeilen-Helfer (faithful port der VS-TextSnaphotLineExtensions, offset-basiert)

    // Startindex des Identifiers, der bei position endet — zeilenbegrenzt rückwärts laufend.
    static int GetStartOfIdentifier(SourceText source, int lineStart, int position) {
        while (position > lineStart && SyntaxFacts.IsIdentifierCharacter(source[position - 1])) {
            position -= 1;
        }

        return position;
    }

    // Index des vorigen Nicht-Whitespace-Zeichens (oder null), zeilenbegrenzt.
    static int? PreviousNonWhitespace(SourceText source, int lineStart, int position) {

        if (position <= lineStart) {
            return null;
        }

        do {
            position -= 1;
        } while (position > lineStart && char.IsWhiteSpace(source[position]));

        return position;
    }

    static char? PreviousNonWhitespaceChar(SourceText source, int lineStart, int position) {
        var index = PreviousNonWhitespace(source, lineStart, position);
        return index.HasValue ? source[index.Value] : null;
    }

    // Der Identifier-Text, der vor position liegt (oder ""), zeilenbegrenzt.
    static string GetPreviousIdentifier(SourceText source, int lineStart, int position) {

        var wordEnd = PreviousNonWhitespace(source, lineStart, position);
        if (wordEnd == null) {
            return string.Empty;
        }

        var wordStart = GetStartOfIdentifier(source, lineStart, wordEnd.Value);
        if (wordEnd.Value + 1 <= wordStart) {
            return string.Empty;
        }

        return source.Substring(TextExtent.FromBounds(wordStart, wordEnd.Value + 1));
    }

    // Startindex der (evtl. angefangenen) Edge, die bei position endet — zeilenbegrenzt über Edge-Zeichen.
    static int GetStartOfEdge(SourceText source, int lineStart, int position) {
        while (position > lineStart && IsEdgeChar(source[position - 1])) {
            position -= 1;
        }

        return position;
    }

    // Vor einer Edge muss Whitespace oder der Zeilenanfang stehen (sonst ist es keine Edge-Position).
    static bool IsEdgeContext(SourceText source, int lineStart, int position) {
        var start = GetStartOfEdge(source, lineStart, position);
        return start == lineStart || char.IsWhiteSpace(source[start - 1]);
    }

    static readonly ImmutableHashSet<char> EdgeChars = SyntaxFacts.EdgeKeywords.SelectMany(k => k).ToImmutableHashSet();

    static bool IsEdgeChar(char c) => EdgeChars.Contains(c);

    #endregion

}
