#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Underlining;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

/// <summary>
/// Übersetzt die (logischen) <see cref="UnderlineTag"/>s eines <see cref="ITagAggregator{T}"/> in
/// sichtbare <see cref="ClassificationTag"/>s des Nav-Underline-Klassifizierungstyps
/// (<see cref="ClassificationTypeNames.Underline"/>) und färbt so die betroffenen Stellen unterstrichen
/// ein. Pro <see cref="ITextView"/> als Singleton geführt und über
/// <see cref="UnderlineClassifierProvider"/> als <see cref="IViewTaggerProvider"/> bereitgestellt.
/// </summary>
class UnderlineClassifier : ITagger<ClassificationTag>, IDisposable {

    /// <summary>Verdrahtet die Weitergabe der Aggregator-Tag-Änderungen an <see cref="TagsChanged"/>.</summary>
    public UnderlineClassifier(ITextBuffer textBuffer,
                               ITagAggregator<UnderlineTag> underlineTagAggregator,
                               IClassificationTypeRegistryService classificationTypeRegistryService) {
        TextBuffer                        = textBuffer;
        UnderlineTagAggregator            = underlineTagAggregator;
        ClassificationTypeRegistryService = classificationTypeRegistryService;

        UnderlineTagAggregator.TagsChanged += OnUnderlineTagsChanged;
    }

    /// <summary>Meldet den <see cref="UnderlineTagAggregator"/> ab.</summary>
    public void Dispose() {
        UnderlineTagAggregator.TagsChanged -= OnUnderlineTagsChanged;
    }

    /// <summary>Reicht eine Änderung der Underline-Tags als <see cref="TagsChanged"/> weiter.</summary>
    void OnUnderlineTagsChanged(object sender, TagsChangedEventArgs e) {            
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(e.Span.GetSpans(TextBuffer)[0]));
    }

    /// <summary>Liefert den View-Singleton als <see cref="ITagger{T}"/> (für die MEF-Provider-Schnittstelle).</summary>
    public static ITagger<T> GetOrCreateSingelton<T>(IClassificationTypeRegistryService classificationTypeRegistryService, ITextView textView, ITextBuffer buffer, ITagAggregator<UnderlineTag> underlineTagAggregator) where T : ITag {            
        return GetOrCreateSingelton(classificationTypeRegistryService, textView, buffer, underlineTagAggregator) as ITagger<T>;            
    }

    /// <summary>Liefert bzw. erzeugt den an die <see cref="ITextView"/> gebundenen Klassifizierer (automatisch beim Schließen der View entsorgt).</summary>
    public static UnderlineClassifier GetOrCreateSingelton(IClassificationTypeRegistryService classificationTypeRegistryService, ITextView textView, ITextBuffer buffer, ITagAggregator<UnderlineTag> underlineTagAggregator) {
        return textView.GetOrCreateAutoClosingProperty(_ =>
                                                           new UnderlineClassifier(buffer, underlineTagAggregator, classificationTypeRegistryService));
    }

    /// <summary>Der zugrunde liegende Textpuffer.</summary>
    public ITextBuffer                        TextBuffer                        { get; }
    /// <summary>Aggregator, der die logischen <see cref="UnderlineTag"/>s des Puffers liefert.</summary>
    public ITagAggregator<UnderlineTag>       UnderlineTagAggregator            { get; }
    /// <summary>Registrierungsdienst zum Auflösen des Underline-Klassifizierungstyps.</summary>
    public IClassificationTypeRegistryService ClassificationTypeRegistryService { get; }

    /// <summary>Wird ausgelöst, wenn sich die Unterstreichungen für einen Bereich geändert haben.</summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>Übersetzt die <see cref="UnderlineTag"/>s der angefragten Bereiche in Underline-Klassifizierungs-Tags.</summary>
    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var classificationType = ClassificationTypeRegistryService.GetClassificationType(ClassificationTypeNames.Underline);

        foreach(var span in spans) {
            foreach(var tagSpan in UnderlineTagAggregator.GetTags(span)) {
                var tagSpans = tagSpan.Span.GetSpans(span.Snapshot);
                yield return new TagSpan<ClassificationTag>(tagSpans[0], new ClassificationTag(classificationType));
            }
        }
    }
}