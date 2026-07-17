#region Using Directives

using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// MEF-Provider für den <see cref="CompletionCommitManager"/>. Erfüllt den VS-SDK-Vertrag
/// <see cref="IAsyncCompletionCommitManagerProvider"/> und ist über <see cref="NavLanguageContentDefinitions.ContentType"/>
/// auf editierbare Nav-Ansichten beschränkt. Hält den Commit-Manager je <see cref="ITextView"/> im Cache
/// (beim Schließen der Ansicht geräumt).
/// </summary>
[Export(typeof(IAsyncCompletionCommitManagerProvider))]
[Name(nameof(CompletionCommitManagerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
class CompletionCommitManagerProvider: IAsyncCompletionCommitManagerProvider {

    readonly IDictionary<ITextView, IAsyncCompletionCommitManager> _cache = new Dictionary<ITextView, IAsyncCompletionCommitManager>();

    /// <summary>
    /// Liefert den zur <paramref name="textView"/> gehörenden <see cref="CompletionCommitManager"/> — beim
    /// ersten Aufruf erzeugt und gecacht, beim <see cref="ITextView.Closed"/>-Ereignis wieder freigegeben.
    /// </summary>
    public IAsyncCompletionCommitManager GetOrCreate(ITextView textView) {

        if (_cache.TryGetValue(textView, out var itemSource)) {
            return itemSource;
        }

        var manager = new CompletionCommitManager();
        textView.Closed += (_, _) => _cache.Remove(textView); // clean up memory as files are closed
        _cache.Add(textView, manager);

        return manager;
    }

}