﻿#region Using Directives

using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

abstract class AsyncCompletionSourceProvider: IAsyncCompletionSourceProvider {

    readonly IDictionary<ITextView, IAsyncCompletionSource> _cache = new Dictionary<ITextView, IAsyncCompletionSource>();

    public IAsyncCompletionSource GetOrCreate(ITextView textView) {
        if (_cache.TryGetValue(textView, out var completionSource)) {
            return completionSource;
        }

        var source = CreateCompletionSource();
        textView.Closed += (_, _) => _cache.Remove(textView);
        _cache.Add(textView, source);

        return source;
    }

    protected abstract IAsyncCompletionSource CreateCompletionSource();

}