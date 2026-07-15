#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

/// <summary>
/// Hebt beim Positionieren des Cursors das Symbol darunter samt allen seinen Referenzen im Editor hervor
/// (analog zum C#-Editor). Als <see cref="ITagger{T}"/> von <see cref="ReferenceHighlightTag"/> liefert
/// er je Fundstelle einen Textmarker-Tag; die erste Fundstelle gilt als Definition
/// (<see cref="DefinitionHighlightTag"/>). Die Fundstellen stammen vom <see cref="ReferenceFinder"/>,
/// gefüttert mit dem Symbol unter dem Cursor und dem aktuellen Semantikmodell (geerbt von
/// <see cref="SemanticModelServiceDependent"/>). Die Neuberechnung wird gedrosselt
/// (<see cref="ServiceProperties.ReferenceHighlighting"/>), um die GUI nicht durch ständiges Umfärben zu
/// beunruhigen.
/// </summary>
sealed class ReferenceHighlightTagger : SemanticModelServiceDependent, ITagger<ReferenceHighlightTag> {

    [NotNull]
    readonly IDisposable _observable;
    [NotNull]
    readonly List<SnapshotSpan> _referenceSpans;

    /// <summary>
    /// Verdrahtet den Tagger: abonniert Cursor- und Layout-Änderungen der <paramref name="view"/> sowie
    /// (gedrosselt) das eigene <c>Invalidated</c>-Ereignis, und berechnet die Fundstellen initial.
    /// </summary>
    public ReferenceHighlightTagger(ITextView view, ITextBuffer textBuffer) : base(textBuffer) {
        View = view;
            
        _referenceSpans = new List<SnapshotSpan>();

        // Wir drosseln hier das Highlighting etwas, um nicht zu viel 
        // Unruhe in die GUI zu bekommen. Der C# Editor verzögert ähnlich.
        _observable = Observable.FromEventPattern<EventArgs>(
                                     handler => Invalidated += handler,
                                     handler => Invalidated -= handler)
                                .Throttle(ServiceProperties.ReferenceHighlighting)
                                .ObserveOn(SynchronizationContext.Current)
                                .Select(   _ => RebuildReferences())
                                .Subscribe(_ => OnTagsChanged());

        View.Caret.PositionChanged += OnCaretPositionChanged;
        View.LayoutChanged         += OnViewLayoutChanged;
        RebuildReferences();
    }
        
    /// <summary>Meldet alle Ereignis-Abonnements ab und beendet die gedrosselte Neuberechnung.</summary>
    public override void Dispose() {
        base.Dispose();

        _observable.Dispose();

        View.Caret.PositionChanged -= OnCaretPositionChanged;
        View.LayoutChanged         -= OnViewLayoutChanged;
    }

    /// <summary>Die Editor-Sicht, an deren Cursor sich das Highlighting orientiert.</summary>
    public ITextView View { get; }

    /// <summary>Stößt bei einem echten Snapshot-Wechsel im Layout eine (verzögerte) Neuberechnung an.</summary>
    void OnViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
        // If a new snapshot wasn't generated, then skip this layout 
        if(e.NewSnapshot != e.OldSnapshot) {
            Invalidate();
        }
    }

    /// <summary>Beim Beginn eines Semantikmodell-Wechsels wird die Hervorhebung sofort geleert (sie könnte veraltet sein).</summary>
    protected override void OnSemanticModelChanging(object sender, EventArgs e) {
        Invalidate(clearImmediately: true);
    }

    /// <summary>Nach einem Semantikmodell-Wechsel wird die Hervorhebung (verzögert) neu berechnet.</summary>
    protected override void OnSemanticModelChanged(object sender, SnapshotSpanEventArgs e) {
        Invalidate();
    }
        
    /// <summary>Reagiert auf Cursorbewegungen und stößt bei Bedarf eine Neuberechnung an.</summary>
    void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
        Invalidate();
    }

    /// <summary>
    /// Prüft, ob eine Neuberechnung nötig ist: Bleibt der Cursor innerhalb der bereits hervorgehobenen
    /// Referenzen, geschieht nichts. Verlässt er sie (oder ist <paramref name="clearImmediately"/> gesetzt),
    /// wird die Hervorhebung sofort geleert; anschließend wird die gedrosselte Neuberechnung angestoßen.
    /// </summary>
    void Invalidate(bool clearImmediately=false) {

        var point = View.GetCaretPoint();

        // Wenn das Caret nur innerhalb der Referenzen positioniert wurde, und kein Neubau erforderlich ist
        // dann bleibt alles wie es ist.
        if(!clearImmediately && IsPointOverReference(point)) {
            return;
        }
            
        if (clearImmediately || (_referenceSpans.Any() && !IsPointOverReference(point))) {
            // Der Cursor geht aus einer der aktuelle hervorgehobenen Referenz heraus => sofortiges Update,
            // um die Hervorhebung zu entfernen
            _referenceSpans.Clear();
            OnTagsChanged();
        } 

        OnInvalidated();
    }

    /// <summary>Löst <c>Invalidated</c> aus — Eingang der gedrosselten Neuberechnungs-Pipeline.</summary>
    void OnInvalidated() {                    
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Internes Signal „Neuberechnung fällig"; im Konstruktor gedrosselt an <see cref="RebuildReferences"/> gekoppelt.</summary>
    event EventHandler<EventArgs> Invalidated;

    /// <summary>Meldet dem Editor über <see cref="TagsChanged"/>, dass die Tags des gesamten Puffers neu abzufragen sind.</summary>
    void OnTagsChanged() {

        var snapshotSpan = TextBuffer.CurrentSnapshot.GetFullSpan();
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(snapshotSpan));
    }

    /// <summary><see cref="ITagger{T}"/>-Ereignis: signalisiert dem Editor, dass Tags neu abzufragen sind.</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        
    /// <summary>
    /// Liefert für die angefragten <paramref name="spans"/> die Highlight-Tags: die erste Fundstelle als
    /// <see cref="DefinitionHighlightTag"/>, die übrigen als <see cref="ReferenceHighlightTag"/>. Liegen
    /// die aktuellen Fundstellen auf einem älteren Snapshot, werden sie auf den angefragten übersetzt.
    /// </summary>
    public IEnumerable<ITagSpan<ReferenceHighlightTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        if(spans.Count == 0 || _referenceSpans.Count == 0) {
            yield break;
        }

        // der erste Span hat immer den Charackter der Definition, die übrigen sind dessen Referenzen.
        var definitionSpan = _referenceSpans.First();
        var referenceSpans = new NormalizedSnapshotSpanCollection(_referenceSpans.Skip(1));

        // If the requested snapshot isn't the same as the one our references are on, 
        // translate our spans to the expected snapshot 
        if (spans[0].Snapshot != _referenceSpans[0].Snapshot) {

            referenceSpans = new NormalizedSnapshotSpanCollection(
                referenceSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

            definitionSpan = definitionSpan.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
        }

        // Die "Definition"
        yield return new TagSpan<ReferenceHighlightTag>(definitionSpan, new DefinitionHighlightTag());

        // Und die zugehörigen Referenzen
        foreach (SnapshotSpan referenceSpan in referenceSpans.Where(spans.IntersectsWith)) {
            yield return new TagSpan<ReferenceHighlightTag>(referenceSpan, new ReferenceHighlightTag());
        }           
    }

    /// <summary>
    /// Berechnet die Fundstellen neu (aus dem aktuellen Semantikmodell) und übernimmt sie nur, wenn mehr
    /// als eine gefunden wurde — eine einzelne Fundstelle (nur die Definition, ohne Referenz) wird nicht
    /// hervorgehoben.
    /// </summary>
    List<SnapshotSpan> RebuildReferences() {

        _referenceSpans.Clear();

        var newReferences = BuildReferences(SemanticModelService.CodeGenerationUnitAndSnapshot).ToList();
        if (newReferences.Count > 1) {
            _referenceSpans.AddRange(newReferences);
        }

        return _referenceSpans;
    }

    /// <summary>
    /// Bestimmt das Symbol unter dem Cursor und lässt vom <see cref="ReferenceFinder"/> dessen Definition
    /// und Referenzen ermitteln; jede Fundstelle wird in einen <see cref="SnapshotSpan"/> auf dem aktuellen
    /// Snapshot übersetzt.
    /// </summary>
    IEnumerable<SnapshotSpan> BuildReferences(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        var symbol = View.TryFindSymbolUnderCaret(codeGenerationUnitAndSnapshot);
        if (symbol == null) {
            yield break;
        }

        var advancedOptions = NavLanguagePackage.AdvancedOptions;
         
        foreach (var reference in ReferenceFinder.FindReferences(symbol, advancedOptions)) {

            yield return new SnapshotSpan(
                new SnapshotPoint(codeGenerationUnitAndSnapshot.Snapshot, reference.Start), 
                reference.Location.Length);
        }
    }

    /// <summary>
    /// Prüft, ob <paramref name="point"/> innerhalb einer der aktuell hervorgehobenen Fundstellen liegt
    /// (bei Bedarf auf den Snapshot des Punktes übersetzt). Grundlage der Optimierung in
    /// <see cref="Invalidate"/>, die reine Cursorbewegungen innerhalb der Referenzen ignoriert.
    /// </summary>
    bool IsPointOverReference(SnapshotPoint? point) {

        if (_referenceSpans.Count == 0 || point == null) {
            return false;
        }

        var referenceSpans = new NormalizedSnapshotSpanCollection(_referenceSpans);

        if (_referenceSpans[0].Snapshot != point.Value.Snapshot) {
            referenceSpans = new NormalizedSnapshotSpanCollection(
                referenceSpans.Select(span => span.TranslateTo(point.Value.Snapshot, SpanTrackingMode.EdgeExclusive)));

        }

        return referenceSpans.Any(r => r.Span.Start <= point.Value.Position && r.Span.End >= point.Value.Position);
    }
}
