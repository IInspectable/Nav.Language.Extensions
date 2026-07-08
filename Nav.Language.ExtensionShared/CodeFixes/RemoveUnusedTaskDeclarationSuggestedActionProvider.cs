#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

[ExportCodeFixSuggestedActionProvider(nameof(RemoveUnusedNodesSuggestedActionProvider))]
class RemoveUnusedTaskDeclarationSuggestedActionProvider: CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public RemoveUnusedTaskDeclarationSuggestedActionProvider(CodeFixSuggestedActionContext context): base(context) {
    }

    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = RemoveUnusedTaskDeclarationCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new RemoveUnusedTaskDeclarationSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));

        return actions;
    }

}