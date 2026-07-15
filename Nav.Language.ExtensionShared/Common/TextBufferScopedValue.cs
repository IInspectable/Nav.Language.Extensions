using System;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Text;

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Ein an einen <see cref="ITextBuffer"/> gebundener, geteilter Wert vom Typ <typeparamref name="T"/>.
/// Der eigentliche Wert wird als <see cref="WeakReference"/> im Property-Bag des Puffers unter einem
/// Schlüssel abgelegt; alle Aufrufer, die denselben Puffer und Schlüssel verwenden, teilen sich
/// dieselbe Instanz. Wird der Puffer verworfen, kann der Wert eingesammelt werden. Grundbaustein
/// von <see cref="TextBufferScopedClassifier"/> und <see cref="TextBufferScopedTagger{TTag}"/>.
/// </summary>
/// <typeparam name="T">Der Referenztyp des gebundenen Werts.</typeparam>
sealed class TextBufferScopedValue<T> where T : class {

    T _value;
        
    TextBufferScopedValue(T value) {
        _value = value;
    }

    /// <summary>Der geteilte, an den Puffer gebundene Wert.</summary>
    internal T Value
    {
        get { return _value; }
    }
        
    /// <summary>
    /// Löst die lokale Bindung an den Wert (setzt sie auf <see langword="null"/>). Der im
    /// Property-Bag abgelegte <see cref="WeakReference"/>-Eintrag bleibt bestehen, bis der Puffer
    /// samt Wert eingesammelt wird.
    /// </summary>
    internal void Dispose() {
        _value = null;
    }

    /// <summary>
    /// Liefert den unter <paramref name="key"/> an <paramref name="textBuffer"/> gebundenen Wert
    /// oder erzeugt ihn über <paramref name="createFunc"/> und hinterlegt ihn (als
    /// <see cref="WeakReference"/>) im Property-Bag.
    /// </summary>
    /// <param name="textBuffer">Der Puffer, an den der Wert gebunden wird.</param>
    /// <param name="key">Der Schlüssel im Property-Bag des Puffers.</param>
    /// <param name="createFunc">Erzeugt den Wert, falls noch keiner existiert.</param>
    /// <returns>Ein Wrapper, der den geteilten Wert hält.</returns>
    internal static TextBufferScopedValue<T> GetOrCreate(ITextBuffer textBuffer, object key, Func<T> createFunc) {

        var value = TryGet(textBuffer, key);

        if (value == null) {
            value = createFunc();
            textBuffer.Properties.GetOrCreateSingletonProperty(key, ()=> new WeakReference(value));
        }

        return new TextBufferScopedValue<T>(value);
    }

    /// <summary>
    /// Liest den unter <paramref name="key"/> hinterlegten Wert aus dem Property-Bag von
    /// <paramref name="textBuffer"/>. Ist der <see cref="WeakReference"/>-Zieleintrag bereits
    /// eingesammelt, wird der verwaiste Eintrag entfernt und <see langword="null"/> geliefert.
    /// </summary>
    /// <param name="textBuffer">Der Puffer, aus dem gelesen wird.</param>
    /// <param name="key">Der Schlüssel im Property-Bag.</param>
    /// <returns>Der gebundene Wert oder <see langword="null"/>.</returns>
    [CanBeNull]
    internal static T TryGet(ITextBuffer textBuffer, object key) {
        T value = null;
        if (textBuffer.Properties.TryGetProperty(key, out WeakReference weakValue)) {
            value = weakValue.Target as T;
            if (value == null) {
                textBuffer.Properties.RemoveProperty(key);
            }
        }
        return value;
    }
}