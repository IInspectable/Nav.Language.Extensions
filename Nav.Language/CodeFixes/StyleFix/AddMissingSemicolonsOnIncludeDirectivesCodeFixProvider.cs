#region Using Directives

using System.Collections.Generic;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Findet den anwendbaren <see cref="AddMissingSemicolonsOnIncludeDirectivesCodeFix"/> zu einem
/// <see cref="CodeFixContext"/>: schlägt den Fix nur vor, wenn der Bereich eine
/// <see cref="IncludeDirectiveSyntax"/> enthält, und liefert ihn genau dann, wenn er anwendbar ist
/// (<see cref="AddMissingSemicolonsOnIncludeDirectivesCodeFix.CanApplyFix"/>).
/// </summary>
public class AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider {

    /// <summary>
    /// Ermittelt den zum <paramref name="context"/> passenden, anwendbaren Fix (siehe Typ-Doku).
    /// </summary>
    /// <param name="context">Der Kontext (Bereich, <see cref="CodeGenerationUnit"/>, Editor-Einstellungen).</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    /// <returns>Der anwendbare Fix, oder eine leere Sequenz.</returns>
    public static IEnumerable<AddMissingSemicolonsOnIncludeDirectivesCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {
        // Wir schlagen den Codefix nur vor, wenn sich das Caret in einer IncludeDirectiveSyntax befindet
        if (!context.ContainsNodes<IncludeDirectiveSyntax>()) {
            yield break;
        }

        var codeFix = new AddMissingSemicolonsOnIncludeDirectivesCodeFix(context);
        if (codeFix.CanApplyFix()) {
            yield return codeFix;
        }
    }

}