#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;
using Pharmatechnik.Nav.Language.CodeFixes.Refactoring;
using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeActions;

/// <summary>
/// VS-freier Service, der zu einem Bereich (Selektion oder reiner Caret) die anwendbaren Code-Aktionen
/// liefert — Grundlage für LSP <c>textDocument/codeAction</c>. Bündelt exakt die CodeFix-Provider, die
/// in der VS-Extension als SuggestedActions exportiert sind (ErrorFix, StyleFix, Refactoring) und nutzt
/// damit dieselbe Engine-Refactoring-Infrastruktur wie die VS-Lightbulb — „eine Engine".
/// </summary>
/// <remarks>
/// Der parametrische <c>RenameCodeFix</c> ist hier bewusst NICHT enthalten: das Umbenennen läuft über
/// den eigenen LSP-Pfad <c>textDocument/rename</c> (mit Eingabefeld + Validierung). Alle gelieferten
/// <see cref="TextChange"/> sind dateilokal (beziehen sich auf <paramref name="unit"/>).
/// </remarks>
public static class NavCodeActionService {

    /// <summary>
    /// Liefert die anwendbaren Code-Aktionen für <paramref name="range"/> (0-basierte Offsets). Ein leerer
    /// Bereich (Start==End) wird auf den Token-Extent an dieser Position ausgedehnt — siehe <see cref="ExpandCaret"/>.
    /// </summary>
    public static IReadOnlyList<NavCodeAction> GetCodeActions(CodeGenerationUnit unit,
                                                              TextExtent range,
                                                              TextEditorSettings settings,
                                                              CancellationToken cancellationToken = default) {

        range = ExpandCaret(unit, range);

        var context = new CodeFixContext(range, unit, settings);
        var actions = new List<NavCodeAction>();

        // ErrorFix — fehlende Exit-Transition ergänzen.
        foreach (var fix in AddMissingExitTransitionCodeFixProvider.SuggestCodeFixes(context, cancellationToken)) {
            actions.Add(new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges().ToList()));
        }

        // StyleFix — Aufräum-Aktionen.
        foreach (var fix in RemoveUnusedNodesCodeFixProvider.SuggestCodeFixes(context, cancellationToken)) {
            actions.Add(new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges().ToList()));
        }

        foreach (var fix in RemoveUnusedTaskDeclarationCodeFixProvider.SuggestCodeFixes(context, cancellationToken)) {
            actions.Add(new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges().ToList()));
        }

        foreach (var fix in RemoveUnusedIncludeDirectiveCodeFixProvider.SuggestCodeFixes(context, cancellationToken)) {
            actions.Add(new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges().ToList()));
        }

        foreach (var fix in AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider.SuggestCodeFixes(context, cancellationToken)) {
            actions.Add(new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges().ToList()));
        }

        // Refactoring — Choice einführen. Ohne Eingabedialog (LSP) wird der von der Engine vorgeschlagene,
        // garantiert gültige Name verwendet; der Nutzer kann anschließend per Rename umbenennen.
        foreach (var fix in IntroduceChoiceCodeFixProvider.SuggestCodeFixes(context, cancellationToken)) {
            actions.Add(new NavCodeAction(fix.Name, fix.Category, fix.GetTextChanges(fix.SuggestChoiceName()).ToList()));
        }

        return actions;
    }

    /// <summary>
    /// Ein leerer Bereich (reiner Caret, Start==End) findet über den nicht-überlappenden Symbol-/Token-Indexer
    /// der CodeFix-Provider (<c>includeOverlapping=false</c>) nichts — dort müssen Elemente vollständig im
    /// Bereich liegen, was bei Länge 0 unmöglich ist. Daher den Caret auf den Extent des Tokens an dieser
    /// Position ausdehnen, sodass die Provider wie bei einer Selektion auf dem Bezeichner greifen.
    /// </summary>
    /// <remarks>
    /// <see cref="SyntaxNode.FindToken"/> hat Roslyn-Owning-Semantik: Steht der Caret in Trivia
    /// (Einrückung/Leerzeile/Kommentar), liefert es das signifikante Token, an dem die Trivia hängt — der
    /// Bereich dehnt sich also auf das umgebende Konstrukt aus und die Provider greifen wie bei der
    /// VS-Lightbulb auch aus dem Zeilen-Whitespace heraus. <see cref="SyntaxToken.IsMissing"/> tritt nur noch
    /// am Rand auf (Position außerhalb des Texts bzw. ohne tragendes Token) und lässt den Bereich dann
    /// unverändert nullbreit.
    /// </remarks>
    static TextExtent ExpandCaret(CodeGenerationUnit unit, TextExtent range) {
        if (range.Start != range.End) {
            return range;
        }

        var token = unit.Syntax.FindToken(range.Start);
        return token.IsMissing ? range : token.Extent;
    }

}
