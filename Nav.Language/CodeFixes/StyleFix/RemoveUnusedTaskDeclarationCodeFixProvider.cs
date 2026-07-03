#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

public sealed class RemoveUnusedTaskDeclarationCodeFixProvider {

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