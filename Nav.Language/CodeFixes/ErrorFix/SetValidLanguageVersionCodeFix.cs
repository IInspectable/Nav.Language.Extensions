#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Ersetzt einen fehlenden oder ungültigen Versionswert einer wirksamen <c>#version</c>-Direktive
/// (<c>Nav3002</c>, z.B. <c>#version</c> ohne Wert, <c>#version abc</c>, <c>#version 999…</c> mit Überlauf)
/// durch die höchste unterstützte Version (<see cref="NavLanguageVersion.Latest"/>). Fehlt der Wert ganz,
/// wird er hinter dem <c>version</c>-Schlüsselwort eingefügt; andernfalls wird die ungültige Wert-Spanne
/// ersetzt.
/// </summary>
/// <remarks>
/// Bewusst NICHT enthalten ist der Überschuss-Fall (gültige Zahl mit überzähligem Rest, z.B.
/// <c>#version 2 xy</c>): dort steht bereits ein wirksamer Wert, das Problem ist der Rest — ein anderer
/// Fix. Zum <c>Nav5001</c>-Fix (wohlgeformte, aber nicht unterstützte Version) ist dieser Fix disjunkt.
/// </remarks>
public sealed class SetValidLanguageVersionCodeFix: ErrorCodeFix {

    internal SetValidLanguageVersionCodeFix(VersionDirectiveSyntax directive, CodeFixContext context)
        : base(context) {
        Directive = directive ?? throw new ArgumentNullException(nameof(directive));
    }

    /// <summary>Die wirksame <c>#version</c>-Direktive, deren fehlender/ungültiger Wert korrigiert wird.</summary>
    public VersionDirectiveSyntax Directive { get; }

    /// <inheritdoc/>
    public override string        Name         => $"Change language version to {NavLanguageVersion.Latest}";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => ValueExtent();
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.High;

    /// <summary>
    /// Ob der Fix anwendbar ist: nur, wenn kein brauchbarer Versionswert vorliegt (Wert fehlt oder ist nicht
    /// parsebar). Der Überschuss-Fall (gültige Zahl mit überzähligem Rest) hat einen wirksamen Wert und wird
    /// ausgeschlossen.
    /// </summary>
    internal bool CanApplyFix() {
        // Nur wenn kein brauchbarer Versionswert vorliegt: der Rückfall auf Default rührt dann von Nav3002
        // her, nicht von einem geschriebenen '#version 1'. Der Überschuss-Fall (gültige Zahl + Rest) hat
        // einen wirksamen Wert und wird hier ausgeschlossen.
        return !HasUsableValue();
    }

    bool HasUsableValue() {
        return !Directive.VersionNumber.IsMissing &&
               NavLanguageVersion.TryParse(SyntaxTree.SourceText.Substring(Directive.VersionNumber.Extent), out _);
    }

    // Die Wert-Token hinter dem 'version'-Schlüsselwort (ohne Zwischenraum/Satzzeichen und Zeilenende) — leer,
    // wenn der Wert ganz fehlt.
    IReadOnlyList<SyntaxToken> ValueTokens() {
        return Directive.ChildTokens()
                        .Where(t => t.Type != SyntaxTokenType.HashToken          &&
                                    t.Type != SyntaxTokenType.VersionKeyword      &&
                                    t.Type != SyntaxTokenType.PreprocessorText     &&
                                    t.Type != SyntaxTokenType.PreprocessorNewLine)
                        .ToList();
    }

    // Zielspanne des Wertes: die ungültige Wert-Spanne, oder — wenn der Wert fehlt — ein nullbreiter
    // Einfügepunkt unmittelbar hinter dem 'version'-Schlüsselwort.
    TextExtent ValueExtent() {
        var tokens = ValueTokens();
        if (tokens.Count == 0) {
            var insertAt = Directive.VersionKeyword.Extent.End;
            return TextExtent.FromBounds(insertAt, insertAt);
        }

        return TextExtent.FromBounds(tokens[0].Extent.Start, tokens[tokens.Count - 1].Extent.End);
    }

    /// <summary>
    /// Erzeugt das Edit-Set: eine einzelne Ersetzung, die die ungültige Wert-Spanne durch
    /// <see cref="NavLanguageVersion.Latest"/> ersetzt — bzw. den Wert (mit führendem Leerzeichen) hinter dem
    /// <c>version</c>-Schlüsselwort einfügt, wenn er ganz fehlt.
    /// </summary>
    /// <returns>Die anzuwendenden <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s.</returns>
    /// <exception cref="InvalidOperationException">Der Fix ist nicht anwendbar (<see cref="CanApplyFix"/>).</exception>
    public IList<TextChange> GetTextChanges() {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        var target  = ValueExtent();
        // Fehlt der Wert (nullbreit), fügen wir ihn mit führendem Leerzeichen hinter 'version' ein; sonst
        // ersetzen wir die ungültige Spanne (deren vorangehender Zwischenraum bleibt erhalten).
        var newText = target.IsEmpty ? $" {NavLanguageVersion.Latest}" : NavLanguageVersion.Latest.ToString();

        return new List<TextChange> {
            TextChange.NewReplace(target, newText)
        };
    }

}
