#region Using Directives

using System.Collections.Generic;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Schlägt <see cref="SetValidLanguageVersionCodeFix"/> vor, wenn der Bereich die wirksame
/// <c>#version</c>-Direktive trifft und deren Wert fehlt oder ungültig ist (<c>Nav3002</c>). Die Direktive
/// ist strukturierte Trivia und liegt nicht im signifikanten Token-Strom; sie wird daher über
/// <see cref="CodeGenerationUnitSyntax.LanguageVersionDirective"/> adressiert, und der Bereich muss ihren
/// Extent schneiden (der Service dehnt einen Caret in der Direktive dorthin aus).
/// </summary>
public class SetValidLanguageVersionCodeFixProvider {

    /// <summary>
    /// Liefert einen <see cref="SetValidLanguageVersionCodeFix"/>, wenn der Bereich die wirksame
    /// <c>#version</c>-Direktive schneidet und deren Wert fehlt oder ungültig ist (<c>Nav3002</c>).
    /// </summary>
    /// <param name="context">Der Fix-Kontext (Bereich, Semantik-Modell, Editor-Einstellungen).</param>
    /// <param name="cancellationToken">Token zum Abbruch der Suche.</param>
    /// <returns>Der anwendbare Fix; leer, wenn keine (fehlerhafte) wirksame Direktive im Bereich liegt.</returns>
    public static IEnumerable<SetValidLanguageVersionCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {

        var directive = context.CodeGenerationUnit.Syntax.LanguageVersionDirective;
        if (directive == null) {
            yield break;
        }

        if (!context.Range.IntersectsWith(directive.Extent)) {
            yield break;
        }

        var codeFix = new SetValidLanguageVersionCodeFix(directive, context);
        if (codeFix.CanApplyFix()) {
            yield return codeFix;
        }
    }

}
