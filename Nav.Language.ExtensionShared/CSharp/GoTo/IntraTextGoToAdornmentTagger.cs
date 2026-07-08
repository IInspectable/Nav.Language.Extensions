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

    protected override IntraTextGoToAdornment CreateAdornment(IntraTextGoToTag dataTag, SnapshotSpan span) {
        return new IntraTextGoToAdornment(dataTag, TextView, span, _goToLocationService);
    }

    protected override bool UpdateAdornment(IntraTextGoToAdornment adornment, IntraTextGoToTag dataTag) {
        adornment.Update(dataTag);
        return true;
    }
}