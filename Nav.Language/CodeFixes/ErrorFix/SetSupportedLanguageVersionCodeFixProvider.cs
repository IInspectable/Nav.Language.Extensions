#region Using Directives

using System.Collections.Generic;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Schlägt <see cref="SetSupportedLanguageVersionCodeFix"/> vor, wenn der Bereich die wirksame
/// <c>#version</c>-Direktive trifft und deren Version wohlgeformt, aber nicht unterstützt ist
/// (<c>Nav5001</c>). Die Direktive ist strukturierte Trivia und liegt nicht im signifikanten Token-Strom;
/// sie wird daher über <see cref="CodeGenerationUnitSyntax.LanguageVersionDirective"/> adressiert, und der
/// Bereich muss ihren Extent schneiden (der Service dehnt einen Caret in der Direktive dorthin aus).
/// </summary>
public class SetSupportedLanguageVersionCodeFixProvider {

    public static IEnumerable<SetSupportedLanguageVersionCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {

        var directive = context.CodeGenerationUnit.Syntax.LanguageVersionDirective;
        if (directive == null) {
            yield break;
        }

        if (!context.Range.IntersectsWith(directive.Extent)) {
            yield break;
        }

        var codeFix = new SetSupportedLanguageVersionCodeFix(directive, context);
        if (codeFix.CanApplyFix()) {
            yield return codeFix;
        }
    }

}
