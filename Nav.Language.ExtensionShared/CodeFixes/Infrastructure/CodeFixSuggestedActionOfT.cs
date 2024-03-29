﻿#region Using Directives

using System;
using System.Threading;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

abstract class CodeFixSuggestedAction<T>: CodeFixSuggestedAction where T : CodeFix {

    protected CodeFixSuggestedAction(CodeFixSuggestedActionContext context, CodeFixSuggestedActionParameter parameter, T codeFix): base(context, parameter) {
        CodeFix = codeFix ?? throw new ArgumentNullException(nameof(codeFix));
    }

    public T CodeFix { get; }

    public sealed override string          UndoDescription  => CodeFix.Name;
    public sealed override Span?           ApplicableToSpan => GetSnapshotSpan(CodeFix.ApplicableTo);
    public sealed override CodeFixPrio     Prio             => CodeFix.Prio;
    public sealed override CodeFixCategory Category         => CodeFix.Category;

    public sealed override void Invoke(CancellationToken cancellationToken) {

        Apply(cancellationToken);

        ThreadHelper.ThrowIfNotOnUIThread();

        SemanticModelService.TryGet(Parameter.TextBuffer)?.UpdateSynchronously();
    }

    SnapshotSpan? GetSnapshotSpan(TextExtent? lineExtent) {
        return lineExtent?.ToSnapshotSpan(Parameter.CodeGenerationUnitAndSnapshot.Snapshot);
    }

}