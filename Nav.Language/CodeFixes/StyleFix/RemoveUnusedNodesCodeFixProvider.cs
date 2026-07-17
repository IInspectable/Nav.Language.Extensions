#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Findet die anwendbaren <see cref="RemoveUnusedNodesCodeFix"/>e zu einem <see cref="CodeFixContext"/>:
/// schlägt den Fix nur vor, wenn der Bereich eine <see cref="NodeDeclarationSyntax"/> berührt, ermittelt die
/// umschließenden <see cref="TaskDefinitionSyntax"/>en (dedupliziert), löst sie zu ihren
/// <see cref="ITaskDefinitionSymbol"/>en auf und liefert je Definition einen Fix — gefiltert auf jene mit
/// tatsächlich entfernbaren Knoten (<see cref="RemoveUnusedNodesCodeFix.CanApplyFix"/>).
/// </summary>
public sealed class RemoveUnusedNodesCodeFixProvider {

    /// <summary>
    /// Ermittelt die zum <paramref name="context"/> passenden, anwendbaren Fixes (siehe Typ-Doku).
    /// </summary>
    /// <param name="context">Der Kontext (Bereich, <see cref="CodeGenerationUnit"/>, Editor-Einstellungen).</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    /// <returns>Die anwendbaren Fixes (ggf. leer).</returns>
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