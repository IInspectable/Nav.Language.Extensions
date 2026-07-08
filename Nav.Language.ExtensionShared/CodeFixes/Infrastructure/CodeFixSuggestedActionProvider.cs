#region Using Directives

using System.Threading;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

interface ICodeFixSuggestedActionProvider {
    IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken);
}

abstract class CodeFixSuggestedActionProvider : ICodeFixSuggestedActionProvider {

    protected CodeFixSuggestedActionProvider(CodeFixSuggestedActionContext context) {
        Context = context;
    }

    protected CodeFixSuggestedActionContext Context { get; }

    public abstract IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken);
}