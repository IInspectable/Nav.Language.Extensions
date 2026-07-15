using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Ein an einen <see cref="ITextBuffer"/> gebundener <see cref="ITagger{T}"/>, der über
/// <see cref="TextBufferScopedValue{T}"/> pro Puffer und Schlüssel genau einen zugrunde liegenden
/// Tagger teilt und beim <see cref="Dispose"/> die Bindung löst. Alle Aufrufe werden an den
/// geteilten Tagger durchgereicht.
/// </summary>
/// <typeparam name="TTag">Der Tag-Typ des Taggers.</typeparam>
sealed class TextBufferScopedTagger<TTag> : ITagger<TTag>, IDisposable
    where TTag : ITag {

    readonly TextBufferScopedValue<ITagger<TTag>> _textBufferScopedTagger;
        
    /// <summary>
    /// Bindet den unter <paramref name="key"/> an <paramref name="textBuffer"/> geteilten
    /// <see cref="ITagger{T}"/> oder erzeugt ihn erstmalig über <paramref name="createFunc"/>.
    /// </summary>
    /// <param name="textBuffer">Der Puffer, an den der Tagger gebunden wird.</param>
    /// <param name="key">Der Schlüssel im Property-Bag des Puffers.</param>
    /// <param name="createFunc">Erzeugt den Tagger, falls noch keiner existiert.</param>
    internal TextBufferScopedTagger(
        ITextBuffer textBuffer,
        object key,
        Func<ITagger<TTag>> createFunc) {
        _textBufferScopedTagger = TextBufferScopedValue<ITagger<TTag>>.GetOrCreate(textBuffer, key, createFunc);
    }

    /// <summary>Der geteilte, an den Puffer gebundene <see cref="ITagger{T}"/>.</summary>
    ITagger<TTag> Tagger {
        get { return _textBufferScopedTagger.Value; }
    }

    /// <summary>Löst die Bindung an den geteilten Tagger (siehe <see cref="TextBufferScopedValue{T}.Dispose"/>).</summary>
    public void Dispose() {
        _textBufferScopedTagger.Dispose();
    }

    IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col) {
        return Tagger.GetTags(col);
    }

    event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged {
        add { Tagger.TagsChanged    += value; }
        remove { Tagger.TagsChanged -= value; }
    }
}