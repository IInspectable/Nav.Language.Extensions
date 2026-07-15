#region Using Directives

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Utilities.Logging;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

/// <summary>
/// Der view-gebundene Adornment-Tagger der Nav-GoTo-Symbole in C#-Editoren: Er bezieht die
/// <see cref="IntraTextGoToTag"/>-Datentags des <see cref="IntraTextGoToTagger"/> über einen
/// <see cref="ITagAggregator{T}"/> und macht daraus per <see cref="IntraTextAdornmentTagger{TData,TAdornment}"/>
/// die klickbaren <see cref="IntraTextGoToAdornment"/>-Buttons. Ein Tagger je <see cref="IWpfTextView"/>
/// (siehe <see cref="GetTagger"/>).
/// </summary>
sealed class IntraTextGoToAdornmentTagger : IntraTextAdornmentTagger<IntraTextGoToTag, IntraTextGoToAdornment>, IDisposable {

    static readonly Logger Logger = Logger.Create<IntraTextGoToAdornmentTagger>();

    readonly ITagAggregator<IntraTextGoToTag> _intraTextGoToTagger;
    readonly GoToLocationService              _goToLocationService;

    IntraTextGoToAdornmentTagger(IWpfTextView textView, ITagAggregator<IntraTextGoToTag> intraTextGoToTagger, GoToLocationService goToLocationService)
        : base(textView) {

        _intraTextGoToTagger = intraTextGoToTagger;
        _goToLocationService = goToLocationService;

        intraTextGoToTagger.TagsChanged += OnTagsChanged;

        Logger.Info($"{nameof(IntraTextGoToAdornmentTagger)}.Ctor");
    }

    public override void Dispose() {
        base.Dispose();

        _intraTextGoToTagger.TagsChanged -= OnTagsChanged;
        _intraTextGoToTagger.Dispose();

        TextView.Properties.RemoveProperty(typeof(IntraTextGoToAdornmentTagger));

        Logger.Info($"{nameof(IntraTextGoToAdornmentTagger)}.{nameof(Dispose)}");
    }

    /// <summary>
    /// Liefert den (pro <paramref name="view"/> als schließbare View-Property gehaltenen) Tagger; der
    /// <paramref name="intraTextGoToTagger"/> wird erst bei Bedarf materialisiert.
    /// </summary>
    internal static ITagger<IntraTextAdornmentTag> GetTagger(IWpfTextView view, Lazy<ITagAggregator<IntraTextGoToTag>> intraTextGoToTagger, GoToLocationService goToLocationService) {
        return view.GetOrCreateAutoClosingProperty( _ => new IntraTextGoToAdornmentTagger(
                                                        textView           : view, 
                                                        intraTextGoToTagger: intraTextGoToTagger.Value, 
                                                        goToLocationService: goToLocationService));
    }

    void OnTagsChanged(object sender, TagsChangedEventArgs e) {

        InvalidateSpans(new List<SnapshotSpan> {
            TextView.TextBuffer.CurrentSnapshot.GetFullSpan()
        });
    }
        
    /// <summary>
    /// Liefert je Datentag die Adornment-Daten: Das Symbol wird an das <em>Ende</em> des markierten Spans
    /// (nach dem Bezeichner) mit <see cref="PositionAffinity.Successor"/> gesetzt. Durch Projektion
    /// gespaltene Datentags werden übersprungen.
    /// </summary>
    protected override IEnumerable<Tuple<SnapshotSpan, PositionAffinity?, IntraTextGoToTag>> GetAdornmentData(NormalizedSnapshotSpanCollection spans) {

        if (spans.Count == 0) {
            yield break;
        }

        var snapshot        = spans[0].Snapshot;
        var mappingTagSpans = _intraTextGoToTagger.GetTags(spans);

        foreach(IMappingTagSpan<IntraTextGoToTag> dataTagSpan in mappingTagSpans) {

            NormalizedSnapshotSpanCollection goToTagSpans = dataTagSpan.Span.GetSpans(snapshot);

            // Ignore data tags that are split by projection.
            // This is theoretically possible but unlikely in current scenarios.
            if(goToTagSpans.Count != 1) {
                continue;
            }

            // Uns interessiert nur das Ende des Spans
            var adornmentSpan = new SnapshotSpan(goToTagSpans[0].End, 0);

            yield return Tuple.Create(adornmentSpan, (PositionAffinity?) PositionAffinity.Successor, dataTagSpan.Tag);
        }
    }

    /// <summary>Erzeugt für einen Datentag den zugehörigen <see cref="IntraTextGoToAdornment"/>-Button.</summary>
    protected override IntraTextGoToAdornment CreateAdornment(IntraTextGoToTag dataTag, SnapshotSpan span) {
        return new IntraTextGoToAdornment(dataTag, TextView, span, _goToLocationService);
    }

    /// <summary>Aktualisiert einen wiederverwendeten Adornment-Button mit dem neuen Datentag (bleibt stets erhalten).</summary>
    protected override bool UpdateAdornment(IntraTextGoToAdornment adornment, IntraTextGoToTag dataTag) {
        adornment.Update(dataTag);
        return true;
    }
}