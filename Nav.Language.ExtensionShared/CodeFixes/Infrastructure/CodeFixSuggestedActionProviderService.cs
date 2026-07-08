#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

interface ICodeFixSuggestedActionProviderService {

    IEnumerable<CodeFixSuggestedAction> GetCodeFixSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken);
}

[Export(typeof(ICodeFixSuggestedActionProviderService))]
class CodeFixSuggestedActionProviderService: ICodeFixSuggestedActionProviderService {

    readonly ImmutableList<ICodeFixSuggestedActionProvider> _codeFixActionProviders;

    [ImportingConstructor]
    public CodeFixSuggestedActionProviderService([ImportMany] IEnumerable<ICodeFixSuggestedActionProvider> codeFixActionProviders) {
        _codeFixActionProviders = codeFixActionProviders?.ToImmutableList() ??ImmutableList<ICodeFixSuggestedActionProvider>.Empty;
    }
        
    public IEnumerable<CodeFixSuggestedAction> GetCodeFixSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {
        return _codeFixActionProviders.SelectMany(p=> p.GetSuggestedActions(parameter, cancellationToken));
    }
}