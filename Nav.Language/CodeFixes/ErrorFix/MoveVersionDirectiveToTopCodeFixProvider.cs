#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Schlägt <see cref="MoveVersionDirectiveToTopCodeFix"/> vor, wenn der Bereich eine deplatzierte
/// <c>#version</c>-Direktive trifft (<c>Nav3003</c>). Die deplatzierte Direktive ist — anders als die vom
/// <c>Nav3002</c>-/<c>Nav5001</c>-Fix adressierte wirksame — <b>nicht</b>
/// <see cref="CodeGenerationUnitSyntax.LanguageVersionDirective"/>; sie wird über
/// <see cref="SyntaxTree.Directives"/> gesucht. Als präziser Auslöser dient die tatsächlich gemeldete
/// <c>Nav3003</c>-Diagnose (deren Location genau die Direktive überdeckt) — so greift der Fix nicht auf ein
/// bloßes Duplikat am Kopf (<c>Nav3004</c>).
/// </summary>
public class MoveVersionDirectiveToTopCodeFixProvider {

    public static IEnumerable<MoveVersionDirectiveToTopCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken) {

        var unit      = context.CodeGenerationUnit;
        var effective = unit.Syntax.LanguageVersionDirective;

        foreach (var directive in unit.Syntax.SyntaxTree.Directives().OfType<VersionDirectiveSyntax>()) {

            if (ReferenceEquals(directive, effective)) {
                continue;
            }

            if (!context.Range.IntersectsWith(directive.Extent)) {
                continue;
            }

            if (!HasMisplacedDiagnostic(unit, directive)) {
                continue;
            }

            var codeFix = new MoveVersionDirectiveToTopCodeFix(directive, effective, context);
            if (codeFix.CanApplyFix()) {
                yield return codeFix;
            }
        }
    }

    // Trägt die Direktive tatsächlich eine Nav3003-Deplatzierungsmeldung? Die Platzierungs-Diagnosen erzeugt
    // der Parser; sie liegen im SyntaxTree (nicht in den semantischen CodeGenerationUnit.Diagnostics). Die
    // Location deckt genau den Direktiv-Extent ab (beide aus demselben Direktiv-Lauf), daher der Extent-Vergleich.
    static bool HasMisplacedDiagnostic(CodeGenerationUnit unit, VersionDirectiveSyntax directive) {
        return unit.Syntax.SyntaxTree.Diagnostics.Any(d => d.Descriptor.Id == DiagnosticId.Nav3003 &&
                                                           d.Location.Extent == directive.Extent);
    }

}
