#region Using Directives

using System;

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension;

/// <summary>
/// Basisklasse für Puffer-gebundene Bausteine, die auf den <see cref="ParserService"/> aufsetzen. Sie
/// bezieht den Puffer-Singleton und meldet sich auf dessen <see cref="ParserService.ParseResultChanging"/>/
/// <see cref="ParserService.ParseResultChanged"/>-Events an; Ableitungen überschreiben
/// <see cref="OnParseResultChanging"/>/<see cref="OnParseResultChanged"/>, um auf einen neuen
/// <see cref="SyntaxTreeAndSnapshot"/> zu reagieren (etwa <see cref="SemanticModelService"/>).
/// </summary>
abstract class ParserServiceDependent: IDisposable {

    /// <summary>
    /// Bindet den Baustein an den <see cref="ParserService"/> von <paramref name="textBuffer"/> und
    /// abonniert dessen Änderungs-Events.
    /// </summary>
    protected ParserServiceDependent(ITextBuffer textBuffer) {

        TextBuffer = textBuffer;

        ParserService = ParserService.GetOrCreateSingelton(textBuffer);

        ParserService.ParseResultChanging += OnParseResultChanging;
        ParserService.ParseResultChanged  += OnParseResultChanged;
    }

    /// <summary>
    /// Meldet die abonnierten <see cref="ParserService"/>-Events wieder ab. Ableitungen, die
    /// selbst Ressourcen halten, überschreiben und rufen <c>base.Dispose()</c>.
    /// </summary>
    public virtual void Dispose() {
        ParserService.ParseResultChanging -= OnParseResultChanging;
        ParserService.ParseResultChanged  -= OnParseResultChanged;
    }

    /// <summary>Der zugrunde liegende Puffer.</summary>
    protected ITextBuffer   TextBuffer    { get; }
    /// <summary>Der Puffer-gebundene <see cref="ParserService"/>, dessen Ergebnisse dieser Baustein nutzt.</summary>
    protected ParserService ParserService { get; }

    /// <summary>
    /// Wird aufgerufen, wenn der bisherige <see cref="SyntaxTreeAndSnapshot"/> ungültig wird. Basis tut nichts.
    /// </summary>
    protected virtual void OnParseResultChanging(object sender, EventArgs e) {
    }

    /// <summary>
    /// Wird aufgerufen, wenn ein neuer <see cref="SyntaxTreeAndSnapshot"/> bereitsteht. Basis tut nichts.
    /// </summary>
    protected virtual void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
    }

}