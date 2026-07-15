#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

/// <summary>
/// MEF-Provider (<see cref="ExportCodeFixSuggestedActionProviderAttribute"/>), der die
/// <see cref="MoveVersionDirectiveToTopSuggestedAction"/> für die aktuelle Editor-Selektion anbietet. Die
/// Fund-Logik liegt in der Engine (<see cref="MoveVersionDirectiveToTopCodeFixProvider.SuggestCodeFixes"/>);
/// jeder gefundene <see cref="MoveVersionDirectiveToTopCodeFix"/> wird in eine Lightbulb-Aktion verpackt.
/// </summary>
[ExportCodeFixSuggestedActionProvider(nameof(MoveVersionDirectiveToTopSuggestedActionProvider))]
class MoveVersionDirectiveToTopSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public MoveVersionDirectiveToTopSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    /// <summary>
    /// Ermittelt über den Engine-CodeFix-Provider die anwendbaren Fixes für die Selektion und verpackt
    /// jeden in eine <see cref="MoveVersionDirectiveToTopSuggestedAction"/>.
    /// </summary>
    /// <param name="parameter">Selektion, Semantik-Modell-Schnappschuss und <c>TextView</c> der Anfrage.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Suche.</param>
    /// <returns>Die anzubietenden Lightbulb-Aktionen (leer, wenn kein Fix anwendbar ist).</returns>
    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = MoveVersionDirectiveToTopCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new MoveVersionDirectiveToTopSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}
