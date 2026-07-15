#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Stil-Fix, der ungenutzte Include-Direktiven (<c>taskref "…"</c>) entfernt: jede
/// <see cref="IncludeDirectiveSyntax"/>, deren eingebundene Datei keine Task-Deklaration beisteuert, die
/// tatsächlich referenziert wird (bzw. gar kein aufgelöstes Include-Symbol besitzt). Erzeugt die
/// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s, die diese Direktiven aus dem Quelltext löschen.
/// Gefunden wird der Fix vom <see cref="RemoveUnusedIncludeDirectiveCodeFixProvider"/>.
/// </summary>
public class RemoveUnusedIncludeDirectiveCodeFix: StyleCodeFix {

    internal RemoveUnusedIncludeDirectiveCodeFix(CodeFixContext context)
        : base(context) {
    }

    /// <summary>Der Anzeigename des Fixes: „Remove Unused Taskref Directive".</summary>
    public override string        Name         => "Remove Unused Taskref Directive";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => null;
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    /// <summary>
    /// Prüft, ob es mindestens eine ungenutzte Include-Direktive gibt (siehe <see cref="GetCandidates"/>).
    /// </summary>
    /// <returns><c>true</c>, wenn der Fix etwas zu tun hat.</returns>
    internal bool CanApplyFix() {
        return GetCandidates().Any();
    }

    /// <summary>
    /// Die entfernbaren Include-Direktiven: alle <see cref="IncludeDirectiveSyntax"/> unter der
    /// <see cref="SyntaxTree.Root"/>, zu denen kein Include-Symbol aufgelöst wurde oder deren Include keine
    /// referenzierte Task-Deklaration beisteuert.
    /// </summary>
    IEnumerable<IncludeDirectiveSyntax> GetCandidates() {

        var includeDirectiveSyntaxes = SyntaxTree.Root.DescendantNodes<IncludeDirectiveSyntax>();
        foreach (var includeDirectiveSyntax in includeDirectiveSyntaxes) {
            var includeSymbol = CodeGenerationUnit.Includes.FirstOrDefault(i => i.Syntax == includeDirectiveSyntax);
            if (includeSymbol == null || !includeSymbol.TaskDeclarations.SelectMany(td => td.References).Any()) {
                yield return includeDirectiveSyntax;
            }
        }
    }

    /// <summary>
    /// Liefert die <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s, die alle ungenutzten
    /// Include-Direktiven (siehe <see cref="GetCandidates"/>) aus dem Quelltext löschen.
    /// </summary>
    /// <returns>Das Lösch-Edit-Set (leer, wenn es keine Kandidaten gibt).</returns>
    public override IList<TextChange> GetTextChanges() {

        var textChanges = new List<TextChange?>();
        foreach (var textChange in GetCandidates().SelectMany(GetRemoveSyntaxNodeChanges)) {
            textChanges.Add(textChange);
        }

        return textChanges.OfType<TextChange>().ToList();
    }

}