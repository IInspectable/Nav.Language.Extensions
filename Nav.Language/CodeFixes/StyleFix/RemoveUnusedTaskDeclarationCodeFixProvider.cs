#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Findet die anwendbaren <see cref="RemoveUnusedTaskDeclarationCodeFix"/>e zu einem
/// <see cref="CodeFixContext"/>: schlägt den Fix nur vor, wenn der Bereich eine
/// <see cref="TaskDeclarationSyntax"/> berührt, und liefert je zugehörigem
/// <see cref="ITaskDeclarationSymbol"/> einen Fix — gefiltert auf jene, die tatsächlich anwendbar sind
/// (<see cref="RemoveUnusedTaskDeclarationCodeFix.CanApplyFix"/>).
/// </summary>
public sealed class RemoveUnusedTaskDeclarationCodeFixProvider {

    /// <summary>
    /// Ermittelt die zum <paramref name="context"/> passenden, anwendbaren Fixes (siehe Typ-Doku).
    /// </summary>
    /// <param name="context">Der Kontext (Bereich, <see cref="CodeGenerationUnit"/>, Editor-Einstellungen).</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    /// <returns>Die anwendbaren Fixes (ggf. leer).</returns>
    public static IEnumerable<RemoveUnusedTaskDeclarationCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {
        // Wir schlagen den Codefix nur vor, wenn sich das Caret in einer Task Declaration befindet
        var taskDeclarationSyntaxes = context.FindNodes<SyntaxNode>()
                                              // Hoch zur zugehörigen TaskDeclarationSyntax
                                             .Select(syntaxNode => syntaxNode?.AncestorsAndSelf().OfType<TaskDeclarationSyntax>().FirstOrDefault())
                                             .Where(taskDeclarationSyntax => taskDeclarationSyntax != null)
                                              // Wenn der Range mehr als eine TaskDeclarationSyntax enthält, dann müssen wir hier die doppelten Syntaxen entfernen
                                             .Distinct();

        // Das zur TaskDeclarationSyntax gehörige Symbol finden
        var taskDeclarationSymbols = taskDeclarationSyntaxes
                                    .Select(taskDeclarationSyntax => context.CodeGenerationUnit.TaskDeclarations.FirstOrDefault(taskDeclarationSymbol => taskDeclarationSymbol.Syntax == taskDeclarationSyntax))
                                    .WhereNotNull();

        var codeFixes = taskDeclarationSymbols
                       .Select(taskDeclarationSymbol => new RemoveUnusedTaskDeclarationCodeFix(taskDeclarationSymbol, context))
                       .Where(codeFix => codeFix.CanApplyFix());

        return codeFixes;
    }

}