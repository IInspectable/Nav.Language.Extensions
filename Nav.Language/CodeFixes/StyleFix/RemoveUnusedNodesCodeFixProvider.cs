#nullable enable

#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

public sealed class RemoveUnusedNodesCodeFixProvider {

    public static IEnumerable<RemoveUnusedNodesCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {
        // Wir schlagen den Codefix nur vor, wenn sich das Caret in einer Node Declaration befindet
        var nodeDeclarationSyntaxes = context.FindNodes<NodeDeclarationSyntax>();

        // Die zugehörigen TaskDefinitionen
        var taskDefinitionSyntaxes = nodeDeclarationSyntaxes
                                    .Select(nodeDeclaration => nodeDeclaration?.Ancestors().OfType<TaskDefinitionSyntax>().FirstOrDefault())
                                    .Where(taskDefinitionSyntax => taskDefinitionSyntax != null)
                                     // Wenn der Range mehr als eine NodeDeclaration enthält, dann müssen wir hier die doppelten Taskdefinitionen entfernen
                                    .Distinct();

        // Die TaskDefinitionSymbols
        var taskDefinitionSymbols = taskDefinitionSyntaxes
                                    // Das zur TaskDefinition gehörige Symbol finden
                                   .Select(taskDefinitionSyntax => context.CodeGenerationUnit.TaskDefinitions.FirstOrDefault(taskDefinition => taskDefinition.Syntax == taskDefinitionSyntax))
                                   .WhereNotNull();

        // Die Codefxes
        var codeFixes = taskDefinitionSymbols
                       .Select(taskDefinition => new RemoveUnusedNodesCodeFix(taskDefinition, context))
                       .Where(codeFix => codeFix.CanApplyFix());

        return codeFixes;
    }

}