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

    /// <summary>Die wirksame <c>#version</c>-Direktive mit wohlgeformter, aber nicht unterstützter Version.</summary>
    public VersionDirectiveSyntax Directive { get; }

    /// <inheritdoc/>
    public override string        Name         => $"Change language version to {NavLanguageVersion.Latest}";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => Directive.VersionNumber.Extent;
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.High;

    /// <summary>
    /// Ob der Fix anwendbar ist: nur, wenn ein numerisches Wert-Token vorhanden (nicht fehlend) und die
    /// Version nicht unterstützt ist (<c>Nav5001</c>). Bei fehlendem/nicht-numerischem Wert (<c>Nav3002</c>)
    /// greift der Fix nicht.
    /// </summary>
    internal bool CanApplyFix() {
        // Wir brauchen ein numerisches Wert-Token, das wir ersetzen können. Ist es nicht unterstützt, aber
        // fehlt/nicht-numerisch (Nav3002 statt Nav5001), greift dieser Fix nicht.
        return !Directive.VersionNumber.IsMissing &&
               !Directive.Version.IsSupported;
    }

    /// <summary>
    /// Erzeugt das Edit-Set: eine einzelne Ersetzung, die allein das numerische Wert-Token der Direktive
    /// durch <see cref="NavLanguageVersion.Latest"/> ersetzt (Schlüsselwort und Platzierung bleiben unberührt).
    /// </summary>
    /// <returns>Die anzuwendenden <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s.</returns>
    /// <exception cref="InvalidOperationException">Der Fix ist nicht anwendbar (<see cref="CanApplyFix"/>).</exception>
    public IList<TextChange> GetTextChanges() {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        return new List<TextChange> {
            TextChange.NewReplace(Directive.VersionNumber.Extent, NavLanguageVersion.Latest.ToString())
        };
    }

}
