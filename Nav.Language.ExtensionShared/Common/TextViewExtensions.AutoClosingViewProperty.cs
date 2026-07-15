#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text.Editor;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

static partial class TextViewExtensions {

    /// <summary>
    /// Ein an eine <see cref="ITextView"/> gebundener Eigenschaftsspeicher, der beim Schließen der
    /// Ansicht (<see cref="ITextView.Closed"/>) automatisch aufgeräumt wird: enthaltene
    /// <see cref="IDisposable"/>-Werte werden verworfen und der Speicher aus dem Property-Bag der
    /// Ansicht entfernt. Grundlage von <see cref="TextViewExtensions"/>.<c>GetOrCreateAutoClosingProperty</c>.
    /// </summary>
    /// <typeparam name="TProperty">Der Typ der gehaltenen Werte.</typeparam>
    /// <typeparam name="TTextView">Der Ansichts-Typ (mindestens <see cref="ITextView"/>).</typeparam>
    class AutoClosingViewProperty<TProperty, TTextView> where TTextView : ITextView {

        readonly TTextView                     _textView;
        readonly Dictionary<object, TProperty> _map = new();

        /// <summary>
        /// Liefert den unter <paramref name="key"/> gebundenen Wert oder erzeugt ihn über
        /// <paramref name="valueCreator"/> und legt ihn ab. Der Speicher selbst wird pro Ansicht als
        /// Singleton geführt und beim Schließen der Ansicht aufgeräumt.
        /// </summary>
        /// <param name="textView">Die Ansicht, an die der Wert gebunden wird.</param>
        /// <param name="key">Der Schlüssel im ansichtsgebundenen Speicher.</param>
        /// <param name="valueCreator">Erzeugt den Wert, falls noch keiner existiert.</param>
        /// <param name="value">Der bestehende oder neu erzeugte Wert.</param>
        /// <returns><see langword="true"/>, wenn der Wert neu erzeugt wurde, sonst <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">Die Ansicht ist bereits geschlossen.</exception>
        public static bool GetOrCreateValue(
            TTextView textView,
            object key,
            Func<TTextView, TProperty> valueCreator,
            out TProperty value) {
            if(textView.IsClosed) {
                throw new InvalidOperationException();
            }

            var properties = textView.Properties.GetOrCreateSingletonProperty(() => new AutoClosingViewProperty<TProperty, TTextView>(textView));
            if(!properties.TryGetValue(key, out value)) {
                // Need to create it.
                value = valueCreator(textView);
                properties.Add(key, value);
                return true;
            }

            // Already there.
            return false;
        }

        /// <summary>Bindet den Speicher an <paramref name="textView"/> und abonniert deren
        /// <see cref="ITextView.Closed"/>-Ereignis für das automatische Aufräumen.</summary>
        AutoClosingViewProperty(TTextView textView) {
            _textView        =  textView;
            _textView.Closed += OnTextViewClosed;
        }

        /// <summary>
        /// Räumt beim Schließen der Ansicht auf: entfernt das Abonnement, verwirft alle gehaltenen
        /// <see cref="IDisposable"/>-Werte und entfernt den Speicher aus dem Property-Bag der Ansicht.
        /// </summary>
        void OnTextViewClosed(object sender, EventArgs e) {
            _textView.Closed -= OnTextViewClosed;

            if(_textView.Properties.TryGetProperty<AutoClosingViewProperty<TProperty, TTextView>>(typeof(AutoClosingViewProperty<TProperty, TTextView>), out var properties)) {
                foreach(var disposable in properties.Values.OfType<IDisposable>()) {
                    disposable.Dispose();
                }
            }
            _textView.Properties.RemoveProperty(typeof(AutoClosingViewProperty<TProperty, TTextView>));
        }

        bool TryGetValue(object key, out TProperty value) {
            return _map.TryGetValue(key, out value);
        }

        void Add(object key, TProperty value) {
            _map[key] = value;
        }

        IEnumerable<TProperty> Values => _map.Values;

    }
}