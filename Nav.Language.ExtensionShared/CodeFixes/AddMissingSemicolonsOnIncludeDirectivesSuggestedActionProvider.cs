#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

[ExportCodeFixSuggestedActionProvider(nameof(AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider))]
class AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public AddMissingSemicolonsOnIncludeDirectivesSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = AddMissingSemicolonsOnIncludeDirectivesCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new AddMissingSemicolonsOnIncludeDirectivesSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}