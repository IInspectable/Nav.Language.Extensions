#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes;

[ExportCodeFixSuggestedActionProvider(nameof(SetSupportedLanguageVersionSuggestedActionProvider))]
class SetSupportedLanguageVersionSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public SetSupportedLanguageVersionSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = SetSupportedLanguageVersionCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new SetSupportedLanguageVersionSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}
