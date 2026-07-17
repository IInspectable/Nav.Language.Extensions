#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Outlining;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.Formatting;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden über <see cref="ITextView"/>: Content-Typen und Caret-Position ermitteln,
/// Spans über den <c>BufferGraph</c> in die Ansicht projizieren, Auswahl setzen, das Caret sichtbar
/// bewegen, ansichtsgebundene Eigenschaften halten (siehe <c>AutoClosingViewProperty</c>), das
/// Nav-<see cref="ISymbol"/> unter dem Caret finden sowie Editor- und Formatierungsoptionen ableiten.
/// </summary>
static partial class TextViewExtensions {

    /// <summary>Liefert die Menge der <see cref="IContentType"/> aller Puffer im
    /// <c>BufferGraph</c> von <paramref name="textView"/>.</summary>
    public static ISet<IContentType> GetContentTypes(this ITextView textView) {
        return new HashSet<IContentType>(
            textView.BufferGraph.GetTextBuffers(_ => true).Select(b => b.ContentType));
    }

    /// <summary>
    /// Liefert den Puffer, der das Caret enthält und dessen Content-Typ zu
    /// <paramref name="contentType"/> passt (Vorgabe: der Nav-Content-Typ), oder
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="contentType">Der geforderte Content-Typ des Puffers.</param>
    /// <returns>Der passende Puffer unter dem Caret, oder <see langword="null"/>.</returns>
    public static ITextBuffer GetBufferContainingCaret(this ITextView textView, string contentType = NavLanguageContentDefinitions.ContentType) {
        var point = GetCaretPoint(textView, s => s.ContentType.IsOfType(contentType));
        return point?.Snapshot.TextBuffer;
    }

    /// <summary>
    /// Bildet die Caret-Position über den <c>BufferGraph</c> auf den ersten Puffer ab, dessen
    /// Snapshot <paramref name="match"/> erfüllt, und liefert den dortigen <see cref="SnapshotPoint"/>
    /// (oder <see langword="null"/>).
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="match">Prädikat über den Ziel-Snapshot.</param>
    /// <returns>Der abgebildete Caret-Punkt, oder <see langword="null"/>.</returns>
    public static SnapshotPoint? GetCaretPoint(this ITextView textView, Predicate<ITextSnapshot> match) {
        var caret = textView.Caret.Position;
        var span  = textView.BufferGraph.MapUpOrDownToFirstMatch(new SnapshotSpan(caret.BufferPosition, 0), match);
        return span?.Start;
    }

    /// <summary>
    /// Liefert die Caret-Position im <see cref="ITextView.TextBuffer"/> der Ansicht, oder
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <returns>Der Caret-Punkt im Puffer der Ansicht, oder <see langword="null"/>.</returns>
    public static SnapshotPoint? GetCaretPoint(this ITextView textView) {
        var caretPoint = textView.Caret.Position.Point.GetPoint(textView.TextBuffer, textView.Caret.Position.Affinity);
        return caretPoint;
    }

    /// <summary>
    /// Gets or creates a view property that would go away when view gets closed
    /// </summary>
    public static TProperty GetOrCreateAutoClosingProperty<TProperty, TTextView>(
        this TTextView textView,
        Func<TTextView, TProperty> valueCreator) where TTextView : ITextView {
        return textView.GetOrCreateAutoClosingProperty(typeof(TProperty), valueCreator);
    }

    /// <summary>
    /// Gets or creates a view property that would go away when view gets closed
    /// </summary>
    public static TProperty GetOrCreateAutoClosingProperty<TProperty, TTextView>(
        this TTextView textView,
        object key,
        Func<TTextView, TProperty> valueCreator) where TTextView : ITextView {
        GetOrCreateAutoClosingProperty(textView, key, valueCreator, out var value);
        return value;
    }

    /// <summary>
    /// Gets or creates a view property that would go away when view gets closed
    /// </summary>
    public static bool GetOrCreateAutoClosingProperty<TProperty, TTextView>(
        this TTextView textView,
        object key,
        Func<TTextView, TProperty> valueCreator,
        out TProperty value) where TTextView : ITextView {
        return AutoClosingViewProperty<TProperty, TTextView>.GetOrCreateValue(textView, key, valueCreator, out value);
    }

    /// <summary>
    /// Wählt <paramref name="span"/> in der Ansicht aus und setzt das Caret an dessen Ende (bzw.
    /// Anfang bei <paramref name="isReversed"/>).
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="span">Der auszuwählende Span (in einem zugrunde liegenden Puffer).</param>
    /// <param name="isReversed">Ob die Auswahl umgekehrt orientiert ist (Caret am Anfang).</param>
    public static void SetSelection(this ITextView textView, SnapshotSpan span, bool isReversed = false) {
        var spanInView = textView.GetSpanInView(span).Single();
        textView.Selection.Select(spanInView, isReversed);
        textView.Caret.MoveTo(isReversed ? spanInView.Start : spanInView.End);
    }

    /// <summary>
    /// Projiziert <paramref name="span"/> über den <c>BufferGraph</c> in den aktuellen Snapshot der
    /// Ansicht.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="span">Der abzubildende Span.</param>
    /// <returns>Die in die Ansicht projizierten Spans.</returns>
    public static NormalizedSnapshotSpanCollection GetSpanInView(this ITextView textView, SnapshotSpan span) {
        return textView.BufferGraph.MapUpToSnapshot(span, SpanTrackingMode.EdgeInclusive, textView.TextSnapshot);
    }

    /// <summary>
    /// Bewegt das Caret auf <paramref name="point"/> und stellt es sichtbar. Überladung, die den
    /// <see cref="SnapshotPoint"/> in einen <see cref="VirtualSnapshotPoint"/> überführt und an
    /// <see cref="TryMoveCaretToAndEnsureVisible(ITextView,VirtualSnapshotPoint,IOutliningManagerService,EnsureSpanVisibleOptions)"/>
    /// weiterreicht.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="point">Das Ziel des Caret.</param>
    /// <param name="outliningManagerService">Optional; um zusammengeklappte Bereiche am Ziel aufzuklappen.</param>
    /// <param name="ensureSpanVisibleOptions">Optionen, wie das Ziel sichtbar gescrollt wird.</param>
    /// <returns><see langword="true"/> bei Erfolg, sonst <see langword="false"/>.</returns>
    public static bool TryMoveCaretToAndEnsureVisible(this ITextView textView, SnapshotPoint point, IOutliningManagerService outliningManagerService = null, EnsureSpanVisibleOptions ensureSpanVisibleOptions = EnsureSpanVisibleOptions.None) {
        return textView.TryMoveCaretToAndEnsureVisible(new VirtualSnapshotPoint(point), outliningManagerService, ensureSpanVisibleOptions);
    }

    /// <summary>
    /// Bewegt das Caret auf <paramref name="point"/> und stellt es sichtbar: klappt bei gegebenem
    /// <paramref name="outliningManagerService"/> zunächst zusammengeklappte Bereiche am Ziel auf,
    /// bewegt dann das Caret und scrollt das Ziel gemäß <paramref name="ensureSpanVisibleOptions"/>
    /// in den sichtbaren Bereich. Liefert <see langword="false"/>, wenn die Ansicht geschlossen ist
    /// oder das Ziel nicht in die Ansicht abgebildet werden kann.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="point">Das Ziel des Caret (inklusive virtueller Leerräume).</param>
    /// <param name="outliningManagerService">Optional; um zusammengeklappte Bereiche am Ziel aufzuklappen.</param>
    /// <param name="ensureSpanVisibleOptions">Optionen, wie das Ziel sichtbar gescrollt wird.</param>
    /// <returns><see langword="true"/> bei Erfolg, sonst <see langword="false"/>.</returns>
    public static bool TryMoveCaretToAndEnsureVisible(this ITextView textView, VirtualSnapshotPoint point, IOutliningManagerService outliningManagerService = null, EnsureSpanVisibleOptions ensureSpanVisibleOptions = EnsureSpanVisibleOptions.None) {
        if (textView.IsClosed) {
            return false;
        }

        var pointInView = textView.GetPositionInView(point.Position);

        if (!pointInView.HasValue) {
            return false;
        }

        // If we were given an outlining service, we need to expand any outlines first, or else
        // the Caret.MoveTo won't land in the correct location if our target is inside a
        // collapsed outline.
        var outliningManager = outliningManagerService?.GetOutliningManager(textView);

        outliningManager?.ExpandAll(new SnapshotSpan(pointInView.Value, length: 0), match: _ => true);

        var newPosition = textView.Caret.MoveTo(new VirtualSnapshotPoint(pointInView.Value, point.VirtualSpaces));

        // We use the caret's position in the view's current snapshot here in case something 
        // changed text in response to a caret move (e.g. line commit)
        var spanInView = new SnapshotSpan(newPosition.BufferPosition, 0);
        textView.ViewScroller.EnsureSpanVisible(spanInView, ensureSpanVisibleOptions);

        return true;
    }

    /// <summary>
    /// Bildet <paramref name="point"/> über den <c>BufferGraph</c> in den aktuellen Snapshot der
    /// Ansicht ab, oder liefert <see langword="null"/>.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="point">Der abzubildende Punkt.</param>
    /// <returns>Der abgebildete Punkt im Ansichts-Snapshot, oder <see langword="null"/>.</returns>
    public static SnapshotPoint? GetPositionInView(this ITextView textView, SnapshotPoint point) {
        return textView.BufferGraph.MapUpToSnapshot(point, PointTrackingMode.Positive, PositionAffinity.Successor, textView.TextSnapshot);
    }

    /// <summary>
    /// Ermittelt das Nav-<see cref="ISymbol"/> an der Caret-Position anhand des angegebenen
    /// <see cref="CodeGenerationUnitAndSnapshot"/>. Liefert <see langword="null"/>, wenn kein aktuelles
    /// Modell vorliegt, die Auswahl nicht eindeutig ist oder dort kein Symbol steht; steht das Caret
    /// direkt hinter einem Bezeichner, wird zusätzlich die Position davor geprüft.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <param name="codeGenerationUnitAndSnapshot">Modell samt zugehörigem Snapshot.</param>
    /// <returns>Das gefundene Symbol, oder <see langword="null"/>.</returns>
    [CanBeNull]
    public static ISymbol TryFindSymbolUnderCaret(this ITextView textView, CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        if (codeGenerationUnitAndSnapshot == null) {
            return null;
        }

        var selectedSpans = textView.Selection.GetSnapshotSpansOnBuffer(codeGenerationUnitAndSnapshot.Snapshot.TextBuffer);
        if (selectedSpans.Count != 1) {
            return null;
        }

        var point = selectedSpans[0].Start;

        if (!codeGenerationUnitAndSnapshot.IsCurrent(point.Snapshot)) {
            return null;
        }

        var symbol = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Symbols.FindAtPosition(point.Position);

        if (symbol == null && point != point.GetContainingLine().Start) {
            symbol = codeGenerationUnitAndSnapshot.CodeGenerationUnit.Symbols.FindAtPosition(point.Position - 1);
        }

        return symbol;
    }

    /// <summary>
    /// Liest die für die Engine relevanten Editor-Einstellungen (Tabulatorweite und Zeilenumbruch)
    /// aus den Optionen der Ansicht aus.
    /// </summary>
    /// <param name="textView">Die Ansicht.</param>
    /// <returns>Die abgeleiteten <see cref="TextEditorSettings"/>.</returns>
    public static TextEditorSettings GetEditorSettings(this ITextView textView) {
        return new TextEditorSettings(
            tabSize: textView.Options.GetTabSize(),
            newLine: textView.Options.GetNewLineCharacter());
    }

    /// <summary>
    /// Leitet die <see cref="NavFormattingOptions"/> für den Formatter aus den Editor-Optionen ab.
    /// Einzugsstil und -breite kommen aus dem bestehenden Editor-Konfig-Kanal (Tabs vs. Leerzeichen,
    /// Einzugsbreite); die übrigen Optionen bleiben auf den kanonischen Vorgaben von
    /// <see cref="NavFormattingOptions.Default"/>.
    /// </summary>
    public static NavFormattingOptions GetFormattingOptions(this ITextView textView) {
        return NavFormattingOptions.Default with {
            IndentStyle = textView.Options.IsConvertTabsToSpacesEnabled() ? IndentStyle.Spaces : IndentStyle.Tabs,
            IndentSize  = textView.Options.GetIndentSize()
        };
    }

}