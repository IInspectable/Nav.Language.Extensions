#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// MEF-exportierter <see cref="IWpfTextViewConnectionListener"/> für Nav-Dokument-Ansichten: hält
/// je <see cref="IWpfTextView"/> eine Liste von Aufräum-Aktionen und führt sie aus, sobald der
/// letzte Nav-Puffer der Ansicht getrennt wird. So können Features an eine Ansicht gebundene
/// Ressourcen deterministisch beim Schließen freigeben.
/// </summary>
[Export(typeof(IWpfTextViewConnectionListener))]
[Export(typeof(TextViewConnectionListener))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Document)]
sealed class TextViewConnectionListener : IWpfTextViewConnectionListener {

    readonly Dictionary<IWpfTextView, List<Action<IWpfTextView>>> _textViews;

    /// <summary>Erzeugt den Listener mit leerer Ansicht-zu-Aktionen-Zuordnung.</summary>
    public TextViewConnectionListener() {
        _textViews = new Dictionary<IWpfTextView, List<Action<IWpfTextView>>>();
    }

    /// <summary>
    /// Wird gerufen, wenn Nav-Puffer mit <paramref name="textView"/> verbunden werden; legt die
    /// (zunächst leere) Liste der Aufräum-Aktionen für die Ansicht an.
    /// </summary>
    /// <param name="textView">Die betroffene Ansicht.</param>
    /// <param name="reason">Der Grund der Verbindung.</param>
    /// <param name="subjectBuffers">Die verbundenen Puffer.</param>
    public void SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers) {
        _textViews[textView] = new List<Action<IWpfTextView>>();
    }

    /// <summary>
    /// Wird gerufen, wenn Nav-Puffer von <paramref name="textView"/> getrennt werden; entfernt die
    /// Ansicht aus der Zuordnung und führt ihre hinterlegten Aufräum-Aktionen aus.
    /// </summary>
    /// <param name="textView">Die betroffene Ansicht.</param>
    /// <param name="reason">Der Grund der Trennung.</param>
    /// <param name="subjectBuffers">Die getrennten Puffer.</param>
    public void SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers) {
        var actions = _textViews[textView];

        _textViews.Remove(textView);

        foreach(var action in actions) {
            action(textView);
        }
    }

    /// <summary>
    /// Liefert die bekannte <see cref="IWpfTextView"/>, deren <see cref="ITextView.TextBuffer"/>
    /// <paramref name="textBuffer"/> ist, oder <see langword="null"/>.
    /// </summary>
    /// <param name="textBuffer">Der gesuchte Puffer.</param>
    /// <returns>Die zugehörige Ansicht oder <see langword="null"/>.</returns>
    public IWpfTextView GetTextViewForBuffer(ITextBuffer textBuffer) {
        return _textViews.Keys.FirstOrDefault(t => t.TextBuffer == textBuffer);
    }

    /// <summary>
    /// Registriert eine Aktion, die beim Trennen der Nav-Puffer von <paramref name="textView"/>
    /// ausgeführt wird (siehe <see cref="SubjectBuffersDisconnected"/>).
    /// </summary>
    /// <param name="textView">Die Ansicht, an deren Trennung die Aktion gebunden wird.</param>
    /// <param name="action">Die beim Trennen auszuführende Aktion.</param>
    public void AddDisconnectAction(IWpfTextView textView, Action<IWpfTextView> action) {
        _textViews[textView].Add(action);
    }
}