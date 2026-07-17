#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

/// <summary>
/// Stil-Fix, der eine ungenutzte Task-Deklaration (<c>taskref</c>) entfernt: eine
/// <see cref="ITaskDeclarationSymbol"/>, auf die es keine einzige Referenz gibt. Erzeugt genau die
/// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s, die die zugehörige
/// <see cref="TaskDeclarationSyntax"/> samt umgebender Trivia aus dem Quelltext löschen. Gefunden wird der
/// Fix vom <see cref="RemoveUnusedTaskDeclarationCodeFixProvider"/>.
/// </summary>
public class RemoveUnusedTaskDeclarationCodeFix: StyleCodeFix {

    internal RemoveUnusedTaskDeclarationCodeFix(ITaskDeclarationSymbol taskDeclarationSymbol, CodeFixContext context)
        : base(context) {
        TaskDeclaration = taskDeclarationSymbol ?? throw new ArgumentNullException(nameof(taskDeclarationSymbol));
    }

    /// <summary>Die zu entfernende (ungenutzte) Task-Deklaration.</summary>
    public ITaskDeclarationSymbol TaskDeclaration { get; }

    /// <summary>Der Anzeigename des Fixes: „Remove Unused Task Declaration".</summary>
    public override string        Name         => "Remove Unused Task Declaration";
    /// <inheritdoc/>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <inheritdoc/>
    public override TextExtent?   ApplicableTo => null;
    /// <inheritdoc/>
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    /// <summary>
    /// Prüft, ob der Fix anwendbar ist: die Deklaration hat keine Referenzen, besitzt eine Syntax, ist eine
    /// echte <see cref="TaskDeclarationOrigin.TaskDeclaration"/> (kein aus einer Task-Definition abgeleiteter
    /// Eintrag) und stammt nicht aus einer eingebundenen Datei
    /// (<see cref="ITaskDeclarationSymbol.IsIncluded"/> <c>false</c>).
    /// </summary>
    /// <returns><c>true</c>, wenn die Deklaration gefahrlos entfernt werden kann.</returns>
    internal bool CanApplyFix() {
        return TaskDeclaration.References.Count == 0                                     &&
               TaskDeclaration.Syntax           != null                                  &&
               TaskDeclaration.Origin           == TaskDeclarationOrigin.TaskDeclaration &&
               TaskDeclaration.IsIncluded       == false;
    }

    /// <summary>
    /// Liefert die <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s, die die
    /// <see cref="TaskDeclarationSyntax"/> der <see cref="TaskDeclaration"/> löschen.
    /// </summary>
    /// <returns>Das Lösch-Edit-Set.</returns>
    /// <exception cref="InvalidOperationException">Wird geworfen, wenn <see cref="CanApplyFix"/>
    /// <c>false</c> ist.</exception>
    public override IList<TextChange> GetTextChanges() {
        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        var textChanges = new List<TextChange?>();
        // CanApplyFix (oben geprüft) garantiert TaskDeclaration.Syntax != null.
        foreach (var textChange in GetRemoveSyntaxNodeChanges(TaskDeclaration.Syntax!)) {
            textChanges.Add(textChange);
        }

        return textChanges.OfType<TextChange>().ToList();
    }

}