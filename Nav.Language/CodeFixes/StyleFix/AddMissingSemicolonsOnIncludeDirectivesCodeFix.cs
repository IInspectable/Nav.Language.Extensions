#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Stil-Fix, der fehlende Semikola an Include-Direktiven ergänzt: jede <see cref="IncludeDirectiveSyntax"/>,
/// deren abschließendes <c>;</c> fehlt (das <see cref="SyntaxToken"/> ist <c>Missing</c>). Erzeugt je
/// betroffener Direktive eine <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>, die am Ende der
/// Direktive ein <see cref="SyntaxFacts.Semicolon"/> einfügt. Gefunden wird der Fix vom
/// <see cref="AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider"/>.
/// </summary>
public class AddMissingSemicolonsOnIncludeDirectivesCodeFix: StyleCodeFix {

    internal AddMissingSemicolonsOnIncludeDirectivesCodeFix(CodeFixContext context)
        : base(context) {
    }

    /// <summary>Der Anzeigename des Fixes: „Add missing ';' on Include Directives".</summary>
    public override string        Name         => "Add missing ';' on Include Directives";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => null;
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.Low;

    /// <summary>
    /// Prüft, ob es mindestens eine Include-Direktive ohne Semikolon gibt (siehe <see cref="GetCanditates"/>).
    /// </summary>
    /// <returns><c>true</c>, wenn der Fix etwas zu tun hat.</returns>
    internal bool CanApplyFix() {
        return GetCanditates().Any();
    }

    /// <summary>
    /// Die betroffenen Include-Direktiven: alle <see cref="IncludeDirectiveSyntax"/> mit fehlendem
    /// (<c>Missing</c>) Semikolon-Token.
    /// </summary>
    IEnumerable<IncludeDirectiveSyntax> GetCanditates() {
        return Syntax.DescendantNodes<IncludeDirectiveSyntax>().Where(ids => ids.Semicolon.IsMissing);
    }

    /// <summary>
    /// Liefert je betroffener Direktive (siehe <see cref="GetCanditates"/>) eine
    /// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>, die an ihrem Ende ein
    /// <see cref="SyntaxFacts.Semicolon"/> einfügt.
    /// </summary>
    /// <returns>Das Einfüge-Edit-Set (leer, wenn nichts zu ergänzen ist).</returns>
    public override IList<TextChange> GetTextChanges() {

        var textChanges = new List<TextChange>();

        foreach (var includeDirectiveSyntax in GetCanditates()) {
            textChanges.AddRange(GetInsertChanges(includeDirectiveSyntax.End, SyntaxFacts.Semicolon.ToString()));
        }

        return textChanges;
    }

}