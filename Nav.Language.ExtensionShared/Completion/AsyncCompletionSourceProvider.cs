#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// Basisklasse der MEF-Provider für die asynchrone IntelliSense-Completion. Erfüllt den VS-SDK-Vertrag
/// <see cref="IAsyncCompletionSourceProvider"/> und hält je <see cref="ITextView"/> genau eine
/// <see cref="IAsyncCompletionSource"/> vor (Cache, beim Schließen der Ansicht geräumt). Die konkrete
/// Quelle liefert die Ableitung über <see cref="CreateCompletionSource"/> (siehe
/// <see cref="NavCompletionSourceProvider"/>).
/// </summary>
abstract class AsyncCompletionSourceProvider: IAsyncCompletionSourceProvider {

    readonly IDictionary<ITextView, IAsyncCompletionSource> _cache = new Dictionary<ITextView, IAsyncCompletionSource>();

    /// <summary>
    /// Liefert die zur <paramref name="textView"/> gehörende <see cref="IAsyncCompletionSource"/> — beim
    /// ersten Aufruf via <see cref="CreateCompletionSource"/> erzeugt und gecacht, danach wiederverwendet.
    /// Der Cache-Eintrag wird beim <see cref="ITextView.Closed"/>-Ereignis wieder freigegeben.
    /// </summary>
    public IAsyncCompletionSource GetOrCreate(ITextView textView) {
        if (_cache.TryGetValue(textView, out var completionSource)) {
            return completionSource;
        }

        var source = CreateCompletionSource();
        textView.Closed += (_, _) => _cache.Remove(textView);
        _cache.Add(textView, source);

        return source;
    }

    /// <summary>
    /// Erzeugt die konkrete, host-spezifische <see cref="IAsyncCompletionSource"/> für eine Ansicht.
    /// Von der Ableitung implementiert.
    /// </summary>
    protected abstract IAsyncCompletionSource CreateCompletionSource();

}