#region Using Directives

using System.Collections.Generic;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

public class AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider {

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