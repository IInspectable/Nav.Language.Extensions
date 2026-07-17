using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Ein an einen <see cref="ITextBuffer"/> gebundener <see cref="IClassifier"/>, der über
/// <see cref="TextBufferScopedValue{T}"/> pro Puffer und Schlüssel genau einen zugrunde liegenden
/// Classifier teilt und beim <see cref="Dispose"/> die Bindung löst. Alle Aufrufe werden an den
/// geteilten Classifier durchgereicht.
/// </summary>
sealed class TextBufferScopedClassifier : IClassifier, IDisposable {
    readonly TextBufferScopedValue<IClassifier> _textBufferScopedValue;
        
    /// <summary>
    /// Bindet den unter <paramref name="key"/> an <paramref name="textBuffer"/> geteilten
    /// <see cref="IClassifier"/> oder erzeugt ihn erstmalig über <paramref name="createFunc"/>.
    /// </summary>
    /// <param name="textBuffer">Der Puffer, an den der Classifier gebunden wird.</param>
    /// <param name="key">Der Schlüssel im Property-Bag des Puffers.</param>
    /// <param name="createFunc">Erzeugt den Classifier, falls noch keiner existiert.</param>
    internal TextBufferScopedClassifier(
        ITextBuffer textBuffer,
        object key,
        Func<IClassifier> createFunc) {
        _textBufferScopedValue = TextBufferScopedValue<IClassifier>.GetOrCreate(textBuffer, key, createFunc);
    }

    /// <summary>Der geteilte, an den Puffer gebundene <see cref="IClassifier"/>.</summary>
    IClassifier Classifier {
        get { return _textBufferScopedValue.Value; }
    }

    /// <summary>Löst die Bindung an den geteilten Classifier (siehe <see cref="TextBufferScopedValue{T}.Dispose"/>).</summary>
    public void Dispose() {
        _textBufferScopedValue.Dispose();
    }

    event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged {
        add { Classifier.ClassificationChanged    += value; }
        remove { Classifier.ClassificationChanged -= value; }
    }

    IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span) {
        return Classifier.GetClassificationSpans(span);
    }
}