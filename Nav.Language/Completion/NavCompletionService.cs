#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// VS-freier Vervollständigungs-Service auf Engine-Ebene — Grundlage für LSP <c>textDocument/completion</c>.
/// Portiert die Logik der VS-<c>NavCompletionSource</c>: nach <c>task</c> die deklarierten Tasks, nach
/// <c>knoten:</c> die Exit-Connection-Points, innerhalb einer Task-Definition die Knoten (unreferenzierte
/// zuerst) und schließlich die Nav-Keywords (ohne versteckte/Edge-Keywords). Keine Vorschläge in
/// Kommentaren, in Zeichenketten (<c>"…"</c>) oder in Code-Blöcken (<c>[ … ]</c>).
/// </summary>
public static class NavCompletionService {

    /// <summary>
    /// Liefert die Vervollständigungs-Vorschläge zur angegebenen Zeichen-Position (0-basierter Offset)
    /// in der Reihenfolge, in der sie dem Nutzer angeboten werden sollen — oder eine leere Liste, wenn
    /// an der Position nichts vorgeschlagen werden soll.
    /// </summary>
    [NotNull]
    public static IReadOnlyList<NavCompletionItem> GetCompletions([NotNull] CodeGenerationUnit unit, int position) {

        var source = unit.Syntax.SyntaxTree.SourceText;

        if (!ShouldProvideCompletions(unit, source, position)) {
            return Array.Empty<NavCompletionItem>();
        }

        var line      = source.GetTextLineAtPosition(position);
        var lineStart = line.Start;

        var startOfIdentifier      = GetStartOfIdentifier(source, lineStart, position);
        var previousNonWhitespace  = PreviousNonWhitespaceChar(source, lineStart, startOfIdentifier);
        var previousIdentifier     = GetPreviousIdentifier(source, lineStart, startOfIdentifier);

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

        // Nav-Keywords (ohne versteckte und ohne Edge-Keywords).
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

        var triggerToken = unit.Syntax.FindToken(position);

        // Keine Vervollständigung in Kommentaren.
        if (triggerToken.Type is SyntaxTokenType.SingleLineComment or SyntaxTokenType.MultiLineComment) {
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
        IChoiceNodeSymbol                                          => NavCompletionItemKind.Choice,
        IGuiNodeSymbol                                             => NavCompletionItemKind.GuiNode,
        ITaskNodeSymbol or ITaskDeclarationSymbol                  => NavCompletionItemKind.Task,
        IInitNodeSymbol or IExitNodeSymbol or IEndNodeSymbol       => NavCompletionItemKind.ConnectionPoint,
        IConnectionPointSymbol                                     => NavCompletionItemKind.ConnectionPoint,
        _                                                          => NavCompletionItemKind.Node
    };

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
        return index.HasValue ? source[index.Value] : (char?) null;
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

    #endregion

}
