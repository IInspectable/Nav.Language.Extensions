#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

[ExportCodeFixSuggestedActionProvider(nameof(MoveVersionDirectiveToTopSuggestedActionProvider))]
class MoveVersionDirectiveToTopSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public MoveVersionDirectiveToTopSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = MoveVersionDirectiveToTopCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new MoveVersionDirectiveToTopSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}
