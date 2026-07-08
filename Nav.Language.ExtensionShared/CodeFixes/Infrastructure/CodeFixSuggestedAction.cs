#region Using Directives

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

abstract class CodeFixSuggestedAction: ISuggestedAction {

    protected CodeFixSuggestedAction(CodeFixSuggestedActionContext context, CodeFixSuggestedActionParameter parameter) {
        Context   = context   ?? throw new ArgumentNullException(nameof(context));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    protected CodeFixSuggestedActionContext   Context   { get; }
    protected CodeFixSuggestedActionParameter Parameter { get; }

    public abstract Span?           ApplicableToSpan { get; }
    public abstract CodeFixPrio     Prio             { get; }
    public abstract CodeFixCategory Category         { get; }
    public abstract string          DisplayText      { get; }
    public abstract string          UndoDescription  { get; }

    public virtual ImageMoniker IconMoniker        => default;
    public virtual string       IconAutomationText => null;
    public virtual string       InputGestureText   => null;

    public virtual void Dispose() {

    }

    public bool TryGetTelemetryId(out Guid telemetryId) {
        telemetryId = Guid.Empty;
        return false;
    }

    public virtual bool HasActionSets => false;

    public virtual Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
        return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
    }

    public virtual bool HasPreview => false;

    public virtual Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
        return Task.FromResult<object>(null);
    }

    public abstract void Invoke(CancellationToken cancellationToken);

    protected abstract void Apply(CancellationToken cancellationToken);

    protected ITextSnapshot ApplyTextChanges(IEnumerable<TextChange> textChanges) {

        var textChangesAndSnapshot = new TextChangesAndSnapshot(
            textChanges: textChanges,
            snapshot: Parameter.CodeGenerationUnitAndSnapshot.Snapshot);

        return Context.TextChangeService.ApplyTextChanges(
            textView: Parameter.TextView,
            undoDescription: UndoDescription,
            textChangesAndSnapshot: textChangesAndSnapshot,
            waitMessage: DisplayText);
    }

}