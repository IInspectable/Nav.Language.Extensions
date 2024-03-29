﻿#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

[ExportCodeFixSuggestedActionProvider(nameof(AddMissingExitTransitionSuggestedActionProvider))]
class AddMissingExitTransitionSuggestedActionProvider : CodeFixSuggestedActionProvider {

    [ImportingConstructor]
    public AddMissingExitTransitionSuggestedActionProvider(CodeFixSuggestedActionContext context) : base(context) {
    }

    public override IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {

        var codeFixes = AddMissingExitTransitionCodeFixProvider.SuggestCodeFixes(parameter.CodeFixContext, cancellationToken);

        var actions = codeFixes.Select(codeFix => new AddMissingExitTransitionSuggestedAction(
                                           codeFix  : codeFix,
                                           parameter: parameter,
                                           context  : Context));
         
        return actions;
    }       
}