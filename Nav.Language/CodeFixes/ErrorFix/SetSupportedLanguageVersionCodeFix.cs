#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Setzt eine wohlgeformte, aber von der Engine nicht unterstützte Sprach-Version (<c>Nav5001</c>, z.B.
/// <c>#version 99</c>) auf die höchste unterstützte Version (<see cref="NavLanguageVersion.Latest"/>).
/// Ersetzt allein das numerische Wert-Token der wirksamen <c>#version</c>-Direktive — Schlüsselwort und
/// Platzierung bleiben unberührt.
/// </summary>
public sealed class SetSupportedLanguageVersionCodeFix: ErrorCodeFix {

    internal SetSupportedLanguageVersionCodeFix(VersionDirectiveSyntax directive, CodeFixContext context)
        : base(context) {
        Directive = directive ?? throw new ArgumentNullException(nameof(directive));
    }

    public VersionDirectiveSyntax Directive { get; }

    public override string        Name         => $"Change language version to {NavLanguageVersion.Latest}";
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => Directive.VersionNumber.Extent;
    public override CodeFixPrio   Prio         => CodeFixPrio.High;

    internal bool CanApplyFix() {
        // Wir brauchen ein numerisches Wert-Token, das wir ersetzen können. Ist es nicht unterstützt, aber
        // fehlt/nicht-numerisch (Nav3002 statt Nav5001), greift dieser Fix nicht.
        return !Directive.VersionNumber.IsMissing &&
               !Directive.Version.IsSupported;
    }

    public IList<TextChange> GetTextChanges() {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        return new List<TextChange> {
            TextChange.NewReplace(Directive.VersionNumber.Extent, NavLanguageVersion.Latest.ToString())
        };
    }

}
