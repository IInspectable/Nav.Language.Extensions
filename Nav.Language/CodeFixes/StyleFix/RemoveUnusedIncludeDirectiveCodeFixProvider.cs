#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Findet den anwendbaren <see cref="RemoveUnusedIncludeDirectiveCodeFix"/> zu einem
/// <see cref="CodeFixContext"/>: schlägt den Fix nur vor, wenn der Bereich eine
/// <see cref="IncludeDirectiveSyntax"/> berührt, und liefert ihn genau dann, wenn er anwendbar ist
/// (<see cref="RemoveUnusedIncludeDirectiveCodeFix.CanApplyFix"/>).
/// </summary>
public sealed class RemoveUnusedIncludeDirectiveCodeFixProvider {

    /// <summary>
    /// Ermittelt den zum <paramref name="context"/> passenden, anwendbaren Fix (siehe Typ-Doku).
    /// </summary>
    /// <param name="context">Der Kontext (Bereich, <see cref="CodeGenerationUnit"/>, Editor-Einstellungen).</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    /// <returns>Der anwendbare Fix, oder eine leere Sequenz.</returns>
    public static IEnumerable<RemoveUnusedIncludeDirectiveCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {

        // Wir schlagen den Codefix nur vor, wenn sich das Caret in einer Task Declaration befindet
        if (!context.FindNodes<IncludeDirectiveSyntax>().Any()) {
            yield break;
        }

        var codeFix = new RemoveUnusedIncludeDirectiveCodeFix(context);
        if (codeFix.CanApplyFix()) {
            yield return codeFix;
        }
    }

}