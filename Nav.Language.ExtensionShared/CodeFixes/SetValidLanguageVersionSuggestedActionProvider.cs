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
/// <see cref="SetValidLanguageVersionSuggestedAction"/> für die aktuelle Editor-Selektion anbietet. Die
/// Fund-Logik liegt in der Engine (<see cref="SetValidLanguageVersionCodeFixProvider.SuggestCodeFixes"/>);
/// jeder gefundene <see cref="SetValidLanguageVersionCodeFix"/> wird in eine Lightbulb-Aktion verpackt.
/// </summary>
[ExportCodeFixSuggestedActionProvider(nameof(SetValidLanguageVersionSuggestedActionProvider))]
class SetValidLanguageVersionSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public SetValidLanguageVersionSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    /// <summary>
    /// Ermittelt über den Engine-CodeFix-Provider die anwendbaren Fixes für die Selektion und verpackt
    /// jeden in eine <see cref="SetValidLanguageVersionSuggestedAction"/>.
    /// </summary>
    /// <param name="parameter">Selektion, Semantik-Modell-Schnappschuss und <c>TextView</c> der Anfrage.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Suche.</param>
    /// <returns>Die anzubietenden Lightbulb-Aktionen (leer, wenn kein Fix anwendbar ist).</returns>
    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = SetValidLanguageVersionCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new SetValidLanguageVersionSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}
