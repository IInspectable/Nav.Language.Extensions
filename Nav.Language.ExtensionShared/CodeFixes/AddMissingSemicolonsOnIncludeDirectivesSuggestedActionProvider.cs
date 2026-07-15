#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// MEF-Provider (<see cref="ExportCodeFixSuggestedActionProviderAttribute"/>), der die
/// <see cref="AddMissingSemicolonsOnIncludeDirectivesSuggestedAction"/> für die aktuelle Editor-Selektion
/// anbietet. Die Fund-Logik liegt in der Engine
/// (<see cref="AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider.SuggestCodeFixes"/>); jeder gefundene
/// <see cref="AddMissingSemicolonsOnIncludeDirectivesCodeFix"/> wird in eine Lightbulb-Aktion verpackt.
/// </summary>
[ExportCodeFixSuggestedActionProvider(nameof(AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider))]
class AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    /// <summary>
    /// Ermittelt über den Engine-CodeFix-Provider die anwendbaren Fixes für die Selektion und verpackt
    /// jeden in eine <see cref="AddMissingSemicolonsOnIncludeDirectivesSuggestedAction"/>.
    /// </summary>
    /// <param name="parameter">Selektion, Semantik-Modell-Schnappschuss und <c>TextView</c> der Anfrage.</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Suche.</param>
    /// <returns>Die anzubietenden Lightbulb-Aktionen (leer, wenn kein Fix anwendbar ist).</returns>
    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new AddMissingSemicolonsOnIncludeDirectivesSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}