#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

sealed class SyntacticClassificationTagger: ParserServiceDependent, ITagger<IClassificationTag> {

    readonly ImmutableDictionary<TextClassification, IClassificationType> _classificationMap;
    readonly IClassificationType                                          _commentClassification;

    public SyntacticClassificationTagger(IClassificationTypeRegistryService registry, ITextBuffer textBuffer): base(textBuffer) {

        _classificationMap = ClassificationTypeDefinitions.GetSyntaxTokenClassificationMap(registry);
        _classificationMap.TryGetValue(TextClassification.Comment, out _commentClassification);
    }

    protected override void OnParseResultChanged(object sender, SnapshotSpanEventArgs e) {
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(e.Span));
    }

    public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {

        var syntaxTreeAndSnapshot = ParserService.SyntaxTreeAndSnapshot;

        if (syntaxTreeAndSnapshot == null) {
            yield break;
        }

        var syntaxTree = syntaxTreeAndSnapshot.SyntaxTree;

        foreach (var span in spans) {

            var extent = TextExtent.FromBounds(span.Start.Position, span.End.Position);

            // Signifikante Token aus dem flachen Strom. Trivia (Whitespace, Zeilenenden, Kommentare) werden
            // hier übersprungen: Kommentare kommen aus der angehängten Trivia (Roslyn-Modell), Whitespace und
            // Zeilenenden tragen ohnehin keine Klassifizierung.
            foreach (var token in syntaxTree.Tokens[extent, includeOverlapping: true]) {

                if (SyntaxFacts.IsTrivia(token.Classification)) {
                    continue;
                }

                if (!_classificationMap.TryGetValue(token.Classification, out var ct) || ct == null) {
                    continue;
                }

                yield return CreateTagSpan(syntaxTreeAndSnapshot.Snapshot, span, token.Start, token.Length, ct);
            }

            // Kommentare aus der angehängten Trivia.
            if (_commentClassification != null) {
                foreach (var comment in syntaxTree.Comments()) {

                    if (comment.Extent.IsEmptyOrMissing || !comment.Extent.IntersectsWith(extent)) {
                        continue;
                    }

                    yield return CreateTagSpan(syntaxTreeAndSnapshot.Snapshot, span, comment.Start, comment.Length, _commentClassification);
                }
            }
        }
    }

    static ITagSpan<IClassificationTag> CreateTagSpan(ITextSnapshot snapshot, SnapshotSpan span, int start, int length, IClassificationType classificationType) {
        var tokenSpan = new SnapshotSpan(snapshot, new Span(start, length));
        var tagSpan   = tokenSpan.TranslateTo(span.Snapshot, SpanTrackingMode.EdgeExclusive);
        return new TagSpan<IClassificationTag>(tagSpan, new ClassificationTag(classificationType));
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

}